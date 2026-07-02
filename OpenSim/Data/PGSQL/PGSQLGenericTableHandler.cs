using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using System.Text;
using Npgsql;
using System.Data.Common;

namespace OpenSim.Data.PGSQL
{
    public class PGSQLGenericTableHandler<T> : PGSqlFramework where T : class, new()
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected string m_ConnectionString;
        protected PGSQLManager m_database; //used for parameter type translation
        protected Dictionary<string, FieldInfo> m_Fields =
                new Dictionary<string, FieldInfo>(StringComparer.OrdinalIgnoreCase);

        protected Dictionary<string, string> m_FieldTypes = new Dictionary<string, string>();

        protected List<string> m_ColumnNames = null;
        protected string m_Realm;
        protected FieldInfo m_DataField = null;

        protected virtual Assembly Assembly
        {
            get { return GetType().Assembly; }
        }

        public PGSQLGenericTableHandler(string connectionString,
                string realm, string storeName)
            : base(connectionString)
        {
            m_Realm = realm;

            m_ConnectionString = connectionString;

            if (storeName != String.Empty)
            {
                using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
                {
                    conn.Open();
                    Migration m = new Migration(conn, GetType().Assembly, storeName);
                    m.Update();
                }

            }
            m_database = new PGSQLManager(m_ConnectionString);

            Type t = typeof(T);
            FieldInfo[] fields = t.GetFields(BindingFlags.Public |
                                             BindingFlags.Instance |
                                             BindingFlags.DeclaredOnly);

            LoadFieldTypes();

            if (fields.Length == 0)
                return;

            foreach (FieldInfo f in fields)
            {
                if (f.Name != "Data")
                    m_Fields[f.Name] = f;
                else
                    m_DataField = f;
            }

        }

        private void LoadFieldTypes()
        {
            m_FieldTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Escape m_Realm to prevent SQL injection in INFORMATION_SCHEMA query
            string quotedRealm = NpgsqlCommandBuilder.QuoteIdentifier(m_Realm);
            
            string query = string.Format(@"select column_name,data_type
                        from INFORMATION_SCHEMA.COLUMNS
                       where table_name = lower({0});

                ", quotedRealm);
            using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand(query, conn))
            {
                conn.Open();
                using (NpgsqlDataReader rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        // query produces 0 to many rows of single column, so always add the first item in each row
                        m_FieldTypes.Add((string)rdr[0], (string)rdr[1]);
                    }
                }
            }
        }

        private void CheckColumnNames(NpgsqlDataReader reader)
        {
            if (m_ColumnNames != null)
                return;

            m_ColumnNames = new List<string>();

            DataTable schemaTable = reader.GetSchemaTable();

            foreach (DataRow row in schemaTable.Rows)
            {
                if (row["ColumnName"] == null)
                    continue;

                string col = row["ColumnName"].ToString();

                if (!m_Fields.ContainsKey(col))
                    m_ColumnNames.Add(col);
            }
        }

        // TODO GET CONSTRAINTS FROM POSTGRESQL
        private List<string> GetConstraints()
        {
            List<string> constraints = new List<string>();
            // Escape m_Realm to prevent SQL injection in pg_class query
            string quotedRealm = NpgsqlCommandBuilder.QuoteIdentifier(m_Realm);
            
            string query = string.Format(@"select
                    a.attname as column_name
                from
                    pg_class t,
                    pg_class i,
                    pg_index ix,
                    pg_attribute a
                where
                    t.oid = ix.indrelid
                    and i.oid = ix.indexrelid
                    and a.attrelid = t.oid
                    and a.attnum = ANY(ix.indkey)
                    and t.relkind = 'r'
                    and ix.indisunique = true
                    and t.relname = lower({0})
            ;", quotedRealm);

            using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand(query, conn))
            {
                conn.Open();
                using (NpgsqlDataReader rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        // query produces 0 to many rows of single column, so always add the first item in each row
                        constraints.Add((string)rdr[0]);
                    }
                }
                return constraints;
            }
        }

        public virtual T[] Get(string field, string key)
        {
            using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand())
            {
                if ( m_FieldTypes.TryGetValue(field, out string ftype) )
                    cmd.Parameters.Add(m_database.CreateParameter(field, key, ftype));
                else
                    cmd.Parameters.Add(m_database.CreateParameter(field, key));

                string quotedRealm = NpgsqlCommandBuilder.QuoteIdentifier(m_Realm);
                string quotedField = NpgsqlCommandBuilder.QuoteIdentifier(field);

                string query = $"SELECT * FROM {quotedRealm} WHERE {quotedField} = :{field}";

                cmd.Connection = conn;
                cmd.CommandText = query;
                conn.Open();
                return DoQuery(cmd);
            }
        }

        public virtual T[] Get(string field, string[] keys)
        {
            int flen = keys.Length;
            if(flen == 0)
                return new T[0];

            List<string> placeholders = new List<string>();
            using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand())
            {
                string quotedRealm = NpgsqlCommandBuilder.QuoteIdentifier(m_Realm);
                string quotedField = NpgsqlCommandBuilder.QuoteIdentifier(field);

                for (int i = 0; i < flen; i++)
                {
                    // Use a distinct parameter name for each key in the IN clause
                    string paramName = "p" + i.ToString();
                    placeholders.Add(":" + paramName);

                    if (m_FieldTypes.TryGetValue(field, out string ftype))
                        cmd.Parameters.Add(m_database.CreateParameter(paramName, keys[i], ftype));
                    else
                        cmd.Parameters.Add(m_database.CreateParameter(paramName, keys[i]));
                }

                string whereClause = $"{quotedField} IN ({string.Join(", ", placeholders)})";
                string query = $"SELECT * FROM {quotedRealm} WHERE {whereClause}";

                cmd.Connection = conn;
                cmd.CommandText = query;
                conn.Open();
                return DoQuery(cmd);
            }
        }

        public virtual T[] Get(string[] fields, string[] keys)
        {
            if (fields.Length != keys.Length)
                return new T[0];

            List<string> terms = new List<string>();

            using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand())
            {

                for (int i = 0; i < fields.Length; i++)
                {
                    if ( m_FieldTypes.TryGetValue(fields[i], out string ftype) )
                        cmd.Parameters.Add(m_database.CreateParameter(fields[i], keys[i], ftype));
                    else
                        cmd.Parameters.Add(m_database.CreateParameter(fields[i], keys[i]));

                    string quotedField = NpgsqlCommandBuilder.QuoteIdentifier(fields[i]);
                    terms.Add($"{quotedField} = :{fields[i]}");
                }

                string where = String.Join(" AND ", terms.ToArray());

                string quotedRealm = NpgsqlCommandBuilder.QuoteIdentifier(m_Realm);
                string query = String.Format("SELECT * FROM {0} WHERE {1}",
                        quotedRealm, where);

                cmd.Connection = conn;
                cmd.CommandText = query;
                conn.Open();
                return DoQuery(cmd);
            }
        }

        protected T[] DoQuery(NpgsqlCommand cmd)
        {
            List<T> result = new List<T>();
            if (cmd.Connection == null)
            {
                cmd.Connection = new NpgsqlConnection(m_ConnectionString);
            }
            if (cmd.Connection.State == ConnectionState.Closed)
            {
                cmd.Connection.Open();
            }
            using (NpgsqlDataReader reader = cmd.ExecuteReader())
            {
                if (reader == null)
                    return new T[0];

                CheckColumnNames(reader);

                while (reader.Read())
                {
                    T row = new T();

                    foreach (string name in m_Fields.Keys)
                    {
                        if (m_Fields[name].GetValue(row) is bool)
                        {
                            int v = Convert.ToInt32(reader[name]);
                            m_Fields[name].SetValue(row, v != 0 ? true : false);
                        }
                        else if (m_Fields[name].GetValue(row) is UUID)
                        {
                            UUID uuid = UUID.Zero;

                            UUID.TryParse(reader[name].ToString(), out uuid);
                            m_Fields[name].SetValue(row, uuid);
                        }
                        else if (m_Fields[name].GetValue(row) is int)
                        {
                            int v = Convert.ToInt32(reader[name]);
                            m_Fields[name].SetValue(row, v);
                        }
                        else
                        {
                            m_Fields[name].SetValue(row, reader[name]);
                        }
                    }

                    if (m_DataField != null)
                    {
                        Dictionary<string, string> data =
                                new Dictionary<string, string>();

                        foreach (string col in m_ColumnNames)
                        {
                            data[col] = reader[col].ToString();

                            if (data[col] == null)
                                data[col] = String.Empty;
                        }

                        m_DataField.SetValue(row, data);
                    }

                    result.Add(row);
                }
                return result.ToArray();
            }
        }

        public virtual T[] Get(string where)
        {
            using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand())
            {
                string quotedRealm = NpgsqlCommandBuilder.QuoteIdentifier(m_Realm);

                string query = String.Format("SELECT * FROM {0} WHERE {1}",
                        quotedRealm, where);
                cmd.Connection = conn;
                cmd.CommandText = query;
                //m_log.WarnFormat("[PGSQLGenericTable]: SELECT {0} WHERE {1}", m_Realm, where);

                conn.Open();
                return DoQuery(cmd);
            }
        }

        public virtual T[] Get(string where, NpgsqlParameter parameter)
        {
            using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
                using (NpgsqlCommand cmd = new NpgsqlCommand())
            {

                string quotedRealm = NpgsqlCommandBuilder.QuoteIdentifier(m_Realm);

                string query = String.Format("SELECT * FROM {0} WHERE {1}",
                                             quotedRealm, where);
                cmd.Connection = conn;
                cmd.CommandText = query;
                //m_log.WarnFormat("[PGSQLGenericTable]: SELECT {0} WHERE {1}", m_Realm, where);

                cmd.Parameters.Add(parameter);

                conn.Open();
                return DoQuery(cmd);
            }
        }

        public virtual bool Store(T row)
        {
            List<string> constraintFields = GetConstraints();
            List<KeyValuePair<string, string>> constraints = new List<KeyValuePair<string, string>>();

            using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand())
            {
                StringBuilder query = new StringBuilder();
                List<String> names = new List<String>();
                List<String> values = new List<String>();

                foreach (FieldInfo fi in m_Fields.Values)
                {
                    names.Add(fi.Name);
                    values.Add(":" + fi.Name);
                    // Temporarily return more information about what field is unexpectedly null for
                    // http://opensimulator.org/mantis/view.php?id=5403.  This might be due to a bug in the
                    // InventoryTransferModule or we may be required to substitute a DBNull here.
                    if (fi.GetValue(row) == null)
                        throw new NullReferenceException(
                            string.Format(
                                "[PGSQL GENERIC TABLE HANDLER]: Trying to store field {0} for {1} which is unexpectedly null",
                                fi.Name, row));

                    if (constraintFields.Count > 0 && constraintFields.Contains(fi.Name))
                    {
                        constraints.Add(new KeyValuePair<string, string>(fi.Name, fi.GetValue(row).ToString() ));
                    }

                    if (m_FieldTypes.TryGetValue(fi.Name, out string ftype))
                        cmd.Parameters.Add(m_database.CreateParameter(fi.Name, fi.GetValue(row), ftype));
                    else
                        cmd.Parameters.Add(m_database.CreateParameter(fi.Name, fi.GetValue(row)));
                }

                if (m_DataField != null)
                {
                    Dictionary<string, string> data =
                            (Dictionary<string, string>)m_DataField.GetValue(row);

                    foreach (KeyValuePair<string, string> kvp in data)
                    {
                        if (constraintFields.Count > 0 && constraintFields.Contains(kvp.Key))
                        {
                            constraints.Add(new KeyValuePair<string, string>(kvp.Key, kvp.Key));
                        }
                        names.Add(kvp.Key);
                        values.Add(":" + kvp.Key);

                        if (m_FieldTypes.TryGetValue(kvp.Key, out string ftype))
                            cmd.Parameters.Add(m_database.CreateParameter("" + kvp.Key, kvp.Value, ftype));
                        else
                            cmd.Parameters.Add(m_database.CreateParameter("" + kvp.Key, kvp.Value));
                    }

                }

                string quotedRealm = NpgsqlCommandBuilder.QuoteIdentifier(m_Realm);

                query.AppendFormat("UPDATE {0} SET ", quotedRealm);
                int i = 0;
                for (i = 0; i < names.Count - 1; i++)
                {
                    string quotedName = NpgsqlCommandBuilder.QuoteIdentifier(names[i]);
                    query.AppendFormat("{0} = {1}, ", quotedName, values[i]);
                }
                query.AppendFormat("{0} = {1} ", NpgsqlCommandBuilder.QuoteIdentifier(names[i]), values[i]);
                if (constraints.Count > 0)
                {
                    List<string> terms = new List<string>();
                    for (int j = 0; j < constraints.Count; j++)
                    {
                        terms.Add(String.Format(" {0} = :{0}", constraints[j].Key));
                    }
                    string where = String.Join(" AND ", terms.ToArray());
                    query.AppendFormat(" WHERE {0} ", where);

                }
                cmd.Connection = conn;
                cmd.CommandText = query.ToString();

                conn.Open();
                if (cmd.ExecuteNonQuery() > 0)
                {
                    //m_log.WarnFormat("[PGSQLGenericTable]: Updating {0}", m_Realm);
                    return true;
                }
                else
                {
                    // assume record has not yet been inserted

                    query = new StringBuilder();
                    query.AppendFormat("INSERT INTO {0} (", quotedRealm);
                    // Build quoted column names for the INSERT INTO clause
                    List<string> quotedNames = new List<string>();
                    foreach (string name in names)
                    {
                        quotedNames.Add(NpgsqlCommandBuilder.QuoteIdentifier(name));
                    }
                    query.Append(String.Join(",", quotedNames.ToArray()));
                    query.Append(") values (" + String.Join(",", values.ToArray()) + ")");
                    cmd.Connection = conn;
                    cmd.CommandText = query.ToString();

                    // m_log.WarnFormat("[PGSQLGenericTable]: Inserting into {0} sql {1}", m_Realm, cmd.CommandText);

                    if (conn.State != ConnectionState.Open)
                        conn.Open();
                    if (cmd.ExecuteNonQuery() > 0)
                        return true;
                }

                return false;
            }
        }

        public virtual bool Delete(string field, string key)
        {
            return Delete(new string[] { field }, new string[] { key });
        }

        public virtual bool Delete(string[] fields, string[] keys)
        {
            if (fields.Length != keys.Length)
                return false;

            List<string> terms = new List<string>();

            using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand())
            {
                for (int i = 0; i < fields.Length; i++)
                {
                    if (m_FieldTypes.TryGetValue(fields[i], out string ftype))
                        cmd.Parameters.Add(m_database.CreateParameter(fields[i], keys[i], ftype));
                    else
                        cmd.Parameters.Add(m_database.CreateParameter(fields[i], keys[i]));

                    string quotedField = NpgsqlCommandBuilder.QuoteIdentifier(fields[i]);
                    terms.Add(" " + quotedField + " = :" + fields[i]);
                }

                string where = String.Join(" AND ", terms.ToArray());
                string quotedRealm = NpgsqlCommandBuilder.QuoteIdentifier(m_Realm);

                string query = String.Format("DELETE FROM {0} WHERE {1}", quotedRealm, where);

                cmd.Connection = conn;
                cmd.CommandText = query;
                conn.Open();

                if (cmd.ExecuteNonQuery() > 0)
                {
                    //m_log.Warn("[PGSQLGenericTable]: " + deleteCommand);
                    return true;
                }
                return false;
            }
        }
        public long GetCount(string field, string key)
        {
            return GetCount(new string[] { field }, new string[] { key });
        }

        public long GetCount(string[] fields, string[] keys)
        {
            if (fields.Length != keys.Length)
                return 0;

            List<string> terms = new List<string>();

            using (NpgsqlCommand cmd = new NpgsqlCommand())
            {
                for (int i = 0; i < fields.Length; i++)
                {
                    cmd.Parameters.AddWithValue(fields[i], new Guid(keys[i]));
                    string quotedField = NpgsqlCommandBuilder.QuoteIdentifier(fields[i]);
                    terms.Add(quotedField + " = :" + fields[i]);
                }

                string where = String.Join(" and ", terms.ToArray());
                string quotedRealm = NpgsqlCommandBuilder.QuoteIdentifier(m_Realm);

                string query = String.Format("select count(*) from {0} where {1}",
                                             quotedRealm, where);

                cmd.CommandText = query;

                Object result = DoQueryScalar(cmd);

                return Convert.ToInt64(result);
            }
        }

        public long GetCount(string where)
        {
            using (NpgsqlCommand cmd = new NpgsqlCommand())
            {
                string quotedRealm = NpgsqlCommandBuilder.QuoteIdentifier(m_Realm);

                string query = String.Format("select count(*) from {0} where {1}",
                                             quotedRealm, where);

                cmd.CommandText = query;

                object result = DoQueryScalar(cmd);

                return Convert.ToInt64(result);
            }
        }

        public object DoQueryScalar(NpgsqlCommand cmd)
        {
            using (NpgsqlConnection dbcon = new NpgsqlConnection(m_ConnectionString))
            {
                dbcon.Open();
                cmd.Connection = dbcon;

                return cmd.ExecuteScalar();
            }
        }
    }
}