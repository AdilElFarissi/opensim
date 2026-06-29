using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using log4net;
using System.Data.SQLite;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;

namespace OpenSim.Data.SQLite
{
    public class SQLiteGenericTableHandler<T> : SQLiteFramework where T : class, new()
    {
        protected Dictionary<string, FieldInfo> m_Fields = new Dictionary<string, FieldInfo>();
        protected List<string> m_ColumnNames = null;
        protected string m_Realm;
        protected FieldInfo m_DataField = null;
        protected static SQLiteConnection m_Connection;
        private static bool m_initialized;
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected virtual Assembly Assembly
        {
            get { return GetType().Assembly; }
        }

        public SQLiteGenericTableHandler(string connectionString,
                string realm, string storeName) : base(connectionString)
        {
            m_Realm = realm;

            if (!m_initialized)
            {
                m_Connection = new SQLiteConnection(connectionString);
                m_Connection.Open();

                if (storeName != String.Empty)
                {
                    Migration m = new Migration(m_Connection, Assembly, storeName);
                    m.Update();
                }

                m_initialized = true;
            }

            Type t = typeof(T);
            FieldInfo[] fields = t.GetFields(BindingFlags.Public |
                                             BindingFlags.Instance |
                                             BindingFlags.DeclaredOnly);

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

        private void CheckColumnNames(IDataReader reader)
        {
            if (m_ColumnNames != null)
                return;

            m_ColumnNames = new List<string>();

            DataTable schemaTable = reader.GetSchemaTable();
            foreach (DataRow row in schemaTable.Rows)
            {
                if (row["ColumnName"] != null &&
                        (!m_Fields.ContainsKey(row["ColumnName"].ToString())))
                    m_ColumnNames.Add(row["ColumnName"].ToString());
            }
        }

        public virtual T[] Get(string field, string key)
        {
            return Get(new string[] { field }, new string[] { key });
        }

        public virtual T[] Get(string[] fields, string[] keys)
        {
            if (fields.Length != keys.Length)
                return new T[0];

            List<string> terms = new List<string>();

            using (SQLiteCommand cmd = new SQLiteCommand())
            {
                for (int i = 0; i < fields.Length; i++)
                {
                    if (!IsValidIdentifier(fields[i]))
                    {
                        m_log.Error($"Invalid field name detected in Get: {fields[i]}");
                        return new T[0];
                    }

                    cmd.Parameters.Add(new SQLiteParameter(":" + fields[i], keys[i]));
                    terms.Add("`" + fields[i] + "` = :" + fields[i]);
                }

                string where = String.Join(" and ", terms.ToArray());

                string query = String.Format("select * from {0} where {1}",
                        m_Realm, where);

                cmd.CommandText = query;

                return DoQuery(cmd);
            }
        }

        protected T[] DoQuery(SQLiteCommand cmd)
        {
            IDataReader reader = ExecuteReader(cmd, m_Connection);
            if (reader == null)
                return new T[0];

            CheckColumnNames(reader);

            List<T> result = new List<T>();

            while (reader.Read())
            {
                T row = new T();

                foreach (string name in m_Fields.Keys)
                {
                    object currentValue = m_Fields[name].GetValue(row);
                    if (currentValue is bool)
                    {
                        int v = Convert.ToInt32(reader[name]);
                        m_Fields[name].SetValue(row, v != 0);
                    }
                    else if (currentValue is UUID)
                    {
                        UUID uuid = UUID.Zero;
                        UUID.TryParse(reader[name].ToString(), out uuid);
                        m_Fields[name].SetValue(row, uuid);
                    }
                    else if (currentValue is int)
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
                    Dictionary<string, string> data = new Dictionary<string, string>();

                    foreach (string col in m_ColumnNames)
                    {
                        data[col] = reader[col].ToString() ?? String.Empty;
                    }

                    m_DataField.SetValue(row, data);
                }

                result.Add(row);
            }

            return result.ToArray();
        }

        public virtual T[] Get(string where)
        {
            if (!IsSafeWhereClause(where))
            {
                m_log.Error($"Potentially unsafe where clause detected: {where}");
                return new T[0];
            }

            using (SQLiteCommand cmd = new SQLiteCommand())
            {
                string query = String.Format("select * from {0} where {1}",
                        m_Realm, where);

                cmd.CommandText = query;

                return DoQuery(cmd);
            }
        }

        public virtual bool Store(T row)
        {
            using (SQLiteCommand cmd = new SQLiteCommand())
            {
                List<string> names = new List<string>();
                List<string> values = new List<string>();

                foreach (FieldInfo fi in m_Fields.Values)
                {
                    if (!IsValidIdentifier(fi.Name))
                    {
                        m_log.Error($"Invalid field name detected in Store: {fi.Name}");
                        return false;
                    }

                    names.Add(fi.Name);
                    values.Add(":" + fi.Name);
                    object fieldValue = fi.GetValue(row);
                    cmd.Parameters.Add(new SQLiteParameter(":" + fi.Name, fieldValue?.ToString() ?? (object)DBNull.Value));
                }

                if (m_DataField != null)
                {
                    var data = (Dictionary<string, string>)m_DataField.GetValue(row);
                    if (data != null)
                    {
                        foreach (KeyValuePair<string, string> kvp in data)
                        {
                            if (!IsValidIdentifier(kvp.Key))
                            {
                                m_log.Error($"Invalid data column name detected in Store: {kvp.Key}");
                                return false;
                            }

                            names.Add(kvp.Key);
                            values.Add(":" + kvp.Key);
                            cmd.Parameters.Add(new SQLiteParameter(":" + kvp.Key, kvp.Value));
                        }
                    }
                }

                string query = $"replace into {m_Realm} (`" + String.Join("`,`", names) + "`) values (" + String.Join(",", values) + ")";
                cmd.CommandText = query;

                return ExecuteNonQuery(cmd, m_Connection) > 0;
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

            using (SQLiteCommand cmd = new SQLiteCommand())
            {
                for (int i = 0; i < fields.Length; i++)
                {
                    if (!IsValidIdentifier(fields[i]))
                    {
                        m_log.Error($"Invalid field name detected in Delete: {fields[i]}");
                        return false;
                    }

                    cmd.Parameters.Add(new SQLiteParameter(":" + fields[i], keys[i]));
                    terms.Add("`" + fields[i] + "` = :" + fields[i]);
                }

                string where = String.Join(" and ", terms.ToArray());

                string query = String.Format("delete from {0} where {1}", m_Realm, where);

                cmd.CommandText = query;

                return ExecuteNonQuery(cmd, m_Connection) > 0;
            }
        }

        private bool IsValidIdentifier(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
                return false;

            foreach (char c in identifier)
            {
                if (!(char.IsLetterOrDigit(c) || c == '_' ))
                    return false;
            }
            return true;
        }

        private bool IsSafeWhereClause(string where)
        {
            if (string.IsNullOrWhiteSpace(where))
                return false;

            // Simple safety check: disallow semicolons and comment sequences
            if (where.Contains(";") || where.Contains("--") || where.Contains("/*") || where.Contains("*/"))
                return false;

            return true;
        }
    }
}