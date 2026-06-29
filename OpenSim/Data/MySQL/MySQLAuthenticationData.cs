using System;
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

        protected virtual Assembly Assembly => GetType().Assembly;

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

                using (MySqlCommand cmd = new MySqlCommand(
                    $"SELECT * FROM `{m_Realm}` WHERE UUID = @principalID", dbcon))
                {
                    cmd.Parameters.AddWithValue("@principalID", principalID.ToString());

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

                                ret.Data[s] = result[s] != DBNull.Value ? result[s].ToString() : null;
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

        public bool Store(AuthenticationData data)
        {
            if (data.Data == null)
                return false;

            data.Data.Remove("UUID");

            string[] fields = new List<string>(data.Data.Keys).ToArray();

            using (MySqlCommand cmd = new MySqlCommand())
            {
                string update = $"UPDATE `{m_Realm}` SET ";
                bool first = true;
                foreach (string field in fields)
                {
                    if (!first)
                        update += ", ";
                    update += $"`{field}` = @{field}";
                    first = false;
                    cmd.Parameters.AddWithValue($"@{field}", data.Data[field]);
                }

                update += " WHERE UUID = @principalID";

                cmd.CommandText = update;
                cmd.Parameters.AddWithValue("@principalID", data.PrincipalID.ToString());

                if (ExecuteNonQuery(cmd) < 1)
                {
                    string insert = $"INSERT INTO `{m_Realm}` (`UUID`, `{string.Join("`, `", fields)}`) VALUES (@UUID, {string.Join(", @", fields)})";

                    cmd.CommandText = insert;
                    cmd.Parameters.AddWithValue("@UUID", data.PrincipalID.ToString());

                    if (ExecuteNonQuery(cmd) < 1)
                        return false;
                }
            }

            return true;
        }

        public bool SetDataItem(UUID principalID, string item, string value)
        {
            if (!IsValidSqlIdentifier(item))
            {
                m_log.ErrorFormat("[MySQLAuthenticationData]: Invalid data item name '{0}'", item);
                return false;
            }

            using (MySqlCommand cmd = new MySqlCommand(
                $"UPDATE `{m_Realm}` SET `{item}` = @value WHERE UUID = @UUID"))
            {
                cmd.Parameters.AddWithValue("@value", value);
                cmd.Parameters.AddWithValue("@UUID", principalID.ToString());

                if (ExecuteNonQuery(cmd) > 0)
                    return true;
            }

            return false;
        }

        public bool SetToken(UUID principalID, string token, int lifetime)
        {
            if (Environment.TickCount - m_LastExpire > 30000)
                DoExpire();

            using (MySqlCommand cmd = new MySqlCommand(
                "INSERT INTO tokens (UUID, token, validity) VALUES (@principalID, @token, DATE_ADD(NOW(), INTERVAL @lifetime MINUTE))"))
            {
                cmd.Parameters.AddWithValue("@principalID", principalID.ToString());
                cmd.Parameters.AddWithValue("@token", token);
                cmd.Parameters.AddWithValue("@lifetime", lifetime);

                if (ExecuteNonQuery(cmd) > 0)
                    return true;
            }

            return false;
        }

        public bool CheckToken(UUID principalID, string token, int lifetime)
        {
            if (Environment.TickCount - m_LastExpire > 30000)
                DoExpire();

            using (MySqlCommand cmd = new MySqlCommand(
                "UPDATE tokens SET validity = DATE_ADD(NOW(), INTERVAL @lifetime MINUTE) WHERE UUID = @principalID AND token = @token AND validity > NOW()"))
            {
                cmd.Parameters.AddWithValue("@principalID", principalID.ToString());
                cmd.Parameters.AddWithValue("@token", token);
                cmd.Parameters.AddWithValue("@lifetime", lifetime);

                if (ExecuteNonQuery(cmd) > 0)
                    return true;
            }

            return false;
        }

        private void DoExpire()
        {
            using (MySqlCommand cmd = new MySqlCommand("DELETE FROM tokens WHERE validity < NOW()"))
            {
                ExecuteNonQuery(cmd);
            }

            m_LastExpire = Environment.TickCount;
        }

        // Validate that an identifier contains only letters, digits, or underscore
        private static bool IsValidSqlIdentifier(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
                return false;

            foreach (char c in identifier)
            {
                if (!char.IsLetterOrDigit(c) && c != '_')
                    return false;
            }
            return true;
        }
    }
}