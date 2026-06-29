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
        private Dictionary<string, FieldInfo> m_FieldMap = new();

        private static readonly HashSet<string> AllowedUuidTables = new()
        {
            "estate_managers",
            "estate_users",
            "estate_groups"
        };

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

            try
            {
                m_log.Info("[REGION DB]: MySql - connecting: " + Util.GetDisplayConnectionString(m_connectionString));
            }
            catch (Exception e)
            {
                m_log.Debug("Exception: password not found in connection string\n" + e.ToString());
            }

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
                    if (f.Name.Substring(0, 2) == "m_")
                        m_FieldMap[f.Name.Substring(2)] = f;
                }
            }
        }

        private string[] FieldList
        {
            get { return new List<string>(m_FieldMap.Keys).ToArray(); }
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

            LoadBanList(es);
            es.EstateManagers = LoadUUIDList(es.EstateID, "estate_managers");
            es.EstateAccess = LoadUUIDList(es.EstateID, "estate_users");
            es.EstateGroups = LoadUUIDList(es.EstateID, "estate_groups");
            return es;
        }

        private void DoCreate(EstateSettings es)
        {
            List<string> names = new List<string>(FieldList);

            if (es.EstateID < 100)
                names.Remove("EstateID");

            string sql = "insert into estate_settings (" + String.Join(",", names) + ") values ( ?" + String.Join(", ?", names) + ")";

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
                            cmd2.Parameters.AddWithValue("?" + name, ((bool)m_FieldMap[name].GetValue(es)) ? "1" : "0");
                        }
                        else
                        {
                            cmd2.Parameters.AddWithValue("?" + name, m_FieldMap[name].GetValue(es)?.ToString() ?? string.Empty);
                        }
                    }

                    cmd2.ExecuteNonQuery();

                    if (es.EstateID < 100)
                    {
                        cmd2.CommandText = "select LAST_INSERT_ID() as id";
                        cmd2.Parameters.Clear();

                        using (IDataReader r = cmd2.ExecuteReader())
                        {
                            if (r.Read())
                                es.EstateID = Convert.ToUInt32(r["id"]);
                        }

                        es.Save();
                    }
                }
                dbcon.Close();
            }
        }

        public void StoreEstateSettings(EstateSettings es)
        {
            string sql = "replace into estate_settings (" + String.Join(",", FieldList) + ") values ( ?" + String.Join(", ?", FieldList) + ")";

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
                            cmd.Parameters.AddWithValue("?" + name, ((bool)m_FieldMap[name].GetValue(es)) ? "1" : "0");
                        }
                        else
                        {
                            cmd.Parameters.AddWithValue("?" + name, m_FieldMap[name].GetValue(es)?.ToString() ?? string.Empty);
                        }
                    }

                    cmd.ExecuteNonQuery();
                }
                dbcon.Close();
            }

            SaveBanList(es);
            SaveUUIDList(es.EstateID, "estate_managers", es.EstateManagers);
            SaveUUIDList(es.EstateID, "estate_users", es.EstateAccess);
            SaveUUIDList(es.EstateID, "estate_groups", es.EstateGroups);
        }

        private void LoadBanList(EstateSettings es)
        {
            es.ClearBans();

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
                            EstateBan eb = new EstateBan
                            {
                                BannedUserID = DBGuid.FromDB(r["bannedUUID"]),
                                BannedHostAddress = "0.0.0.0",
                                BannedHostIPMask = "0.0.0.0",
                                BanningUserID = DBGuid.FromDB(r["banningUUID"]),
                                BanTime = Convert.ToInt32(r["banTime"])
                            };
                            es.AddBan(eb);
                        }
                    }
                }
                dbcon.Close();
            }
        }

        private void SaveBanList(EstateSettings es)
        {
            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd = dbcon.CreateCommand())
                {
                    cmd.CommandText = "delete from estateban where EstateID = ?EstateID";
                    cmd.Parameters.AddWithValue("?EstateID", es.EstateID.ToString());

                    cmd.ExecuteNonQuery();

                    cmd.Parameters.Clear();

                    cmd.CommandText = "insert into estateban (EstateID, bannedUUID, bannedIp, bannedIpHostMask, bannedNameMask, banningUUID, banTime) values ( ?EstateID, ?bannedUUID, '', '', '', ?banningUUID, ?banTime)";

                    foreach (EstateBan b in es.EstateBans)
                    {
                        cmd.Parameters.AddWithValue("?EstateID", es.EstateID.ToString());
                        cmd.Parameters.AddWithValue("?bannedUUID", b.BannedUserID.ToString());
                        cmd.Parameters.AddWithValue("?banningUUID", b.BanningUserID.ToString());
                        cmd.Parameters.AddWithValue("?banTime", b.BanTime);

                        cmd.ExecuteNonQuery();
                        cmd.Parameters.Clear();
                    }
                }
                dbcon.Close();
            }
        }

        private void SaveUUIDList(uint EstateID, string table, UUID[] data)
        {
            if (!AllowedUuidTables.Contains(table))
            {
                m_log.Error("[REGION DB]: Attempted to save to unauthorized table: " + table);
                return;
            }

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd = dbcon.CreateCommand())
                {
                    cmd.CommandText = "delete from " + table + " where EstateID = ?EstateID";
                    cmd.Parameters.AddWithValue("?EstateID", EstateID.ToString());

                    cmd.ExecuteNonQuery();

                    cmd.Parameters.Clear();

                    cmd.CommandText = "insert into " + table + " (EstateID, uuid) values ( ?EstateID, ?uuid )";

                    foreach (UUID uuid in data)
                    {
                        cmd.Parameters.AddWithValue("?EstateID", EstateID.ToString());
                        cmd.Parameters.AddWithValue("?uuid", uuid.ToString());

                        cmd.ExecuteNonQuery();
                        cmd.Parameters.Clear();
                    }
                }
                dbcon.Close();
            }
        }

        private UUID[] LoadUUIDList(uint EstateID, string table)
        {
            if (!AllowedUuidTables.Contains(table))
            {
                m_log.Error("[REGION DB]: Attempted to load from unauthorized table: " + table);
                return Array.Empty<UUID>();
            }

            List<UUID> uuids = new List<UUID>();

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd = dbcon.CreateCommand())
                {
                    cmd.CommandText = "select uuid from " + table + " where EstateID = ?EstateID";
                    cmd.Parameters.AddWithValue("?EstateID", EstateID);

                    using (IDataReader r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            uuids.Add(DBGuid.FromDB(r["uuid"]));
                        }
                    }
                }
                dbcon.Close();
            }

            return uuids.ToArray();
        }

        public EstateSettings LoadEstateSettings(int estateID)
        {
            using (MySqlCommand cmd = new MySqlCommand())
            {
                string sql = "select estate_settings." + String.Join(",estate_settings.", FieldList) + " from estate_settings where EstateID = ?EstateID";

                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("?EstateID", estateID);

                EstateSettings e = DoLoad(cmd, UUID.Zero, false);
                if (e.EstateID != estateID)
                    return null;
                return e;
            }
        }

        public List<EstateSettings> LoadEstateSettingsAll()
        {
            List<EstateSettings> allEstateSettings = new List<EstateSettings>();

            List<int> allEstateIds = GetEstatesAll();

            foreach (int estateId in allEstateIds)
                allEstateSettings.Add(LoadEstateSettings(estateId));

            return allEstateSettings;
        }

        public List<int> GetEstatesAll()
        {
            List<int> result = new List<int>();

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd = dbcon.CreateCommand())
                {
                    cmd.CommandText = "select estateID from estate_settings";

                    using (IDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            result.Add(Convert.ToInt32(reader["EstateID"]));
                        }
                    }
                }
                dbcon.Close();
            }

            return result;
        }

        public List<int> GetEstates(string search)
        {
            List<int> result = new List<int>();

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd = dbcon.CreateCommand())
                {
                    cmd.CommandText = "select estateID from estate_settings where EstateName = ?EstateName";
                    cmd.Parameters.AddWithValue("?EstateName", search);

                    using (IDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            result.Add(Convert.ToInt32(reader["EstateID"]));
                        }
                    }
                }
                dbcon.Close();
            }

            return result;
        }

        public List<int> GetEstatesByOwner(UUID ownerID)
        {
            List<int> result = new List<int>();

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd = dbcon.CreateCommand())
                {
                    cmd.CommandText = "select estateID from estate_settings where EstateOwner = ?EstateOwner";
                    cmd.Parameters.AddWithValue("?EstateOwner", ownerID.ToString());

                    using (IDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            result.Add(Convert.ToInt32(reader["EstateID"]));
                        }
                    }
                }
                dbcon.Close();
            }

            return result;
        }

        public bool LinkRegion(UUID regionID, int estateID)
        {
            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();
                using (MySqlTransaction transaction = dbcon.BeginTransaction())
                {
                    try
                    {
                        using (MySqlCommand cmd = dbcon.CreateCommand())
                        {
                            cmd.Transaction = transaction;
                            cmd.CommandText = "delete from estate_map where RegionID = ?RegionID";
                            cmd.Parameters.AddWithValue("?RegionID", regionID.ToString());
                            cmd.ExecuteNonQuery();
                        }

                        using (MySqlCommand cmd = dbcon.CreateCommand())
                        {
                            cmd.Transaction = transaction;
                            cmd.CommandText = "insert into estate_map values (?RegionID, ?EstateID)";
                            cmd.Parameters.AddWithValue("?RegionID", regionID.ToString());
                            cmd.Parameters.AddWithValue("?EstateID", estateID);
                            int ret = cmd.ExecuteNonQuery();

                            if (ret != 0)
                                transaction.Commit();
                            else
                                transaction.Rollback();

                            return ret != 0;
                        }
                    }
                    catch (MySqlException ex)
                    {
                        m_log.Error("[REGION DB]: LinkRegion failed: " + ex.Message);
                        try { transaction.Rollback(); } catch { }
                    }
                }
                dbcon.Close();
            }

            return false;
        }

        public List<UUID> GetRegions(int estateID)
        {
            List<UUID> result = new List<UUID>();

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                try
                {
                    using (MySqlCommand cmd = dbcon.CreateCommand())
                    {
                        cmd.CommandText = "select RegionID from estate_map where EstateID = ?EstateID";
                        cmd.Parameters.AddWithValue("?EstateID", estateID.ToString());

                        using (IDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                                result.Add(DBGuid.FromDB(reader["RegionID"]));
                        }
                    }
                }
                catch (Exception e)
                {
                    m_log.Error("[REGION DB]: Error reading estate map. " + e);
                }
                dbcon.Close();
            }

            return result;
        }

        public bool DeleteEstate(int estateID)
        {
            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();
                using (MySqlTransaction transaction = dbcon.BeginTransaction())
                {
                    try
                    {
                        // Remove estate related data
                        using (MySqlCommand cmd = dbcon.CreateCommand())
                        {
                            cmd.Transaction = transaction;
                            cmd.CommandText = "delete from estateban where EstateID = ?EstateID";
                            cmd.Parameters.AddWithValue("?EstateID", estateID);
                            cmd.ExecuteNonQuery();
                        }

                        foreach (string table in AllowedUuidTables)
                        {
                            using (MySqlCommand cmd = dbcon.CreateCommand())
                            {
                                cmd.Transaction = transaction;
                                cmd.CommandText = $"delete from {table} where EstateID = ?EstateID";
                                cmd.Parameters.AddWithValue("?EstateID", estateID);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        using (MySqlCommand cmd = dbcon.CreateCommand())
                        {
                            cmd.Transaction = transaction;
                            cmd.CommandText = "delete from estate_map where EstateID = ?EstateID";
                            cmd.Parameters.AddWithValue("?EstateID", estateID);
                            cmd.ExecuteNonQuery();
                        }

                        using (MySqlCommand cmd = dbcon.CreateCommand())
                        {
                            cmd.Transaction = transaction;
                            cmd.CommandText = "delete from estate_settings where EstateID = ?EstateID";
                            cmd.Parameters.AddWithValue("?EstateID", estateID);
                            int affected = cmd.ExecuteNonQuery();

                            if (affected > 0)
                                transaction.Commit();
                            else
                                transaction.Rollback();

                            return affected > 0;
                        }
                    }
                    catch (Exception ex)
                    {
                        m_log.Error("[REGION DB]: DeleteEstate failed: " + ex.Message);
                        try { transaction.Rollback(); } catch { }
                        return false;
                    }
                }
            }
        }
    }
}