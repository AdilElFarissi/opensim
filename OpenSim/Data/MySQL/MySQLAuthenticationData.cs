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
using System.Reflection;
using System.Data;
using System.Text;
using OpenMetaverse;
using MySql.Data.MySqlClient;

namespace OpenSim.Data.MySQL
{
    /// <summary>
    /// Provides MySQL-based implementation for authentication data storage and retrieval.
    /// </summary>
    public class MySqlAuthenticationData : MySqlFramework, IAuthenticationData
    {
        private readonly string m_Realm;
        private List<string> m_ColumnNames;
        private int m_LastExpire;
        // private string m_connectionString;

        protected virtual Assembly Assembly
        {
            get { return GetType().Assembly; }
        }

        /// <summary>
        /// Initializes a new instance of the MySqlAuthenticationData class with the specified connection string and realm.
        /// </summary>
        /// <param name="connectionString">The database connection string.</param>
        /// <param name="realm">The authentication realm.</param>
        public MySqlAuthenticationData(string connectionString, string realm)
                : base(connectionString)
        {
            m_Realm = realm;
            m_connectionString = connectionString;

            Assembly assembly = typeof(MySqlAuthenticationData).Assembly;

            using (MySqlConnection dbcon = new(m_connectionString))
            {
                dbcon.Open();
                Migration m = new(dbcon, assembly, "AuthStore");
                m.Update();
                dbcon.Close();
            }
        }

        /// <summary>
        /// Retrieves authentication data for the specified principal UUID.
        /// </summary>
        /// <param name="principalID">The UUID of the principal.</param>
        /// <returns>The authentication data, or null if not found.</returns>
        public AuthenticationData Get(UUID principalID)
        {
            AuthenticationData ret = new()
            {
                Data = []
            };

            using (MySqlConnection dbcon = new(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd
                    = new("select * from `" + m_Realm + "` where UUID = ?principalID", dbcon))
                {
                    cmd.Parameters.AddWithValue("?principalID", principalID.ToString());

                    using(IDataReader result = cmd.ExecuteReader())
                    {
                         if(result.Read())
                        {
                            ret.PrincipalID = principalID;

                            CheckColumnNames(result);

                            foreach(string s in m_ColumnNames.Where(col => col != "UUID"))
                            {
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
            }
        }

        private void CheckColumnNames(IDataReader result)
        {
            if (m_ColumnNames != null)
                return;

            List<string> columnNames = [];

            DataTable schemaTable = result.GetSchemaTable();
            foreach (DataRow row in schemaTable.Rows)
                columnNames.Add(row["ColumnName"].ToString());

            m_ColumnNames = columnNames;
        }

        /// <summary>
        /// Stores the specified authentication data.
        /// </summary>
        /// <param name="data">The authentication data to store.</param>
        /// <returns>True if the operation was successful; otherwise, false.</returns>
        public bool Store(AuthenticationData data)
        {
            data.Data.Remove("UUID");

            string[] fields = new List<string>(data.Data.Keys).ToArray();

            using (MySqlCommand cmd = new())
            {
                StringBuilder update = new StringBuilder("update `" + m_Realm + "` set ");
                bool first = true;
                foreach (string field in fields)
                {
                    if (!first)
                        update.Append(", ");
                    update.Append("`" + field + "` = ?" + field);

                    first = false;

                    cmd.Parameters.AddWithValue("?"+field, data.Data[field]);
                }

                update.Append(" where UUID = ?principalID");

                cmd.CommandText = update.ToString();
                cmd.Parameters.AddWithValue("?principalID", data.PrincipalID.ToString());

                if (ExecuteNonQuery(cmd) < 1)
                {
                    StringBuilder insert = new StringBuilder("insert into `" + m_Realm + "` (`UUID`, `" +
                            string.Join("`, `", fields) +
                            "`) values (?principalID, ?" + string.Join(", ?", fields) + ")");

                    cmd.CommandText = insert.ToString();

                    if (ExecuteNonQuery(cmd) < 1)
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Sets a specific data item for the principal.
        /// </summary>
        /// <param name="principalID">The UUID of the principal.</param>
        /// <param name="item">The name of the data item.</param>
        /// <param name="value">The value to set.</param>
        /// <returns>True if the operation was successful; otherwise, false.</returns>
        public bool SetDataItem(UUID principalID, string item, string value)
        {
            using (MySqlCommand cmd
                = new("update `" + m_Realm + "` set `" + item + "` = ?" + item + " where UUID = ?UUID"))
            {
                cmd.Parameters.AddWithValue("?"+item, value);
                cmd.Parameters.AddWithValue("?UUID", principalID.ToString());

                if (ExecuteNonQuery(cmd) > 0)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Sets a token for the specified principal.
        /// </summary>
        /// <param name="principalID">The UUID of the principal.</param>
        /// <param name="token">The token value.</param>
        /// <param name="lifetime">The token lifetime in minutes.</param>
        /// <returns>True if the token was set successfully; otherwise, false.</returns>
        public bool SetToken(UUID principalID, string token, int lifetime)
        {
            if (System.Environment.TickCount - m_LastExpire > 30000)
                DoExpire();

            using (MySqlCommand cmd
                = new(
                    "insert into tokens (UUID, token, validity) values (?principalID, ?token, date_add(now(), interval ?lifetime minute))"))
            {
                cmd.Parameters.AddWithValue("?principalID", principalID.ToString());
                cmd.Parameters.AddWithValue("?token", token);
                cmd.Parameters.AddWithValue("?lifetime", lifetime.ToString());

                if (ExecuteNonQuery(cmd) > 0)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Checks and validates a token for the specified principal.
        /// </summary>
        /// <param name="principalID">The UUID of the principal.</param>
        /// <param name="token">The token value.</param>
        /// <param name="lifetime">The token lifetime in minutes.</param>
        /// <returns>True if the token is valid; otherwise, false.</returns>
        public bool CheckToken(UUID principalID, string token, int lifetime)
        {
            if (System.Environment.TickCount - m_LastExpire > 30000)
                DoExpire();

            using (MySqlCommand cmd
                = new(
                    "update tokens set validity = date_add(now(), interval ?lifetime minute) where UUID = ?principalID and token = ?token and validity > now()"))
            {
                cmd.Parameters.AddWithValue("?principalID", principalID.ToString());
                cmd.Parameters.AddWithValue("?token", token);
                cmd.Parameters.AddWithValue("?lifetime", lifetime.ToString());

                if (ExecuteNonQuery(cmd) > 0)
                    return true;
            }

            return false;
        }

        private void DoExpire()
        {
            using (MySqlCommand cmd = new("delete from tokens where validity < now()"))
            {
                ExecuteNonQuery(cmd);
            }

            m_LastExpire = System.Environment.TickCount;
        }
    }
}