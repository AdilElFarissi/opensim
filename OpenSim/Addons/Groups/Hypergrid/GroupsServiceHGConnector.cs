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

using OpenSim.Framework;
using OpenSim.Server.Base;

using OpenMetaverse;
using log4net;

namespace OpenSim.Groups
{
    public class GroupsServiceHGConnector
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string m_ServerURI;
        private object m_Lock = new();

        public GroupsServiceHGConnector(string url)
        {
            m_ServerURI = url;
            if (!m_ServerURI.EndsWith("/"))
                m_ServerURI += "/";

            m_log.DebugFormat("[Groups.HGConnector]: Groups server at {0}", m_ServerURI);
        }

        public bool CreateProxy(string RequestingAgentID, string AgentID, string accessToken, UUID groupID, string url, string name, out string reason)
        {
            reason = string.Empty;

            Dictionary<string, object> sendData = new()
            {
                ["RequestingAgentID"] = RequestingAgentID,
                ["AgentID"] = AgentID.ToString(),
                ["AccessToken"] = accessToken,
                ["GroupID"] = groupID.ToString(),
                ["Location"] = url,
                ["Name"] = name
            };
            Dictionary<string, object> ret = MakeRequest("POSTGROUP", sendData);

            if (ret == null)
                return false;

            if (!ret.TryGetValue("RESULT", out object oresult))
                return false;

            string result = oresult.ToString();
            if (!result.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                reason = result;
                return false;
            }

            return true;

        }

        public void RemoveAgentFromGroup(string AgentID, UUID GroupID, string token)
        {
            Dictionary<string, object> sendData = new()
            {
                ["AgentID"] = AgentID,
                ["GroupID"] = GroupID.ToString(),
                ["AccessToken"] = GroupsDataUtils.Sanitize(token)
            };
            MakeRequest("REMOVEAGENTFROMGROUP", sendData);
        }

        public ExtendedGroupRecord GetGroupRecord(string RequestingAgentID, UUID GroupID, string GroupName, string token)
        {
            if (GroupID.IsZero() && string.IsNullOrEmpty(GroupName))
                return null;

            Dictionary<string, object> sendData = [];
            if (!GroupID.IsZero())
                sendData["GroupID"] = GroupID.ToString();
            if (!string.IsNullOrEmpty(GroupName))
                sendData["Name"] = GroupsDataUtils.Sanitize(GroupName);

            sendData["RequestingAgentID"] = RequestingAgentID;
            sendData["AccessToken"] = GroupsDataUtils.Sanitize(token);

            Dictionary<string, object> ret = MakeRequest("GETGROUP", sendData);

            if (ret == null)
                return null;

            if (!ret.TryGetValue("RESULT", out object oRESULT))
                return null;

            if (oRESULT is string sRESULT && sRESULT.Equals("NULL", StringComparison.OrdinalIgnoreCase))
                return null;

            return GroupsDataUtils.GroupRecord((Dictionary<string, object>)oRESULT);
        }

        public List<ExtendedGroupMembersData> GetGroupMembers(string RequestingAgentID, UUID GroupID, string token)
        {
            List<ExtendedGroupMembersData> members = [];

            Dictionary<string, object> sendData = new()
            {
                ["GroupID"] = GroupID.ToString(),
                ["RequestingAgentID"] = RequestingAgentID,
                ["AccessToken"] = GroupsDataUtils.Sanitize(token)
            };
            Dictionary<string, object> ret = MakeRequest("GETGROUPMEMBERS", sendData);

            if (ret == null)
                return members;

            if (!ret.TryGetValue("RESULT", out object oRESULT))
                return members;

            if (oRESULT is string sRESULT && sRESULT.Equals("NULL", StringComparison.OrdinalIgnoreCase))
                return members;

            foreach (object v in ((Dictionary<string, object>)oRESULT).Values)
            {
                ExtendedGroupMembersData m = GroupsDataUtils.GroupMembersData((Dictionary<string, object>)v);
                members.Add(m);
            }

            return members;
        }

        public List<GroupRolesData> GetGroupRoles(string RequestingAgentID, UUID GroupID, string token)
        {
            List<GroupRolesData> roles = [];

            Dictionary<string, object> sendData = new()
            {
                ["GroupID"] = GroupID.ToString(),
                ["RequestingAgentID"] = RequestingAgentID,
                ["AccessToken"] = GroupsDataUtils.Sanitize(token)
            };
            Dictionary<string, object> ret = MakeRequest("GETGROUPROLES", sendData);

            if (ret == null)
                return roles;

            if (!ret.TryGetValue("RESULT", out object oRESULT))
                return roles;

            if (oRESULT is string sRESULT && sRESULT.Equals("NULL", StringComparison.OrdinalIgnoreCase))
                return roles;

            foreach (object v in ((Dictionary<string, object>)oRESULT).Values)
            {
                GroupRolesData m = GroupsDataUtils.GroupRolesData((Dictionary<string, object>)v);
                roles.Add(m);
            }

            return roles;
        }

        public List<ExtendedGroupRoleMembersData> GetGroupRoleMembers(string RequestingAgentID, UUID GroupID, string token)
        {
            List<ExtendedGroupRoleMembersData> rmembers = [];

            Dictionary<string, object> sendData = new()
            {
                ["GroupID"] = GroupID.ToString(),
                ["RequestingAgentID"] = RequestingAgentID,
                ["AccessToken"] = GroupsDataUtils.Sanitize(token)
            };
            Dictionary<string, object> ret = MakeRequest("GETROLEMEMBERS", sendData);

            if (ret == null)
                return rmembers;

            if (!ret.TryGetValue("RESULT", out object oRESULT))
                return rmembers;

            if (oRESULT is string sRESULT && sRESULT.Equals("NULL", StringComparison.OrdinalIgnoreCase))
                return rmembers;

            foreach (object v in ((Dictionary<string, object>)oRESULT).Values)
            {
                ExtendedGroupRoleMembersData m = GroupsDataUtils.GroupRoleMembersData((Dictionary<string, object>)v);
                rmembers.Add(m);
            }

            return rmembers;
        }

        public bool AddNotice(string RequestingAgentID, UUID groupID, UUID noticeID, string fromName, string subject, string message,
                                    bool hasAttachment, byte attType, string attName, UUID attItemID, string attOwnerID)
        {
            Dictionary<string, object> sendData = new()
            {
                ["GroupID"] = groupID.ToString(),
                ["NoticeID"] = noticeID.ToString(),
                ["FromName"] = GroupsDataUtils.Sanitize(fromName),
                ["Subject"] = GroupsDataUtils.Sanitize(subject),
                ["Message"] = GroupsDataUtils.Sanitize(message),
                ["HasAttachment"] = hasAttachment.ToString()
            };
            if (hasAttachment)
            {
                sendData["AttachmentType"] = attType.ToString();
                sendData["AttachmentName"] = attName.ToString();
                sendData["AttachmentItemID"] = attItemID.ToString();
                sendData["AttachmentOwnerID"] = attOwnerID;
            }
            sendData["RequestingAgentID"] = RequestingAgentID;

            Dictionary<string, object> ret = MakeRequest("ADDNOTICE", sendData);

            if (ret == null)
                return false;

            if (!ret.TryGetValue("RESULT", out object value))
                return false;

             return value.ToString().Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        public bool VerifyNotice(UUID noticeID, UUID groupID)
        {
            Dictionary<string, object> sendData = new()
            {
                ["NoticeID"] = noticeID.ToString(),
                ["GroupID"] = groupID.ToString()
            };
            Dictionary<string, object> ret = MakeRequest("VERIFYNOTICE", sendData);

            if (ret == null)
                return false;

            if (!ret.TryGetValue("RESULT", out object value))
                return false;

            return value.ToString().Equals("true", StringComparison.CurrentCultureIgnoreCase);
        }

        //
        //
        //
        //
        //

        #region Make Request

        private Dictionary<string, object> MakeRequest(string method, Dictionary<string, object> sendData)
        {
            sendData["METHOD"] = method;

            string reply = string.Empty;
            try
            {
                lock (m_Lock)
                    reply = SynchronousRestFormsRequester.MakeRequest("POST",
                         m_ServerURI + "hg-groups",
                         ServerUtils.BuildQueryString(sendData));
            }
            catch
            {
                return null;
            }

            //m_log.DebugFormat("[XXX]: reply was {0}", reply);

            if (string.IsNullOrEmpty(reply))
                return null;

            Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

            return replyData;
        }
        #endregion

    }
}
