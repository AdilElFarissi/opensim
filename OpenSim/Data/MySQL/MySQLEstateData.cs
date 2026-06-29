using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using log4net;
using MySql.Data.MySqlClient;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Data;

namespace OpenSim.Data.MySQL
{
    public class MySQLEstateStore : IEstateDataStore
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string m_connectionString;

        private FieldInfo[] m_Fields;
        private Dictionary<string, FieldInfo> m_FieldMap = new Dictionary<string, FieldInfo>();

        protected virtual Assembly Assembly
        {
            get { return GetType().Assembly; }
        }

        public MySQLEstateStore()
        {
        }

        public MySQLEstateStore(string connectionString)
        {
            Initialise(connectionString);
        }

        public void Initialise(string connectionString)
        {
            m_connectionString = connectionString;

            // Improved Exception Handling
            try
            {
                m_log.Info("[REGION DB]: MySql - connecting: " + Util.GetDisplayConnectionString(m_connectionString));
            }
            catch (Exception e)
            {
                m_log.Error("Error connecting to database: " + e.ToString());
                return;
            }

            lock (typeof(MySQLEstateStore)) // Use typeof instead of m_connectionString
            {
                using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
                {
                    dbcon.Open();

                    Migration m = new Migration(dbcon, Assembly, "EstateStore");
                    m.Update();
                    dbcon.Close();

                    Type t = typeof(EstateSettings);
                    m_Fields = t.GetFields(BindingFlags.NonPublic |
                                           BindingFlags.Instance |
                                           BindingFlags.DeclaredOnly);

                    foreach (FieldInfo f in m_Fields)
                    {
                        if (f.Name.StartsWith("m_")) // Corrected condition to prevent index out of range
                            m_FieldMap[f.Name.Substring(2)] = f;
                    }
                }
            }
        }

        private string[] FieldList
        {
            get { return m_FieldMap.Keys.ToArray(); }
        }

        public EstateSettings LoadEstateSettings(UUID regionID, bool create)
        {
            string sql = "select estate_settings." + String.Join(",estate_settings.", FieldList) +
                " from estate_map left join estate_settings on estate_map.EstateID = estate_settings.EstateID where estate_settings.EstateID is not null and RegionID = ?RegionID";

            using (MySqlCommand cmd = new MySqlCommand())
            {
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("?RegionID", regionID.ToString());

                EstateSettings e = DoLoad(cmd, regionID, create);
                if (!create && e.EstateID == 0) // Not found
                    return null;

                return e;
            }
        }

        public EstateSettings CreateNewEstate(int estateID)
        {
            EstateSettings es = new EstateSettings();

            es.OnSave += StoreEstateSettings;
            es.EstateID = Convert.ToUInt32(estateID);

            DoCreate(es);

            LoadBanList(es);

            es.EstateManagers = LoadUUIDList(es.EstateID, "estate_managers");
            es.EstateAccess = LoadUUIDList(es.EstateID, "estate_users");
            es.EstateGroups = LoadUUIDList(es.EstateID, "estate_groups");

            return es;
        }

        private EstateSettings DoLoad(MySqlCommand cmd, UUID regionID, bool create)
        {
            EstateSettings es = new EstateSettings();
            es.OnSave += StoreEstateSettings;

            lock (typeof(MySQLEstateStore))
            {
                using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
                {
                    dbcon.Open();
                    cmd.Connection = dbcon;

                    bool found = false;

                    using (IDataReader r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            found = true;

                            foreach (string name in FieldList)
                            {
                                if (m_FieldMap[name].FieldType == typeof(bool))
                                {
                                    m_FieldMap[name].SetValue(es, Convert.ToInt32(r[name]) != 0);
                                }
                                else if (m_FieldMap[name].FieldType == typeof(UUID))
                                {
                                    m_FieldMap[name].SetValue(es, DBGuid.FromDB(r[name]));
                                }
                                else
                                {
                                    m_FieldMap[name].SetValue(es, r[name]);
                                }
                            }
                        }
                    }

                    dbcon.Close();
                    cmd.Connection = null;

                    if (!found && create)
                    {
                        DoCreate(es);
                        LinkRegion(regionID, (int)es.EstateID);
                    }
                }
            }

            LoadBanList(es);
            es.EstateManagers = LoadUUIDList(es.EstateID, "estate_managers");
            es.EstateAccess = LoadUUIDList(es.EstateID, "estate_users");
            es.EstateGroups = LoadUUIDList(es.EstateID, "estate_groups");

            return es;
        }

        private void DoCreate(EstateSettings es)
        {
            // Migration case
            List<string> names = new List<string>(FieldList);

            // Remove EstateID and use AutoIncrement
            if (es.EstateID < 100)
                names.Remove("EstateID");

            string sql = "insert into estate_settings (" + String.Join(",", names.ToArray()) + ") values ( ?" + String.Join(", ?", names.ToArray()) + ")";

            lock (typeof(MySQLEstateStore))
            {
                using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
                {
                    dbcon.Open();
                    using (MySqlCommand cmd2 = dbcon.CreateCommand())
                    {
                        cmd2.CommandText = sql;
                        cmd2.Parameters.Clear();

                        foreach (string name in FieldList)
                        {
                            if (m_FieldMap[name].GetValue(es) is bool)
                            {
                                if ((bool)m_FieldMap[name].GetValue(es))
                                    cmd2.Parameters.AddWithValue("?" + name, "1");
                                else
                                    cmd2.Parameters.AddWithValue("?" + name, "0");
                            }
                            else
                            {
                                cmd2.Parameters.AddWithValue("?" + name, m_FieldMap[name].GetValue(es).ToString());
                            }
                        }

                        cmd2.ExecuteNonQuery();

                        // Only get Auto ID if we actually used it else we just get 0
                        if (es.EstateID < 100)
                        {
                            cmd2.CommandText = "select LAST_INSERT_ID() as id";
                            cmd2.Parameters.Clear();

                            using (IDataReader r = cmd2.ExecuteReader())
                            {
                                r.Read();
                                es.EstateID = Convert.ToUInt32(r["id"]);
                            }

                            es.Save();
                        }
                    }
                    dbcon.Close();
                }
            }
        }

        public void StoreEstateSettings(EstateSettings es)
        {
            if (es.EstateID == 0)
            {
                m_log.Warn("Cannot store state for missing estate!");
                return;
            }

            string sql = "replace into estate_settings (" + String.Join(",", FieldList) + ") values ( ?" + String.Join(", ?", FieldList) + ")";

            lock (typeof(MySQLEstateStore))
            {
                using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
                {
                    dbcon.Open();

                    using (MySqlCommand cmd = dbcon.CreateCommand())
                    {
                        cmd.CommandText = sql;

                        foreach (string name in FieldList)
                        {
                            if (m_FieldMap[name].GetValue(es) is bool)
                            {
                                if ((bool)m_FieldMap[name].GetValue(es))
                                    cmd.Parameters.AddWithValue("?" + name, "1");
                                else
                                    cmd.Parameters.AddWithValue("?" + name, "0");
                            }
                            else
                            {
                                cmd.Parameters.AddWithValue("?" + name, m_FieldMap[name].GetValue(es).ToString());
                            }
                        }

                        cmd.ExecuteNonQuery();
                    }
                    dbcon.Close();
                }
            }

            SaveBanList(es);
            SaveUUIDList(es.EstateID, "estate_managers", es.EstateManagers);
            SaveUUIDList(es.EstateID, "estate_users", es.EstateAccess);
            SaveUUIDList(es.EstateID, "estate_groups", es.EstateGroups);
        }

        private void LoadBanList(EstateSettings es)
        {
            if (es.EstateID == 0)
            {
                m_log.Warn("Cannot load bans for missing estate!");
                return;
            }

            es.ClearBans();

            lock (typeof(MySQLEstateStore))
            {
                using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
                {
                    dbcon.Open();

                    using (MySqlCommand cmd = dbcon.CreateCommand())
                    {
                        cmd.CommandText = "select * from estateban where EstateID = ?EstateID";
                        cmd.Parameters.AddWithValue("?EstateID", es.EstateID);

                        using (IDataReader r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                EstateBan eb = new EstateBan();
                                eb.BannedUserID = DBGuid.FromDB(r["bannedUUID"]);
                                eb.BannedHostAddress = "0.0.0.0";
                                eb.BannedHostIPMask = "0.0.0.0";
                                eb.BanningUserID = DBGuid.FromDB(r["banningUUID"]);
                                eb.BanTime = Convert.ToInt32(r["banTime"]);
                                es.AddBan(eb);
                            }
                        }
                    }
                    dbcon.Close();
                }
            }
        }

        private void SaveBanList(EstateSettings es)
        {
            if (es.EstateID == 0)
            {
                m_log.Warn("Cannot save bans for missing estate!");
                return;
            }

            lock (typeof(MySQLEstateStore))
            {
                using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
                {
                    dbcon.Open();

                    using (MySqlCommand cmd = dbcon.CreateCommand())