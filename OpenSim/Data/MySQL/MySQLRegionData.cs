using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
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
        //private string m_connectionString;

        protected virtual Assembly Assembly
        {
            get { return GetType().Assembly; }
        }

        public MySqlRegionData(string connectionString, string realm)
                : base(connectionString)
        {
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

        public List<RegionData> Get(string regionName, UUID scopeID)
        {
            string command = "select * from " + EscapeIdentifier(m_Realm) + " where regionName like ?regionName";
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
            string command = "select * from " + EscapeIdentifier(m_Realm) + " where regionName = ?regionName";
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
            string command = "select * from " + EscapeIdentifier(m_Realm) + " where locX between ?startX and ?endX and locY between ?startY and ?endY";
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
            string command = "select * from " + EscapeIdentifier(m_Realm) + " where uuid = ?regionID";
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
            string command = "select * from " + EscapeIdentifier(m_Realm) + " where locX between ?startX and ?endX and locY between ?startY and ?endY";
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
                            if (value is DBNull)
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

            string[] fields = new List<string>(data.Data.Keys).ToArray();

            using (MySqlCommand cmd = new MySqlCommand())
            {
                string update = "update " + EscapeIdentifier(m_Realm) + " set locX=?posX, locY=?posY, sizeX=?sizeX, sizeY=?sizeY";
                foreach (string field in fields)
                {
                    update += ", ";
                    update += EscapeIdentifier(field) + " = ?" + field;

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
                    string insert = "insert into " + EscapeIdentifier(m_Realm) + " (`uuid`, `ScopeID`, `locX`, `locY`, `sizeX`, `sizeY`, `regionName`, `" +
                            String.Join("`, `", fields) +
                            "`) values ( ?regionID, ?scopeID, ?posX, ?posY, ?sizeX, ?sizeY, ?regionName, ?" + String.Join(", ?", fields) + ")";

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
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            string escapedItem = EscapeIdentifier(item);
            using (MySqlCommand cmd = new MySqlCommand("update " + EscapeIdentifier(m_Realm) + " set " + escapedItem + " = ?value where uuid = ?UUID"))
            {
                cmd.Parameters.AddWithValue("?value", value);
                cmd.Parameters.AddWithValue("?UUID", regionID.ToString());

                if (ExecuteNonQuery(cmd) > 0)
                    return true;
            }

            return false;
        }

        public bool Delete(UUID regionID)
        {
            using (MySqlCommand cmd = new MySqlCommand("delete from " + EscapeIdentifier(m_Realm) + " where uuid = ?UUID"))
            {
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
            string command = "select * from " + EscapeIdentifier(m_Realm) + " where (flags & " + regionFlags.ToString() + ") <> 0";
            if (!scopeID.IsZero())
                command += " and ScopeID = ?scopeID";

            using (MySqlCommand cmd = new MySqlCommand(command))
            {
                cmd.Parameters.AddWithValue("?scopeID", scopeID.ToString());

                return RunCommand(cmd);
            }
        }

        private static string EscapeIdentifier(string identifier)
        {
            if (identifier == null)
                throw new ArgumentNullException(nameof(identifier));
            return "`" + identifier.Replace("`", "``") + "`";
        }
    }
}