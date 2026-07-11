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
using System.Collections;
using System.Reflection;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Services.Interfaces;
using Caps = OpenSim.Framework.Capabilities.Caps;

namespace OpenSim.Capabilities.Handlers
{
    /// <summary>
    /// Handles mesh retrieval requests by fetching mesh assets from the asset service.
    /// Supports both full mesh retrieval and HTTP range requests for partial content.
    /// </summary>
    public class GetMeshHandler
    {
        private static readonly ILog m_log =
                   LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly IAssetService m_assetService;

        /// <summary>
        /// Initializes a new instance of the <see cref="GetMeshHandler"/> class with the specified asset service.
        /// </summary>
        /// <param name="assService">The asset service used to retrieve mesh assets.</param>
        public GetMeshHandler(IAssetService assService)
        {
            m_assetService = assService;
        }

        /// <summary>
        /// Handles a basic mesh retrieval request without agent context.
        /// </summary>
        /// <param name="request">The request hashtable containing mesh_id and headers.</param>
        /// <returns>A hashtable containing the response data including status code and mesh content.</returns>
        public Hashtable Handle(Hashtable request)
        {
            return ProcessGetMesh(request, UUID.Zero, null);
        }

        /// <summary>
        /// Processes a mesh retrieval request with agent context and capabilities.
        /// </summary>
        /// <param name="request">The request hashtable containing mesh_id and headers.</param>
        /// <param name="AgentId">The UUID of the agent making the request.</param>
        /// <param name="cap">The capabilities associated with this request.</param>
        /// <returns>A hashtable containing the response data including status code, mesh content, and optional headers.</returns>
        public Hashtable ProcessGetMesh(Hashtable request, UUID AgentId, Caps cap)
        {
            Hashtable responsedata = [];
            if (m_assetService == null)
            {
                responsedata["int_response_code"] = (int)System.Net.HttpStatusCode.ServiceUnavailable;
                responsedata["str_response_string"] = "The asset service is unavailable";
                responsedata["keepalive"] = false;
                return responsedata;
            }

            responsedata["int_response_code"] = (int)System.Net.HttpStatusCode.BadRequest;
            responsedata["content_type"] = "text/plain";
            responsedata["int_bytes"] = 0;

            string meshStr = string.Empty;
            if (request.ContainsKey("mesh_id"))
                meshStr = request["mesh_id"].ToString();

            if (string.IsNullOrEmpty(meshStr))
                return responsedata;

            UUID meshID = UUID.Zero;
            if (!UUID.TryParse(meshStr, out meshID))
                return responsedata;

            AssetBase mesh = m_assetService.Get(meshID.ToString());
            if (mesh == null)
            {
                responsedata["int_response_code"] = (int)System.Net.HttpStatusCode.NotFound;
                responsedata["str_response_string"] = "Mesh not found.";
                return responsedata;
            }

            if (mesh.Type != (sbyte)AssetType.Mesh)
            {
                responsedata["str_response_string"] = "Asset isn't a mesh.";
                return responsedata;
            }

            string range = string.Empty;

            Hashtable headers = request["headers"] as Hashtable;
            if (headers != null)
            {
                if (headers["range"] != null)
                    range = headers["range"].ToString();
                else if (headers["Range"] != null)
                    range = headers["Range"].ToString();
            }

            responsedata["content_type"] = "application/vnd.ll.mesh";
            if (string.IsNullOrEmpty(range))
            {
                // full mesh
                responsedata["str_response_string"] = Convert.ToBase64String(mesh.Data);
                responsedata["int_response_code"] = (int)System.Net.HttpStatusCode.OK;
                return responsedata;
            }

            // range request
            int start, end;
            if (Util.TryParseHttpRange(range, out start, out end))
            {
                // Before clamping start make sure we can satisfy it in order to avoid
                // sending back the last byte instead of an error status
                if (start >= mesh.Data.Length)
                {
                    responsedata["str_response_string"] = "This range doesnt exist.";
                    return responsedata;
                }

                end = Utils.Clamp(end, 0, mesh.Data.Length - 1);
                start = Utils.Clamp(start, 0, end);
                int len = end - start + 1;

                //m_log.Debug("Serving " + start + " to " + end + " of " + texture.Data.Length + " bytes for texture " + texture.ID);
                Hashtable responseHeaders = new()
                {
                    ["Content-Range"] = string.Format("bytes {0}-{1}/{2}", start, end, mesh.Data.Length)
                };
                responsedata["headers"] = responseHeaders;
                responsedata["int_response_code"] = (int)System.Net.HttpStatusCode.PartialContent;

                byte[] d = new byte[len];
                Array.Copy(mesh.Data, start, d, 0, len);
                responsedata["bin_response_data"] = d;
                responsedata["int_bytes"] = len;
                return responsedata;
            }

            m_log.Warn("[GETMESH]: Failed to parse a range from GetMesh request, sending full asset: " + (string)request["uri"]);
            responsedata["str_response_string"] = Convert.ToBase64String(mesh.Data);
            responsedata["int_response_code"] = (int)System.Net.HttpStatusCode.OK;
            return responsedata;
        }
    }
}