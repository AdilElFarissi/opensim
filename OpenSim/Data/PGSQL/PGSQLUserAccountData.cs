using System;
using System.Collections.Generic;
using System.Data;
using OpenMetaverse;
using System.Text;
using Npgsql;
using log4net;
using System.Reflection;

namespace OpenSim.Data.PGSQL
{
    public class PGSQLUserAccountData : PGSQLGenericTableHandler<UserAccountData>, IUserAccountData
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly string m_Realm;
        private List<string> m_ColumnNames = null;
        private PGSQLManager m_database;
        private readonly Dictionary<string, string> m_FieldTypes = new Dictionary<string, string>();

        public PGSQLUserAccountData(string connectionString, string realm) :
            base(connectionString, realm, "UserAccount")
        {
            m_Realm = realm;
            m_ConnectionString = connectionString;
            m_database = new PGSQLManager(connectionString);

            using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
            {
                conn.Open();
                Migration m = new Migration(conn, GetType().Assembly, "UserAccount");
                m.Update();
            }
        }

        public UserAccountData Get(UUID principalID, UUID scopeID)
        {
            UserAccountData ret = new UserAccountData();
            ret.Data = new Dictionary<string, string>();

            string sql = string.Format(@"select * from {0} where ""PrincipalID"" = :PrincipalID", m_Realm);
            if (scopeID != UUID.Zero)
                sql += @" and ""ScopeID"" = :ScopeID";

            using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.Add(m_database.CreateParameter("PrincipalID", principalID));
                cmd.Parameters.Add(m_database.CreateParameter("ScopeID", scopeID));

                conn.Open();
                using (NpgsqlDataReader result = cmd.ExecuteReader())
                {
                    if (result.Read())
                    {
                        ret.PrincipalID = principalID;
                        UUID scope;
                        UUID.TryParse(result["scopeid"].ToString(), out scope);
                        ret.ScopeID = scope;

                        if (m_ColumnNames == null)
                        {
                            m_ColumnNames = new List<string>();
                            DataTable schemaTable = result.GetSchemaTable();
                            foreach (DataRow row in schemaTable.Rows)
                                m_ColumnNames.Add(row["ColumnName"].ToString());
                        }

                        foreach (string s in m_ColumnNames)
                        {
                            if (s == "uuid" || s == "scopeid")
                                continue;

                            ret.Data[s] = result[s].ToString();
                        }
                        return ret;
                    }
                }
            }
            return null;
        }

        public override bool Store(UserAccountData data)
        {
            if (data.Data.ContainsKey("PrincipalID"))
                data.Data.Remove("PrincipalID");
            if (data.Data.ContainsKey("ScopeID"))
                data.Data.Remove("ScopeID");

            string[] fields = new List<string>(data.Data.Keys).ToArray();

            using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand())
            {
                m_log.DebugFormat("[USER]: Try to update user {0} {1}", data.FirstName, data.LastName);

                StringBuilder updateBuilder = new StringBuilder();
                updateBuilder.AppendFormat("update {0} set ", m_Realm);
                bool first = true;
                foreach (string field in fields)
                {
                    if (!first)
                        updateBuilder.Append(", ");
                    updateBuilder.AppendFormat("\"{0}\" = :{0}", field);
                    first = false;

                    if (m_FieldTypes.ContainsKey(field))
                        cmd.Parameters.Add(m_database.CreateParameter("" + field, data.Data[field], m_FieldTypes[field]));
                    else
                        cmd.Parameters.Add(m_database.CreateParameter("" + field, data.Data[field]));
                }

                updateBuilder.Append(" where \"PrincipalID\" = :principalID");
                if (data.ScopeID != UUID.Zero)
                    updateBuilder.Append(" and \"ScopeID\" = :scopeID");

                cmd.CommandText = updateBuilder.ToString();
                cmd.Connection = conn;
                cmd.Parameters.Add(m_database.CreateParameter("principalID", data.PrincipalID));
                cmd.Parameters.Add(m_database.CreateParameter("scopeID", data.ScopeID));

                m_log.DebugFormat("[USER]: SQL update user {0} ", cmd.CommandText);
                conn.Open();

                int conta = cmd.ExecuteNonQuery();

                if (conta < 1)
                {
                    m_log.DebugFormat("[USER]: Try to insert user {0} {1}", data.FirstName, data.LastName);
                    StringBuilder insertBuilder = new StringBuilder();
                    insertBuilder.AppendFormat(@"insert into {0} (""PrincipalID"", ""ScopeID"", ""FirstName"", ""LastName"", ", m_Realm);
                    insertBuilder.Append(String.Join(@""", """, fields));
                    insertBuilder.Append(@""") values (:principalID, :scopeID, :FirstName, :LastName, :");
                    insertBuilder.Append(String.Join(", :", fields));
                    insertBuilder.Append(");");

                    cmd.Parameters.Add(m_database.CreateParameter("FirstName", data.FirstName));
                    cmd.Parameters.Add(m_database.CreateParameter("LastName", data.LastName));

                    cmd.CommandText = insertBuilder.ToString();
                    if (cmd.ExecuteNonQuery() < 1)
                        return false;
                }
                else
                    m_log.DebugFormat("[USER]: User {0} {1} exists", data.FirstName, data.LastName);
            }
            return true;
        }

        public bool Store(UserAccountData data, UUID principalID, string token)
        {
            return false;
        }

        public bool SetDataItem(UUID principalID, string item, string value)
        {
            // Whitelist allowed column names to prevent SQL injection
            var allowedColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "FirstName", "LastName", "PrincipalID", "ScopeID", "SomeOtherColumn" // add any additional allowed columns
            };

            if (!allowedColumns.Contains(item))
                return false; // reject unknown column names

            // Properly quote identifier for PostgreSQL
            string quotedItem = $"\"{item.Replace("\"", "\"\"")}\"";

            string sql = string.Format(@"update {0} set {1} = :value where ""UUID"" = :uuid", m_Realm, quotedItem);
            using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand(sql, conn))
            {
                if (m_FieldTypes.ContainsKey(item))
                    cmd.Parameters.Add(m_database.CreateParameter("" + item, value, m_FieldTypes[item]));
                else
                    cmd.Parameters.Add(m_database.CreateParameter("" + item, value));

                cmd.Parameters.Add(m_database.CreateParameter("uuid", principalID));
                conn.Open();

                if (cmd.ExecuteNonQuery() > 0)
                    return true;
            }
            return false;
        }

        public UserAccountData[] GetUsers(UUID scopeID, string query)
        {
            string[] words = query.Split();

            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length < 3)
                {
                    if (i != words.Length - 1)
                        Array.Copy(words, i + 1, words, i, words.Length - i - 1);
                    Array.Resize(ref words, words.Length - 1);
                }
            }

            if (words.Length == 0)
                return new UserAccountData[0];

            if (words.Length > 2)
                return new UserAccountData[0];

            string sql;
            using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand())
            {
                if (words.Length == 1)
                {
                    sql = String.Format(
                        @"select * from {0} where (\"ScopeID\" = :ScopeID or \"ScopeID\" = :UUIDZero) 
                          and (LOWER(\"FirstName\" COLLATE \"en_US.utf8\") LIKE LOWER(:search) 
                               or LOWER(\"LastName\" COLLATE \"en_US.utf8\") LIKE LOWER(:search))",
                        m_Realm);
                    cmd.Parameters.Add(m_database.CreateParameter("ScopeID", scopeID));
                    cmd.Parameters.Add(m_database.CreateParameter("UUIDZero", UUID.Zero));
                    cmd.Parameters.Add(m_database.CreateParameter("search", $"%{words[0]}%"));
                }
                else
                {
                    sql = String.Format(
                        @"select * from {0} where (\"ScopeID\" = :ScopeID or \"ScopeID\" = :UUIDZero) 
                          and (LOWER(\"FirstName\" COLLATE \"en_US.utf8\") LIKE LOWER(:searchFirst) 
                               or LOWER(\"LastName\" COLLATE \"en_US.utf8\") LIKE LOWER(:searchLast))",
                        m_Realm);
                    cmd.Parameters.Add(m_database.CreateParameter("searchFirst", $"%{words[0]}%"));
                    cmd.Parameters.Add(m_database.CreateParameter("searchLast", $"%{words[1]}%"));
                    cmd.Parameters.Add(m_database.CreateParameter("UUIDZero", UUID.Zero));
                    cmd.Parameters.Add(m_database.CreateParameter("ScopeID", scopeID));
                }

                cmd.Connection = conn;
                cmd.CommandText = sql;
                conn.Open();
                return DoQuery(cmd);
            }
        }

        public UserAccountData[] GetUsersWhere(UUID scopeID, string where)
        {
            // Only allow simple, safe equality checks on whitelisted columns
            // Parse the where clause for known patterns: "FirstName = :p" or "LastName = :p" etc.
            // Reject anything else to avoid SQL injection.

            // Normalize whitespace
            where = where.Trim();

            // Expected format: <Column> = <Value>
            var parts = where.Split(new[] { '=' }, 2);
            if (parts.Length != 2)
                return new UserAccountData[0];

            string column = parts[0].Trim();
            string valueParam = parts[1].Trim();

            // Whitelist column names
            var allowedColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "FirstName", "LastName", "PrincipalID", "ScopeID"
            };

            if (!allowedColumns.Contains(column))
                return new UserAccountData[0];

            // Build safe parameterized query
            string sql = $"SELECT * FROM {m_Realm} " +
                         $"WHERE (\"ScopeID\" = :scopeOrZero OR \"ScopeID\" = '00000000-0000-0000-0000-000000000000') " +
                         $"AND (\"{column}\" = :searchValue)";

            using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand())
            {
                cmd.Parameters.Add(m_database.CreateParameter("scopeOrZero", scopeID));
                cmd.Parameters.Add(m_database.CreateParameter("searchValue", valueParam));

                cmd.Connection = conn;
                cmd.CommandText = sql;
                conn.Open();
                return DoQuery(cmd);
            }
        }
    }
}