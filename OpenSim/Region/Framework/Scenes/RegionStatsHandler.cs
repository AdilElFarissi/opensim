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
using System.IO;
using System.Net;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;

namespace OpenSim.Region.Framework.Scenes
{
    /// <summary>
    /// Handler for providing region statistics in a simple stream format.
    /// </summary>
    public class RegionStatsSimpleHandler : SimpleStreamHandler
    {
        //private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// URI for OSStats endpoint.
        /// </summary>
        private readonly string osXStatsURI = string.Empty;
        //private string osSecret = String.Empty;
        /// <summary>
        /// Region information for the handler.
        /// </summary>
        private readonly OpenSim.Framework.RegionInfo regionInfo;
        /// <summary>
        /// Local time zone name.
        /// </summary>
        public string localZone = TimeZoneInfo.Local.StandardName;
        /// <summary>
        /// UTC offset for local time zone.
        /// </summary>
        public TimeSpan utcOffset = TimeZoneInfo.Local.GetUtcOffset(DateTime.Now);

        /// <summary>
        /// Initializes a new instance of the RegionStatsSimpleHandler class.
        /// </summary>
        /// <param name="region_info">Region information to use for statistics.</param>
        public RegionStatsSimpleHandler(RegionInfo region_info) : base("/" + Util.SHA1Hash(region_info.regionSecret))
        {
            regionInfo = region_info;
            osXStatsURI = Util.SHA1Hash(regionInfo.osSecret);
        }

        /// <summary>
        /// Processes the HTTP request and writes the response.
        /// </summary>
        /// <param name="httpRequest">Incoming HTTP request.</param>
        /// <param name="httpResponse">HTTP response to write to.</param>
        protected override void ProcessRequest(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            if (regionInfo == null)
            {
                httpResponse.StatusCode = (int)HttpStatusCode.NotImplemented;
                return;
            }

            if (httpRequest.HttpMethod != "GET")
            {
                httpResponse.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }
            httpResponse.RawBuffer = Util.UTF8.GetBytes(Report());
        }

        /// <summary>
        /// Generates the statistics report as a JSON string.
        /// </summary>
        /// <returns>JSON formatted statistics report.</returns>
        private string Report()
        {
            OSDMap args = new(30)
            {
                //int time = Util.ToUnixTime(DateTime.Now);
                ["OSStatsURI"] = OSD.FromString("http://" + regionInfo.ExternalHostName + ":" + regionInfo.HttpPort + "/" + osXStatsURI + "/"),
                ["TimeZoneName"] = OSD.FromString(localZone),
                ["TimeZoneOffs"] = OSD.FromReal(utcOffset.TotalHours),
                ["UxTime"] = OSD.FromInteger(Util.ToUnixTime(DateTime.Now)),
                ["Memory"] = OSD.FromReal(Math.Round(GC.GetTotalMemory(false) / 1024.0 / 1024.0)),
                ["Version"] = OSD.FromString(VersionInfo.Version)
            };

            string strBuffer = "";
            strBuffer = OSDParser.SerializeJsonString(args);

            return strBuffer;
         }
    }

    /// <summary>
    /// Legacy handler for providing region statistics. This will be removed in future versions.
    /// </summary>
    // legacy do not use. This will removed in future
    public class RegionStatsHandler : BaseStreamHandler
    {
        //private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// URI for OSStats endpoint.
        /// </summary>
        private readonly string osXStatsURI = string.Empty;
        //private string osSecret = String.Empty;
        /// <summary>
        /// Region information for the handler.
        /// </summary>
        private readonly OpenSim.Framework.RegionInfo regionInfo;
        /// <summary>
        /// Local time zone name.
        /// </summary>
        public string localZone = TimeZoneInfo.Local.StandardName;
        /// <summary>
        /// UTC offset for local time zone.
        /// </summary>
        public TimeSpan utcOffset = TimeZoneInfo.Local.GetUtcOffset(DateTime.Now);

        /// <summary>
        /// Initializes a new instance of the RegionStatsHandler class.
        /// </summary>
        /// <param name="region_info">Region information to use for statistics.</param>
        public RegionStatsHandler(RegionInfo region_info)
            : base("GET", "/" + Util.SHA1Hash(region_info.regionSecret), "RegionStats", "Region Statistics")
        {
            regionInfo = region_info;
            osXStatsURI = Util.SHA1Hash(regionInfo.osSecret);
        }

        /// <summary>
        /// Processes the HTTP request and returns the response bytes.
        /// </summary>
        /// <param name="path">Request path.</param>
        /// <param name="request">Request stream.</param>
        /// <param name="httpRequest">Incoming HTTP request.</param>
        /// <param name="httpResponse">HTTP response to write to.</param>
        /// <returns>Response bytes.</returns>
        protected override byte[] ProcessRequest(
            string path, Stream request, IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            return Util.UTF8.GetBytes(Report());
        }

        /// <summary>
        /// Gets the content type for the response.
        /// </summary>
        public override string ContentType
        {
            get { return "text/plain"; }
        }

        /// <summary>
        /// Generates the statistics report as a JSON string.
        /// </summary>
        /// <returns>JSON formatted statistics report.</returns>
        private string Report()
        {
            OSDMap args = new(30)
            {
                //int time = Util.ToUnixTime(DateTime.Now);
                ["OSStatsURI"] = OSD.FromString("http://" + regionInfo.ExternalHostName + ":" + regionInfo.HttpPort + "/" + osXStatsURI + "/"),
                ["TimeZoneName"] = OSD.FromString(localZone),
                ["TimeZoneOffs"] = OSD.FromReal(utcOffset.TotalHours),
                ["UxTime"] = OSD.FromInteger(Util.ToUnixTime(DateTime.Now)),
                ["Memory"] = OSD.FromReal(Math.Round(GC.GetTotalMemory(false) / 1024.0 / 1024.0)),
                ["Version"] = OSD.FromString(VersionInfo.Version)
            };

            string strBuffer = "";
            strBuffer = OSDParser.SerializeJsonString(args);

            return strBuffer;
        }
    }
}