/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using System.Text;
using MySql.Data.MySqlClient;
using OpenMetaverse;

namespace OpenSim.Data.MySQL
{
    /// <summary>
    /// Handles generic table operations for MySQL databases.
    /// </summary>
    /// <typeparam name="T">The type of objects to handle.</typeparam>
    public class MySQLGenericTableHandler<T> : MySqlFramework where T: class, new()
    {
        //private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected Dictionary<string, FieldInfo> m_Fields = [];

        protected List<string> m_ColumnNames = null;
        protected string m_Realm;
        protected FieldInfo m_DataField = null;

        protected virtual Assembly Assembly
        {
            get { return GetType().Assembly; }
        }

        /// <summary>
        /// Initializes a new instance of the MySQLGenericTableHandler class with a transaction.
        /// </summary>
        /// <param name="trans">The MySqlTransaction to use.</param>
        /// <param name="realm">The database realm or table name.</param>
        /// <param name="storeName">The name of the store for migrations.</param>
        public MySQLGenericTableHandler(MySqlTransaction trans,
                string realm, string storeName) : base(trans)
        {
            m_Realm = realm;

            CommonConstruct(storeName);
        }

        /// <summary>
        /// Initializes a new instance of the MySQLGenericTableHandler class with a connection string.
        /// </summary>
        /// <param name="connectionString">The database connection string.</param>
        /// <param name="realm">The database realm or table name.</param>
        /// <param name="storeName">The name of the store for migrations.</param>
        public MySQLGenericTableHandler(string connectionString,
                string realm, string storeName) : base(connectionString)
        {
            m_Realm = realm;

            CommonConstruct(storeName);
        }

        protected void CommonConstruct(string storeName)
        {
            if (!string.IsNullOrEmpty(storeName))
            {
                // We always use a new connection for any Migrations
                using (MySqlConnection dbcon = new(m_connectionString))
                {
                    dbcon.Open();
                    Migration m = new(dbcon, Assembly, storeName);
                    m.Update();
                }
            }

            Type t = typeof(T);
            FieldInfo[] fields = t.GetFields(BindingFlags.Public |
                                             BindingFlags.Instance |
                                             BindingFlags.DeclaredOnly);

            if (fields.Length == 0)
                return;

            foreach (FieldInfo f in  fields)
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

            List<string> columnNames = [];

            DataTable schemaTable = reader.GetSchemaTable();
            foreach (DataRow row in schemaTable.Rows)
            {
                if (row["ColumnName"] != null &&
                        (!m_Fields.ContainsKey(row["ColumnName"].ToString())))
                    columnNames.Add(row["ColumnName"].ToString());
            }

            m_ColumnNames = columnNames;
        }

        /// <summary>
        /// Retrieves an array of T objects matching the specified field and key.
        /// </summary>
        /// <param name="field">The field name to query.</param>
        /// <param name="key">The key value to match.</param>
        /// <returns>An array of T objects.</returns>
        public virtual T[] Get(string field, string key)
        {   
            using (MySqlCommand cmd = new())
            {
                cmd.Parameters.AddWithValue(field, key);
                cmd.CommandText = $"select * from {m_Realm} where `{field}` = ?{field}";
                return DoQuery(cmd);
            }
        }

        /// <summary>
        /// Retrieves an array of T objects matching any of the specified keys for the given field.
        /// </summary>
        /// <param name="field">The field name to query.</param>
        /// <param name="keys">The array of key values to match.</param>
        /// <returns>An array of T objects.</returns>
        public virtual T[] Get(string field, string[] keys)
        {
            int flen = keys.Length;
            if(flen == 0)
                return new T[0];

            int flast = flen - 1;
            StringBuilder sb = new(1024);
            sb.AppendFormat("select * from {0} where {1} IN (?", m_Realm, field);
            using (MySqlCommand cmd = new())
            {
                for (int i = 0 ; i < flen ; i++)
                {
                    string fname = field + i.ToString();
                    cmd.Parameters.AddWithValue(fname, keys[i]);

                    sb.Append(fname);
                    if(i < flast)
                        sb.Append(",?");
                    else
                        sb.Append(")");
                }
                cmd.CommandText = sb.ToString();
                return DoQuery(cmd);
            }
        }

        /// <summary>
        /// Retrieves an array of T objects matching the specified fields and keys.
        /// </summary>
        /// <param name="fields">The array of field names to query.</param>
        /// <param name="keys">The array of key values to match.</param>
        /// <returns>An array of T objects.</returns>
        public virtual T[] Get(string[] fields, string[] keys)
        {
            return Get(fields, keys, string.Empty);
        }

        /// <summary>
        /// Retrieves an array of T objects matching the specified fields, keys, and options.
        /// </summary>
        /// <param name="fields">The array of field names to query.</param>
        /// <param name="keys">The array of key values to match.</param>
        /// <param name="options">Additional SQL options to append.</param>
        /// <returns>An array of T objects.</returns>
        public virtual T[] Get(string[] fields, string[] keys, string options)
        {
            int flen = fields.Length;
            if (flen == 0 || flen != keys.Length)
                return new T[0];

            int flast = flen - 1;
            StringBuilder sb = new(1024);
            sb.AppendFormat("select * from {0} where ", m_Realm);

            using (MySqlCommand cmd = new())
            {
                for (int i = 0 ; i < flen ; i++)
                {
                    cmd.Parameters.AddWithValue(fields[i], keys[i]);
                    if(i < flast)
                        sb.AppendFormat("`{0}` = ?{0} and ", fields[i]);
                    else
                        sb.AppendFormat("`{0}` = ?{0} ", fields[i]);
                }

                sb.Append(options);
                cmd.CommandText = sb.ToString();

                return DoQuery(cmd);
            }
        }

        protected T[] DoQuery(MySqlCommand cmd)
        {
            if (m_trans == null)
            {
                using (MySqlConnection dbcon = new(m_connectionString))
                {
                    dbcon.Open();
                    T[] ret = DoQueryWithConnection(cmd, dbcon);
                    dbcon.Close();
                    return ret;
                }
            }
            else
            {
                return DoQueryWithTransaction(cmd, m_trans);
            }
        }

        protected T[] DoQueryWithTransaction(MySqlCommand cmd, MySqlTransaction trans)
        {
            cmd.Transaction = trans;

            return DoQueryWithConnection(cmd, trans.Connection);
        }

        /// <summary>
        /// Executes the query and returns an array of T objects using the provided connection.
        /// </summary>
        /// <param name="cmd">The MySqlCommand to execute.</param>
        /// <param name="dbcon">The MySqlConnection to use.</param>
        /// <returns>An array of T objects.</returns>
        protected T[] DoQueryWithConnection(MySqlCommand cmd, MySqlConnection dbcon)
        {
            List<T> result = [];

            cmd.Connection = dbcon;

            using (IDataReader reader = cmd.ExecuteReader())
            {
                if (reader == null)
                    return new T[0];

                CheckColumnNames(reader);

                while (reader.Read())
                {
                    T row = new();

                    foreach (string name in m_Fields.Keys)
                    {
                        if (reader[name] is DBNull)
                        {
                            continue;
                        }
                        if (m_Fields[name].FieldType == typeof(bool))
                        {
                            int v = Convert.ToInt32(reader[name]);
                            m_Fields[name].SetValue(row, v != 0);
                        }
                        else if (m_Fields[name].FieldType == typeof(UUID))
                        {
                            m_Fields[name].SetValue(row, DBGuid.FromDB(reader[name]));
                        }
                        else if (m_Fields[name].FieldType == typeof(int))
                        {
                            int v = Convert.ToInt32(reader[name]);
                            m_Fields[name].SetValue(row, v);
                        }
                        else if (m_Fields[name].FieldType == typeof(uint))
                        {
                            uint v = Convert.ToUInt32(reader[name]);
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
                            [];

                        foreach (string col in m_ColumnNames)
                        {
                            object val = reader[col];
                            data[col] = val == DBNull.Value ? string.Empty : val.ToString();
                        }

                        m_DataField.SetValue(row, data);
                    }

                    result.Add(row);
                }
            }
            cmd.Connection = null;
            return result.ToArray();
        }

        /// <summary>
        /// Retrieves an array of T objects matching the specified where clause.
        /// </summary>
        /// <param name="where">The where clause to use in the query.</param>
        /// <returns>An array of T objects.</returns>
        public virtual T[] Get(string where)
        {
            using (MySqlCommand cmd = new())
            {
                cmd.CommandText = $"select * from {m_Realm} where {where}"; ;

                return DoQuery(cmd);
            }
        }

        /// <summary>
        /// Stores the specified row in the database.
        /// </summary>
        /// <param name="row">The row object to store.</param>
        /// <returns>True if the operation was successful; otherwise, false.</returns>
        public virtual bool Store(T row)
        {
            //m_log.DebugFormat("[MYSQL GENERIC TABLE HANDLER]: Store(T row) invoked");

            using (MySqlCommand cmd = new())
            {
                string query = "";
                List<string> names = [];
                List<string> values = [];

                foreach (FieldInfo fi in m_Fields.Values)
                {
                    names.Add(fi.Name);
                    values.Add("?" + fi.Name);

                    // Temporarily return more information about what field is unexpectedly null for
                    // http://opensimulator.org/mantis/view.php?id=5403.  This might be due to a bug in the
                    // InventoryTransferModule or we may be required to substitute a DBNull here.
                    if (fi.GetValue(row) == null)
                        throw new NullReferenceException(
                            $"[MYSQL GENERIC TABLE HANDLER]: Trying to store field {fi.Name} for {row} which is unexpectedly null");

                    cmd.Parameters.AddWithValue(fi.Name, fi.GetValue(row).ToString());
                }

                if (m_DataField != null)
                {
                    Dictionary<string, string> data =
                        (Dictionary<string, string>)m_DataField.GetValue(row);

                    foreach (KeyValuePair<string, string> kvp in data)
                    {
                        names.Add(kvp.Key);
                        values.Add("?" + kvp.Key);
                        cmd.Parameters.AddWithValue("?" + kvp.Key, kvp.Value);
                    }
                }

                query = $"replace into {m_Realm} (`" + string.Join("`,`", names.ToArray()) + "`) values (" + string.Join(",", values.ToArray()) + ")";

                cmd.CommandText = query;

                if (ExecuteNonQuery(cmd) > 0)
                    return true;

                return false;
            }
        }

        /// <summary>
        /// Deletes a row matching the specified field and key.
        /// </summary>
        /// <param name="field">The field name to use for deletion.</param>
        /// <param name="key">The key value to match.</param>
        /// <returns>True if the operation was successful; otherwise, false.</returns>
        public virtual bool Delete(string field, string key)
        {
            return Delete(new string[] { field }, new string[] { key });
        }

        /// <summary>
        /// Deletes rows matching the specified fields and keys.
        /// </summary>
        /// <param name="fields">The array of field names to use for deletion.</param>
        /// <param name="keys">The array of key values to match.</param>
        /// <returns>True if the operation was successful; otherwise, false.</returns>
        public virtual bool Delete(string[] fields, string[] keys)
        {
            //m_log.DebugFormat(
            //      "[MYSQL GENERIC TABLE HANDLER]: Delete(string[] fields, string[] keys) invoked with {0}:{1}",
            //    string.Join(",", fields), string.Join(",", keys));

            int flen = fields.Length;
            if (flen == 0 || flen != keys.Length)
                return false;

            int flast = flen - 1;
            StringBuilder sb = new(1024);
            sb.AppendFormat("delete from {0} where ", m_Realm);

            using (MySqlCommand cmd = new())
            {
                for (int i = 0 ; i < flen ; i++)
                {
                    cmd.Parameters.AddWithValue(fields[i], keys[i]);
                    if(i < flast)
                        sb.AppendFormat("`{0}` = ?{0} and ", fields[i]);
                    else
                        sb.AppendFormat("`{0}` = ?{0}", fields[i]);
                }

                cmd.CommandText = sb.ToString();
                return ExecuteNonQuery(cmd) > 0;
            }
        }

        /// <summary>
        /// Gets the count of rows matching the specified field and key.
        /// </summary>
        /// <param name="field">The field name to query.</param>
        /// <param name="key">The key value to match.</param>
        /// <returns>The count of matching rows.</returns>
        public long GetCount(string field, string key)
        {
            return GetCount(new string[] { field }, new string[] { key });
        }

        /// <summary>
        /// Gets the count of rows matching the specified fields and keys.
        /// </summary>
        /// <param name="fields">The array of field names to query.</param>
        /// <param name="keys">The array of key values to match.</param>
        /// <returns>The count of matching rows.</returns>
        public long GetCount(string[] fields, string[] keys)
        {
            int flen = fields.Length;
            if (flen == 0 || flen != keys.Length)
                return 0;

            int flast = flen - 1;
            StringBuilder sb = new(1024);
            sb.AppendFormat("select count(*) from {0} where ", m_Realm);

            using (MySqlCommand cmd = new())
            {
                for (int i = 0 ; i < flen ; i++)
                {
                    cmd.Parameters.AddWithValue(fields[i], keys[i]);
                    if(i < flast)
                        sb.AppendFormat("`{0}` = ?{0} and ", fields[i]);
                    else
                        sb.AppendFormat("`{0}` = ?{0}", fields[i]);
                }

                cmd.CommandText = sb.ToString();
                object result = DoQueryScalar(cmd);

                return Convert.ToInt64(result);
            }
        }

        /// <summary>
        /// Gets the count of rows matching the specified where clause.
        /// </summary>
        /// <param name="where">The where clause to use in the query.</param>
        /// <returns>The count of matching rows.</returns>
        public long GetCount(string where)
        {
            using (MySqlCommand cmd = new())
            {
                string query = string.Format("select count(*) from {0} where {1}",
                                             m_Realm, where);

                cmd.CommandText = query;

                object result = DoQueryScalar(cmd);

                return Convert.ToInt64(result);
            }
        }

        /// <summary>
        /// Executes a scalar query and returns the result.
        /// </summary>
        /// <param name="cmd">The MySqlCommand to execute.</param>
        /// <returns>The scalar result.</returns>
        public object DoQueryScalar(MySqlCommand cmd)
        {
            if (m_trans == null)
            {
                using (MySqlConnection dbcon = new(m_connectionString))
                {
                    dbcon.Open();
                    cmd.Connection = dbcon;

                    object ret = cmd.ExecuteScalar();
                    cmd.Connection = null;
                    dbcon.Close();
                    return ret;
                }
            }
            else
            {
                cmd.Connection = m_trans.Connection;
                cmd.Transaction = m_trans;

                return cmd.ExecuteScalar();
            }
        }
    }
}