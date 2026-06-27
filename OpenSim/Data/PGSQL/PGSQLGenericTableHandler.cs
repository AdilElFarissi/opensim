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
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using System.Text;
using Npgsql;

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

            string query = string.Format(@"select column_name,data_type
                        from INFORMATION_SCHEMA.COLUMNS
                       where table_name = lower('{0}');

                ", m_Realm);
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
            string query = string.Format(@"select
                    a.attname as column_name
                from
                    pg_class t,
                    pg_class i,
                    pg_index ix,
                    pg_attribute a
using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
using (NpgsqlCommand cmd = new NpgsqlCommand())
{
    cmd.Connection = conn;
    cmd.CommandText = "SELECT * FROM {0} WHERE {1} IN (@keys)";
    cmd.Parameters.AddWithValue("@keys", string.Join(",", keys));
    conn.Open();
    return DoQuery(cmd);
}

public virtual T[] Get(string field, string key)
{
    using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
    using (NpgsqlCommand cmd = new NpgsqlCommand())
    {
        if (m_FieldTypes.TryGetValue(field, out string ftype))
            cmd.Parameters.AddWithValue(field, key, ftype);
        else
            cmd.Parameters.AddWithValue(field, key);

        cmd.CommandText = $"SELECT * FROM {m_Realm} WHERE \"{field}\" = :{field}";
        cmd.Connection = conn;
        conn.Open();
        return DoQuery(cmd);
    }
}

public virtual T[] Get(string field, string[] keys)
{
    if (keys.Length == 0)
        return new T[0];

    using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
public virtual T[] Get(string[] fields, string[] keys)
{
    if (fields.Length == 0 || keys.Length == 0)
        return new T[0];

    if (fields.Length != keys.Length)
        return new T[0];

    List<string> terms = new List<string>();

    using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
    using (NpgsqlCommand cmd = new NpgsqlCommand())
    {
        for (int i = 0; i < fields.Length; i++)
        {
            if (m_FieldTypes.TryGetValue(fields[i], out string ftype))
                cmd.Parameters.AddWithValue(fields[i], keys[i], ftype);
            else
                cmd.Parameters.AddWithValue(fields[i], keys[i]);

            terms.Add($"\"{fields[i]}\" = @{fields[i]}");
        }

        string where = String.Join(" AND ", terms.ToArray());

        cmd.CommandText = $"SELECT * FROM {m_Realm} WHERE {where}";
        cmd.Connection = conn;
        conn.Open();
        return DoQuery(cmd);
    }
}

protected T[] DoQuery(NpgsqlCommand cmd)
using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
using (NpgsqlCommand cmd = new NpgsqlCommand())
{
    cmd.Connection = conn;
    cmd.CommandText = "SELECT * FROM {0} WHERE {1}";
    cmd.Parameters.AddWithValue("@realm", m_Realm);
    cmd.Parameters.AddWithValue("@where", where);

    if (conn.State == ConnectionState.Closed)
    {
        conn.Open();
    }

    using (NpgsqlDataReader reader = cmd.ExecuteReader())
    {
        // ... (rest of the code remains the same)
    }
}
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

                string query = String.Format("SELECT * FROM {0} WHERE {1}",
                                             m_Realm, where);
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

    List<string> names = new List<string>();
    List<string> values = new List<string>();
    List<string> constraints = new List<string>();

    foreach (KeyValuePair<string, string> kvp in data)
    {
        if (constraintFields.Count > 0 && constraintFields.Contains(kvp.Key))
        {
            constraints.Add(kvp.Key);
        }
        names.Add(kvp.Key);
        values.Add(":" + kvp.Key);

public virtual bool Delete(string field, string key)
{
    return Delete(new string[] { field }, new string[] { key });
}

public virtual bool Delete(string[] fields, string[] keys)
{
    if (fields.Length != keys.Length)
        return false;

    List<string> names = new List<string>();
    List<string> values = new List<string>();

    for (int i = 0; i < fields.Length; i++)
    {
        names.Add(fields[i]);
        values.Add(":" + fields[i]);
    }

    query = new StringBuilder();
    query.AppendFormat("DELETE FROM {0} WHERE ", m_Realm);
    query.Append(String.Join(" AND ", names.Select(n => String.Format("{0} = :{0}", n))));

    using (var cmd = new SqlCommand(query.ToString(), conn))
    {
        cmd.Parameters.AddRange(names.Select(n => new SqlParameter(n, SqlDbType.NVarChar)).ToArray());

        conn.Open();
        return cmd.ExecuteNonQuery() > 0;
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

    List<string> names = new List<string>();
    List<string> values = new List<string>();

    for (int i = 0; i < fields.Length; i++)
    {
        names.Add(fields[i]);
        values.Add(keys[i]);
    }

    query = new StringBuilder();
    query.AppendFormat("UPDATE {0} SET ", m_Realm);
    query.Append(String.Join(" = :", names.Select(n => n)));

    if (keys.Length > 0)
    {
        query.Append(" WHERE ");
        query.Append(String.Join(" AND ", names.Select(n => String.Format("{0} = :{0}", n))));
    }

    using (var cmd = new SqlCommand(query.ToString(), conn))
    {
        cmd.Parameters.AddRange(names.Select(n => new SqlParameter(n, SqlDbType.NVarChar)).ToArray());
        cmd.Parameters.AddRange(names.Select(n => new SqlParameter(n + "_value", SqlDbType.NVarChar)).ToArray());

        conn.Open();
        return cmd.ExecuteNonQuery() > 0;
    }
}
    conn.Open();
    if (cmd.ExecuteNonQuery() > 0)
        return true;

    return false;
}

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
using (NpgsqlCommand cmd = new NpgsqlCommand())
{
    cmd.Connection = conn;
    cmd.CommandText = "DELETE FROM " + m_Realm + " WHERE " + where;

    foreach (NpgsqlParameter param in cmd.Parameters)
    {
        param.Value = DBNull.Value;
    }

    conn.Open();

    if (cmd.ExecuteNonQuery() > 0)
    {
        //m_log.Warn("[PGSQLGenericTable]: " + deleteCommand);
        return true;
    }
public long GetCount(string field, string key)
{
    return GetCount(new string[] { field }, new string[] { key });
}

public long GetCount(string[] fields, string[] keys)
{
    if (fields.Length != keys.Length)
        return 0;

    using (NpgsqlCommand cmd = new NpgsqlCommand())
    {
        cmd.Connection = conn;
        cmd.CommandText = "SELECT COUNT(*) FROM " + m_Realm + " WHERE ";

        for (int i = 0; i < fields.Length; i++)
        {
            cmd.Parameters.AddWithValue(":key" + i, keys[i]);
            cmd.CommandText += "\"" + fields[i] + "\" = :key" + i + " AND ";
        }

        cmd.CommandText = cmd.CommandText.Substring(0, cmd.CommandText.Length - 5); // Remove the extra " AND "

        Object result = DoQueryScalar(cmd);

        return Convert.ToInt64(result);
    }
}

public long GetCount(string where)
{
    using (NpgsqlCommand cmd = new NpgsqlCommand())
    {
        cmd.Connection = conn;
        cmd.CommandText = "SELECT COUNT(*) FROM " + m_Realm + " WHERE " + where;

        Object result = DoQueryScalar(cmd);

        return Convert.ToInt64(result);
    }
}

public object DoQueryScalar(NpgsqlCommand cmd)
{
using (NpgsqlConnection dbcon = new NpgsqlConnection(m_ConnectionString))
{
    dbcon.Open();
    using (NpgsqlCommand cmd = new NpgsqlCommand())
    {
        cmd.Connection = dbcon;
        cmd.CommandText = "SELECT COUNT(*) FROM " + m_Realm + " WHERE ";

        for (int i = 0; i < fields.Length; i++)
        {
            cmd.Parameters.AddWithValue(":key" + i, keys[i]);
            cmd.CommandText += "\"" + fields[i] + "\" = :key" + i + " AND ";
        }

        cmd.CommandText = cmd.CommandText.Substring(0, cmd.CommandText.Length - 5); // Remove the extra " AND "

        return cmd.ExecuteScalar();
    }
}

public long GetCount(string[] fields, string[] keys)
{
    if (fields.Length != keys.Length)
        return 0;

    using (NpgsqlConnection dbcon = new NpgsqlConnection(m_ConnectionString))
    {
        dbcon.Open();
        using (NpgsqlCommand cmd = new NpgsqlCommand())
        {
            cmd.Connection = dbcon;
            cmd.CommandText = "SELECT COUNT(*) FROM " + m_Realm + " WHERE ";

            for (int i = 0; i < fields.Length; i++)
            {
                cmd.Parameters.AddWithValue(":key" + i, keys[i]);
public long GetCount(string field, string key)
{
    using (NpgsqlConnection dbcon = new NpgsqlConnection(m_ConnectionString))
    {
        dbcon.Open();
        using (NpgsqlCommand cmd = new NpgsqlCommand())
        {
            cmd.Connection = dbcon;
            cmd.CommandText = "SELECT COUNT(*) FROM " + m_Realm + " WHERE " + field + " = @key";

            cmd.Parameters.AddWithValue("@key", key);

            return Convert.ToInt64(cmd.ExecuteScalar());
        }
    }
public long GetCount(string where)
{
    using (NpgsqlConnection dbcon = new NpgsqlConnection(m_ConnectionString))
    {
        dbcon.Open();
        using (NpgsqlCommand cmd = new NpgsqlCommand())
        {
            cmd.Connection = dbcon;
            cmd.CommandText = "SELECT COUNT(*) FROM " + m_Realm + " WHERE " + where;
            cmd.Parameters.AddWithValue("@where", where);

            return Convert.ToInt64(cmd.ExecuteScalar());
        }
    }
}

public long GetCount(string[] fields, string[] keys)
{
    if (fields.Length != keys.Length)
        return 0;

    using (NpgsqlConnection dbcon = new NpgsqlConnection(m_ConnectionString))
    {
        dbcon.Open();
        using (NpgsqlCommand cmd = new NpgsqlCommand())
        {
            cmd.Connection = dbcon;
            cmd.CommandText = "SELECT COUNT(*) FROM " + m_Realm + " WHERE ";

            for (int i = 0; i < fields.Length; i++)
            {
                cmd.Parameters.AddWithValue(":key" + i, keys[i]);
                cmd.CommandText += "\"" + fields[i] + "\" = :key" + i + " AND ";
            }

            cmd.CommandText = cmd.CommandText.Substring(0, cmd.CommandText.Length - 5); // Remove the extra " AND "

            return Convert.ToInt64(cmd.ExecuteScalar());
        }
    }
}

public long GetCount(string[] fields, string[] keys)
{
    if (fields.Length != keys.Length)
        return 0;

    using (NpgsqlConnection dbcon = new NpgsqlConnection(m_ConnectionString))
    {
        dbcon.Open();
        using (NpgsqlCommand cmd = new NpgsqlCommand())
        {
            cmd.Connection = dbcon;
            cmd.CommandText = "SELECT COUNT(*) FROM " + m_Realm + " WHERE ";

            for (int i = 0; i < fields.Length; i++)
            {
                cmd.Parameters.AddWithValue(":key" + i, keys[i]);
                cmd.CommandText += "\"" + fields[i] + "\" = :key" + i + " AND ";
            }

            cmd.CommandText = cmd.CommandText.Substring(0, cmd.CommandText.Length - 5); // Remove the extra " AND "

            Object result = DoQueryScalar(cmd);

            return Convert.ToInt64(result);
        }
    }
}

public long GetCount(string where)
{
    using (NpgsqlConnection dbcon = new NpgsqlConnection(m_ConnectionString))
    {
        dbcon.Open();
        using (NpgsqlCommand cmd = new NpgsqlCommand())
        {
            cmd.Connection = dbcon;
            cmd.CommandText = "SELECT COUNT(*) FROM " + m_Realm + " WHERE @where";
            cmd.Parameters.AddWithValue("@where", where);

            return Convert.ToInt64(cmd.ExecuteScalar());
        }
    }
}

public object DoQueryScalar(NpgsqlCommand cmd)
{
    using (NpgsqlConnection dbcon = new NpgsqlConnection(m_ConnectionString))
    {
        dbcon.Open();
        return cmd.ExecuteScalar();
    }
}
    {
        dbcon.Open();
        cmd.Connection = dbcon;

        return cmd.ExecuteScalar();
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

    using (NpgsqlCommand cmd = new NpgsqlCommand())
    {
        cmd.Connection = conn;
        cmd.CommandText = "SELECT COUNT(*) FROM " + m_Realm + " WHERE ";

        for (int i = 0; i < fields.Length; i++)
        {
            cmd.Parameters.AddWithValue(":key" + i, keys[i]);
            cmd.CommandText += "\"" + fields[i] + "\" = :key" + i + " AND ";
        }

        cmd.CommandText = cmd.CommandText.Substring(0, cmd.CommandText.Length - 5); // Remove the extra " AND "

        Object result = DoQueryScalar(cmd);

        return Convert.ToInt64(result);
    }
}

public long GetCount(string where)
{
    using (NpgsqlCommand cmd = new NpgsqlCommand())
    {
        cmd.Connection = conn;
        cmd.CommandText = "SELECT COUNT(*) FROM " + m_Realm + " WHERE " + where;

        Object result = DoQueryScalar(cmd);

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



public long GetCount(string field, string key)
{
    return GetCount(new string[] { field }, new string[] { key });
}

public long GetCount(string[] fields, string[] keys)
{
    if (fields.Length != keys.Length)
        return 0;

    using (NpgsqlCommand cmd = new NpgsqlCommand())
    {
        cmd.Connection = conn;
        cmd.CommandText = "SELECT COUNT(*) FROM " + m_Realm + " WHERE ";

        for (int i = 0; i < fields.Length; i++)
        {
            cmd.Parameters.AddWithValue(":key" + i, keys[i]);
            cmd.CommandText += "\"" + fields[i] + "\" = :key" + i + " AND ";
        }

        cmd.CommandText = cmd.CommandText.Substring(0, cmd.CommandText.Length - 5); // Remove the extra " AND "

        Object result = DoQueryScalar(cmd);

        return Convert.ToInt64(result);
    }
}

public long GetCount(string where)
{
    using (NpgsqlCommand cmd = new NpgsqlCommand())
    {
        cmd.Connection = conn;
        cmd.CommandText = "SELECT COUNT(*) FROM " + m_Realm + " WHERE " + where;

        Object result = DoQueryScalar(cmd);

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



public long GetCount(string field, string key)
{
    return GetCount(new string[] { field }, new string[] { key });
}

public long GetCount(string[] fields, string[] keys)
{
    if (fields.Length != keys.Length)
        return 0;

    using (NpgsqlCommand cmd = new NpgsqlCommand())
    {
        cmd.Connection = conn;
        cmd.CommandText = "SELECT COUNT(*) FROM " + m_Realm + " WHERE ";

        for (int i = 0; i < fields.Length; i++)
        {
            cmd.Parameters.AddWithValue(":key" + i, keys[i]);
            cmd.CommandText += "\"" + fields[i] + "\" = :key" + i + " AND ";
        }

        cmd.CommandText = cmd.CommandText.Substring(0, cmd.CommandText.Length - 5); // Remove the extra " AND "

        Object result = DoQueryScalar(cmd);

        return Convert.ToInt64(result);
    }
}

public long GetCount(string where)
{
    using (NpgsqlCommand cmd = new NpgsqlCommand())
    {
        cmd.Connection = conn;
        cmd.CommandText = "SELECT COUNT(*) FROM " + m_Realm + " WHERE " + where;

        Object result = DoQueryScalar(cmd);

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



public long GetCount(string field, string key)
{
    return GetCount(new string[] { field }, new string[] { key });
}

public long GetCount(string[] fields, string[] keys)
{
    if (fields.Length != keys.Length)
        return 0;

    using (NpgsqlCommand cmd = new NpgsqlCommand())
    {
        cmd.Connection = conn;
        cmd.CommandText = "SELECT COUNT(*) FROM " + m_Realm + " WHERE ";

        for (int i = 0; i < fields.Length; i++)
        {
            cmd.Parameters.AddWithValue(":key" + i, keys[i]);
            cmd.CommandText += "\"" + fields[i] + "\" = :key" + i + " AND ";
        }

        cmd.CommandText = cmd.CommandText.Substring(0, cmd.CommandText.Length - 5); // Remove the extra " AND "

        Object result = DoQueryScalar(cmd);

        return Convert.ToInt64(result);
    }
}

public long GetCount(string where)
{
    using (NpgsqlCommand cmd = new NpgsqlCommand())
    {
        cmd.Connection = conn;
        cmd.CommandText = "SELECT COUNT(*) FROM " + m_Realm + " WHERE " + where;

        Object result = DoQueryScalar(cmd);

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



public long GetCount(string field, string key)
{
    return GetCount(new string[] { field }, new string[] { key });
}

public long GetCount(string[] fields, string[] keys)
{
    if (fields.Length != keys.Length)
        return 0;

    using (NpgsqlCommand cmd = new NpgsqlCommand())
    {
        cmd.Connection = conn;
        cmd.CommandText = "SELECT COUNT(*) FROM " + m_Realm + " WHERE ";

        for (int i = 0; i < fields.Length; i++)
        {
            cmd.Parameters.AddWithValue(":key" + i, keys[i]);
            cmd.CommandText += "\"" + fields[i] + "\" = :key" + i + " AND ";
        }

        cmd.CommandText = cmd.CommandText.Substring(0, cmd.CommandText.Length - 5); // Remove the extra " AND "

        Object result = DoQueryScalar(cmd);

        return Convert.ToInt64(result);
    }
}

public long GetCount(string where)
{
    using (NpgsqlCommand cmd = new NpgsqlCommand())
    {
        cmd.Connection = conn;
        cmd.CommandText = "SELECT COUNT(*) FROM " + m_Realm + " WHERE " + where;

        Object result = DoQueryScalar(cmd);

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



public long GetCount(string field, string key)
{
    return GetCount(new string[] { field }, new string[] { key });
}

public long GetCount(string[] fields, string[] keys)
{
    if (fields.Length != keys.Length)
        return 0;

    using (NpgsqlCommand cmd = new NpgsqlCommand())
    {
        cmd.Connection = conn;
        cmd.CommandText = "SELECT COUNT(*) FROM " + m_Realm + " WHERE ";

        for (int i = 0; i < fields.Length; i++)
        {
            cmd.Parameters.AddWithValue(":key" + i, keys[i]);
            cmd.CommandText += "\"" + fields[i] + "\" = :key" + i + " AND ";
        }

        cmd.CommandText = cmd.CommandText.Substring(0, cmd.CommandText.Length - 5); // Remove the extra " AND "

        Object result = DoQueryScalar(cmd);

        return Convert.ToInt64(result);
    }
}

public long GetCount(string where)
{
    using (NpgsqlCommand cmd = new NpgsqlCommand())
    {
        cmd.Connection = conn;
        cmd.CommandText = "SELECT COUNT(*) FROM " + m_Realm + " WHERE " + where;

        Object result = DoQueryScalar(cmd);

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



public long GetCount(string field, string key)
{
    return GetCount(new string[] { field }, new string[] { key });
}

public long GetCount(string[] fields, string[] keys)
{
    if (fields.Length != keys.Length)
        return 0;

    using (NpgsqlCommand cmd = new NpgsqlCommand())
    {
        cmd.Connection = conn;
        cmd.CommandText = "SELECT COUNT(*) FROM " + m_Realm + " WHERE ";

        for (int i = 0; i < fields.Length; i++)
        {
            cmd.Parameters.AddWithValue(":key" + i, keys[i]);
            cmd.CommandText += "\"" + fields[i] + "\" = :key" + i + " AND ";
        }

        cmd.CommandText = cmd.CommandText.Substring(0, cmd.CommandText.Length - 5); // Remove the extra " AND "

        Object result = DoQueryScalar(cmd);

        return Convert.ToInt64(result);
    }
}

public long GetCount(string where)
{
    using (NpgsqlCommand cmd = new NpgsqlCommand())
    {
        cmd.Connection = conn;
        cmd.CommandText = "SELECT COUNT(*) FROM " + m_Realm + " WHERE " + where;

        Object result = DoQueryScalar(cmd);

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



public long GetCount(string field, string key)
{
    return GetCount(new string[] { field }, new string[] { key });
}

public long GetCount(string[] fields, string[] keys)
{
    if (fields.Length != keys.Length)
        return 0;

    using (NpgsqlCommand cmd = new NpgsqlCommand())
    {
        cmd.Connection = conn;
        cmd.CommandText = "SELECT COUNT(*) FROM " + m_Realm + " WHERE ";

        for (int i = 0; i < fields.Length; i++)
        {
            cmd.Parameters.AddWithValue(":key" + i, keys[i]);
            cmd.CommandText += "\"" + fields[i] + "\" = :key" + i + " AND ";
        }

        cmd.CommandText = cmd.CommandText.Substring(0, cmd.CommandText.Length - 5); // Remove the extra " AND "

        Object result = DoQueryScalar(cmd);

        return Convert.ToInt64(result);
    }
}

public long GetCount(string where)
{
    using (NpgsqlCommand cmd = new NpgsqlCommand())
    {
        cmd.Connection = conn;
        cmd.CommandText = "SELECT COUNT(*) FROM " + m_Realm + " WHERE " + where;

        Object result = DoQueryScalar(cmd);

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



public long GetCount(string field, string key)
{
    return GetCount(new string[] { field }, new string[] { key });
}

public long GetCount(string[] fields, string[] keys)
{
    if (fields.Length != keys.Length)
        return 0;

    using (NpgsqlCommand cmd = new NpgsqlCommand())
    {
        cmd.Connection = conn;
        cmd.CommandText = "SELECT COUNT(*) FROM " + m_Realm + " WHERE ";

        for (int i = 0; i < fields.Length; i++)
        {
            cmd.Parameters.AddWithValue(":key" + i, keys[i]);
            cmd.CommandText += "\"" + fields[i] + "\" = :key" + i + " AND ";
        }

        cmd.CommandText = cmd.CommandText.Substring(0, cmd.CommandText.Length - 5); // Remove the extra " AND "

        Object result = DoQueryScalar(cmd);

        return Convert.ToInt64(result);
    }
}

public long GetCount(string where)
{
    using (NpgsqlCommand cmd = new NpgsqlCommand())
    {
        cmd.Connection = conn;
        cmd.CommandText = "SELECT COUNT(*) FROM " + m_Realm + " WHERE " + where;

        Object result = DoQueryScalar(cmd);

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



public long GetCount(string field, string key)
{
    return GetCount(new string[] { field }, new string
