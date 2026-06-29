using System;
using System.Reflection;
using System.Collections.Generic;
using System.Data;
using System.Text.RegularExpressions;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using log4net;
using MySql.Data.MySqlClient;
using OpenMetaverse;

namespace OpenSim.Data.MySQL
{
    public class MySQLFSAssetData : IFSAssetDataPlugin
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly Regex TableNameRegex = new Regex(@"^[A-Za-z0-9_]+$");
        protected string m_ConnectionString;
        protected string m_Table;

        /// <summary>
        /// Number of days that must pass before we update the access time on an asset when it has been fetched
        /// Config option to change this is "DaysBetweenAccessTimeUpdates"
        /// </summary>
        private int DaysBetweenAccessTimeUpdates = 0;

        protected virtual Assembly Assembly => GetType().Assembly;

        public MySQLFSAssetData()
        {
        }

        #region IPlugin Members

        public string Version => "1.0.0.0";

        public void Initialise(string connect, string realm, int UpdateAccessTime)
        {
            m_ConnectionString = connect;
            ValidateTableName(realm);
            m_Table = realm;
            DaysBetweenAccessTimeUpdates = UpdateAccessTime;

            try
            {
                using (MySqlConnection conn = new MySqlConnection(m_ConnectionString))
                {
                    conn.Open();
                    Migration m = new Migration(conn, Assembly, "FSAssetStore");
                    m.Update();
                }
            }
            catch (MySqlException e)
            {
                m_log.ErrorFormat("[FSASSETS]: Can't connect to database: {0}", e.Message);
            }
        }

        public void Initialise()
        {
            throw new NotImplementedException();
        }

        public void Dispose() { }

        public string Name => "MySQL FSAsset storage engine";

        #endregion

        private void ValidateTableName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || !TableNameRegex.IsMatch(name))
                throw new ArgumentException("Invalid table name supplied.", nameof(name));
        }

        private bool ExecuteNonQuery(MySqlCommand cmd)
        {
            using (MySqlConnection conn = new MySqlConnection(m_ConnectionString))
            {
                try
                {
                    conn.Open();
                }
                catch (MySqlException e)
                {
                    m_log.ErrorFormat("[FSASSETS]: Database open failed with {0}", e.ToString());
                    return false;
                }

                cmd.Connection = conn;
                try
                {
                    cmd.ExecuteNonQuery();
                }
                catch (MySqlException e)
                {
                    cmd.Connection = null;
                    conn.Close();
                    m_log.ErrorFormat("[FSASSETS]: Query {0} failed with {1}", cmd.CommandText, e.ToString());
                    return false;
                }
                conn.Close();
                cmd.Connection = null;
            }

            return true;
        }

        #region IFSAssetDataPlugin Members

        public AssetMetadata Get(string id, out string hash)
        {
            hash = string.Empty;
            AssetMetadata meta = new AssetMetadata();

            using (MySqlConnection conn = new MySqlConnection(m_ConnectionString))
            {
                try
                {
                    conn.Open();
                }
                catch (MySqlException e)
                {
                    m_log.ErrorFormat("[FSASSETS]: Database open failed with {0}", e.ToString());
                    return null;
                }

                using (MySqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = $"SELECT id, name, description, type, hash, create_time, asset_flags, access_time FROM `{m_Table}` WHERE id = ?id";
                    cmd.Parameters.AddWithValue("?id", id);

                    using (IDataReader reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read())
                            return null;

                        hash = reader["hash"].ToString();

                        meta.ID = id;
                        meta.FullID = new UUID(id);
                        meta.Name = reader["name"].ToString();
                        meta.Description = reader["description"].ToString();
                        meta.Type = (sbyte)Convert.ToInt32(reader["type"]);
                        meta.ContentType = SLUtil.SLAssetTypeToContentType(meta.Type);
                        meta.CreationDate = Util.ToDateTime(Convert.ToInt32(reader["create_time"]));
                        meta.Flags = (AssetFlags)Convert.ToInt32(reader["asset_flags"]);

                        int accessTime = Convert.ToInt32(reader["access_time"]);
                        UpdateAccessTime(id, accessTime);
                    }
                }
                conn.Close();
            }

            return meta;
        }

        private void UpdateAccessTime(string assetID, int accessTime)
        {
            if (DaysBetweenAccessTimeUpdates > 0 &&
                (DateTime.UtcNow - Utils.UnixTimeToDateTime(accessTime)).TotalDays < DaysBetweenAccessTimeUpdates)
                return;

            using (MySqlConnection conn = new MySqlConnection(m_ConnectionString))
            {
                try
                {
                    conn.Open();
                }
                catch (MySqlException e)
                {
                    m_log.ErrorFormat("[FSASSETS]: Database open failed with {0}", e.ToString());
                    return;
                }

                using (MySqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = $"UPDATE `{m_Table}` SET `access_time` = UNIX_TIMESTAMP() WHERE `id` = ?id";
                    cmd.Parameters.AddWithValue("?id", assetID);
                    cmd.ExecuteNonQuery();
                }
                conn.Close();
            }
        }

        public bool Store(AssetMetadata meta, string hash)
        {
            try
            {
                string oldhash;
                AssetMetadata existingAsset = Get(meta.ID, out oldhash);

                using (MySqlCommand cmd = new MySqlCommand())
                {
                    cmd.Parameters.AddWithValue("?id", meta.ID);
                    cmd.Parameters.AddWithValue("?name", meta.Name);
                    cmd.Parameters.AddWithValue("?description", meta.Description);
                    cmd.Parameters.AddWithValue("?type", meta.Type);
                    cmd.Parameters.AddWithValue("?hash", hash);
                    cmd.Parameters.AddWithValue("?asset_flags", meta.Flags);

                    if (existingAsset == null)
                    {
                        cmd.CommandText = $"INSERT INTO `{m_Table}` (id, name, description, type, hash, asset_flags, create_time, access_time) " +
                                          $"VALUES (?id, ?name, ?description, ?type, ?hash, ?asset_flags, UNIX_TIMESTAMP(), UNIX_TIMESTAMP())";
                        ExecuteNonQuery(cmd);
                        return true;
                    }

                    // Asset exists; keep existing hash and update access time
                    // The existing hash is already in the database; only update the access time
                    cmd.CommandText = $"UPDATE `{m_Table}` SET hash = ?hash, access_time = UNIX_TIMESTAMP() WHERE id = ?id";
                    ExecuteNonQuery(cmd);
                    return true;
                }
            }
            catch (Exception e)
            {
                m_log.Error("[FSAssets] Failed to store asset with ID " + meta.ID);
                m_log.Error(e.ToString());
                return false;
            }
        }

        public bool[] AssetsExist(UUID[] uuids)
        {
            if (uuids.Length == 0)
                return Array.Empty<bool>();

            bool[] results = new bool[uuids.Length];
            for (int i = 0; i < uuids.Length; i++)
                results[i] = false;

            HashSet<UUID> exists = new HashSet<UUID>();

            string ids = "'" + string.Join("','", uuids) + "'";
            string sql = $"SELECT id FROM `{m_Table}` WHERE id IN ({ids})";

            using (MySqlConnection conn = new MySqlConnection(m_ConnectionString))
            {
                try
                {
                    conn.Open();
                }
                catch (MySqlException e)
                {
                    m_log.ErrorFormat("[FSASSETS]: Failed to open database: {0}", e.ToString());
                    return results;
                }

                using (MySqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = sql;

                    using (MySqlDataReader dbReader = cmd.ExecuteReader())
                    {
                        while (dbReader.Read())
                        {
                            UUID id = DBGuid.FromDB(dbReader["ID"]);
                            exists.Add(id);
                        }
                    }
                }
                conn.Close();
            }

            for (int i = 0; i < uuids.Length; i++)
                results[i] = exists.Contains(uuids[i]);

            return results;
        }

        public int Count()
        {
            int count = 0;

            using (MySqlConnection conn = new MySqlConnection(m_ConnectionString))
            {
                try
                {
                    conn.Open();
                }
                catch (MySqlException e)
                {
                    m_log.ErrorFormat("[FSASSETS]: Failed to open database: {0}", e.ToString());
                    return 0;
                }

                using (MySqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = $"SELECT COUNT(*) AS count FROM `{m_Table}`";

                    using (IDataReader reader = cmd.ExecuteReader())
                    {
                        reader.Read();
                        count = Convert.ToInt32(reader["count"]);
                    }
                }
                conn.Close();
            }

            return count;
        }

        public bool Delete(string id)
        {
            using (MySqlCommand cmd = new MySqlCommand())
            {
                cmd.CommandText = $"DELETE FROM `{m_Table}` WHERE id = ?id";
                cmd.Parameters.AddWithValue("?id", id);

                ExecuteNonQuery(cmd);
            }

            return true;
        }

        public void Import(string conn, string table, int start, int count, bool force, FSStoreDelegate store)
        {
            ValidateTableName(table);
            int imported = 0;

            using (MySqlConnection importConn = new MySqlConnection(conn))
            {
                try
                {
                    importConn.Open();
                }
                catch (MySqlException e)
                {
                    m_log.ErrorFormat("[FSASSETS]: Can't connect to database: {0}", e.Message);
                    return;
                }

                using (MySqlCommand cmd = importConn.CreateCommand())
                {
                    string limit = string.Empty;
                    if (count != -1)
                    {
                        limit = $" LIMIT {start},{count}";
                    }

                    cmd.CommandText = $"SELECT * FROM `{table}`{limit}";

                    MainConsole.Instance.Output("Querying database");
                    using (IDataReader reader = cmd.ExecuteReader())
                    {
                        MainConsole.Instance.Output("Reading data");

                        while (reader.Read())
                        {
                            if ((imported % 100) == 0)
                                MainConsole.Instance.Output($"{imported} assets imported so far");

                            AssetBase asset = new AssetBase();
                            AssetMetadata meta = new AssetMetadata();

                            meta.ID = reader["id"].ToString();
                            meta.FullID = new UUID(meta.ID);
                            meta.Name = reader["name"].ToString();
                            meta.Description = reader["description"].ToString();
                            meta.Type = (sbyte)Convert.ToInt32(reader["assetType"]);
                            meta.ContentType = SLUtil.SLAssetTypeToContentType(meta.Type);
                            meta.CreationDate = Util.ToDateTime(Convert.ToInt32(reader["create_time"]));

                            asset.Metadata = meta;
                            asset.Data = (byte[])reader["data"];

                            store(asset, force);
                            imported++;
                        }
                    }
                }
                importConn.Close();
            }

            MainConsole.Instance.Output($"Import done, {imported} assets imported");
        }

        #endregion
    }
}