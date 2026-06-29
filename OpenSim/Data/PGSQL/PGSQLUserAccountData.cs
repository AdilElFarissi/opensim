using System;
using System.Collections.Generic;
using System.Data;
using OpenMetaverse;
using System.Text;
using Npgsql;
using log4net;
using System.Reflection;
using System.Text.RegularExpressions;

namespace OpenSim.Data.PGSQL
{
    public class PGSQLUserAccountData : PGSQLGenericTableHandler<UserAccountData>, IUserAccountData
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public PGSQLUserAccountData(string connectionString, string realm) :
            base(connectionString, realm, "UserAccount")
        {
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
                        cmd.Parameters.Add(m_database.CreateParameter(field, data.Data[field], m_FieldTypes[field]));
                    else
                        cmd.Parameters.Add(m_database.CreateParameter(field, data.Data[field]));
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

                int affected = 0;
                try
                {
                    affected = cmd.ExecuteNonQuery();
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[USER]: ERROR executing update user {0} ", e.Message);
                }

                if (affected < 1)
                {
                    m_log.DebugFormat("[USER]: Try to insert user {0} {1}", data.FirstName, data.LastName);

                    StringBuilder insertBuilder = new StringBuilder();
                    insertBuilder.AppendFormat(@"insert into {0} (""PrincipalID"", ""ScopeID"", ""FirstName"", ""LastName"", """, m_Realm);
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
                {
                    m_log.DebugFormat("[USER]: User {0} {1} exists", data.FirstName, data.LastName);
                }
            }
            return true;
        }

        public bool Store(UserAccountData data, UUID principalID, string token)
        {
            return false;
        }

        private static readonly Regex ColumnNameRegex = new Regex(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

        public bool SetDataItem(UUID principalID, string item, string value)
        {
            if (!ColumnNameRegex.IsMatch(item))
            {
                m_log.Error("[USER]: Invalid column name supplied to SetDataItem");
                return false;
            }

            string sql = string.Format(@"update {0} set ""{1}"" = :value where ""UUID"" = :UUID", m_Realm, item);
            using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand(sql, conn))
            {
                if (m_FieldTypes.ContainsKey(item))
                    cmd.Parameters.Add(m_database.CreateParameter(item, value, m_FieldTypes[item]));
                else
                    cmd.Parameters.Add(m_database.CreateParameter(item, value));

                cmd.Parameters.Add(m_database.CreateParameter("UUID", principalID));
                conn.Open();

                return cmd.ExecuteNonQuery() > 0;
            }
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

            using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand())
            {
                if (words.Length == 1)
                {
                    cmd.CommandText = string.Format(@"select * from {0} where (""ScopeID""=:ScopeID or ""ScopeID""=:UUIDZero) and (LOWER(""FirstName"" COLLATE ""en_US.utf8"") like LOWER(:search) or LOWER(""LastName"" COLLATE ""en_US.utf8"") like LOWER(:search))", m_Realm);
                    cmd.Parameters.Add(m_database.CreateParameter("ScopeID", scopeID));
                    cmd.Parameters.Add(m_database.CreateParameter("UUIDZero", UUID.Zero));
                    cmd.Parameters.Add(m_database.CreateParameter("search", "%" + words[0] + "%"));
                }
                else
                {
                    cmd.CommandText = string.Format(@"select * from {0} where (""ScopeID""=:ScopeID or ""ScopeID""=:UUIDZero) and (LOWER(""FirstName"" COLLATE ""en_US.utf8"") like LOWER(:searchFirst) or LOWER(""LastName"" COLLATE ""en_US.utf8"") like LOWER(:searchLast))", m_Realm);
                    cmd.Parameters.Add(m_database.CreateParameter("searchFirst", "%" + words[0] + "%"));
                    cmd.Parameters.Add(m_database.CreateParameter("searchLast", "%" + words[1] + "%"));
                    cmd.Parameters.Add(m_database.CreateParameter("UUIDZero", UUID.Zero));
                    cmd.Parameters.Add(m_database.CreateParameter("ScopeID", scopeID));
                }

                cmd.Connection = conn;
                conn.Open();
                return DoQuery(cmd);
            }
        }

        public UserAccountData[] GetUsersWhere(UUID scopeID, string where)
        {
            // Simple whitelist validation to mitigate SQL injection
            if (string.IsNullOrWhiteSpace(where) || where.Contains(";") || where.Contains("--") || where.Contains("'"))
            {
                m_log.Error("[USER]: Invalid or unsafe WHERE clause supplied to GetUsersWhere");
                return new UserAccountData[0];
            }

            // Replace known column names with quoted identifiers
            var allowedColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "PrincipalID", "ScopeID", "FirstName", "LastName"
            };

            foreach (var col in allowedColumns)
            {
                where = Regex.Replace(where, $@"\b{col}\b", $"\"{col}\"", RegexOptions.IgnoreCase);
            }

            using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand())
            {
                if (!scopeID.IsZero())
                {
                    where = $"(\"ScopeID\"=:ScopeID or \"ScopeID\"='00000000-0000-0000-0000-000000000000') and ({where})";
                    cmd.Parameters.Add(m_database.CreateParameter("ScopeID", scopeID));
                }

                cmd.CommandText = string.Format("select * from {0} where {1}", m_Realm, where);
                cmd.Connection = conn;

                conn.Open();
                return DoQuery(cmd);
            }
        }
    }
}