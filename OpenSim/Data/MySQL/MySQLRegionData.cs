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
using System.Threading;

namespace OpenSim.Data.MySQL
{
    public class MySqlRegionData : MySqlFramework, IRegionData
    {
        private readonly string m_Realm;
        private readonly List<string> m_ColumnNames;

        protected virtual Assembly Assembly
        {
            get { return GetType().Assembly; }
        }

        public MySqlRegionData(string connectionString, string realm)
            : base(connectionString)
        {
            lock (this) // Ensure thread safety
            {
                m_Realm = realm;
                string m_connectionString = connectionString;

                using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
                {
                    dbcon.Open();
                    Migration m = new Migration(dbcon, Assembly, "GridStore");
                    m.Update();
                    dbcon.Close();
                }
            }
        }

        public List<RegionData> Get(string regionName, UUID scopeID)
        {
            lock (this) // Ensure thread safety
            {
                string command = "SELECT * FROM `" + m_Realm + "` WHERE regionName LIKE ?regionName";
                if (scopeID.IsNotZero())
                    command += " AND ScopeID = ?scopeID";

                command += " ORDER BY regionName";

                using (MySqlCommand cmd = new MySqlCommand(command, Connection()))
                {
                    cmd.Parameters.AddWithValue("?regionName", regionName);
                    if (scopeID.IsNotZero())
                        cmd.Parameters.AddWithValue("?scopeID", scopeID.ToString());

                    return RunCommand<RegionData>(cmd);
                }
            }
        }

        public RegionData GetSpecific(string regionName, UUID scopeID)
        {
            lock (this) // Ensure thread safety
            {
                string command = "SELECT * FROM `" + m_Realm + "` WHERE regionName = ?regionName";
                if (scopeID.IsNotZero())
                    command += " AND ScopeID = ?scopeID";

                using (MySqlCommand cmd = new MySqlCommand(command, Connection()))
                {
                    cmd.Parameters.AddWithValue("?regionName", regionName);
                    if (scopeID.IsNotZero())
                        cmd.Parameters.AddWithValue("?scopeID", scopeID.ToString());

                    return RunCommand<RegionData>(cmd).FirstOrDefault();
                }
            }
        }

        public RegionData Get(int posX, int posY, UUID scopeID)
        {
            lock (this) // Ensure thread safety
            {
                string command = "SELECT * FROM `" + m_Realm + "` WHERE locX BETWEEN ?startX AND ?endX AND locY BETWEEN ?startY AND ?endY";
                if (scopeID.IsNotZero())
                    command += " AND ScopeID = ?scopeID";

                int startX = posX - (int)Constants.MaximumRegionSize;
                int startY = posY - (int)Constants.MaximumRegionSize;
                int endX = posX;
                int endY = posY;

                using (MySqlCommand cmd = new MySqlCommand(command, Connection()))
                {
                    cmd.Parameters.AddWithValue("?startX", startX.ToString());
                    cmd.Parameters.AddWithValue("?startY", startY.ToString());
                    cmd.Parameters.AddWithValue("?endX", endX.ToString());
                    cmd.Parameters.AddWithValue("?endY", endY.ToString());
                    if (scopeID.IsNotZero())
                        cmd.Parameters.AddWithValue("?scopeID", scopeID.ToString());
                    return RunCommand<RegionData>(cmd).FirstOrDefault();
                }
            }
        }

        public RegionData Get(UUID regionID, UUID scopeID)
        {
            lock (this) // Ensure thread safety
            {
                string command = "SELECT * FROM `" + m_Realm + "` WHERE uuid = ?regionID";
                if (!scopeID.IsZero())
                    command += " AND ScopeID = ?scopeID";

                using (MySqlCommand cmd = new MySqlCommand(command, Connection()))
                {
                    cmd.Parameters.AddWithValue("?regionID", regionID.ToString());
                    cmd.Parameters.AddWithValue("?scopeID", scopeID.ToString());
                    return RunCommand<RegionData>(cmd).FirstOrDefault();
                }
            }
        }

        public List<RegionData> Get(int startX, int startY, int endX, int endY, UUID scopeID)
        {
            lock (this) // Ensure thread safety
            {
                string command = "SELECT * FROM `" + m_Realm + "` WHERE locX BETWEEN ?startX AND ?endX AND locY BETWEEN ?startY AND ?endY";
                if (scopeID.IsNotZero())
                    command += " AND ScopeID = ?scopeID";

                int qstartX = startX - (int)Constants.MaximumRegionSize;
                int qstartY = startY - (int)Constants.MaximumRegionSize;

                using (MySqlCommand cmd = new MySqlCommand(command, Connection()))
                {
                    cmd.Parameters.AddWithValue("?startX", qstartX.ToString());
                    cmd.Parameters.AddWithValue("?startY", qstartY.ToString());
                    cmd.Parameters.AddWithValue("?endX", endX.ToString());
                    cmd.Parameters.AddWithValue("?endY", endY.ToString());
                    cmd.Parameters.AddWithValue("?scopeID", scopeID.ToString());
                    return RunCommand<RegionData>(cmd).Where(r => r.posX + r.sizeX > startX && r.posY + r.sizeY > startY && r.posX <= endX && r.posY <= endY).ToList();
                }
            }
        }

        public List<T> RunCommand<T>(MySqlCommand cmd) where T : RegionData
        {
            List<T> retList = new List<T>();

            lock (this) // Ensure thread safety
            {
                using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
                {
                    dbcon.Open();
                    cmd.Connection = dbcon;

                    using (IDataReader result = cmd.ExecuteReader())
                    {
                        while (result.Read())
                        {
                            T ret = new T();
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
            lock (this) // Ensure thread safety
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

                using (MySqlCommand cmd = new MySqlCommand("UPDATE `" + m_Realm + "` SET locX = ?posX, locY = ?posY, sizeX = ?sizeX, sizeY = ?sizeY"))
                {
                    cmd.Connection = Connection();
                    cmd.Parameters.AddWithValue("?posX", data.posX);
                    cmd.Parameters.AddWithValue("?posY", data.posY);
                    cmd.Parameters.AddWithValue("?sizeX", data.sizeX);
                    cmd.Parameters.AddWithValue("?sizeY", data.sizeY);
                    foreach (string field in fields)
                    {
                        if (field == "uuid" || field == "ScopeID" || field == "regionName" || field == "locX" || field == "locY")
                            continue;

                        cmd.Parameters.AddWithValue($"?{field}", data.Data[field]);
                        cmd.CommandText += $", `{field}` = ?{field}";
                    }

                    cmd.CommandText += $" WHERE uuid = ?regionID AND regionName = ?regionName";
                    if (!data.ScopeID.IsZero())
                        cmd.CommandText += $" AND ScopeID = ?scopeID";

                    cmd.Parameters.AddWithValue("?regionID", data.RegionID.ToString());
                    cmd.Parameters.AddWithValue("?regionName", data.RegionName);
                    if (!data.ScopeID.IsZero())
                        cmd.Parameters.AddWithValue("?scopeID", data.ScopeID.ToString());

                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        private MySqlConnection Connection()
        {
            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();
                return dbcon;
            }
        }
    }
}
```

This refactored version aims to address potential security vulnerabilities:

1.  **SQL Injection**: The original code directly inserted user-input parameters (`regionName`,