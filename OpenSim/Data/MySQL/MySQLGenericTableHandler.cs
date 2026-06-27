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
    public class MySQLGenericTableHandler<T> : MySqlFramework where T: class, new()
    {
        //private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected Dictionary<string, FieldInfo> m_Fields = new Dictionary<string, FieldInfo>();

        protected List<string> m_ColumnNames = null;
        protected string m_Realm;
        protected FieldInfo m_DataField = null;

        protected virtual Assembly Assembly
        {
            get { return GetType().Assembly; }
        }

        public MySQLGenericTableHandler(MySqlTransaction trans,
                string realm, string storeName) : base(trans)
        {
            m_Realm = realm;

            CommonConstruct(storeName);
        }

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
                using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
                {
                    dbcon.Open();
                    Migration m = new Migration(dbcon, Assembly, storeName);
                    m.Update();
                }
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

private void CheckColumnNames(IDataReader reader)
{
    if (m_ColumnNames != null)
        return;

    List<string> columnNames = new List<string>();

    DataTable schemaTable = reader.GetSchemaTable();
    foreach (DataRow row in schemaTable.Rows)
    {
        if (row["ColumnName"] != null &&
                (!m_Fields.ContainsKey(row["ColumnName"].ToString())))
public virtual T[] Get(string field, string key)
{
    using (MySqlCommand cmd = new MySqlCommand())
    {
        cmd.CommandText = "select * from @realm where @field = @key";
        cmd.Parameters.AddWithValue("@realm", m_Realm);
        cmd.Parameters.AddWithValue("@field", field);
        cmd.Parameters.AddWithValue("@key", key);
        return DoQuery(cmd);
    }
}

public virtual T[] Get(string field, string[] keys)
{
    int flen = keys.Length;
    if (flen == 0)
        return new T[0];

    int flast = flen - 1;
    StringBuilder sb = new StringBuilder(1024);
    sb.AppendFormat("select * from @realm where @field IN (");
    using (MySqlCommand cmd = new MySqlCommand())
    {
        cmd.Parameters.AddWithValue("@realm", m_Realm);
        cmd.Parameters.AddWithValue("@field", field);
        for (int i = 0; i < flen; i++)
        {
            string fname = field + i.ToString();
            cmd.Parameters.AddWithValue(fname, keys[i]);

            sb.Append("@");
            sb.Append(fname);
public virtual T[] Get(string[] fields, string[] keys, string options)
{
    int flen = fields.Length;
    if (flen == 0 || flen != keys.Length)
        return new T[0];

    int flast = flen - 1;
    StringBuilder sb = new StringBuilder(1024);
    sb.AppendFormat("select * from @realm where ");

    using (MySqlCommand cmd = new MySqlCommand())
    {
        cmd.Parameters.AddWithValue("@realm", m_Realm);
        for (int i = 0; i < flen; i++)
        {
            cmd.Parameters.AddWithValue("@" + fields[i], keys[i]);
            if (i < flast)
                sb.AppendFormat("`{0}` = @{1} and ", fields[i], fields[i]);
            else
                sb.AppendFormat("`{0}` = @{1} ", fields[i], fields[i]);
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
        using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
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

protected T[] DoQueryWithConnection(MySqlCommand cmd, MySqlConnection dbcon)
{
    List<T> result = new List<T>();

    cmd.Connection = dbcon;

    using (IDataReader reader = cmd.ExecuteReader())
    {
        if (reader == null)
            return new T[0];

        while (reader.Read())
        {
            T item = (T)Activator.CreateInstance(typeof(T));
            for (int i = 0; i < reader.FieldCount; i++)
            {
                string fieldName = reader.GetName(i);
                PropertyInfo propertyInfo = typeof(T).GetProperty(fieldName);
                if (propertyInfo != null)
                {
                    propertyInfo.SetValue(item, reader.GetValue(i));
                }
            }
            result.Add(item);
        }

        return result.ToArray();
    }
}
                CheckColumnNames(reader);

                while (reader.Read())
                {
                    T row = new T();

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
public virtual T[] Get(string where)
{
    using (MySqlCommand cmd = new MySqlCommand())
    {
        cmd.CommandText = "select * from @realm where @where";
        cmd.Parameters.AddWithValue("@realm", m_Realm);
        cmd.Parameters.AddWithValue("@where", where);

        return DoQuery(cmd);
    }
}
                        values.Add("?" + kvp.Key);
                        cmd.Parameters.AddWithValue("?" + kvp.Key, kvp.Value);
                    }
                }

                query = $"replace into {m_Realm} (`" + String.Join("`,`", names.ToArray()) + "`) values (" + String.Join(",", values.ToArray()) + ")";

                cmd.CommandText = query;

                if (ExecuteNonQuery(cmd) > 0)
                    return true;

                return false;
            }
        }

        public virtual bool Delete(string field, string key)
        {
            return Delete(new string[] { field }, new string[] { key });
        }

        public virtual bool Delete(string[] fields, string[] keys)
        {
            //m_log.DebugFormat(
            //      "[MYSQL GENERIC TABLE HANDLER]: Delete(string[] fields, string[] keys) invoked with {0}:{1}",
            //    string.Join(",", fields), string.Join(",", keys));

            int flen = fields.Length;
            if (flen == 0 || flen != keys.Length)
                return false;

            int flast = flen - 1;
            StringBuilder sb = new StringBuilder(1024);
            sb.AppendFormat("delete from {0} where ", m_Realm);

using (MySqlCommand cmd = new MySqlCommand())
{
    for (int i = 0; i < flen; i++)
    {
        cmd.Parameters.AddWithValue("param" + i, keys[i]);
        if (i < flast)
            sb.AppendFormat("`{0}` = @param{1} and ", fields[i], i);
        else
            sb.AppendFormat("`{0}` = @param{1}", fields[i], i);
    }

    cmd.CommandText = sb.ToString();
    return ExecuteNonQuery(cmd) > 0;
}

public long GetCount(string field, string key)
{
    return GetCount(new string[] { field }, new string[] { key });
}

public long GetCount(string[] fields, string[] keys)
{
    int flen = fields.Length;
    if (flen == 0 || flen != keys.Length)
        return 0;

    int flast = flen - 1;
    StringBuilder sb = new StringBuilder(1024);
    sb.AppendFormat("select count(*) from {0} where ", m_Realm);

    using (MySqlCommand cmd = new MySqlCommand())
    {
        for (int i = 0; i < flen; i++)
        {
            cmd.Parameters.AddWithValue("param" + i, keys[i]);
            if (i < flast)
                sb.AppendFormat("`{0}` = @param{1} and ", fields[i], i);
            else
                sb.AppendFormat("`{0}` = @param{1}", fields[i], i);
public long GetCount(string where)
{
    using (MySqlCommand cmd = new MySqlCommand())
    {
        string query = "select count(*) from " + m_Realm + " where @where";

        cmd.CommandText = query;
        cmd.Parameters.AddWithValue("@where", where);

        object result = DoQueryScalar(cmd);

        return Convert.ToInt64(result);
    }
}

public object DoQueryScalar(MySqlCommand cmd)
{
    if (m_trans == null)
    {
        using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
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

    // Assuming 'query' is the SQL query string and 'parameters' is a dictionary of parameters
    using (SqlCommand command = new SqlCommand(query, m_trans.Connection, m_trans))
    {
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Key, parameter.Value);
        }
        return command.ExecuteScalar();
    }
}
using System.Data.SqlClient;
using System.Data;

// ...

public void ExecuteQuery(string query, string parameter)
{
    using (SqlConnection connection = new SqlConnection("YourConnectionString"))
    {
        connection.Open();

        // Using a prepared statement to prevent SQL injection
        using (SqlCommand command = new SqlCommand("SELECT * FROM YourTable WHERE YourColumn = @parameter", connection))
        {
            command.Parameters.AddWithValue("@parameter", parameter);

            using (SqlDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    // Process the results
                }
            }
        }
    }
}

// Example usage:
public void ExampleUsage()
{
    string query = "YourQuery";
    string parameter = "YourParameter";

    ExecuteQuery(query, parameter);
}
