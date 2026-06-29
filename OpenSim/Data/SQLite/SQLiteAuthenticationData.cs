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
                // Always register the assembly first to avoid DllmapconfigHelper errors
                DllmapConfigHelper.RegisterAssembly(typeof(SQLiteConnection).Assembly);

                // Initialize the database connection before migration
                m_Connection = new SQLiteConnection(connectionString);
                m_Connection.Open();

                // Migration should come before registration to avoid any potential issues
                Migration m = new Migration(m_Connection, Assembly, "AuthStore");
                m.Update();

                m_initialized = true;
            }
        }

        public AuthenticationData Get(UUID principalID)
        {
            AuthenticationData ret = new AuthenticationData();
            ret.Data = new Dictionary<string, object>();
            IDataReader result;

            try
            {
                using (SQLiteCommand cmd = new SQLiteCommand("SELECT * FROM `" + m_Realm + "` WHERE UUID = @PrincipalID"))
                {
                    cmd.Connection = m_Connection;
                    cmd.Parameters.AddWithValue("@PrincipalID", principalID);

                    result = cmd.ExecuteReader();

                    while (result.Read())
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

                            ret.Data[s] = result[s];
                        }
                    }

                    result.Close();

                    return ret;
                }
            }
            catch (Exception e)
            {
                m_log.Error("[SQLITE]: Exception getting authentication data", e);
                return null;
            }
        }

        public bool Store(AuthenticationData data)
        {
            if (data.Data.ContainsKey("UUID"))
                data.Data.Remove("UUID");

            try
            {
                using (SQLiteTransaction tx = m_Connection.BeginTransaction())
                {
                    using (SQLiteCommand cmd = new SQLiteCommand())
                    {
                        if (Get(data.PrincipalID) != null)
                        {
                            string update = "UPDATE `" + m_Realm + "`";
                            update += " SET `UUID` = @" + data.PrincipalID.ToString() + ", ";
                            update += string.Join(", ", data.Data.Keys.Select(k => "`" + k + "` = @" + k));

                            cmd.Transaction = tx;
                            cmd.CommandText = update;
                            cmd.Connection = m_Connection;

                            cmd.Parameters.AddWithValue("@UUID", data.PrincipalID);

                            foreach (string key in data.Data.Keys)
                                cmd.Parameters.AddWithValue(@"$" + key, data.Data[key]);

                            cmd.ExecuteNonQuery();
                        }
                        else
                        {
                            string insert = "INSERT INTO `" + m_Realm + "` (`UUID`, `" +
                                string.Join("`, `", data.Data.Keys) +
                                "`) VALUES (@UUID, ";

                            List<string> paramNames = new List<string>();

                            foreach (string key in data.Data.Keys)
                            {
                                insert += " @$" + key + ", ";
                                paramNames.Add(key);
                            }

                            insert = insert.Remove(insert.Length - 1) + ")";

                            cmd.Transaction = tx;
                            cmd.CommandText = insert;
                            cmd.Connection = m_Connection;

                            cmd.Parameters.AddWithValue("@UUID", data.PrincipalID);
                            foreach (string key in paramNames)
                            {
                                cmd.Parameters.AddWithValue(@"$" + key, data.Data[key]);
                            }

                            cmd.ExecuteNonQuery();
                        }
                    }

                    tx.Commit();

                    return true;
                }
            }
            catch (SQLiteException ex)
            {
                m_log.Error("[SQLITE]: Exception storing authentication data", ex);
                return false;
            }
            catch (Exception ex)
            {
                m_log.Error("[SQLITE]: Exception storing authentication data", ex);
                return false;
            }
        }

        public bool SetDataItem(UUID principalID, string item, string value)
        {
            try
            {
                using (SQLiteTransaction tx = m_Connection.BeginTransaction())
                {
                    using (SQLiteCommand cmd = new SQLiteCommand("UPDATE `" + m_Realm +
                            "` SET `" + item + "` = @Value WHERE UUID = @PrincipalID"))
                    {
                        cmd.Transaction = tx;
                        cmd.Connection = m_Connection;

                        cmd.Parameters.AddWithValue("@PrincipalID", principalID.ToString());
                        cmd.Parameters.AddWithValue("@Value", value);

                        cmd.ExecuteNonQuery();

                        tx.Commit();
                    }

                    return true;
                }
            }
            catch (SQLiteException ex)
            {
                m_log.Error("[SQLITE]: Exception setting data item", ex);
                return false;
            }
            catch (Exception ex)
            {
                m_log.Error("[SQLITE]: Exception setting data item", ex);
                return false;
            }
        }

        public bool SetToken(UUID principalID, string token, int lifetime)
        {
            try
            {
                using (SQLiteTransaction tx = m_Connection.BeginTransaction())
                {
                    using (SQLiteCommand cmd = new SQLiteCommand("INSERT INTO tokens (UUID, token, validity) VALUES (@UUID, @Token, DATETIME('now', 'localtime', '"+ lifetime.ToString() + "' minutes))"))
                    {
                        cmd.Transaction = tx;
                        cmd.Connection = m_Connection;

                        cmd.Parameters.AddWithValue("@UUID", principalID.ToString());
                        cmd.Parameters.AddWithValue("@Token", token);

                        cmd.ExecuteNonQuery();

                        tx.Commit();
                    }

                    return true;
                }
            }
            catch (SQLiteException ex)
            {
                m_log.Error("[SQLITE]: Exception setting token", ex);
                return false;
            }
            catch (Exception ex)
            {
                m_log.Error("[SQLITE]: Exception setting token", ex);
                return false;
            }
        }

        public bool CheckToken(UUID principalID, string token, int lifetime)
        {
            try
            {
                using (SQLiteTransaction tx = m_Connection.BeginTransaction())
                {
                    using (SQLiteCommand cmd = new SQLiteCommand("UPDATE tokens SET validity = DATETIME('now', 'localtime', '" + lifetime.ToString() + "' minutes) WHERE UUID = @PrincipalID AND token = @Token AND validity > DATETIME('now', 'localtime')"))
                    {
                        cmd.Transaction = tx;
                        cmd.Connection = m_Connection;

                        cmd.Parameters.AddWithValue("@PrincipalID", principalID.ToString());
                        cmd.Parameters.AddWithValue("@Token", token);

                        cmd.ExecuteNonQuery();

                        tx.Commit();
                    }

                    return true;
                }
            }
            catch (SQLiteException ex)
            {
                m_log.Error("[SQLITE]: Exception checking token", ex);
                return false;
            }
            catch (Exception ex)
            {
                m_log.Error("[SQLITE]: Exception checking token", ex);
                return false;
            }
        }

        private void DoExpire()
        {
            try
            {
                using (SQLiteTransaction tx = m_Connection.BeginTransaction())
                {
                    using (SQLiteCommand cmd = new SQLiteCommand("DELETE FROM tokens WHERE validity < DATETIME('now', 'localtime')"))
                    {
                        cmd.Transaction = tx;
                        cmd.Connection = m_Connection;

                        cmd.ExecuteNonQuery();
                        tx.Commit();

                        m_LastExpire = DateTime.Now.Ticks / TimeSpan.TicksPerMinute;
                    }
                }
            }
            catch (SQLiteException ex)
            {
                m_log.Error("[SQLITE]: Exception expiring tokens", ex);
            }
            catch (Exception ex)
            {
                m_log.Error("[SQLITE]: Exception expiring tokens", ex);
            }
        }
    }
}