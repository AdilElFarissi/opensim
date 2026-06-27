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
using MySql.Data.MySqlClient;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Data;

namespace OpenSim.Data.MySQL
{
    public class MySQLEstateStore : IEstateDataStore
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string m_connectionString;

        private FieldInfo[] m_Fields;
        private Dictionary<string, FieldInfo> m_FieldMap = new();

        protected virtual Assembly Assembly
        {
            get { return GetType().Assembly; }
        }

        public MySQLEstateStore()
        {
        }

        public MySQLEstateStore(string connectionString)
        {
            Initialise(connectionString);
        }

        public void Initialise(string connectionString)
        {
            m_connectionString = connectionString;

            try
            {
                m_log.Info("[REGION DB]: MySql - connecting: " + Util.GetDisplayConnectionString(m_connectionString));
using System;
using System.Collections.Generic;
using System.Reflection;
using MySql.Data.MySqlClient;

public class EstateSettings
{
    private Dictionary<string, FieldInfo> m_FieldMap = new Dictionary<string, FieldInfo>();
    private FieldInfo[] m_Fields;

    public EstateSettings LoadEstateSettings(UUID regionID, bool create)
    {
        string sql = "select estate_settings." + String.Join(",estate_settings.", FieldList) +
            " from estate_map left join estate_settings on estate_map.EstateID = estate_settings.EstateID where estate_settings.EstateID is not null and RegionID = @RegionID";

        using (MySqlCommand cmd = new MySqlCommand())
        {
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("@RegionID", regionID.ToString());

            EstateSettings e = DoLoad(cmd, regionID, create);
            if (!create && e.EstateID == 0) // Not found
                return null;

            return e;
        }
    }

    private EstateSettings DoLoad(MySqlCommand cmd, UUID regionID, bool create)
    {
        EstateSettings es = new EstateSettings();
        es.OnSave += StoreEstateSettings;

        using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
        {
            dbcon.Open();
            cmd.Connection = dbcon;

            bool found = false;

            using (MySqlDataReader reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    found = true;
                    foreach (FieldInfo f in m_Fields)
                    {
                        if (f.Name.Substring(0, 2) == "m_")
                        {
                            string fieldName = f.Name.Substring(2);
                            int ordinal = reader.GetOrdinal(fieldName);
                            if (reader.IsDBNull(ordinal))
                            {
                                f.SetValue(es, null);
                            }
                            else
                            {
                                f.SetValue(es, reader[ordinal]);
                            }
                        }
                    }
                }
            }

            if (!found && create)
            {
                DoCreate(es);
            }

            return es;
        }
    }

    private string[] FieldList
    {
        get { return new List<string>(m_FieldMap.Keys).ToArray(); }
    }

    public EstateSettings CreateNewEstate(int estateID)
    {
        EstateSettings es = new EstateSettings();

        es.OnSave += StoreEstateSettings;
        es.EstateID = Convert.ToUInt32(estateID);

        DoCreate(es);

        LoadBanList(es);

        es.EstateManagers = LoadUUIDList(es.EstateID, "estate_managers");
        es.EstateAccess = LoadUUIDList(es.EstateID, "estate_users");
        es.EstateGroups = LoadUUIDList(es.EstateID, "estate_groups");

        return es;
    }
}
                using (IDataReader r = cmd.ExecuteReader())
private void DoCreate(EstateSettings es)
{
    // Migration case
    List<string> names = new List<string>(FieldList);

    // Remove EstateID and use AutoIncrement
    if (es.EstateID < 100)
        names.Remove("EstateID");

    string sql = "insert into estate_settings (" + String.Join(",", names.ToArray()) + ") values (" + String.Join(",", names.Select(n => $"@{n}").ToArray()) + ")";

    using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
    {
        dbcon.Open();
        using (MySqlCommand cmd2 = dbcon.CreateCommand())
        {
            cmd2.CommandText = sql;
            cmd2.Parameters.Clear();

            foreach (string name in FieldList)
            {
                if (m_FieldMap[name].GetValue(es) is bool)
                {
                    cmd2.Parameters.AddWithValue($"@{name}", (bool)m_FieldMap[name].GetValue(es));
                }
                else if (m_FieldMap[name].GetValue(es) is UUID)
                {
                    cmd2.Parameters.AddWithValue($"@{name}", ((UUID)m_FieldMap[name].GetValue(es)).ToString());
                }
                else
                {
                    cmd2.Parameters.AddWithValue($"@{name}", m_FieldMap[name].GetValue(es).ToString());
                }
            }

            cmd2.ExecuteNonQuery();

            // Only get Auto ID if we actually used it else we just get 0
            if (es.EstateID < 100)
            {
                cmd2.CommandText = "select LAST_INSERT_ID() as id";
                cmd2.Parameters.Clear();
                object result = cmd2.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                {
                    es.EstateID = Convert.ToInt32(result);
                }
            }
        }
using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
{
    dbcon.Open();

    using (MySqlCommand cmd = dbcon.CreateCommand())
    {
        cmd.CommandText = "select * from estateban where EstateID = @EstateID";
        cmd.Parameters.AddWithValue("@EstateID", es.EstateID);

        using (IDataReader r = cmd.ExecuteReader())
        {
            while (r.Read())
            {
                EstateBan eb = new EstateBan();
                eb.BannedUserID = DBGuid.FromDB(r["bannedUUID"]);
                eb.BannedHostAddress = "0.0.0.0";
                eb.BannedHostIPMask = "0.0.0.0";
                eb.BanningUserID = DBGuid.FromDB(r["banningUUID"]);
                eb.BanTime = Convert.ToInt32(r["banTime"]);
                es.AddBan(eb);
            }
        }
    }
    dbcon.Close();
}
                }
                dbcon.Close();
            }
        }

        private void SaveBanList(EstateSettings es)
        {
            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd = dbcon.CreateCommand())
                {
                    cmd.CommandText = "delete from estateban where EstateID = ?EstateID";
                    cmd.Parameters.AddWithValue("?EstateID", es.EstateID.ToString());

                    cmd.ExecuteNonQuery();

                    cmd.Parameters.Clear();

                    cmd.CommandText = "insert into estateban (EstateID, bannedUUID, bannedIp, bannedIpHostMask, bannedNameMask, banningUUID, banTime) values ( ?EstateID, ?bannedUUID, '', '', '', ?banningUUID, ?banTime)";

                    foreach (EstateBan b in es.EstateBans)
                    {
                        cmd.Parameters.AddWithValue("?EstateID", es.EstateID.ToString());
                        cmd.Parameters.AddWithValue("?bannedUUID", b.BannedUserID.ToString());
                        cmd.Parameters.AddWithValue("?banningUUID", b.BanningUserID.ToString());
                        cmd.Parameters.AddWithValue("?banTime", b.BanTime);

                        cmd.ExecuteNonQuery();
                        cmd.Parameters.Clear();
                    }
                }
                dbcon.Close();
            }
        }

        void SaveUUIDList(uint EstateID, string table, UUID[] data)
        {
            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd = dbcon.CreateCommand())
                {
                    cmd.CommandText = "delete from " + table + " where EstateID = ?EstateID";
                    cmd.Parameters.AddWithValue("?EstateID", EstateID.ToString());

                    cmd.ExecuteNonQuery();

                    cmd.Parameters.Clear();

                    cmd.CommandText = "insert into " + table + " (EstateID, uuid) values ( ?EstateID, ?uuid )";

                    foreach (UUID uuid in data)
                    {
                        cmd.Parameters.AddWithValue("?EstateID", EstateID.ToString());
                        cmd.Parameters.AddWithValue("?uuid", uuid.ToString());

                        cmd.ExecuteNonQuery();
                        cmd.Parameters.Clear();
                    }
                }
                dbcon.Close();
            }
        }

        UUID[] LoadUUIDList(uint EstateID, string table)
        {
            List<UUID> uuids = new List<UUID>();

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();
using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
{
    dbcon.Open();

    using (MySqlCommand cmd = dbcon.CreateCommand())
    {
        cmd.CommandText = "select uuid from @table where EstateID = @EstateID";
public List<int> GetEstates(string search)
{
    List<int> result = new List<int>();

    using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
    {
        dbcon.Open();

        using (MySqlCommand cmd = dbcon.CreateCommand())
        {
            string sql = "select estateID from estate_settings where estate_name like @search";
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("@search", "%" + search + "%");

            using (IDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    result.Add(Convert.ToInt32(reader["estateID"]));
                }
                reader.Close();
            }
        }
        dbcon.Close();
    }
public List<int> GetEstatesBySearch(string search)
{
    List<int> result = new List<int>();

    using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
    {
        dbcon.Open();

        using (MySqlCommand cmd = dbcon.CreateCommand())
        {
            cmd.CommandText = "select estateID from estate_settings where estateID like @search";
            cmd.Parameters.AddWithValue("@search", "%" + search + "%");

            using (IDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    result.Add(Convert.ToInt32(reader["estateID"]));
                }
                reader.Close();
            }
        }
        dbcon.Close();
    }
return result;
}

public List<int> GetEstatesByName(string search)
{
    List<int> result = new List<int>();

    using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
    {
        dbcon.Open();

        using (MySqlCommand cmd = dbcon.CreateCommand())
        {
            cmd.CommandText = "select estateID from estate_settings where EstateName = @EstateName";
            cmd.Parameters.AddWithValue("@EstateName", search);

            using (IDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    result.Add(Convert.ToInt32(reader["estateID"]));
                }
                reader.Close();
            }
        }
        dbcon.Close();
    }

    return result;
}

public List<int> GetEstatesByOwner(UUID ownerID)
{
    List<int> result = new List<int>();

    using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
    {
        dbcon.Open();

        using (MySqlCommand cmd = dbcon.CreateCommand())
        {
            cmd.CommandText = "select estateID from estate_settings where EstateOwner = @EstateOwner";
            cmd.Parameters.AddWithValue("@EstateOwner", ownerID.ToString());

            using (IDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    result.Add(Convert.ToInt32(reader["estateID"]));
                }
                reader.Close();
            }
        }
        dbcon.Close();
    }

    return result;
}

public bool LinkRegion(UUID regionID, int estateID)
{
    using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
    {
        dbcon.Open();
        MySqlTransaction transaction = dbcon.BeginTransaction();

        try
        {
            // Delete any existing association of this region with an estate.
            using (MySqlCommand cmd = dbcon.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = "delete from estate_map where RegionID = @RegionID";
                cmd.Parameters.AddWithValue("@RegionID", regionID.ToString());

                cmd.ExecuteNonQuery();
            }

            using (MySqlCommand cmd = dbcon.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = "insert into estate_map (RegionID, EstateID) values (@RegionID, @EstateID)";
                cmd.Parameters.AddWithValue("@RegionID", regionID.ToString());
                cmd.Parameters.AddWithValue("@EstateID", estateID);

                cmd.ExecuteNonQuery();
            }

            transaction.Commit();
            return true;
        }
        catch (Exception ex)
        {
            m_log.Error("Error linking region to estate", ex);
            transaction.Rollback();
            return false;
        }
        finally
        {
            dbcon.Close();
        }
    }
}
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = "insert into estate_map values (?RegionID, ?EstateID)";
                        cmd.Parameters.AddWithValue("?RegionID", regionID.ToString());
                        cmd.Parameters.AddWithValue("?EstateID", estateID);

                        int ret = cmd.ExecuteNonQuery();

                        if (ret != 0)
                            transaction.Commit();
                        else
                            transaction.Rollback();

                        dbcon.Close();

                        return (ret != 0);
                    }
                }
                catch (MySqlException ex)
                {
                    m_log.Error("[REGION DB]: LinkRegion failed: " + ex.Message);
                    transaction.Rollback();
                }

                dbcon.Close();
            }

            return false;
        }

        public List<UUID> GetRegions(int estateID)
        {
            List<UUID> result = new List<UUID>();

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                try
                {
                    using (MySqlCommand cmd = dbcon.CreateCommand())
                    {
                        cmd.CommandText = "select RegionID from estate_map where EstateID = ?EstateID";
                        cmd.Parameters.AddWithValue("?EstateID", estateID.ToString());

                        using (IDataReader reader = cmd.ExecuteReader())
                        {
                            while(reader.Read())
                                result.Add(DBGuid.FromDB(reader["RegionID"]));
                            reader.Close();
                        }
                    }
                }
                catch (Exception e)
                {
                    m_log.Error("[REGION DB]: Error reading estate map. " + e.ToString());
                    return result;
                }
                dbcon.Close();
            }

            return result;
        }

        public bool DeleteEstate(int estateID)
        {
            return false;
        }
    }
}
