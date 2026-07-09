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
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Server.Base;
using OpenSim.Server.Handlers.Base;
using OpenSim.Services.Interfaces;


namespace OpenSim.Region.CoreModules.ServiceConnectorsIn.Neighbour
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "NeighbourServiceInConnectorModule")]
    public class NeighbourServiceInConnectorModule : ISharedRegionModule, INeighbourService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private bool m_Enabled = false;
        private bool m_Registered = false;

        private IConfigSource m_Config;
        private readonly List<Scene> m_Scenes = new List<Scene>();

        #region Region Module interface

        /// <summary>
        /// Initialises the module with the given configuration.
        /// </summary>
        /// <param name="config">The configuration source.</param>
        public void Initialise(IConfigSource config)
        {
            m_Config = config;

            IConfig moduleConfig = config.Configs["Modules"];
            if (moduleConfig != null)
            {
                m_Enabled = moduleConfig.GetBoolean("NeighbourServiceInConnector", false);
                if (m_Enabled)
                {
                    m_log.Info("[NEIGHBOUR IN CONNECTOR]: NeighbourServiceInConnector enabled");
                }

            }

        }

        /// <summary>
        /// Performs post-initialisation tasks.
        /// </summary>
        public void PostInitialise()
        {
            if (!m_Enabled)
                return;

//            m_log.Info("[NEIGHBOUR IN CONNECTOR]: Starting...");
        }

        /// <summary>
        /// Closes the module and releases resources.
        /// </summary>
        public void Close()
        {
        }

        /// <summary>
        /// Gets the type of interface this module replaces.
        /// </summary>
        public Type ReplaceableInterface
        {
            get { return null; }
        }

        /// <summary>
        /// Gets the name of this module.
        /// </summary>
        public string Name
        {
            get { return "NeighbourServiceInConnectorModule"; }
        }

        /// <summary>
        /// Adds a region to the module.
        /// </summary>
        /// <param name="scene">The scene to add.</param>
        public void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            if (!m_Registered)
            {
                m_Registered = true;
                object[] args = new object[] { m_Config, MainServer.Instance, this, scene };
                ServerUtils.LoadPlugin<IServiceConnector>("OpenSim.Server.Handlers.dll:NeighbourServiceInConnector", args);
            }

            m_Scenes.Add(scene);

        }

        /// <summary>
        /// Removes a region from the module.
        /// </summary>
        /// <param name="scene">The scene to remove.</param>
        public void RemoveRegion(Scene scene)
        {
            if (m_Enabled && m_Scenes.Contains(scene))
                m_Scenes.Remove(scene);
        }

        /// <summary>
        /// Called when the region is loaded.
        /// </summary>
        /// <param name="scene">The loaded scene.</param>
        public void RegionLoaded(Scene scene)
        {
        }

        #endregion

        #region INeighbourService

        /// <summary>
        /// Handles the HelloNeighbour request from a neighboring region.
        /// </summary>
        /// <param name="regionHandle">The handle of the region being greeted.</param>
        /// <param name="thisRegion">Information about the requesting region.</param>
        /// <returns>GridRegion information for the greeting region, or null if not found.</returns>
        public GridRegion HelloNeighbour(ulong regionHandle, RegionInfo thisRegion)
        {
            foreach (Scene s in m_Scenes.Where(s => s.RegionInfo.RegionHandle == regionHandle))
            {
                //m_log.DebugFormat("[NEIGHBOUR IN CONNECTOR]: HelloNeighbour from {0} to {1}", thisRegion.RegionName, s.RegionInfo.RegionName);
                return s.IncomingHelloNeighbour(thisRegion);
            }
            return null;
        }

        #endregion INeighbourService
    }
}