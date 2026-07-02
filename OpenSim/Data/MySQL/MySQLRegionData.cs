using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using System.Text.RegularExpressions;
using MySql.Data.MySqlClient;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Data;
using RegionFlags = OpenSim.Framework.RegionFlags;

namespace OpenSim.Data.MySQL
{
    public class MySqlRegionData : MySqlFramework, IRegionData
    {
        private string m_Realm;
        private List<string> m_ColumnNames;
        private string m_connectionString;

        // Whitelisted column names that can be stored
        private static readonly HashSet<string> StoreAllowedColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "RegionName","PosX","PosY","SizeX","SizeY","ScopeID","Uuid"
        };

        // Valid characters for realm identifiers
        private static readonly Regex RealmNameRegex = new Regex(@"^[a-zA-Z0-9_]+$", RegexOptions.Compiled);

        protected virtual Assembly Assembly
        {
            get { return GetType().Assembly; }
        }

        public MySqlRegionData(string connectionString, string realm)
            : base(connectionString)
        {
            // Validate and sanitize the realm name before use
            if (!RealmNameRegex.IsMatch(realm))
                throw new ArgumentException("Invalid realm name format. Only alphanumeric characters and underscores are allowed.");

            m_Realm = realm;
            m_connectionString = connectionString;

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();
                Migration m = new Migration(dbcon, Assembly, "GridStore");
                m.Update();
                dbcon.Close();
            }
        }

        private string SanitizeRealmName()
        {
            // Use the already validated m_Realm, but ensure it is safely quoted
            return m_Realm;
        }

        public List<RegionData> Get(string regionName, UUID scopeID)
        {
            string safeRealm = SanitizeRealmName();
            string command = $"select * from `{safeRealm}` where regionName like ?regionName";
            if (scopeID.IsNotZero())
                command += " and ScopeID = ?scopeID";

            command += " order by regionName";

            using (MySqlCommand cmd = new MySqlCommand(command))
            {
                cmd.Parameters.AddWithValue("?regionName", regionName);
                if (scopeID.IsNotZero())
                    cmd.Parameters.AddWithValue("?scopeID", scopeID.ToString());

                return RunCommand(cmd);
            }
        }

        public RegionData GetSpecific(string regionName, UUID scopeID)
        {
            string safeRealm = SanitizeRealmName();
            string command = $"select * from `{safeRealm}` where regionName = ?regionName";
            if (scopeID.IsNotZero())
                command += " and ScopeID = ?scopeID";

            using (MySqlCommand cmd = new MySqlCommand(command))
            {
                cmd.Parameters.AddWithValue("?regionName", regionName);
                if (scopeID.IsNotZero())
                    cmd.Parameters.AddWithValue("?scopeID", scopeID.ToString());

                List<RegionData> ret = RunCommand(cmd);
                if (ret.Count == 0)
                    return null;

                return ret[0];
            }
        }

        public RegionData Get(int posX, int posY, UUID scopeID)
        {
            string safeRealm = SanitizeRealmName();
            string command = $"select * from `{safeRealm}` where locX between ?startX and ?endX and locY between ?startY and ?endY";
            if (scopeID.IsNotZero())
                command += " and ScopeID = ?scopeID";

            int startX = posX - (int)Constants.MaximumRegionSize;
            int startY = posY - (int)Constants.MaximumRegionSize;
            int endX = posX;
            int endY = posY;

            List<RegionData> ret;
            using (MySqlCommand cmd = new MySqlCommand(command))
            {
                cmd.Parameters.AddWithValue("?startX", startX.ToString());
                cmd.Parameters.AddWithValue("?startY", startY.ToString());
                cmd.Parameters.AddWithValue("?endX", endX.ToString());
                cmd.Parameters.AddWithValue("?endY", endY.ToString());
                if (scopeID.IsNotZero())
                    cmd.Parameters.AddWithValue("?scopeID", scopeID.ToString());

                ret = RunCommand(cmd);
            }

            if (ret.Count == 0)
                return null;

            // find the first that contains pos
            RegionData rg = null;
            foreach (RegionData r in ret)
            {
                if (posX >= r.posX && posX < r.posX + r.sizeX
                    && posY >= r.posY && posY < r.posY + r.sizeY)
                {
                    rg = r;
                    break;
                }
            }

            return rg;
        }

        public RegionData Get(UUID regionID, UUID scopeID)
        {
            string safeRealm = SanitizeRealmName();
            string command = $"select * from `{safeRealm}` where uuid = ?regionID";
            if (!scopeID.IsZero())
                command += " and ScopeID = ?scopeID";

            using (MySqlCommand cmd = new MySqlCommand(command))
            {
                cmd.Parameters.AddWithValue("?regionID", regionID.ToString());
                cmd.Parameters.AddWithValue("?scopeID", scopeID.ToString());

                List<RegionData> ret = RunCommand(cmd);
                if (ret.Count == 0)
                    return null;

                return ret[0];
            }
        }

        public List<RegionData> Get(int startX, int startY, int endX, int endY, UUID scopeID)
        {
            string safeRealm = SanitizeRealmName();
            string command = $"select * from `{safeRealm}` where locX between ?startX and ?endX and locY between ?startY and ?endY";
            if (scopeID != UUID.Zero)
                command += " and ScopeID = ?scopeID";

            int qstartX = startX - (int)Constants.MaximumRegionSize;
            int qstartY = startY - (int)Constants.MaximumRegionSize;

            List<RegionData> dbret;
            using (MySqlCommand cmd = new MySqlCommand(command))
            {
                cmd.Parameters.AddWithValue("?startX", qstartX.ToString());
                cmd.Parameters.AddWithValue("?startY", qstartY.ToString());
                cmd.Parameters.AddWithValue("?endX", endX.ToString());
                cmd.Parameters.AddWithValue("?endY", endY.ToString());
                cmd.Parameters.AddWithValue("?scopeID", scopeID.ToString());

                dbret = RunCommand(cmd);
            }

            List<RegionData> ret = new List<RegionData>();

            if (dbret.Count == 0)
                return ret;

            foreach (RegionData r in dbret)
            {
                if (r.posX + r.sizeX > startX && r.posX <= endX
                    && r.posY + r.sizeY > startY && r.posY <= endY)
                    ret.Add(r);
            }
            return ret;
        }

        public List<RegionData> RunCommand(MySqlCommand cmd)
        {
            List<RegionData> retList = new List<RegionData>();

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();
                cmd.Connection = dbcon;

                using (IDataReader result = cmd.ExecuteReader())
                {
                    while (result.Read())
                    {
                        RegionData ret = new RegionData();
                        ret.Data = new Dictionary<string, object>();

                        ret.RegionID = DBGuid.FromDB(result["uuid"]);
                        ret.ScopeID = DBGuid.FromDB(result["ScopeID"]);

                        ret.RegionName = result["regionName"].ToString();
                        ret.posX = Convert.ToInt32(result["locX"]);
                        ret.posY = Convert.ToInt32(result["locY"]);
                        ret.sizeX = Convert.ToInt32(result["sizeX"]);
                        ret.sizeY = Convert.ToInt32(result["sizeY"]);

                        CheckColumnNames(result);

                        foreach (string s in m_ColumnNames)
                        {
                            if (s == "uuid")
                                continue;
                            if (s == "ScopeID")
                                continue;
                            if (s == "regionName")
                                continue;
                            if (s == "locX")
                                continue;
                            if (s == "locY")
                                continue;

                            object value = result[s];
                            if (value == DBNull.Value)
                                ret.Data[s] = null;
                            else
                                ret.Data[s] = result[s].ToString();
                        }

                        retList.Add(ret);
                    }
                }
                cmd.Connection = null;
                dbcon.Close();
            }

            return retList;
        }

        private void CheckColumnNames(IDataReader result)
        {
            if (m_ColumnNames != null)
                return;

            List<string> columnNames = new List<string>();

            DataTable schemaTable = result.GetSchemaTable();
            foreach (DataRow row in schemaTable.Rows)
            {
                if (row["ColumnName"] != null)
                    columnNames.Add(row["ColumnName"].ToString());
            }

            m_ColumnNames = columnNames;
        }

        public bool Store(RegionData data)
        {
            // Remove internal keys before building the store payload
            data.Data.Remove("uuid");
            data.Data.Remove("ScopeID");
            data.Data.Remove("regionName");
            data.Data.Remove("posX");
            data.Data.Remove("posY");
            data.Data.Remove("sizeX");
            data.Data.Remove("sizeY");
            data.Data.Remove("locX");
            data.Data.Remove("locY");

            if (data.RegionName.Length > 128)
                data.RegionName = data.RegionName.Substring(0, 128);

            // Filter keys to only those that are allowed to be stored
            var filteredKeys = new List<string>();
            foreach (var key in data.Data.Keys)
            {
                if (StoreAllowedColumns.Contains(key))
                    filteredKeys.Add(key);
            }

            string[] fields = filteredKeys.ToArray();

            using (MySqlCommand cmd = new MySqlCommand())
            {
                string safeRealm = SanitizeRealmName();
                string update = $"update `{safeRealm}` set locX=?posX, locY=?posY, sizeX=?sizeX, sizeY=?sizeY";
                foreach (string field in fields)
                {
                    update += ", `";
                    update += field;
                    update += "` = ?" + field;

                    cmd.Parameters.AddWithValue("?" + field, data.Data[field]);
                }

                update += " where uuid = ?regionID";

                if (!data.ScopeID.IsZero())
                    update += " and ScopeID = ?scopeID";

                cmd.CommandText = update;
                cmd.Parameters.AddWithValue("?regionID", data.RegionID.ToString());
                cmd.Parameters.AddWithValue("?regionName", data.RegionName);
                cmd.Parameters.AddWithValue("?scopeID", data.ScopeID.ToString());
                cmd.Parameters.AddWithValue("?posX", data.posX.ToString());
                cmd.Parameters.AddWithValue("?posY", data.posY.ToString());
                cmd.Parameters.AddWithValue("?sizeX", data.sizeX.ToString());
                cmd.Parameters.AddWithValue("?sizeY", data.sizeY.ToString());

                if (ExecuteNonQuery(cmd) < 1)
                {
                    string insert = $"insert into `{safeRealm}` (`uuid`, `ScopeID`, `locX`, `locY`, `sizeX`, `sizeY`, `regionName`, `";
                    string fieldList = string.Join("`, `", fields);
                    insert += fieldList;
                    insert += "`) values ( ?regionID, ?scopeID, ?posX, ?posY, ?sizeX, ?sizeY, ?regionName, ?" + string.Join(", ?", fields) + ")";

                    cmd.CommandText = insert;

                    if (ExecuteNonQuery(cmd) < 1)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public bool SetDataItem(UUID regionID, string item, string value)
        {
            // Validate column name to prevent identifier injection
            if (!StoreAllowedColumns.Contains(item))
                return false;

            using (MySqlCommand cmd = new MySqlCommand())
            {
                string safeRealm = SanitizeRealmName();
                cmd.CommandText = $"update `{safeRealm}` set `{item}` = ?{item} where uuid = ?UUID";
                cmd.Parameters.AddWithValue("?" + item, value);
                cmd.Parameters.AddWithValue("?UUID", regionID.ToString());

                if (ExecuteNonQuery(cmd) > 0)
                    return true;
            }

            return false;
        }

        public bool Delete(UUID regionID)
        {
            string safeRealm = SanitizeRealmName();
            using (MySqlCommand cmd = new MySqlCommand())
            {
                cmd.CommandText = $"delete from `{safeRealm}` where uuid = ?UUID";
                cmd.Parameters.AddWithValue("?UUID", regionID.ToString());

                if (ExecuteNonQuery(cmd) > 0)
                    return true;
            }

            return false;
        }

        public List<RegionData> GetDefaultRegions(UUID scopeID)
        {
            return Get((int)RegionFlags.DefaultRegion, scopeID);
        }

        public List<RegionData> GetDefaultHypergridRegions(UUID scopeID)
        {
            return Get((int)RegionFlags.DefaultHGRegion, scopeID);
        }

        public List<RegionData> GetFallbackRegions(UUID scopeID)
        {
            return Get((int)RegionFlags.FallbackRegion, scopeID);
        }

        public List<RegionData> GetHyperlinks(UUID scopeID)
        {
            return Get((int)RegionFlags.Hyperlink, scopeID);
        }

        public List<RegionData> GetOnlineRegions(UUID scopeID)
        {
            return Get((int)RegionFlags.RegionOnline, scopeID);
        }

        private List<RegionData> Get(int regionFlags, UUID scopeID)
        {
            string safeRealm = SanitizeRealmName();
            string command = $"select * from `{safeRealm}` where (flags & {regionFlags}) <> 0";
            if (!scopeID.IsZero())
                command += " and ScopeID = ?scopeID";

            using (MySqlCommand cmd = new MySqlCommand(command))
            {
                cmd.Parameters.AddWithValue("?scopeID", scopeID.ToString());

                return RunCommand(cmd);
            }
        }
    }
}