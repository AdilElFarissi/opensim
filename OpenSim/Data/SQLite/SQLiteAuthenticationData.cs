using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using System.Data.SQLite;

namespace OpenSim.Data.SQLite
{
    public class SQLiteAuthenticationData : SQLiteFramework, IAuthenticationData
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string m_Realm;
        private List<string> m_ColumnNames;
        private int m_LastExpire;

        protected static SQLiteConnection m_Connection;
        private static bool m_initialized = false;

        protected virtual Assembly Assembly
        {
            get { return GetType().Assembly; }
        }

        public SQLiteAuthenticationData(string connectionString, string realm)
                : base(connectionString)
        {
            m_Realm = realm;

            if (!m_initialized)
            {
                DllmapConfigHelper.RegisterAssembly(typeof(SQLiteConnection).Assembly);

                m_Connection = new SQLiteConnection(connectionString);
                m_Connection.Open();

                Migration m = new Migration(m_Connection, Assembly, "AuthStore");
                m.Update();

                m_initialized = true;
            }
        }

        public AuthenticationData Get(UUID principalID)
        {
            AuthenticationData ret = new AuthenticationData
            {
                Data = new Dictionary<string, object>()
            };
            IDataReader result;

            using (SQLiteCommand cmd = new SQLiteCommand("select * from `" + m_Realm + "` where UUID = :PrincipalID"))
            {
                cmd.Parameters.Add(new SQLiteParameter(":PrincipalID", principalID.ToString()));

                result = ExecuteReader(cmd, m_Connection);
            }

            try
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
                        if (s == "UUID")
                            continue;

                        ret.Data[s] = result[s].ToString();
                    }

                    return ret;
                }
                else
                {
                    return null;
                }
            }
            catch
            {
            }

            return null;
        }

        public bool Store(AuthenticationData data)
        {
            data.Data.Remove("UUID");

            string[] fields = new List<string>(data.Data.Keys).ToArray();
            string[] values = new string[data.Data.Count];
            int i = 0;
            foreach (object o in data.Data.Values)
                values[i++] = o.ToString();

            using (SQLiteCommand cmd = new SQLiteCommand())
            {
                if (Get(data.PrincipalID) != null)
                {
                    string update = "update `" + m_Realm + "` set ";
                    bool first = true;
                    foreach (string field in fields)
                    {
                        if (!first)
                            update += ", ";
                        update += "`" + field + "` = :" + field;
                        cmd.Parameters.Add(new SQLiteParameter(":" + field, data.Data[field]));

                        first = false;
                    }

                    update += " where UUID = :UUID";
                    cmd.Parameters.Add(new SQLiteParameter(":UUID", data.PrincipalID.ToString()));

                    cmd.CommandText = update;
                    try
                    {
                        if (ExecuteNonQuery(cmd, m_Connection) < 1)
                        {
                            return false;
                        }
                    }
                    catch (Exception e)
                    {
                        m_log.Error("[SQLITE]: Exception storing authentication data", e);
                        return false;
                    }
                }
                else
                {
                    string insert = "insert into `" + m_Realm + "` (`UUID`, `" +
                            String.Join("`, `", fields) +
                            "`) values (:UUID, :" + String.Join(", :", fields) + ")";

                    cmd.Parameters.Add(new SQLiteParameter(":UUID", data.PrincipalID.ToString()));
                    foreach (string field in fields)
                        cmd.Parameters.Add(new SQLiteParameter(":" + field, data.Data[field]));

                    cmd.CommandText = insert;

                    try
                    {
                        if (ExecuteNonQuery(cmd, m_Connection) < 1)
                        {
                            return false;
                        }
                    }
                    catch (Exception e)
                    {
                        m_log.Error("[SQLITE]: Exception storing authentication data", e);
                        return false;
                    }
                }
            }

            return true;
        }

        public bool SetDataItem(UUID principalID, string item, string value)
        {
            // Basic validation to avoid SQL injection via column name
            if (string.IsNullOrEmpty(item) || !System.Text.RegularExpressions.Regex.IsMatch(item, @"^[A-Za-z0-9_]+$"))
                return false;

            using (SQLiteCommand cmd = new SQLiteCommand())
            {
                cmd.CommandText = $"update `{m_Realm}` set `{item}` = @value where UUID = @uuid";
                cmd.Parameters.Add(new SQLiteParameter("@value", value));
                cmd.Parameters.Add(new SQLiteParameter("@uuid", principalID.ToString()));

                if (ExecuteNonQuery(cmd, m_Connection) > 0)
                    return true;
            }

            return false;
        }

        public bool SetToken(UUID principalID, string token, int lifetime)
        {
            if (System.Environment.TickCount - m_LastExpire > 30000)
                DoExpire();

            using (SQLiteCommand cmd = new SQLiteCommand())
            {
                cmd.CommandText = "insert into tokens (UUID, token, validity) values (@uuid, @token, datetime('now', 'localtime', @lifetime))";
                cmd.Parameters.Add(new SQLiteParameter("@uuid", principalID.ToString()));
                cmd.Parameters.Add(new SQLiteParameter("@token", token));
                cmd.Parameters.Add(new SQLiteParameter("@lifetime", $"+{lifetime} minutes"));

                if (ExecuteNonQuery(cmd, m_Connection) > 0)
                    return true;
            }

            return false;
        }

        public bool CheckToken(UUID principalID, string token, int lifetime)
        {
            if (System.Environment.TickCount - m_LastExpire > 30000)
                DoExpire();

            using (SQLiteCommand cmd = new SQLiteCommand())
            {
                cmd.CommandText = "update tokens set validity = datetime('now', 'localtime', @lifetime) where UUID = @uuid and token = @token and validity > datetime('now', 'localtime')";
                cmd.Parameters.Add(new SQLiteParameter("@lifetime", $"+{lifetime} minutes"));
                cmd.Parameters.Add(new SQLiteParameter("@uuid", principalID.ToString()));
                cmd.Parameters.Add(new SQLiteParameter("@token", token));

                if (ExecuteNonQuery(cmd, m_Connection) > 0)
                    return true;
            }

            return false;
        }

        private void DoExpire()
        {
            using (SQLiteCommand cmd = new SQLiteCommand("delete from tokens where validity < datetime('now', 'localtime')"))
                ExecuteNonQuery(cmd, m_Connection);

            m_LastExpire = System.Environment.TickCount;
        }
    }
}