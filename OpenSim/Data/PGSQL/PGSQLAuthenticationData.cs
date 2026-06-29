using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Text.RegularExpressions;
using OpenMetaverse;
using OpenSim.Framework;
using Npgsql;

namespace OpenSim.Data.PGSQL
{
    public class PGSQLAuthenticationData : IAuthenticationData
    {
        private string m_Realm;
        private List<string> m_ColumnNames = null;
        private int m_LastExpire = 0;
        private string m_ConnectionString;
        private PGSQLManager m_database;

        protected virtual Assembly Assembly
        {
            get { return GetType().Assembly; }
        }

        public PGSQLAuthenticationData(string connectionString, string realm)
        {
            m_Realm = realm;
            m_ConnectionString = connectionString;
            using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
            {
                conn.Open();
                Migration m = new Migration(conn, GetType().Assembly, "AuthStore");
                m_database = new PGSQLManager(m_ConnectionString);
                m.Update();
            }
        }

        public AuthenticationData Get(UUID principalID)
        {
            AuthenticationData ret = new AuthenticationData();
            ret.Data = new Dictionary<string, object>();

            string sql = string.Format("select * from {0} where uuid = :principalID", m_Realm);

            using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.Add(m_database.CreateParameter("principalID", principalID));
                conn.Open();
                using (NpgsqlDataReader result = cmd.ExecuteReader())
                {
                    if (result.Read())
                    {
                        ret.PrincipalID = principalID;

                        if (m_ColumnNames == null)
                        {
                            m_ColumnNames = new List<string>();

                            DataTable schemaTable = result.GetSchemaTable();
                            foreach (DataRow row in schemaTable.Rows)
                                m_ColumnNames.Add(row["ColumnName"].ToString());
                        }

                        foreach (string s in m_ColumnNames)
                        {
                            if (s == "UUID" || s == "uuid")
                                continue;

                            ret.Data[s] = result[s].ToString();
                        }
                        return ret;
                    }
                }
            }
            return null;
        }

        public bool Store(AuthenticationData data)
        {
            data.Data.Remove("UUID");
            data.Data.Remove("uuid");

            string[] fields = new List<string>(data.Data.Keys).ToArray();
            StringBuilder updateBuilder = new StringBuilder();

            using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand())
            {
                updateBuilder.AppendFormat("update {0} set ", m_Realm);

                bool first = true;
                foreach (string field in fields)
                {
                    if (!first)
                        updateBuilder.Append(", ");
                    updateBuilder.AppendFormat("\"{0}\" = :{0}", field);
                    first = false;

                    cmd.Parameters.Add(m_database.CreateParameter(field, data.Data[field]));
                }

                updateBuilder.Append(" where uuid = :principalID");

                cmd.CommandText = updateBuilder.ToString();
                cmd.Connection = conn;
                cmd.Parameters.Add(m_database.CreateParameter("principalID", data.PrincipalID));

                conn.Open();
                if (cmd.ExecuteNonQuery() < 1)
                {
                    StringBuilder insertBuilder = new StringBuilder();

                    insertBuilder.AppendFormat("insert into {0} (uuid, \"", m_Realm);
                    insertBuilder.Append(String.Join("\", \"", fields));
                    insertBuilder.Append("\") values (:principalID, :");
                    insertBuilder.Append(String.Join(", :", fields));
                    insertBuilder.Append(")");

                    cmd.CommandText = insertBuilder.ToString();

                    if (cmd.ExecuteNonQuery() < 1)
                        return false;
                }
            }
            return true;
        }

        public bool SetDataItem(UUID principalID, string item, string value)
        {
            // Validate column identifier to prevent SQL injection
            if (!Regex.IsMatch(item, @"^[A-Za-z_][A-Za-z0-9_]*$"))
                return false;

            string sql = string.Format("update {0} set \"{1}\" = :{1} where uuid = :principalID", m_Realm, item);
            using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.Add(m_database.CreateParameter(item, value));
                cmd.Parameters.Add(m_database.CreateParameter("principalID", principalID));
                conn.Open();
                if (cmd.ExecuteNonQuery() > 0)
                    return true;
            }
            return false;
        }

        public bool SetToken(UUID principalID, string token, int lifetime)
        {
            if (System.Environment.TickCount - m_LastExpire > 30000)
                DoExpire();

            string sql = "insert into tokens (uuid, token, validity) values (:principalID, :token, :lifetime)";
            using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.Add(m_database.CreateParameter("principalID", principalID));
                cmd.Parameters.Add(m_database.CreateParameter("token", token));
                cmd.Parameters.Add(m_database.CreateParameter("lifetime", DateTime.Now.AddMinutes(lifetime)));
                conn.Open();

                if (cmd.ExecuteNonQuery() > 0)
                    return true;
            }
            return false;
        }

        public bool CheckToken(UUID principalID, string token, int lifetime)
        {
            if (System.Environment.TickCount - m_LastExpire > 30000)
                DoExpire();

            DateTime validDate = DateTime.Now.AddMinutes(lifetime);
            string sql = "update tokens set validity = :validDate where uuid = :principalID and token = :token and validity > (CURRENT_DATE + CURRENT_TIME)";

            using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.Add(m_database.CreateParameter("principalID", principalID));
                cmd.Parameters.Add(m_database.CreateParameter("token", token));
                cmd.Parameters.Add(m_database.CreateParameter("validDate", validDate));
                conn.Open();

                if (cmd.ExecuteNonQuery() > 0)
                    return true;
            }
            return false;
        }

        private void DoExpire()
        {
            DateTime currentDateTime = DateTime.Now;
            string sql = "delete from tokens where validity < :currentDateTime";
            using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand(sql, conn))
            {
                conn.Open();
                cmd.Parameters.Add(m_database.CreateParameter("currentDateTime", currentDateTime));
                cmd.ExecuteNonQuery();
            }
            m_LastExpire = System.Environment.TickCount;
        }
    }
}