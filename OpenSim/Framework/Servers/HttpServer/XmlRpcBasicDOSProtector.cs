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

using System.Net;
using Nwc.XmlRpc;


namespace OpenSim.Framework.Servers.HttpServer
{
    /// <summary>
    /// Provides DOS protection for XML-RPC requests by managing normal and throttled method processing.
    /// </summary>
    public class XmlRpcBasicDOSProtector
    {
        private readonly XmlRpcMethod _normalMethod;
        private readonly XmlRpcMethod _throttledMethod;

        private readonly BasicDosProtectorOptions _options;
        private readonly BasicDOSProtector _dosProtector;

        /// <summary>
        /// Initializes a new instance of the <see cref="XmlRpcBasicDOSProtector"/> class.
        /// </summary>
        /// <param name="normalMethod">The method to call when the request is not throttled.</param>
        /// <param name="throttledMethod">The method to call when the request is throttled.</param>
        /// <param name="options">The DOS protector options configuration.</param>
        public XmlRpcBasicDOSProtector(XmlRpcMethod normalMethod, XmlRpcMethod throttledMethod, BasicDosProtectorOptions options)
        {
            _normalMethod = normalMethod;
            _throttledMethod = throttledMethod;

            _options = options;
            _dosProtector = new BasicDOSProtector(_options);

        }

        /// <summary>
        /// Processes an XML-RPC request with DOS protection.
        /// </summary>
        /// <param name="request">The XML-RPC request to process.</param>
        /// <param name="client">The client endpoint making the request.</param>
        /// <returns>The XML-RPC response from either the normal or throttled method.</returns>
        public XmlRpcResponse Process(XmlRpcRequest request, IPEndPoint client)
        {

            XmlRpcResponse resp = null;
            string clientstring = GetClientString(request, client);
            string endpoint = GetEndPoint(request, client);
            if (_dosProtector.Process(clientstring, endpoint))
                resp = _normalMethod(request, client);
            else
                resp = _throttledMethod(request, client);
            if (_options.MaxConcurrentSessions > 0)
                _dosProtector.ProcessEnd(clientstring, endpoint);
            return resp;
        }

        /// <summary>
        /// Gets the client string identifier for DOS protection purposes.
        /// </summary>
        /// <param name="request">The XML-RPC request.</param>
        /// <param name="client">The client endpoint.</param>
        /// <returns>A string identifying the client for DOS protection.</returns>
        private string GetClientString(XmlRpcRequest request, IPEndPoint client)
        {
            string clientstring;
            if (_options.AllowXForwardedFor && request.Params.Count > 3)
            {
                object headerstr = request.Params[3];
                clientstring = (headerstr != null && !string.IsNullOrEmpty(headerstr.ToString()))
                    ? request.Params[3].ToString()
                    : client.Address.ToString();
            }
            else
                clientstring = client.Address.ToString();
            return clientstring;
        }

        /// <summary>
        /// Gets the endpoint string for DOS protection purposes.
        /// </summary>
        /// <param name="request">The XML-RPC request.</param>
        /// <param name="client">The client endpoint.</param>
        /// <returns>A string identifying the endpoint for DOS protection.</returns>
        private string GetEndPoint(XmlRpcRequest request, IPEndPoint client)
        {
            return client.Address.ToString();
        }

    }


}