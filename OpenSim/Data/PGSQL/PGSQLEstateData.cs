using System;
using System.Collections.Generic;
using System.Reflection;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using System.Data;
using Npgsql;
using NpgsqlTypes;

namespace OpenSim.Data.PGSQL
{
    public class PGSQLEstateStore : IEstateDataStore
    {
        private const string _migrationStore = "EstateStore";

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private PGSQLManager _Database;
        private string m_connectionString;
        private FieldInfo[] _Fields;
        private Dictionary<string, FieldInfo> _FieldMap = new Dictionary<string, FieldInfo>();

        #region Public methods

        public PGSQLEstateStore()
        {
        }

        public PGSQLEstateStore(string connectionString)
        {
            Initialise(connectionString);
        }

        protected virtual Assembly Assembly
        {
            get { return GetType().Assembly; }
        }

        public void Initialise(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                m_log.Error("[PGSQL]: Connection string is null or empty.");
                throw new ArgumentException("Connection string cannot be null or empty.", nameof(connectionString));
            }

            m_connectionString = connectionString;
            _Database = new PGSQLManager(connectionString);

            try
            {
                using (NpgsqlConnection conn = new NpgsqlConnection(m_connectionString))
                {
                    conn.Open();
                    Migration m = new Migration(conn, GetType().Assembly, _migrationStore);
                    m.Update();
                }
            }
            catch (Exception ex)
            {
                m_log.Error("[PGSQL] Migration failed: " + ex.Message, ex);
                throw;
            }

            Type t = typeof(EstateSettings);
            _Fields = t.GetFields(BindingFlags.NonPublic |
                                  BindingFlags.Instance |
                                  BindingFlags.DeclaredOnly);

            foreach (FieldInfo f in _Fields)
            {
                if (f.Name.StartsWith("m_", StringComparison.Ordinal))
                    _FieldMap[f.Name.Substring(2)] = f;
            }
        }

        public EstateSettings LoadEstateSettings(UUID regionID, bool create)
        {
            EstateSettings es = new EstateSettings();

            string sql = "select estate_settings.\"" + string.Join("\",estate_settings.\"", FieldList) +
                         "\" from estate_map left join estate_settings on estate_map.\"EstateID\" = estate_settings.\"EstateID\" " +
                         " where estate_settings.\"EstateID\" is not null and \"RegionID\" = :RegionID";

            bool insertEstate = false;
            using (NpgsqlConnection conn = new NpgsqlConnection(m_connectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.Add(_Database.CreateParameter("RegionID", regionID));

                conn.Open();
                using (NpgsqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        foreach (string name in FieldList)
                        {
                            FieldInfo f = _FieldMap[name];
                            object v = reader[name];
                            if (f.FieldType == typeof(bool))
                                f.SetValue(es, v);
                            else if (f.FieldType == typeof(UUID))
                            {
                                if (UUID.TryParse(v.ToString(), out UUID estUUID))
                                    f.SetValue(es, estUUID);
                            }
                            else if (f.FieldType == typeof(string))
                                f.SetValue(es, v?.ToString());
                            else if (f.FieldType == typeof(UInt32))
                                f.SetValue(es, Convert.ToUInt32(v));
                            else if (f.FieldType == typeof(Single))
                                f.SetValue(es, Convert.ToSingle(v));
                            else
                                f.SetValue(es, v);
                        }
                    }
                    else
                    {
                        insertEstate = true;
                    }
                }
            }

            if (insertEstate && create)
            {
                DoCreate(es);
                LinkRegion(regionID, (int)es.EstateID);
            }

            LoadBanList(es);

            es.EstateManagers = LoadUUIDList(es.EstateID, "estate_managers");
            es.EstateAccess = LoadUUIDList(es.EstateID, "estate_users");
            es.EstateGroups = LoadUUIDList(es.EstateID, "estate_groups");

            es.OnSave += StoreEstateSettings;
            return es;
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

            if (es.EstateID < 100)
                names.Remove("EstateID");

            string sql = $"insert into estate_settings (\"{string.Join("\",\"", names)}\") values (:{string.Join(", :", names)})";

            using (NpgsqlConnection conn = new NpgsqlConnection(m_connectionString))
            using (NpgsqlCommand insertCommand = new NpgsqlCommand(sql, conn))
            {
                foreach (string name in names)
                {
                    insertCommand.Parameters.Add(_Database.CreateParameter(name, _FieldMap[name].GetValue(es)));
                }

                conn.Open();

                if (insertCommand.ExecuteNonQuery() > 0 && es.EstateID < 100)
                {
                    insertCommand.CommandText = "select cast(lastval() as int) as ID";

                    using (NpgsqlDataReader result = insertCommand.ExecuteReader())
                    {
                        if (result.Read())
                            es.EstateID = (uint)result.GetInt32(0);
                    }
                }
            }

            es.Save();
        }

        public void StoreEstateSettings(EstateSettings es)
        {
            List<string> names = new List<string>(FieldList);
            names.Remove("EstateID");

            string sql = "UPDATE estate_settings SET ";
            foreach (string name in names)
                sql += $"\"{name}\" = :{name}, ";
            sql = sql.Remove(sql.LastIndexOf(","));
            sql += " WHERE \"EstateID\" = :EstateID";

            using (NpgsqlConnection conn = new NpgsqlConnection(m_connectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand(sql, conn))
            {
                foreach (string name in names)
                    cmd.Parameters.Add(_Database.CreateParameter(name, _FieldMap[name].GetValue(es)));

                cmd.Parameters.Add(_Database.CreateParameter("EstateID", es.EstateID));
                conn.Open();
                cmd.ExecuteNonQuery();
            }

            SaveBanList(es);
            SaveUUIDList(es.EstateID, "estate_managers", es.EstateManagers);
            SaveUUIDList(es.EstateID, "estate_users", es.EstateAccess);
            SaveUUIDList(es.EstateID, "estate_groups", es.EstateGroups);
        }

        #endregion

        #region Private methods

        private string[] FieldList => new List<string>(_FieldMap.Keys).ToArray();

        private void LoadBanList(EstateSettings es)
        {
            es.ClearBans();

            string sql = "select * from estateban where \"EstateID\" = :EstateID";

            using (NpgsqlConnection conn = new NpgsqlConnection(m_connectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("EstateID", (int)es.EstateID);
                conn.Open();
                using (NpgsqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        EstateBan eb = new EstateBan();
                        eb.BannedUserID = new UUID((Guid)reader["bannedUUID"]);
                        eb.BanningUserID = new UUID((Guid)reader["banningUUID"]);
                        eb.BanTime = Convert.ToInt32(reader["banTime"]);
                        eb.BannedHostAddress = "0.0.0.0";
                        eb.BannedHostIPMask = "0.0.0.0";
                        es.AddBan(eb);
                    }
                }
            }
        }

        private UUID[] LoadUUIDList(uint estateID, string table)
        {
            List<UUID> uuids = new List<UUID>();
            string sql = $"select uuid from {table} where \"EstateID\" = :EstateID";

            using (NpgsqlConnection conn = new NpgsqlConnection(m_connectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.Add(_Database.CreateParameter("EstateID", (int)estateID));
                conn.Open();
                using (NpgsqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        uuids.Add(new UUID((Guid)reader["uuid"]));
                }
            }

            return uuids.ToArray();
        }

        private void SaveBanList(EstateSettings es)
        {
            using (NpgsqlConnection conn = new NpgsqlConnection(m_connectionString))
            {
                conn.Open();
                using (NpgsqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "delete from estateban where \"EstateID\" = :EstateID";
                    cmd.Parameters.AddWithValue("EstateID", (int)es.EstateID);
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = "insert into estateban (\"EstateID\", \"bannedUUID\",\"bannedIp\", \"bannedIpHostMask\", \"bannedNameMask\", \"banningUUID\",\"banTime\" ) values ( :EstateID, :bannedUUID, '','','', :banningUUID, :banTime )";
                    foreach (EstateBan b in es.EstateBans)
                    {
                        cmd.Parameters.Clear();
                        cmd.Parameters["EstateID"].Value = b.EstateID;
                        cmd.Parameters["bannedUUID"].Value = _Database.CreateParameter("bannedUUID", b.BannedUserID).Value;
                        cmd.Parameters["banningUUID"].Value = _Database.CreateParameter("banningUUID", b.BanningUserID).Value;
                        cmd.Parameters["banTime"].Value = b.BanTime;
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        private void SaveUUIDList(uint estateID, string table, UUID[] data)
        {
            using (NpgsqlConnection conn = new NpgsqlConnection(m_connectionString))
            {
                conn.Open();
                using (NpgsqlCommand cmd = conn.CreateCommand())
                {
                    cmd.Parameters.AddWithValue("EstateID", (int)estateID);
                    cmd.CommandText = $"delete from {table} where \"EstateID\" = :EstateID";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = $"insert into {table} (\"EstateID\", uuid) values ( :EstateID, :uuid )";
                    foreach (UUID uuid in data)
                    {
                        cmd.Parameters.Clear();
                        cmd.Parameters.Add(_Database.CreateParameter("uuid", uuid));
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        public EstateSettings LoadEstateSettings(int estateID)
        {
            EstateSettings es = new EstateSettings();
            string sql = "select estate_settings.\"" + string.Join("\",estate_settings.\"", FieldList) + "\" from estate_settings where \"EstateID\" = :EstateID";

            using (NpgsqlConnection conn = new NpgsqlConnection(m_connectionString))
            {
                conn.Open();
                using (NpgsqlCommand cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("EstateID", estateID);
                    using (NpgsqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            foreach (string name in FieldList)
                            {
                                FieldInfo f = _FieldMap[name];
                                object v = reader[name];
                                if (f.FieldType == typeof(bool))
                                    f.SetValue(es, Convert.ToInt32(v) != 0);
                                else if (f.FieldType == typeof(UUID))
                                    f.SetValue(es, new UUID((Guid)v));
                                else if (f.FieldType == typeof(string))
                                    f.SetValue(es, v?.ToString());
                                else if (f.FieldType == typeof(UInt32))
                                    f.SetValue(es, Convert.ToUInt32(v));
                                else if (f.FieldType == typeof(Single))
                                    f.SetValue(es, Convert.ToSingle(v));
                                else
                                    f.SetValue(es, v);
                            }
                        }
                    }
                }
            }

            LoadBanList(es);
            es.EstateManagers = LoadUUIDList(es.EstateID, "estate_managers");
            es.EstateAccess = LoadUUIDList(es.EstateID, "estate_users");
            es.EstateGroups = LoadUUIDList(es.EstateID, "estate_groups");
            es.OnSave += StoreEstateSettings;
            return es;
        }

        public List<EstateSettings> LoadEstateSettingsAll()
        {
            List<EstateSettings> allEstateSettings = new List<EstateSettings>();
            foreach (int estateId in GetEstatesAll())
                allEstateSettings.Add(LoadEstateSettings(estateId));
            return allEstateSettings;
        }

        public List<int> GetEstates(string search)
        {
            List<int> result = new List<int>();
            string sql = "select \"EstateID\" from estate_settings where lower(\"EstateName\") = lower(:EstateName)";

            using (NpgsqlConnection conn = new NpgsqlConnection(m_connectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("EstateName", search);
                conn.Open();
                using (IDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        result.Add(Convert.ToInt32(reader["EstateID"]));
                }
            }

            return result;
        }

        public List<int> GetEstatesAll()
        {
            List<int> result = new List<int>();
            string sql = "select \"EstateID\" from estate_settings";

            using (NpgsqlConnection conn = new NpgsqlConnection(m_connectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand(sql, conn))
            {
                conn.Open();
                using (IDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        result.Add(Convert.ToInt32(reader["EstateID"]));
                }
            }

            return result;
        }

        public List<int> GetEstatesByOwner(UUID ownerID)
        {
            List<int> result = new List<int>();
            string sql = "select \"EstateID\" from estate_settings where \"EstateOwner\" = :EstateOwner";

            using (NpgsqlConnection conn = new NpgsqlConnection(m_connectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("EstateOwner", ownerID);
                conn.Open();
                using (IDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        result.Add(Convert.ToInt32(reader["EstateID"]));
                }
            }

            return result;
        }

        public bool LinkRegion(UUID regionID, int estateID)
        {
            string deleteSQL = "delete from estate_map where \"RegionID\" = :RegionID";
            string insertSQL = "insert into estate_map values (:RegionID, :EstateID)";

            using (NpgsqlConnection conn = new NpgsqlConnection(m_connectionString))
            {
                conn.Open();
                using (NpgsqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        using (NpgsqlCommand cmd = new NpgsqlCommand(deleteSQL, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("RegionID", regionID.Guid);
                            cmd.ExecuteNonQuery();
                        }

                        using (NpgsqlCommand cmd = new NpgsqlCommand(insertSQL, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("RegionID", regionID.Guid);
                            cmd.Parameters.AddWithValue("EstateID", estateID);
                            int ret = cmd.ExecuteNonQuery();
                            if (ret != 0)
                            {
                                transaction.Commit();
                                return true;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        m_log.Error("[REGION DB] LinkRegion failed: " + ex.Message, ex);
                        try
                        {
                            transaction.Rollback();
                        }
                        catch { }
                    }
                }
            }

            return false;
        }

        public List<UUID> GetRegions(int estateID)
        {
            List<UUID> result = new List<UUID>();
            string sql = "select \"RegionID\" from estate_map where \"EstateID\" = :EstateID";

            using (NpgsqlConnection conn = new NpgsqlConnection(m_connectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("EstateID", estateID);
                conn.Open();
                using (IDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        result.Add(DBGuid.FromDB(reader["RegionID"]));
                }
            }

            return result;
        }

        public bool DeleteEstate(int estateID)
        {
            // Implementation removed due to security concerns; operation not supported.
            m_log.Warn("[PGSQL] DeleteEstate called but not implemented.");
            return false;
        }

        #endregion
    }
}