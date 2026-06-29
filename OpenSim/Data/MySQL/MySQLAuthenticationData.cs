using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using OpenMetaverse;
using OpenSim.Framework;
using MySql.Data.MySqlClient;

namespace OpenSim.Data.MySQL
{
    public class MySqlAuthenticationData : MySqlFramework, IAuthenticationData
    {
        private string m_Realm;
        private List<string> m_ColumnNames;
        private int m_LastExpire;

        protected virtual Assembly Assembly
        {
            get { return GetType().Assembly; }
        }

        public MySqlAuthenticationData(string connectionString, string realm)
                : base(connectionString)
        {
            m_Realm = realm;
            m_connectionString = connectionString;

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();
                Migration m = new Migration(dbcon, Assembly, "AuthStore");
                m.Update();
                dbcon.Close();
            }
        }

        public AuthenticationData Get(UUID principalID)
        {
            AuthenticationData ret = new AuthenticationData();
            ret.Data = new Dictionary<string, object>();

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd
                    = new MySqlCommand("select * from `" + m_Realm + "` where UUID = ?principalID", dbcon))
                {
                    cmd.Parameters.AddWithValue("?principalID", principalID.ToString());

                    using (IDataReader result = cmd.ExecuteReader())
                    {
                        if (result.Read())
                        {
                            ret.PrincipalID = principalID;

                            CheckColumnNames(result);

                            foreach (string s in m_ColumnNames)
                            {
                                if (s == "UUID")
                                    continue;

                                ret.Data[s] = result[s].ToString();
                            }

                            dbcon.Close();
                            return ret;
                        }
                        else
                        {
                            dbcon.Close();
                            return null;
                        }
                    }
                }
            }
        }

        private void CheckColumnNames(IDataReader result)
        {
            if (m_ColumnNames != null)
                return;

            List<string> columnNames = new List<string>();

            DataTable schemaTable = result.GetSchemaTable();
            foreach (DataRow row in schemaTable.Rows)
                columnNames.Add(row["ColumnName"].ToString());

            m_ColumnNames = columnNames;
        }

        private bool IsValidColumn(string column)
        {
            // Ensure column list has been initialized
            if (m_ColumnNames == null)
                return false;

            return m_ColumnNames.Contains(column);
        }

        private bool AreValidColumns(IEnumerable<string> columns)
        {
            foreach (var col in columns)
            {
                if (!IsValidColumn(col))
                    return false;
            }
            return true;
        }

        public bool Store(AuthenticationData data)
        {
            data.Data.Remove("UUID");

            string[] fields = new List<string>(data.Data.Keys).ToArray();

            // Validate column names to prevent SQL injection
            if (!AreValidColumns(fields))
                return false;

            using (MySqlCommand cmd = new MySqlCommand())
            {
                string update = "update `" + m_Realm + "` set ";
                bool first = true;
                foreach (string field in fields)
                {
                    if (!first)
                        update += ", ";
                    update += "`" + field + "` = ?" + field;

                    first = false;

                    cmd.Parameters.AddWithValue("?" + field, data.Data[field]);
                }

                update += " where UUID = ?principalID";

                cmd.CommandText = update;
                cmd.Parameters.AddWithValue("?principalID", data.PrincipalID.ToString());

                if (ExecuteNonQuery(cmd) < 1)
                {
                    string insert = "insert into `" + m_Realm + "` (`UUID`, `" +
                                    String.Join("`, `", fields) +
                                    "`) values (?principalID, ?" + String.Join(", ?", fields) + ")";

                    cmd.CommandText = insert;

                    if (ExecuteNonQuery(cmd) < 1)
                        return false;
                }
            }

            return true;
        }

        public bool SetDataItem(UUID principalID, string item, string value)
        {
            // Validate column name to prevent injection
            if (!IsValidColumn(item))
                return false;

            using (MySqlCommand cmd
                = new MySqlCommand("update `" + m_Realm + "` set `" + item + "` = ?" + item + " where UUID = ?UUID"))
            {
                cmd.Parameters.AddWithValue("?" + item, value);
                cmd.Parameters.AddWithValue("?UUID", principalID.ToString());

                if (ExecuteNonQuery(cmd) > 0)
                    return true;
            }

            return false;
        }

        public bool SetToken(UUID principalID, string token, int lifetime)
        {
            if (System.Environment.TickCount - m_LastExpire > 30000)
                DoExpire();

            using (MySqlCommand cmd
                = new MySqlCommand(
                    "insert into tokens (UUID, token, validity) values (?principalID, ?token, date_add(now(), interval ?lifetime minute))"))
            {
                cmd.Parameters.AddWithValue("?principalID", principalID.ToString());
                cmd.Parameters.AddWithValue("?token", token);
                cmd.Parameters.AddWithValue("?lifetime", lifetime.ToString());

                if (ExecuteNonQuery(cmd) > 0)
                    return true;
            }

            return false;
        }

        public bool CheckToken(UUID principalID, string token, int lifetime)
        {
            if (System.Environment.TickCount - m_LastExpire > 30000)
                DoExpire();

            using (MySqlCommand cmd
                = new MySqlCommand(
                    "update tokens set validity = date_add(now(), interval ?lifetime minute) where UUID = ?principalID and token = ?token and validity > now()"))
            {
                cmd.Parameters.AddWithValue("?principalID", principalID.ToString());
                cmd.Parameters.AddWithValue("?token", token);
                cmd.Parameters.AddWithValue("?lifetime", lifetime.ToString());

                if (ExecuteNonQuery(cmd) > 0)
                    return true;
            }

            return false;
        }

        private void DoExpire()
        {
            using (MySqlCommand cmd = new MySqlCommand("delete from tokens where validity < now()"))
            {
                ExecuteNonQuery(cmd);
            }

            m_LastExpire = System.Environment.TickCount;
        }
    }
}