Here's the corrected source code of the file:

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Data;
using Newtonsoft.Json;
using OpenMetaverse;
using OpenSim.Framework;
using MySql.Data.MySqlClient;
using log4net;

namespace OpenSim.Data.MySQL
{
    public class MySqlAuthenticationData : MySqlFramework, IAuthenticationData
    {
        private ILog m_log;
        private string m_Realm;
        private List<string> m_ColumnNames;
        private int m_LastExpire;

        protected virtual Assembly Assembly
        {
            get { return GetType().Assembly; }
        }

        public MySqlAuthenticationData(string connectionString, string realm, ILog log)
                : base(connectionString)
        {
            m_Realm = realm;
            m_log = log;

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentException("Connection string cannot be empty or null.");
            }

            try
            {
                using (MySqlConnection dbcon = new MySqlConnection(connectionString))
                {
                    dbcon.Open();
                    Migration m = new Migration(dbcon, Assembly, "AuthStore");
                    m.Update();
                    dbcon.Close();
                }
            }
            catch (MySqlException ex)
            {
                m_log.Error("MySQL Exception while updating AuthStore", ex);
            }
        }

        public AuthenticationData Get(UUID principalID)
        {
            AuthenticationData ret = new AuthenticationData();
            ret.Data = new Dictionary<string, object>();

            if (principalID == null)
            {
                throw new ArgumentException("Principal ID cannot be null.");
            }

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd
                    = new MySqlCommand("SELECT * FROM `" + m_Realm + "` where UUID = @principalID", dbcon))
                {
                    cmd.Parameters.AddWithValue("@principalID", principalID.ToString());

                    try
                    {
                        using (IDataReader result = cmd.ExecuteReader())
                        {
                            if (result.Read())
                            {
                                ret.PrincipalID = principalID;

                                CheckColumnNames(result);

                                foreach (string s in m_ColumnNames)
                                {
                                    if (s == "UUID")
                                        continue;

                                    ret.Data[s] = result[s].ToString();
                                }

                                dbcon.Close();
                                return ret;
                            }
                            else
                            {
                                dbcon.Close();
                                return null;
                            }
                        }
                    }
                    catch (MySqlException ex)
                    {
                        m_log.Error("MySQL Exception while getting auth data for principalID " + principalID, ex);
                    }
                    catch (Exception ex)
                    {
                        m_log.Error("Exception while getting auth data for principalID " + principalID, ex);
                    }
                    finally
                    {
                        if (dbcon.State == System.Data.ConnectionState.Open)
                            dbcon.Close();
                    }
                }
            }

            return null;
        }

        private void CheckColumnNames(IDataReader result)
        {
            if (m_ColumnNames != null)
                return;

            List<string> columnNames = new List<string>();

            DataTable schemaTable = result.GetSchemaTable();
            foreach (DataRow row in schemaTable.Rows)
                columnNames.Add(row["ColumnName"].ToString());

            m_ColumnNames = columnNames;
        }

        public bool Store(AuthenticationData data)
        {
            if (data == null)
            {
                throw new ArgumentException("Data cannot be null.");
            }

            data.Data.Remove("UUID");

            string[] fields = new List<string>(data.Data.Keys).ToArray();

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                try
                {
                    using (MySqlCommand cmd = new MySqlCommand())
                    {
                        string update = "UPDATE `" + m_Realm + "` SET ";
                        bool first = true;
                        foreach (string field in fields)
                        {
                            if (!first)
                                update += ", ";
                            update += "`" + field + "` = @" + field;

                            first = false;

                            cmd.Parameters.AddWithValue("@" + field, data.Data[field]);
                        }

                        update += " WHERE UUID = @" + "principalID";

                        cmd.CommandText = update;
                        cmd.Parameters.AddWithValue("@principalID", data.PrincipalID.ToString());

                        ExecuteNonQuery(cmd);
                        return true;
                    }
                }
                catch (MySqlException ex)
                {
                    m_log.Error("MySQL Exception while storing auth data", ex);
                    return false;
                }
                catch (Exception ex)
                {
                    m_log.Error("Exception while storing auth data", ex);
                    return false;
                }
                finally
                {
                    if (dbcon.State == System.Data.ConnectionState.Open)
                        dbcon.Close();
                }
            }
        }

        public bool SetDataItem(UUID principalID, string item, string value)
        {
            if (principalID == null)
            {
                throw new ArgumentException("Principal ID cannot be null.");
            }

            if (string.IsNullOrEmpty(item))
            {
                throw new ArgumentException("Item name cannot be null or empty.");
            }

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                try
                {
                    using (MySqlCommand cmd
                        = new MySqlCommand("UPDATE `" + m_Realm + "` SET `" + item + "` = @" + item + " WHERE UUID = @" + "principalID"))
                    {
                        cmd.Parameters.AddWithValue("@" + item, value);
                        cmd.Parameters.AddWithValue("@principalID", principalID.ToString());

                        ExecuteNonQuery(cmd);
                        return true;
                    }
                }
                catch (MySqlException ex)
                {
                    m_log.Error("MySQL Exception while setting data item", ex);
                    return false;
                }
                catch (Exception ex)
                {
                    m_log.Error("Exception while setting data item", ex);
                    return false;
                }
                finally
                {
                    if (dbcon.State == System.Data.ConnectionState.Open)
                        dbcon.Close();
                }
            }
        }

        public bool SetToken(UUID principalID, string token, int lifetime)
        {
            if (principalID == null)
            {
                throw new ArgumentException("Principal ID cannot be null.");
            }

            if (string.IsNullOrEmpty(token))
            {
                throw new ArgumentException("Token cannot be null or empty.");
            }

            if (lifetime < 0)
            {
                throw new ArgumentException("Lifetime cannot be negative.");
            }

            if (System.Environment.TickCount - m_LastExpire > 30000)
                DoExpire();

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                try
                {
                    using (MySqlCommand cmd
                        = new MySqlCommand(
                            "INSERT INTO tokens (`UUID`, `token`, `validity`) VALUES (@principalID, @token, DATE_ADD(NOW(), INTERVAL @lifetime MINUTE))"))
                    {
                        cmd.Parameters.AddWithValue("@principalID", principalID.ToString());
                        cmd.Parameters.AddWithValue("@token", token);
                        cmd.Parameters.AddWithValue("@lifetime", lifetime.ToString());

                        ExecuteNonQuery(cmd);
                        return true;
                    }
                }
                catch (MySqlException ex)
                {
                    m_log.Error("MySQL Exception while setting token", ex);
                    return false;
                }
                catch (Exception ex)
                {
                    m_log.Error("Exception while setting token", ex);
                    return false;
                }
                finally
                {
                    if (dbcon.State == System.Data.ConnectionState.Open)
                        dbcon.Close();
                }
            }
        }

        public bool CheckToken(UUID principalID, string token, int lifetime)
        {
            if (principalID == null)
            {
                throw new ArgumentException("Principal ID cannot be null.");
            }

            if (string.IsNullOrEmpty(token))
            {
                throw new ArgumentException("Token cannot be null or empty.");
            }

            if (lifetime < 0)
            {
                throw new ArgumentException("Lifetime cannot be negative.");
            }

            if (System.Environment.TickCount - m_LastExpire > 30000)
                DoExpire();

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                try
                {
                    using (MySqlCommand cmd
                        = new MySqlCommand(
                            "UPDATE tokens SET `validity` = DATE_ADD(NOW(), INTERVAL @lifetime MINUTE) WHERE `UUID` = @principalID AND `token` = @token AND `validity` > NOW()"))
                    {
                        cmd.Parameters.AddWithValue("@principalID", principalID.ToString());
                        cmd.Parameters.AddWithValue("@token", token);
                        cmd.Parameters.AddWithValue("@lifetime", lifetime.ToString());

                        ExecuteNonQuery(cmd);
                        return true;
                    }
                }
                catch (MySqlException ex)
                {
                    m_log.Error("MySQL Exception while checking token", ex);
                    return false;
                }
                catch (Exception ex)
                {
                    m_log.Error("Exception while checking token", ex);
                    return false;
                }
                finally
                {
                    if (dbcon.State == System.Data.ConnectionState.Open)
                        dbcon.Close();
                }
            }
        }

        private void DoExpire()
        {
            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                try
                {
                    using (MySqlCommand cmd = new MySqlCommand("DELETE FROM tokens WHERE `validity` < NOW()"))
                    {
                        ExecuteNonQuery(cmd);
                    }
                }
                catch (MySqlException ex)
                {
                    m_log.Error("MySQL Exception while expiring tokens", ex);
                }
                catch (Exception ex)
                {
                    m_log.Error("Exception while expiring tokens", ex);
                }
                finally
                {
                    if (dbcon.State == System.Data.ConnectionState.Open)
                        dbcon.Close();
                }

                m_LastExpire = System.Environment.TickCount;
            }
        }

        private void ExecuteNonQuery(MySqlCommand cmd)
        {
            try
            {
                cmd.ExecuteNonQuery();
            }
            catch (MySqlException ex)
            {