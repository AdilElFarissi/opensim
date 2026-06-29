using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.Reflection;
using log4net;
using System.Data.SQLite;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using System.Data.SQLite.Linq;

namespace OpenSim.Data.SQLite
{
    public class SQLiteGenericTableHandler<T> : SQLiteFramework where T : class, new()
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected Dictionary<string, FieldInfo> m_Fields = new Dictionary<string, FieldInfo>();
        protected List<string> m_ColumnNames = null;
        protected string m_Realm;
        protected FieldInfo m_DataField = null;

        protected static SQLiteConnection m_Connection;
        private static readonly object m_lock = new object();
        private static bool m_initialized;

        protected virtual Assembly Assembly
        {
            get { return GetType().Assembly; }
        }

        public SQLiteGenericTableHandler(string connectionString, string realm, string storeName)
            : base(connectionString)
        {
            m_Realm = realm;

            lock (m_lock)
            {
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
            }

            Type t = typeof(T);
            FieldInfo[] fields = t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

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

            using (SQLiteCommand cmd = new SQLiteCommand())
            {
                lock (m_lock)
                {
                    for (int i = 0; i < fields.Length; i++)
                    {
                        cmd.Parameters.Add(new SQLiteParameter(":" + fields[i], keys[i]));
                    }

                    cmd.CommandText = string.Format("select * from {0} where {1}", 
                        m_Realm, GetWhereClause(fields, keys));

                    var table = this.ExecuteQuery<T>(cmd);

                    return table.ToList().ToArray();
                }
            }
        }

        public virtual T[] Get(string where)
        {
            return Get(new string[] {}, new string[] { where });
        }

        protected T[] ExecuteQuery<T>(SQLiteCommand cmd) where T : class, new()
        {
            using (var DataContext = new SQLiteDataContext(m_Connection))
            {
                return DataContext.GetTable<T>().ToList().ToArray();
            }
        }

        public virtual bool Store(T row)
        {
            lock (m_lock)
            {
                using (SQLiteCommand cmd = new SQLiteCommand())
                {
                    String names = "";
                    String values = "";

                    foreach (FieldInfo fi in m_Fields.Values)
                    {
                        names += fi.Name + ", ";
                        values += "@" + fi.Name + ", ";
                    }

                    if (m_DataField != null)
                    {
                        Dictionary<string, string> data =
                                (Dictionary<string, string>)m_DataField.GetValue(row);

                        foreach (KeyValuePair<string, string> kvp in data)
                        {
                            names += kvp.Key + ", ";
                            values += "@" + kvp.Key + ", ";
                        }
                    }

                    String query = String.Format("replace into {0} (`", m_Realm) + names.TrimEnd(' ', ',') + "`) values (" + values.TrimEnd(' ', ',') + ")";

                    cmd.CommandText = query;

                    parameterize(cmd.Parameters, cmd);

                    using (var DataContext = new SQLiteDataContext(m_Connection))
                    {
                        var table = DataContext.GetTable<T>();

                        table.InsertOnSubmit(row);

                        DataContext.SubmitChanges();

                        if (DataContext.GetChangeSet().InnerChanges != 0)
                        {
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
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

            using (SQLiteCommand cmd = new SQLiteCommand())
            {
                lock (m_lock)
                {
                    for (int i = 0; i < fields.Length; i++)
                    {
                        cmd.Parameters.Add(new SQLiteParameter(":" + fields[i], keys[i]));
                    }

                    cmd.CommandText = string.Format("delete from {0} where {1}", 
                        m_Realm, GetWhereClause(fields, keys));

                    if (ExecuteNonQuery(cmd, m_Connection) > 0)
                    {
                        return true;
                    }
                    else
                    {
                        m_log.Error($"Failed to delete row: {m_Realm} {cmd.CommandText}");
                        return false;
                    }
                }
            }
        }

        private String GetWhereClause(string[] fields, string[] keys)
        {
            List<string> terms = new List<string>();

            for (int i = 0; i < fields.Length; i++)
            {
                terms.Add("@" + fields[i] + " = :" + fields[i]);
            }

            return String.Join(" and ", terms.ToArray());
        }

        private static void parameterize(SqlParameterCollection parameters, IDbCommand cmd)
        {
            foreach (SqlParameter parameter in parameters)
            {
                cmd.Parameters.Add(new SQLiteParameter(parameter.ParameterName, parameter.Value));
            }
        }
    }
}