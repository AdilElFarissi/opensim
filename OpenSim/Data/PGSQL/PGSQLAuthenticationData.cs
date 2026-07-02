using System;
using System.Collections;
using System.Collections.Generic;
using OpenMetaverse;
using OpenSim.Framework;
using System.Reflection;
using System.Text;
using System.Data;
using Npgsql;
using NpgsqlTypes;

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

        private string EscapeIdentifier(string identifier)
        {
            if (identifier == null)
                throw new ArgumentNullException(nameof(identifier));
            return "\"" + identifier.Replace("\"", "\"\"") + "\"";
        }

        public AuthenticationData Get(UUID principalID)
        {
            AuthenticationData ret = new AuthenticationData();
            ret.Data = new Dictionary<string, object>();

            string sql = string.Format("select * from {0} where uuid = :principalID", EscapeIdentifier(m_Realm));

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
                            if (s == "UUID"||s == "uuid")
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

            using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
            {
                conn.Open();
                using (NpgsqlCommand cmd = new NpgsqlCommand())
                {
                    cmd.Connection = conn;

                    StringBuilder updateBuilder = new StringBuilder();
                    updateBuilder.AppendFormat("update {0} set ", EscapeIdentifier(m_Realm));

                    bool first = true;
                    List<string> paramNames = new List<string>();
                    for (int i = 0; i < fields.Length; i++)
                    {
                        string field = fields[i];
                        if (!first)
                            updateBuilder.Append(", ");
                        string paramName = "p" + i.ToString();
                        paramNames.Add(paramName);
                        updateBuilder.AppendFormat("\"{0}\" = :{1}", EscapeIdentifier(field), paramName);
                        first = false;
                    }

                    updateBuilder.Append(" where uuid = :principalID");
                    cmd.CommandText = updateBuilder.ToString();

                    for (int i = 0; i < fields.Length; i++)
                    {
                        cmd.Parameters.Add(m_database.CreateParameter(paramNames[i], data.Data[fields[i]]));
                    }
                    cmd.Parameters.Add(m_database.CreateParameter("principalID", data.PrincipalID));

                    if (cmd.ExecuteNonQuery() < 1)
                    {
                        StringBuilder insertBuilder = new StringBuilder();
                        insertBuilder.AppendFormat("insert into {0} (uuid, \"", EscapeIdentifier(m_Realm));
                        insertBuilder.Append(String.Join("\", \"", fields.Select(f => EscapeIdentifier(f))));
                        insertBuilder.Append("\") values (:principalID, :");
                        insertBuilder.Append(String.Join(", :", fields.Select((f, index) => "p" + index.ToString())));
                        insertBuilder.Append(")");

                        cmd.CommandText = insertBuilder.ToString();
                        cmd.Parameters.Clear();

                        cmd.Parameters.Add(m_database.CreateParameter("principalID", data.PrincipalID));
                        for (int i = 0; i < fields.Length; i++)
                        {
                            cmd.Parameters.Add(m_database.CreateParameter("p" + i.ToString(), data.Data[fields[i]]));
                        }

                        if (cmd.ExecuteNonQuery() < 1)
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        public bool SetDataItem(UUID principalID, string item, string value)
        {
            string sql = string.Format("update {0} set {1} = :value where uuid = :principalID", 
                EscapeIdentifier(m_Realm), EscapeIdentifier(item));
            using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.Add(m_database.CreateParameter("value", value));
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
                {
                    return true;
                }
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
                {
                    return true;
                }
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