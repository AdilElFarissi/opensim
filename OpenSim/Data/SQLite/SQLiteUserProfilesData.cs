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
using System.Data.SQLite;

using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;

namespace OpenSim.Data.SQLite
{
    public class SQLiteUserProfilesData: IProfilesData
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private SQLiteConnection m_connection;
        private string m_connectionString;

        private Dictionary<string, FieldInfo> m_FieldMap =
            new Dictionary<string, FieldInfo>();

        protected virtual Assembly Assembly
        {
            get { return GetType().Assembly; }
        }

        public SQLiteUserProfilesData()
        {
        }

        public SQLiteUserProfilesData(string connectionString)
        {
            Initialise(connectionString);
        }

        public void Initialise(string connectionString)
        {
            DllmapConfigHelper.RegisterAssembly(typeof(SQLiteConnection).Assembly);

            m_connectionString = connectionString;

            m_log.Info("[PROFILES_DATA]: Sqlite - connecting: "+m_connectionString);

            m_connection = new SQLiteConnection(m_connectionString);
            m_connection.Open();

            Migration m = new Migration(m_connection, Assembly, "UserProfiles");
            m.Update();
        }

        private string[] FieldList
        {
            get { return new List<string>(m_FieldMap.Keys).ToArray(); }
        }

        #region IProfilesData implementation
        public OSDArray GetClassifiedRecords(UUID creatorId)
        {
            OSDArray data = new OSDArray();
            string query = "SELECT classifieduuid, name FROM classifieds WHERE creatoruuid = :Id";
            IDataReader reader = null;

            using (SQLiteCommand cmd = (SQLiteCommand)m_connection.CreateCommand())
            {
                cmd.CommandText = query;
                cmd.Parameters.AddWithValue(":Id", creatorId);
                reader = cmd.ExecuteReader();
            }

            while (reader.Read())
            {
                OSDMap n = new OSDMap();
                UUID Id = UUID.Zero;
                string Name = null;
                try
                {
                    UUID.TryParse(Convert.ToString( reader["classifieduuid"]), out Id);
                    Name = Convert.ToString(reader["name"]);
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[PROFILES_DATA]" +
                                      ": UserAccount exception {0}", e.Message);
                }
                n.Add("classifieduuid", OSD.FromUUID(Id));
                n.Add("name", OSD.FromString(Name));
                data.Add(n);
            }

            reader.Close();

            return data;
        }
        public bool UpdateClassifiedRecord(UserClassifiedAdd ad, ref string result)
        {
            string query = string.Empty;

            query += "INSERT OR REPLACE INTO classifieds (";
            query += "`classifieduuid`,";
            query += "`creatoruuid`,";
            query += "`creationdate`,";
            query += "`expirationdate`,";
            query += "`category`,";
            query += "`name`,";
            query += "`description`,";
            query += "`parceluuid`,";
            query += "`parentestate`,";
            query += "`snapshotuuid`,";
            query += "`simname`,";
            query += "`posglobal`,";
            query += "`parcelname`,";
            query += "`classifiedflags`,";
            query += "`priceforlisting`) ";
            query += "VALUES (";
            query += ":ClassifiedId,";
            query += ":CreatorId,";
            query += ":CreatedDate,";
            query += ":ExpirationDate,";
            query += ":Category,";
            query += ":Name,";
            query += ":Description,";
using (SQLiteCommand cmd = (SQLiteCommand)m_connection.CreateCommand())
{
    cmd.CommandText = "DELETE FROM classifieds WHERE classifieduuid = @ClasifiedId";
    cmd.Parameters.AddWithValue("@ClasifiedId", recordId.ToString());
    cmd.ExecuteNonQuery();
}
catch (Exception e)
{
    m_log.ErrorFormat("[PROFILES_DATA]" +
                      ": ClassifiedesUpdate exception {0}", e.Message);
    result = e.Message;
    return false;
}
return true;
                    cmd.Parameters.AddWithValue(":ClassifiedId", recordId.ToString());

                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[PROFILES_DATA]" +
                                  ": DeleteClassifiedRecord exception {0}", e.Message);
                return false;
            }
            return true;
        }

        public bool GetClassifiedInfo(ref UserClassifiedAdd ad, ref string result)
        {
            IDataReader reader = null;
            string query = string.Empty;

            query += "SELECT * FROM classifieds WHERE ";
            query += "classifieduuid = :AdId";

            try
            {
                using (SQLiteCommand cmd = (SQLiteCommand)m_connection.CreateCommand())
                {
public bool GetPickInfo(UUID adId, UUID pickId)
{
    IDataReader reader = null;
    string query = string.Empty;
    UserProfilePick pick = new UserProfilePick();

    query = "SELECT * FROM userpicks WHERE creatoruuid = @CreatorId AND pickuuid = @PickId";

    try
    {
        using (SQLiteCommand cmd = (SQLiteCommand)m_connection.CreateCommand())
        {
            cmd.CommandText = query;
            cmd.Parameters.AddWithValue("@CreatorId", adId.ToString());
            cmd.Parameters.AddWithValue("@PickId", pickId.ToString());

            using (reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    pick.CreatorId = new UUID(reader["creatoruuid"].ToString());
                    pick.ParcelId = new UUID(reader["parceluuid"].ToString());
                    pick.SnapshotId = new UUID(reader["snapshotuuid"].ToString());
                    pick.CreationDate = Convert.ToInt32(reader["creationdate"]);
                    pick.ExpirationDate = Convert.ToInt32(reader["expirationdate"]);
                    pick.ParentEstate = Convert.ToInt32(reader["parentestate"]);
                    pick.Flags = (byte)Convert.ToUInt32(reader["classifiedflags"]);
public UserProfilePick GetPickInfo(UUID avatarId, UUID pickId)
{
    string query = "SELECT `description`, `pickuuid`, `creatoruuid`, `parceluuid`, `snapshotuuid` FROM userpicks WHERE creatoruuid = @CreatorId AND pickuuid = @PickId";

    try
    {
        using (SQLiteCommand cmd = (SQLiteCommand)m_connection.CreateCommand())
        {
            cmd.CommandText = query;
            cmd.Parameters.AddWithValue("@CreatorId", avatarId.ToString());
            cmd.Parameters.AddWithValue("@PickId", pickId.ToString());

            using (IDataReader reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    UserProfilePick pick = new UserProfilePick();

                    string description = (string)reader["description"];

                    if (string.IsNullOrEmpty(description))
                        description = "No description given.";

                    UUID.TryParse((string)reader["pickuuid"], out pick.PickId);
                    UUID.TryParse((string)reader["creatoruuid"], out pick.CreatorId);
                    UUID.TryParse((string)reader["parceluuid"], out pick.ParcelId);
                    UUID.TryParse((string)reader["snapshotuuid"], out pick.SnapshotId);

                    pick.Category = Convert.ToInt32(reader["category"]);
                    pick.Price = Convert.ToInt16(reader["priceforlisting"]);
                    pick.Name = reader["name"].ToString();
                    pick.Description = description;
                    pick.SimName = reader["simname"].ToString();
                    pick.GlobalPos = reader["posglobal"].ToString();
                    pick.ParcelName = reader["parcelname"].ToString();

                    return pick;
                }
            }
        }
    }
    catch (Exception e)
    {
        m_log.ErrorFormat("[PROFILES_DATA]" +
                          ": GetPickInfo exception {0}", e.Message);
    }
public UserProfilePick GetPickInfo(string pickId)
{
    UserProfilePick pick = new UserProfilePick();

    try
    {
        using (SQLiteCommand cmd = (SQLiteCommand)m_connection.CreateCommand())
        {
            cmd.CommandText = "SELECT * FROM userpicks WHERE pickuuid = :PickId";
            cmd.Parameters.AddWithValue(":PickId", pickId);

            using (SQLiteDataReader reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    pick.PickId = pickId;
                    pick.CreatorId = reader["creatoruuid"].ToString();
                    bool topPick;
                    bool.TryParse(reader["toppick"].ToString(), out topPick);
                    pick.TopPick = topPick;
                    pick.ParcelId = reader["parceluuid"].ToString();
                    pick.Name = reader["name"].ToString();
                    pick.Desc = reader["description"].ToString();
                    pick.SnapshotId = reader["snapshotuuid"].ToString();
                    pick.ParcelName = reader["user"].ToString();
                    pick.OriginalName = reader["originalname"].ToString();
                    pick.SimName = reader["simname"].ToString();
                    pick.GlobalPos = reader["posglobal"].ToString();
                    int sortOrder;
                    int.TryParse(reader["sortorder"].ToString(), out sortOrder);
                    pick.SortOrder = sortOrder;
                    bool enabled;
                    bool.TryParse(reader["enabled"].ToString(), out enabled);
                    pick.Enabled = enabled;
                }
            }
        }
public bool UpdatePicksRecord(UserProfilePick pick)
{
    string query = @"
        INSERT OR REPLACE INTO userpicks (
            pickuuid, 
            creatoruuid, 
            toppick, 
            parceluuid, 
            name, 
            description, 
            snapshotuuid, 
            user, 
            originalname, 
            simname, 
            posglobal, 
            sortorder, 
            enabled 
        ) 
        VALUES (
            :PickId,
            :CreatorId,
            :TopPick,
            :ParcelId,
            :Name,
            :Desc,
            :SnapshotId,
            :User,
            :Original,
            :SimName,
            :GlobalPos,
            :SortOrder,
            :Enabled
        )";

    try
    {
        using (SQLiteCommand cmd = (SQLiteCommand)m_connection.CreateCommand())
        {
            cmd.CommandText = query;

            cmd.Parameters.AddWithValue(":PickId", pick.PickId.ToString());
            cmd.Parameters.AddWithValue(":CreatorId", pick.CreatorId.ToString());
            cmd.Parameters.AddWithValue(":TopPick", pick.TopPick);
            cmd.Parameters.AddWithValue(":ParcelId", pick.ParcelId.ToString());
            cmd.Parameters.AddWithValue(":Name", pick.Name.ToString());
            cmd.Parameters.AddWithValue(":Desc", pick.Desc.ToString());
            cmd.Parameters.AddWithValue(":SnapshotId", pick.SnapshotId.ToString());
            cmd.Parameters.AddWithValue(":User", pick.ParcelName.ToString());
            cmd.Parameters.AddWithValue(":Original", pick.OriginalName.ToString());
            cmd.Parameters.AddWithValue(":SimName", pick.SimName.ToString());
            cmd.Parameters.AddWithValue(":GlobalPos", pick.GlobalPos);
            cmd.Parameters.AddWithValue(":SortOrder", pick.SortOrder.ToString());
            cmd.Parameters.AddWithValue(":Enabled", pick.Enabled);

            return cmd.ExecuteNonQuery() > 0;
        }
    }
    catch (Exception e)
    {
        m_log.ErrorFormat("[PROFILES_DATA]" +
                          ": UpdatePicksRecord exception {0}", e.Message);
        return false;
    }
}
        }

        public bool DeletePicksRecord(UUID pickId)
        {
            string query = string.Empty;

            query += "DELETE FROM userpicks WHERE ";
            query += "pickuuid = :PickId";

            try
            {
                using (SQLiteCommand cmd = (SQLiteCommand)m_connection.CreateCommand())
                {
                    cmd.CommandText = query;
                    cmd.Parameters.AddWithValue(":PickId", pickId.ToString());
                    cmd.ExecuteNonQuery();
                }
public bool DeleteUserPickRecord()
{
    try
    {
        using (SQLiteCommand cmd = (SQLiteCommand)m_connection.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM usernotes WHERE useruuid = :Id AND targetuuid = :TargetId";
            cmd.Parameters.AddWithValue(":Id", m_userId.ToString());
            cmd.Parameters.AddWithValue(":TargetId", m_targetId.ToString());
            cmd.ExecuteNonQuery();
        }
        return true;
    }
    catch (Exception e)
    {
        m_log.Error("[PROFILES_DATA]: DeleteUserPickRecord exception", e);
        return false;
    }
}

public bool GetAvatarNotes(ref UserProfileNotes notes)
{
    try
    {
        using (SQLiteCommand cmd = (SQLiteCommand)m_connection.CreateCommand())
        {
            cmd.CommandText = "SELECT `notes` FROM usernotes WHERE useruuid = :Id AND targetuuid = :TargetId";
            cmd.Parameters.AddWithValue(":Id", notes.UserId.ToString());
            cmd.Parameters.AddWithValue(":TargetId", notes.TargetId.ToString());
            using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleRow))
            {
                if (reader.Read())
                {
                    notes.Notes = OSD.FromString((string)reader["notes"]);
                }
            }
        }
        return true;
public bool UpdateAvatarNotes(ref UserProfileNotes note, ref string result)
{
    try
    {
        using (SQLiteCommand cmd = (SQLiteCommand)m_connection.CreateCommand())
        {
            if (string.IsNullOrEmpty(note.Notes))
            {
                cmd.CommandText = "DELETE FROM usernotes WHERE useruuid = @UserId AND targetuuid = @TargetId";
                cmd.Parameters.AddWithValue("@UserId", note.UserId.ToString());
                cmd.Parameters.AddWithValue("@TargetId", note.TargetId.ToString());
            }
            else
            {
                cmd.CommandText = "INSERT OR REPLACE INTO usernotes VALUES ( @UserId, @TargetId, @Notes )";
                cmd.Parameters.AddWithValue("@UserId", note.UserId.ToString());
                cmd.Parameters.AddWithValue("@TargetId", note.TargetId.ToString());
                cmd.Parameters.AddWithValue("@Notes", note.Notes);
            }
            cmd.ExecuteNonQuery();
        }
        return true;
    }
    catch (Exception e)
    {
        m_log.Error("[PROFILES_DATA]: UpdateAvatarNotes exception", e);
        return false;
    }
public bool GetAvatarProperties(ref UserProfileProperties props, ref string result)
{
    IDataReader reader = null;
    string query = string.Empty;

    query += "SELECT * FROM userprofile WHERE ";
    query += "useruuid = @Id";

    using (SQLiteCommand cmd = (SQLiteCommand)m_connection.CreateCommand())
    {
        cmd.CommandText = query;
        cmd.Parameters.AddWithValue("@Id", props.UserId.ToString());

        try
        {
            reader = cmd.ExecuteReader();
        }
        catch (Exception e)
        {
            m_log.Error("[PROFILES_DATA]: GetAvatarProperties exception", e);
            result = e.Message;
            return false;
        }

        if (reader != null && reader.Read())
        {
            props.WebUrl = (string)reader["profileURL"];
            UUID.TryParse((string)reader["profileImage"], out props.ImageId);
            props.AboutText = (string)reader["profileAboutText"];
            UUID.TryParse((string)reader["profileFirstImage"], out props.FirstLifeImageId);
            props.FirstLifeText = (string)reader["profileFirstText"];
            UUID.TryParse((string)reader["profilePartner"], out props.PartnerId);
            props.WantToMask = (int)reader["profileWantToMask"];
            props.WantToText = (string)reader["profileWantToText"];
            props.SkillsMask = (int)reader["profileSkillsMask"];
            props.SkillsText = (string)reader["profileSkillsText"];
            props.Language = (string)reader["profileLanguages"];
        }
    }
    return true;
}

public bool SaveAvatarProperties(ref UserProfileProperties props, ref string result)
{
    string query = "INSERT INTO userprofile (";
    query += "useruuid, ";
using (SQLiteCommand cmd = (SQLiteCommand)m_connection.CreateCommand())
{
    cmd.CommandText = "INSERT INTO profiles_data (profilePartner, profileAllowPublish, profileMaturePublish, profileURL, profileWantToMask, profileWantToText, profileSkillsMask, profileSkillsText, profileLanguages, profileImage, profileAboutText, profileFirstImage, profileFirstText) VALUES (@userId, @profilePartner, @profileAllowPublish, @profileMaturePublish, @profileURL, @profileWantToMask, @profileWantToText, @profileSkillsMask, @profileSkillsText, @profileLanguages, @profileImage, @profileAboutText, @profileFirstImage, @profileFirstText)";
    cmd.Parameters.AddWithValue("@userId", props.UserId.ToString());
    cmd.Parameters.AddWithValue("@profilePartner", props.PartnerId);
    cmd.Parameters.AddWithValue("@profileAllowPublish", props.PublishProfile);
    cmd.Parameters.AddWithValue("@profileMaturePublish", props.PublishMature);
    cmd.Parameters.AddWithValue("@profileURL", props.WebUrl);
    cmd.Parameters.AddWithValue("@profileWantToMask", props.WantToMask);
    cmd.Parameters.AddWithValue("@profileWantToText", props.WantToText);
    cmd.Parameters.AddWithValue("@profileSkillsMask", props.SkillsMask);
    cmd.Parameters.AddWithValue("@profileSkillsText", props.SkillsText);
    cmd.Parameters.AddWithValue("@profileLanguages", props.Language);
    cmd.Parameters.AddWithValue("@profileImage", props.ImageId);
    cmd.Parameters.AddWithValue("@profileAboutText", props.AboutText);
    cmd.Parameters.AddWithValue("@profileFirstImage", props.FirstLifeImageId);
    cmd.Parameters.AddWithValue("@profileFirstText", props.FirstLifeText);

    try
    {
        cmd.ExecuteNonQuery();
    }
    catch (Exception e)
    {
        m_log.Error("[PROFILES_DATA]: SaveAvatarProperties exception", e);
        result = e.Message;
        return false;
    }
}
public bool UpdateAvatarProperties(ref UserProfileProperties props, ref string result)
{
    string query = string.Empty;

    query += "UPDATE userprofile SET ";
    query += "profileURL=:profileURL, ";
    query += "profileImage=:image, ";
    query += "profileAboutText=:abouttext,";
    query += "profileFirstImage=:firstlifeimage,";
    query += "profileFirstText=:firstlifetext ";
    query += "WHERE useruuid=:uuid";

    try
    {
        using (SQLiteCommand cmd = (SQLiteCommand)m_connection.CreateCommand())
        {
            cmd.CommandText = query;
            cmd.Parameters.AddWithValue(":profileURL", props.WebUrl);
            cmd.Parameters.AddWithValue(":image", props.ImageId.ToString());
            cmd.Parameters.AddWithValue(":abouttext", props.AboutText);
            cmd.Parameters.AddWithValue(":firstlifeimage", props.FirstLifeImageId.ToString());
            cmd.Parameters.AddWithValue(":firstlifetext", props.FirstLifeText);
            cmd.Parameters.AddWithValue(":uuid", props.UserId.ToString());

            cmd.ExecuteNonQuery();
        }
    }
    catch (Exception e)
    {
        m_log.ErrorFormat("[PROFILES_DATA]" +
                          ": AgentPropertiesUpdate exception {0}", e.Message);

        return false;
    }
    return true;
}

public bool UpdateAvatarInterests(UserProfileProperties up, ref string result)
{
    string query = string.Empty;

    query += "UPDATE userprofile SET ";
    query += "profileWantToMask=:WantMask, ";
    query += "profileWantToText=:WantText,";
    query += "profileSkillsMask=:SkillsMask,";
    query += "profileSkillsText=:SkillsText, ";
    query += "profileLanguages=:Languages ";
    query += "WHERE useruuid=:uuid";

    try
    {
        using (SQLiteCommand cmd = (SQLiteCommand)m_connection.CreateCommand())
        {
            cmd.CommandText = query;
            cmd.Parameters.AddWithValue(":WantMask", up.WantToMask);
            cmd.Parameters.AddWithValue(":WantText", up.WantToText);
            cmd.Parameters.AddWithValue(":SkillsMask", up.SkillsMask);
            cmd.Parameters.AddWithValue(":SkillsText", up.SkillsText);
            cmd.Parameters.AddWithValue(":Languages", up.Language);
            cmd.Parameters.AddWithValue(":uuid", up.UserId.ToString());

            cmd.ExecuteNonQuery();
        }
    }
    catch (Exception e)
    {
        m_log.ErrorFormat("[PROFILES_DATA]" +
                          ": UpdateAvatarInterests exception {0}", e.Message);

        return false;
    }
    return true;
}

public bool UpdateAvatarPropertiesWithPreparedStatements(ref UserProfileProperties props, ref string result)
{
    string query = string.Empty;

    query += "UPDATE userprofile SET ";
    query += "profileURL=@profileURL, ";
    query += "profileImage=@image, ";
    query += "profileAboutText=@abouttext,";
    query += "profileFirstImage=@firstlifeimage,";
    query += "profileFirstText=@firstlifetext ";
    query += "WHERE useruuid=@uuid";

    try
    {
        using (SQLiteCommand cmd = (SQLiteCommand)m_connection.CreateCommand())
        {
            cmd.CommandText = query;

            cmd.Parameters.AddWithValue("@profileURL", props.WebUrl);
            cmd.Parameters.AddWithValue("@image", props.ImageId.ToString());
            cmd.Parameters.AddWithValue("@abouttext", props.AboutText);
            cmd.Parameters.AddWithValue("@firstlifeimage", props.FirstLifeImageId.ToString());
            cmd.Parameters.AddWithValue("@firstlifetext", props.FirstLifeText);
            cmd.Parameters.AddWithValue("@uuid", props.UserId.ToString());

            cmd.ExecuteNonQuery();
        }
    }
    catch (Exception e)
    {
        m_log.ErrorFormat("[PROFILES_DATA]" +
                          ": AgentPropertiesUpdate exception {0}", e.Message);

        return false;
    }
    return true;
}

public bool UpdateAvatarInterestsWithPreparedStatements(UserProfileProperties up, ref string result)
{
    string query = string.Empty;

public bool UpdateUserProfile(UserProfile up, ref string result)
{
    string query = string.Empty;

    query += "UPDATE userprofile SET ";
    query += "profileWantToMask=:WantMask, ";
    query += "profileWantToText=:WantText,";
    query += "profileSkillsMask=:SkillsMask,";
    query += "profileSkillsText=:SkillsText, ";
    query += "profileLanguages=:Languages ";
    query += "WHERE useruuid=:uuid";

    try
    {
        using (SQLiteCommand cmd = (SQLiteCommand)m_connection.CreateCommand())
        {
            cmd.CommandText = query;

            cmd.Parameters.AddWithValue(":WantMask", up.WantToMask);
            cmd.Parameters.AddWithValue(":WantText", up.WantToText);
            cmd.Parameters.AddWithValue(":SkillsMask", up.SkillsMask);
            cmd.Parameters.AddWithValue(":SkillsText", up.SkillsText);
            cmd.Parameters.AddWithValue(":Languages", up.Language);
            cmd.Parameters.AddWithValue(":uuid", up.UserId.ToString());

            cmd.ExecuteNonQuery();
        }
    }
    catch (Exception e)
    {
        m_log.ErrorFormat("[PROFILES_DATA]" +
                          ": UpdateAvatarInterests exception {0}", e.Message);

        return false;
    }
    return true;
}
public bool UpdateUserPreferences(UserPreferences pref, ref string result)
{
    try
    {
        using (SQLiteCommand cmd = (SQLiteCommand)m_connection.CreateCommand())
        {
            cmd.CommandText = "UPDATE usersettings SET imviaemail = :ImViaEmail, visible = :Visible, email = :EMail WHERE useruuid = :uuid";
            cmd.Parameters.AddWithValue(":ImViaEmail", pref.IMViaEmail);
            cmd.Parameters.AddWithValue(":Visible", pref.Visible);
            cmd.Parameters.AddWithValue(":EMail", pref.EMail);
            cmd.Parameters.AddWithValue(":uuid", pref.UserId.ToString());

            cmd.ExecuteNonQuery();
        }
    }
    catch (Exception e)
    {
        m_log.ErrorFormat("[PROFILES_DATA]" +
                          ": AgentInterestsUpdate exception {0}", e.Message);
        result = e.Message;
        return false;
    }
    return true;
}

public bool GetUserPreferences(UserPreferences pref, ref string result)
{
    IDataReader reader = null;
    try
    {
        using (SQLiteCommand cmd = (SQLiteCommand)m_connection.CreateCommand())
        {
            cmd.CommandText = "SELECT imviaemail, visible, email FROM usersettings WHERE useruuid = :Id";
            cmd.Parameters.AddWithValue(":Id", pref.UserId.ToString());

            using (reader = cmd.ExecuteReader(CommandBehavior.SingleRow))
public bool GetUserPreferences(ref UserPreferences pref, ref string result)
{
    IDataReader reader = null;
    string query = string.Empty;

    query += "SELECT * FROM `usersettings` WHERE ";
    query += "UserId = :Id";

    try
    {
        using (SQLiteCommand cmd = (SQLiteCommand)m_connection.CreateCommand())
        {
            cmd.CommandText = query;
            cmd.Parameters.AddWithValue(":Id", pref.UserId.ToString());

            using (reader = cmd.ExecuteReader(CommandBehavior.SingleRow))
            {
                if (reader.Read())
                {
                    pref.IMViaEmail = (bool)reader["imviaemail"];
                    pref.Visible = (bool)reader["visible"];
                    pref.EMail = (string)reader["email"];
                }
                else
                {
                    using (SQLiteCommand put = (SQLiteCommand)m_connection.CreateCommand())
                    {
                        put.CommandText = "INSERT INTO usersettings VALUES (:Id, 'false', 'false', :Email)";
                        put.Parameters.AddWithValue(":Id", pref.UserId.ToString());
                        put.Parameters.AddWithValue(":Email", pref.EMail);
                        put.ExecuteNonQuery();
                    }
                }
            }
public bool GetUserAppData(ref UserAppData props, ref string result)
{
    IDataReader reader = null;
    string query = string.Empty;

    query += "SELECT * FROM `userdata` WHERE ";
    query += "UserId = @Id AND ";
    query += "TagId = @TagId";

    try
    {
        using (SQLiteCommand cmd = (SQLiteCommand)m_connection.CreateCommand())
        {
            cmd.CommandText = query;
            cmd.Parameters.AddWithValue("@Id", props.UserId.ToString());
            cmd.Parameters.AddWithValue("@TagId", props.TagId.ToString());

            using (reader = cmd.ExecuteReader(CommandBehavior.SingleRow))
            {
                if (reader.Read())
                {
                    props.DataKey = (string)reader["DataKey"];
                    props.DataVal = (string)reader["DataVal"];
                }
                else
                {
                    using (SQLiteCommand put = (SQLiteCommand)m_connection.CreateCommand())
                    {
                        put.CommandText = "INSERT INTO userdata VALUES (@UserId, @TagId, @DataKey, @DataVal)";
                        put.Parameters.AddWithValue("@UserId", props.UserId.ToString());
                        put.Parameters.AddWithValue("@TagId", props.TagId.ToString());
                        put.Parameters.AddWithValue("@DataKey", props.DataKey.ToString());
                        put.Parameters.AddWithValue("@DataVal", props.DataVal.ToString());

                        put.ExecuteNonQuery();
                    }
                }
            }
        }
    }
    catch (Exception e)
    {
        m_log.ErrorFormat("[PROFILES_DATA]" +
                          ": GetUserAppData exception {0}", e.Message);
        result = e.Message;
        return false;
    }
    return true;
}

public bool SetUserAppData(UserAppData props, ref string result)
{
    string query = "UPDATE userdata SET TagId = @TagId, DataKey = @DataKey, DataVal = @DataVal WHERE UserId = @UserId AND TagId = @TagId";

    try
    {
        using (SQLiteCommand cmd = (SQLiteCommand)m_connection.CreateCommand())
        {
            cmd.CommandText = query;
            cmd.Parameters.AddWithValue("@UserId", props.UserId.ToString());
            cmd.Parameters.AddWithValue("@TagId", props.TagId.ToString());
            cmd.Parameters.AddWithValue("@DataKey", props.DataKey.ToString());
            cmd.Parameters.AddWithValue("@DataVal", props.DataVal.ToString());

            cmd.ExecuteNonQuery();
        }
    }
    catch (Exception e)
    {
        m_log.ErrorFormat("[PROFILES_DATA]" +
                          ": SetUserAppData exception {0}", e.Message);
        result = e.Message;
        return false;
    }
    return true;
}

public OSDArray GetUserImageAssets(UUID avatarId)
{
    IDataReader reader = null;
    OSDArray data = new OSDArray();
    string query = "SELECT `snapshotuuid` FROM {0} WHERE `creatoruuid` = @Id";

    try
    {
        using (SQLiteCommand cmd = (SQLiteCommand)m_connection.CreateCommand())
        {
            cmd.CommandText = query;
            cmd.Parameters.AddWithValue("@Id", avatarId.ToString());
            cmd.Parameters.AddWithValue("@table_name", "\"classifieds\"");

            using (reader = cmd.ExecuteReader(CommandBehavior.SingleRow))
            {
                while (reader.Read())
                {
                    data.Add(new OSDString((string)reader["snapshotuuid"].ToString()));
                }
            }
        }
    }
    catch (Exception e)
    {
        m_log.ErrorFormat("[DATABASE]" +
                          ": GetUserImageAssets exception {0}", e.Message);
        return null;
    }
using (SQLiteCommand cmd = (SQLiteCommand)m_connection.CreateCommand())
{
    cmd.CommandText = query;
    cmd.Parameters.AddWithValue(":Id", avatarId.ToString());

    using (reader = cmd.ExecuteReader(CommandBehavior.SingleRow))
    {
        if (reader.Read())
        {
            data.Add(new OSDString((string)reader["snapshotuuid"]));
            data.Add(new OSDString((string)reader["profileImage"]));
            data.Add(new OSDString((string)reader["profileFirstImage"]));
        }
    }
}

query = "SELECT `profileImage`, `profileFirstImage`, `snapshotuuid` FROM `userprofile` WHERE `useruuid` = :Id";

using (SQLiteCommand cmd = (SQLiteCommand)m_connection.CreateCommand())
{
    cmd.CommandText = query;
    cmd.Parameters.AddWithValue(":Id", avatarId.ToString());

    using (reader = cmd.ExecuteReader(CommandBehavior.SingleRow))
    {
        if (reader.Read())
        {
            data.Add(new OSDString((string)reader["snapshotuuid"]));
            data.Add(new OSDString((string)reader["profileImage"]));
            data.Add(new OSDString((string)reader["profileFirstImage"]));
catch (Exception e)
{
    m_log.ErrorFormat("[PROFILES_DATA]: GetAvatarNotes exception {0}", e.Message);
}
string query = "SELECT * FROM regions WHERE name = '" + regionName + "'";
SqlCommand command = new SqlCommand(query, connection);
SqlDataReader reader = command.ExecuteReader();

// ...

string query = "SELECT * FROM regions WHERE name = @name";
SqlCommand command = new SqlCommand(query, connection);
command.Parameters.AddWithValue("@name", regionName);
SqlDataReader reader = command.ExecuteReader();
