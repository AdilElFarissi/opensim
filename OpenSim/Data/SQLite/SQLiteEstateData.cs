using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using log4net;
using System.Data.SQLite;

using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;

namespace OpenSim.Data.SQLite
{
    public class SQLiteEstateStore : IEstateDataStore
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private SQLiteConnection m_connection;
        private string m_connectionString;

        private FieldInfo[] m_Fields;
        private Dictionary<string, FieldInfo> m_FieldMap =
                new Dictionary<string, FieldInfo>();

        protected virtual Assembly Assembly
        {
            get { return GetType().Assembly; }
        }

        public SQLiteEstateStore()
        {
        }

        public SQLiteEstateStore(string connectionString)
        {
            m_connectionString = connectionString;
            Initialise();
        }

        public void Initialise()
        {
            m_connection = new SQLiteConnection($"Data Source={m_connectionString};fail_if_missing=True;");
            m_connection.Open();

            Migration m = new Migration(m_connection, Assembly, "EstateStore");
            m.Update();

            Type t = typeof(EstateSettings);
            m_Fields = t.GetFields(BindingFlags.NonPublic |
                                   BindingFlags.Instance |
                                   BindingFlagsDeclaredOnly);

            foreach (FieldInfo f in m_Fields)
            {
                if (f.Name.Substring(0, 2) == "m_")
                    m_FieldMap[f.Name.Substring(2)] = f;
            }
        }

        private string[] FieldList
        {
            get { return new List<string>(m_FieldMap.Keys).ToArray(); }
        }

        public EstateSettings LoadEstateSettings(UUID regionID, bool create)
        {
            string sql = "SELECT estate_settings." + String.Join(", estate_settings.", FieldList) +
                         " FROM estate_map LEFT JOIN estate_settings ON estate_map.EstateID = estate_settings.EstateID " +
                         "WHERE estate_settings.EstateID IS NOT NULL AND RegionID = @RegionID";

            using (SQLiteCommand cmd = new SQLiteCommand(sql, m_connection))
            {
                cmd.Parameters.AddWithValue("@RegionID", regionID.ToString());
                try
                {
                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            EstateSettings es = new EstateSettings();
                            es.OnSave += StoreEstateSettings;

                            for (int columnIndex = 0; columnIndex < reader.FieldCount; columnIndex++)
                            {
                                FieldInfo fieldInfo = m_FieldMap[reader.GetName(columnIndex)];
                                object value = reader[columnIndex];

                                if (fieldInfo.GetValue(es) is bool)
                                {
                                    if (value.ToString() != "0")
                                    {
                                        fieldInfo.SetValue(es, true);
                                    }
                                }
                                else if (fieldInfo.GetValue(es) is UUID)
                                {
                                    UUID uuid = UUID.Zero;

                                    UUID.TryParse(value.ToString(), out uuid);
                                    fieldInfo.SetValue(es, uuid);
                                }
                                else
                                {
                                    fieldInfo.SetValue(es, Convert.ChangeType(value, fieldInfo.FieldType));
                                }
                            }

                            LinkRegion(regionID, (int)es.EstateID);

                            LoadBanList(es);

                            es.EstateManagers = LoadUUIDList(es.EstateID, "estate_managers");
                            es.EstateAccess = LoadUUIDList(es.EstateID, "estate_users");
                            es.EstateGroups = LoadUUIDList(es.EstateID, "estate_groups");

                            return es;
                        }
                    }
                }
                catch (SQLiteException ex)
                {
                    // Handle database exception
                    m_log.Error("[SQLITE]: An exception of type SQLiteException occurred. Message: {0}", ex.Message);
                    throw;
                }
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

        private void DoCreate(EstateSettings es)
        {
            List<string> names = new List<string>(FieldList);

            using (SQLiteTransaction transaction = m_connection.BeginTransaction())
            {
                try
                {
                    using (SQLiteCommand cmd = new SQLiteCommand())
                    {
                        cmd.Connection = m_connection;
                        cmd.Transaction = transaction;

                        if (es.EstateID < 100)
                        {
                            cmd.CommandText = "SELECT MAX(EstateID) FROM estate_settings";
                            uint a = Convert.ToUInt32(cmd.ExecuteScalar());

                            if (a == 0)
                            {
                                a = 100;
                            }
                            else if (a < 100)
                            {
                                ++a;
                            }
                            es.EstateID = a;
                        }

                        cmd.CommandText = "INSERT INTO estate_settings (" + String.Join(",", names.ToArray()) + ") VALUES ( :" +
                                         String.Join(", :", names.ToArray()) + ")";
                        cmd.Parameters.Clear();

                        for (int i = 0; i < FieldList.Length; i++)
                        {
                            if (m_FieldMap[FieldList[i]].GetValue(es) is bool)
                            {
                                if ((bool)m_FieldMap[FieldList[i]].GetValue(es))
                                    cmd.Parameters.AddWithValue(":" + FieldList[i], "1");
                                else
                                    cmd.Parameters.AddWithValue(":" + FieldList[i], "0");
                            }
                            else
                            {
                                cmd.Parameters.AddWithValue(":" + FieldList[i], m_FieldMap[FieldList[i]].GetValue(es).ToString());
                            }
                        }

                        cmd.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
                catch (SQLiteException ex)
                {
                    // Handle database exception
                    m_log.Error("[SQLITE]: An exception of type SQLiteException occurred. Message: {0}", ex.Message);
                    transaction.Rollback();
                }
            }
        }

        public void StoreEstateSettings(EstateSettings es)
        {
            List<string> fields = new List<string>(FieldList);
            fields.Remove("EstateID");

            List<string> terms = new List<string>();

            for (int i = 0; i < fields.Count; i++)
                terms.Add(fields[i] + " = :" + fields[i]);

            using (SQLiteTransaction transaction = m_connection.BeginTransaction())
            {
                try
                {
                    using (SQLiteCommand cmd = new SQLiteCommand())
                    {
                        cmd.Connection = m_connection;
                        cmd.Transaction = transaction;

                        cmd.CommandText = "UPDATE estate_settings SET " + String.Join(", ", terms.ToArray()) + " WHERE EstateID = :EstateID";
                        cmd.Parameters.AddWithValue(":EstateID", es.EstateID.ToString());

                        for (int i = 0; i < FieldList.Length; i++)
                        {
                            if (m_FieldMap[FieldList[i]].GetValue(es) is bool)
                            {
                                if ((bool)m_FieldMap[FieldList[i]].GetValue(es))
                                    cmd.Parameters.AddWithValue(":" + FieldList[i], "1");
                                else
                                    cmd.Parameters.AddWithValue(":" + FieldList[i], "0");
                            }
                            else
                            {
                                cmd.Parameters.AddWithValue(":" + FieldList[i], m_FieldMap[FieldList[i]].GetValue(es).ToString());
                            }
                        }

                        cmd.ExecuteNonQuery();
                    }

                    SaveBanList(es);
                    SaveUUIDList(es.EstateID, "estate_managers", es.EstateManagers);
                    SaveUUIDList(es.EstateID, "estate_users", es.EstateAccess);
                    SaveUUIDList(es.EstateID, "estate_groups", es.EstateGroups);

                    transaction.Commit();
                }
                catch (SQLiteException ex)
                {
                    // Handle database exception
                    m_log.Error("[SQLITE]: An exception of type SQLiteException occurred. Message: {0}", ex.Message);
                    transaction.Rollback();
                }
            }
        }

        private void LoadBanList(EstateSettings es)
        {
            if (es == null)
                return;

            es.ClearBans();

            using (SQLiteCommand cmd = new SQLiteCommand("SELECT * FROM estateban WHERE EstateID = @EstateID", m_connection))
            {
                cmd.Parameters.AddWithValue("@EstateID", es.EstateID.ToString());

                using (SQLiteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        EstateBan eb = new EstateBan();

                        eb.BannedUserID = DBGuid.FromDB(reader["bannedUUID"]);
                        eb.BannedHostAddress = "0.0.0.0";
                        eb.BannedHostIPMask = "0.0.0.0";
                        eb.BanningUserID = DBGuid.FromDB(reader["banningUUID"]);
                        eb.BanTime = Convert.ToInt32(reader["banTime"]);
                        es.AddBan(eb);
                    }
                }
            }
        }

        public void Destroy()
        {
            m_connection.Close();
        }

        // Rest of the code...
    }
}