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

using log4net;
using System;
using System.Reflection;
using System.IO;
using System.Xml;
using System.Collections.Generic;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using FriendInfo = OpenSim.Services.Interfaces.FriendInfo;
using OpenSim.Framework;
using OpenSim.Framework.ServiceAuth;
using OpenSim.Framework.Servers.HttpServer;
using OpenMetaverse;

namespace OpenSim.Server.Handlers.Friends
{
    /// <summary>
    /// Handles HTTP POST requests for friends-related operations.
    /// </summary>
    public class FriendsServerPostHandler : BaseStreamHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly IFriendsService m_FriendsService;

        /// <summary>
        /// Initializes a new instance of the FriendsServerPostHandler class.
        /// </summary>
        /// <param name="service">The friends service.</param>
        /// <param name="auth">The service authentication.</param>
        public FriendsServerPostHandler(IFriendsService service, IServiceAuth auth) :
                base("POST", "/friends", auth)
        {
            m_FriendsService = service;
        }

        /// <summary>
        /// Processes the HTTP POST request for friends operations.
        /// </summary>
        /// <param name="path">The request path.</param>
        /// <param name="requestData">The request data stream.</param>
        /// <param name="httpRequest">The HTTP request.</param>
        /// <param name="httpResponse">The HTTP response.</param>
        /// <returns>The response bytes.</returns>
        protected override byte[] ProcessRequest(string path, Stream requestData,
                IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            string body;
            using(StreamReader sr = new(requestData))
                body = sr.ReadToEnd();
            body = body.Trim();

            //m_log.DebugFormat("[XXX]: query String: {0}", body);

            try
            {
                Dictionary<string, object> request =
                        ServerUtils.ParseQueryString(body);

                if (!request.ContainsKey("METHOD"))
                    return FailureResult();

                string method = request["METHOD"].ToString();

                switch (method)
                {
                    case "getfriends":
                        return GetFriends(request);

                    case "getfriends_string":
                        return GetFriendsString(request);

                    case "storefriend":
                        return StoreFriend(request);

                    case "deletefriend":
                        return DeleteFriend(request);

                    case "deletefriend_string":
                        return DeleteFriendString(request);

                }

                m_log.DebugFormat("[FRIENDS HANDLER]: unknown method request {0}", method);
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[FRIENDS HANDLER]: Exception {0}", e);
            }

            return FailureResult();
        }

        #region Method-specific handlers

        /// <summary>
        /// Gets the friends for the specified principal ID.
        /// </summary>
        /// <param name="request">The request dictionary.</param>
        /// <returns>The response bytes.</returns>
        byte[] GetFriends(Dictionary<string, object> request)
        {
            UUID principalID = UUID.Zero;
            if (request.TryGetValue("PRINCIPALID", out object principalIdValue))
                UUID.TryParse(principalIdValue.ToString(), out principalID);
            else
                m_log.WarnFormat("[FRIENDS HANDLER]: no principalID in request to get friends");

            FriendInfo[] finfos = m_FriendsService.GetFriends(principalID);

            return PackageFriends(finfos);
        }

        /// <summary>
        /// Gets the friends as a string for the specified principal ID.
        /// </summary>
        /// <param name="request">The request dictionary.</param>
        /// <returns>The response bytes.</returns>
        byte[] GetFriendsString(Dictionary<string, object> request)
        {
            string principalID = string.Empty;
            if (request.TryGetValue("PRINCIPALID", out object principalIdValue))
                principalID = principalIdValue.ToString();
            else
                m_log.WarnFormat("[FRIENDS HANDLER]: no principalID in request to get friends");

            FriendInfo[] finfos = m_FriendsService.GetFriends(principalID);

            return PackageFriends(finfos);
        }

        /// <summary>
        /// Packages the friend information into an XML response.
        /// </summary>
        /// <param name="finfos">The friend information array.</param>
        /// <returns>The response bytes.</returns>
        private byte[] PackageFriends(FriendInfo[] finfos)
        {

            Dictionary<string, object> result = [];
            if (finfos == null || finfos.Length == 0)
                result["result"] = "null";
            else
            {
                int i = 0;
                foreach (FriendInfo finfo in finfos)
                {
                    Dictionary<string, object> rinfoDict = finfo.ToKeyValuePairs();
                    result["friend" + i] = rinfoDict;
                    i++;
                }
            }

            string xmlString = ServerUtils.BuildXmlResponse(result);

            //m_log.DebugFormat("[FRIENDS HANDLER]: resp string: {0}", xmlString);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }

        /// <summary>
        /// Stores a friend relationship.
        /// </summary>
        /// <param name="request">The request dictionary.</param>
        /// <returns>The response bytes.</returns>
        byte[] StoreFriend(Dictionary<string, object> request)
        {
            string principalID = string.Empty, friend = string.Empty; int flags = 0;
            FromKeyValuePairs(request, out principalID, out friend, out flags);
            bool success = m_FriendsService.StoreFriend(principalID, friend, flags);

            return success ? SuccessResult() : FailureResult();
        }

        /// <summary>
        /// Deletes a friend relationship.
        /// </summary>
        /// <param name="request">The request dictionary.</param>
        /// <returns>The response bytes.</returns>
        byte[] DeleteFriend(Dictionary<string, object> request)
        {
            UUID principalID = UUID.Zero;
            if (request.TryGetValue("PRINCIPALID", out object principalIdValue))
                UUID.TryParse(principalIdValue.ToString(), out principalID);
            else
                m_log.WarnFormat("[FRIENDS HANDLER]: no principalID in request to delete friend");
            string friend = string.Empty;
            if (request.TryGetValue("FRIEND", out object friendValue))
                friend = friendValue.ToString();

            bool success = m_FriendsService.Delete(principalID, friend);
            if (success)
                return SuccessResult();
            else
                return FailureResult();
        }

        /// <summary>
        /// Deletes a friend relationship using string principal ID.
        /// </summary>
        /// <param name="request">The request dictionary.</param>
        /// <returns>The response bytes.</returns>
        byte[] DeleteFriendString(Dictionary<string, object> request)
        {
            string principalID = string.Empty;
            if (request.TryGetValue("PRINCIPALID", out object principalIdValue))
                principalID = principalIdValue.ToString();
            else
                m_log.WarnFormat("[FRIENDS HANDLER]: no principalID in request to delete friend");
            string friend = string.Empty;
            if (request.TryGetValue("FRIEND", out object friendValue))
                friend = friendValue.ToString();

            bool success = m_FriendsService.Delete(principalID, friend);
            if (success)
                return SuccessResult();
            else
                return FailureResult();
        }

        #endregion

        #region Misc

        /// <summary>
        /// Creates a success XML response.
        /// </summary>
        /// <returns>The response bytes.</returns>
        private byte[] SuccessResult()
        {
            XmlDocument doc = new();

            XmlNode xmlnode = doc.CreateNode(XmlNodeType.XmlDeclaration,
                    "", "");

            doc.AppendChild(xmlnode);

            XmlElement rootElement = doc.CreateElement("", "ServerResponse",
                    "");

            doc.AppendChild(rootElement);

            XmlElement result = doc.CreateElement("", "Result", "");
            result.AppendChild(doc.CreateTextNode("Success"));

            rootElement.AppendChild(result);

            return Util.DocToBytes(doc);
        }

        /// <summary>
        /// Creates a failure XML response.
        /// </summary>
        /// <returns>The response bytes.</returns>
        private byte[] FailureResult()
        {
            return FailureResult(string.Empty);
        }

        /// <summary>
        /// Creates a failure XML response with a message.
        /// </summary>
        /// <param name="msg">The failure message.</param>
        /// <returns>The response bytes.</returns>
        private byte[] FailureResult(string msg)
        {
            XmlDocument doc = new();

            XmlNode xmlnode = doc.CreateNode(XmlNodeType.XmlDeclaration,
                    "", "");

            doc.AppendChild(xmlnode);

            XmlElement rootElement = doc.CreateElement("", "ServerResponse",
                    "");

            doc.AppendChild(rootElement);

            XmlElement result = doc.CreateElement("", "Result", "");
            result.AppendChild(doc.CreateTextNode("Failure"));

            rootElement.AppendChild(result);

            XmlElement message = doc.CreateElement("", "Message", "");
            message.AppendChild(doc.CreateTextNode(msg));

            rootElement.AppendChild(message);

            return Util.DocToBytes(doc);
        }

        /// <summary>
        /// Extracts principal ID, friend, and flags from the key-value pairs.
        /// </summary>
        /// <param name="kvp">The key-value pairs dictionary.</param>
        /// <param name="principalID">The output principal ID.</param>
        /// <param name="friend">The output friend ID.</param>
        /// <param name="flags">The output flags.</param>
        void FromKeyValuePairs(Dictionary<string, object> kvp, out string principalID, out string friend, out int flags)
        {
            principalID = string.Empty;
            if (kvp.TryGetValue("PrincipalID", out object principalIdValue) && principalIdValue != null)
                principalID = principalIdValue.ToString();
            friend = string.Empty;
            if (kvp.TryGetValue("Friend", out object friendValue) && friendValue != null)
                friend = friendValue.ToString();
            flags = 0;
            if (kvp.TryGetValue("MyFlags", out object flagsValue) && flagsValue != null)
                int.TryParse(flagsValue.ToString(), out flags);
        }

        #endregion
    }
}
</think>
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

using log4net;
using System;
using System.Reflection;
using System.IO;
using System.Xml;
using System.Collections.Generic;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using FriendInfo = OpenSim.Services.Interfaces.FriendInfo;
using OpenSim.Framework;
using OpenSim.Framework.ServiceAuth;
using OpenSim.Framework.Servers.HttpServer;
using OpenMetaverse;

namespace OpenSim.Server.Handlers.Friends
{
    /// <summary>
    /// Handles HTTP POST requests for friends-related operations.
    /// </summary>
    public class FriendsServerPostHandler : BaseStreamHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly IFriendsService m_FriendsService;

        /// <summary>
        /// Initializes a new instance of the FriendsServerPostHandler class.
        /// </summary>
        /// <param name="service">The friends service.</param>
        /// <param name="auth">The service authentication.</param>
        public FriendsServerPostHandler(IFriendsService service, IServiceAuth auth) :
                base("POST", "/friends", auth)
        {
            m_FriendsService = service;
        }

        /// <summary>
        /// Processes the HTTP POST request for friends operations.
        /// </summary>
        /// <param name="path">The request path.</param>
        /// <param name="requestData">The request data stream.</param>
        /// <param name="httpRequest">The HTTP request.</param>
        /// <param name="httpResponse">The HTTP response.</param>
        /// <returns>The response bytes.</returns>
        protected override byte[] ProcessRequest(string path, Stream requestData,
                IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            string body;
            using(StreamReader sr = new(requestData))
                body = sr.ReadToEnd();
            body = body.Trim();

            //m_log.DebugFormat("[XXX]: query String: {0}", body);

            try
            {
                Dictionary<string, object> request =
                        ServerUtils.ParseQueryString(body);

                if (!request.ContainsKey("METHOD"))
                    return FailureResult();

                string method = request["METHOD"].ToString();

                switch (method)
                {
                    case "getfriends":
                        return GetFriends(request);

                    case "getfriends_string":
                        return GetFriendsString(request);

                    case "storefriend":
                        return StoreFriend(request);

                    case "deletefriend":
                        return DeleteFriend(request);

                    case "deletefriend_string":
                        return DeleteFriendString(request);

                }

                m_log.DebugFormat("[FRIENDS HANDLER]: unknown method request {0}", method);
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[FRIENDS HANDLER]: Exception {0}", e);
            }

            return FailureResult();
        }

        #region Method-specific handlers

        /// <summary>
        /// Gets the friends for the specified principal ID.
        /// </summary>
        /// <param name="request">The request dictionary.</param>
        /// <returns>The response bytes.</returns>
        byte[] GetFriends(Dictionary<string, object> request)
        {
            UUID principalID = UUID.Zero;
            if (request.TryGetValue("PRINCIPALID", out object principalIdValue))
                UUID.TryParse(principalIdValue.ToString(), out principalID);
            else
                m_log.WarnFormat("[FRIENDS HANDLER]: no principalID in request to get friends");

            FriendInfo[] finfos = m_FriendsService.GetFriends(principalID);

            return PackageFriends(finfos);
        }

        /// <summary>
        /// Gets the friends as a string for the specified principal ID.
        /// </summary>
        /// <param name="request">The request dictionary.</param>
        /// <returns>The response bytes.</returns>
        byte[] GetFriendsString(Dictionary<string, object> request)
        {
            string principalID = string.Empty;
            if (request.TryGetValue("PRINCIPALID", out object principalIdValue))
                principalID = principalIdValue.ToString();
            else
                m_log.WarnFormat("[FRIENDS HANDLER]: no principalID in request to get friends");

            FriendInfo[] finfos = m_FriendsService.GetFriends(principalID);

            return PackageFriends(finfos);
        }

        /// <summary>
        /// Packages the friend information into an XML response.
        /// </summary>
        /// <param name="finfos">The friend information array.</param>
        /// <returns>The response bytes.</returns>
        private byte[] PackageFriends(FriendInfo[] finfos)
        {

            Dictionary<string, object> result = [];
            if (finfos == null || finfos.Length == 0)
                result["result"] = "null";
            else
            {
                int i = 0;
                foreach (FriendInfo finfo in finfos)
                {
                    Dictionary<string, object> rinfoDict = finfo.ToKeyValuePairs();
                    result["friend" + i] = rinfoDict;
                    i++;
                }
            }

            string xmlString = ServerUtils.BuildXmlResponse(result);

            //m_log.DebugFormat("[FRIENDS HANDLER]: resp string: {0}", xmlString);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }

        /// <summary>
        /// Stores a friend relationship.
        /// </summary>
        /// <param name="request">The request dictionary.</param>
        /// <returns>The response bytes.</returns>
        byte[] StoreFriend(Dictionary<string, object> request)
        {
            string principalID = string.Empty, friend = string.Empty; int flags = 0;
            FromKeyValuePairs(request, out principalID, out friend, out flags);
            bool success = m_FriendsService.StoreFriend(principalID, friend, flags);

            return success ? SuccessResult() : FailureResult();
        }

        /// <summary>
        /// Deletes a friend relationship.
        /// </summary>
        /// <param name="request">The request dictionary.</param>
        /// <returns>The response bytes.</returns>
        byte[] DeleteFriend(Dictionary<string, object> request)
        {
            UUID principalID = UUID.Zero;
            if (request.TryGetValue("PRINCIPALID", out object principalIdValue))
                UUID.TryParse(principalIdValue.ToString(), out principalID);
            else
                m_log.WarnFormat("[FRIENDS HANDLER]: no principalID in request to delete friend");
            string friend = string.Empty;
            if (request.TryGetValue("FRIEND", out object friendValue))
                friend = friendValue.ToString();

            bool success = m_FriendsService.Delete(principalID, friend);
            if (success)
                return SuccessResult();
            else
                return FailureResult();
        }

        /// <summary>
        /// Deletes a friend relationship using string principal ID.
        /// </summary>
        /// <param name="request">The request dictionary.</param>
        /// <returns>The response bytes.</returns>
        byte[] DeleteFriendString(Dictionary<string, object> request)
        {
            string principalID = string.Empty;
            if (request.TryGetValue("PRINCIPALID", out object principalIdValue))
                principalID = principalIdValue.ToString();
            else
                m_log.WarnFormat("[FRIENDS HANDLER]: no principalID in request to delete friend");
            string friend = string.Empty;
            if (request.TryGetValue("FRIEND", out object friendValue))
                friend = friendValue.ToString();

            bool success = m_FriendsService.Delete(principalID, friend);
            if (success)
                return SuccessResult();
            else
                return FailureResult();
        }

        #endregion

        #region Misc

        /// <summary>
        /// Creates a success XML response.
        /// </summary>
        /// <returns>The response bytes.</returns>
        private byte[] SuccessResult()
        {
            XmlDocument doc = new();

            XmlNode xmlnode = doc.CreateNode(XmlNodeType.XmlDeclaration,
                    "", "");

            doc.AppendChild(xmlnode);

            XmlElement rootElement = doc.CreateElement("", "ServerResponse",
                    "");

            doc.AppendChild(rootElement);

            XmlElement result = doc.CreateElement("", "Result", "");
            result.AppendChild(doc.CreateTextNode("Success"));

            rootElement.AppendChild(result);

            return Util.DocToBytes(doc);
        }

        /// <summary>
        /// Creates a failure XML response.
        /// </summary>
        /// <returns>The response bytes.</returns>
        private byte[] FailureResult()
        {
            return FailureResult(string.Empty);
        }

        /// <summary>
        /// Creates a failure XML response with a message.
        /// </summary>
        /// <param name="msg">The failure message.</param>
        /// <returns>The response bytes.</returns>
        private byte[] FailureResult(string msg)
        {
            XmlDocument doc = new();

            XmlNode xmlnode = doc.CreateNode(XmlNodeType.XmlDeclaration,
                    "", "");

            doc.AppendChild(xmlnode);

            XmlElement rootElement = doc.CreateElement("", "ServerResponse",
                    "");

            doc.AppendChild(rootElement);

            XmlElement result = doc.CreateElement("", "Result", "");
            result.AppendChild(doc.CreateTextNode("Failure"));

            rootElement.AppendChild(result);

            XmlElement message = doc.CreateElement("", "Message", "");
            message.AppendChild(doc.CreateTextNode(msg));

            rootElement.AppendChild(message);

            return Util.DocToBytes(doc);
        }

        /// <summary>
        /// Extracts principal ID, friend, and flags from the key-value pairs.
        /// </summary>
        /// <param name="kvp">The key-value pairs dictionary.</param>
        /// <param name="principalID">The output principal ID.</param>
        /// <param name="friend">The output friend ID.</param>
        /// <param name="flags">The output flags.</param>
        void FromKeyValuePairs(Dictionary<string, object> kvp, out string principalID, out string friend, out int flags)
        {
            principalID = string.Empty;
            if (kvp.TryGetValue("PrincipalID", out object principalIdValue) && principalIdValue != null)
                principalID = principalIdValue.ToString();
            friend = string.Empty;
            if (kvp.TryGetValue("Friend", out object friendValue) && friendValue != null)
                friend = friendValue.ToString();
            flags = 0;
            if (kvp.TryGetValue("MyFlags", out object flagsValue) && flagsValue != null)
                int.TryParse(flagsValue.ToString(), out flags);
        }

        #endregion
    }
}