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

using System.Collections.Generic;
using System.Runtime.CompilerServices;

using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.World.Land
{
    /// <summary>
    /// Implementation of the land channel interface for managing land parcels in a scene.
    /// </summary>
    public class LandChannel : ILandChannel
    {
        #region Constants

        //Land types set with flags in ParcelOverlay.
        //Only one of these can be used.

        //RequestResults (I think these are right, they seem to work):
        public const int LAND_RESULT_MULTIPLE = 1; // The request they made contained more than a single peice of land
        public const int LAND_RESULT_SINGLE = 0; // The request they made contained only a single piece of land

        //ParcelSelectObjects
        public const int LAND_SELECT_OBJECTS_OWNER = 2;
        public const int LAND_SELECT_OBJECTS_GROUP = 4;
        public const int LAND_SELECT_OBJECTS_OTHER = 8;


        public const byte LAND_TYPE_PUBLIC = 0; //Equals 00000000
        // types 1 to 7 are exclusive
        public const byte LAND_TYPE_OWNED_BY_OTHER = 1; //Equals 00000001
        public const byte LAND_TYPE_OWNED_BY_GROUP = 2; //Equals 00000010
        public const byte LAND_TYPE_OWNED_BY_REQUESTER = 3; //Equals 00000011
        public const byte LAND_TYPE_IS_FOR_SALE = 4; //Equals 00000100
        public const byte LAND_TYPE_IS_BEING_AUCTIONED = 5; //Equals 00000101
        public const byte LAND_TYPE_unused6 = 6;
        public const byte LAND_TYPE_unused7 = 7;
        // next are flags
        public const byte LAND_FLAG_unused8 = 0x08; // this may become excluside in future
        public const byte LAND_FLAG_HIDEAVATARS = 0x10;
        public const byte LAND_FLAG_LOCALSOUND = 0x20;
        public const byte LAND_FLAG_PROPERTY_BORDER_WEST = 0x40; //Equals 01000000
        public const byte LAND_FLAG_PROPERTY_BORDER_SOUTH = 0x80; //Equals 10000000


        //These are other constants. Yay!
        public const int START_LAND_LOCAL_ID = 1;

        #endregion

        private readonly ILandManagementModule m_landManagementModule;

        private float m_BanLineSafeHeight = 100.0f;
        /// <summary>
        /// Gets the safe height for the ban line.
        /// </summary>
        public float BanLineSafeHeight
        {
            get
            {
                return m_BanLineSafeHeight;
            }
            private set
            {
                m_BanLineSafeHeight = (value >= 20f && value <= 5000f) ? value : 100.0f;
            }
        }

        /// <summary>
        /// Initializes a new instance of the LandChannel class.
        /// </summary>
        /// <param name="scene">The scene associated with this land channel.</param>
        /// <param name="landManagementMod">The land management module for handling land operations.</param>
        public LandChannel(Scene scene, ILandManagementModule landManagementMod)
        {
            m_landManagementModule = landManagementMod;
            if(landManagementMod is not null)
                m_BanLineSafeHeight = landManagementMod.BanLineSafeHeight;
        }

        #region ILandChannel Members
        /// <summary>
        /// Gets the land object at the specified coordinates.
        /// </summary>
        /// <param name="x_float">The x coordinate as a float.</param>
        /// <param name="y_float">The y coordinate as a float.</param>
        /// <returns>The land object at the specified location, or null if not found.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ILandObject GetLandObject(float x_float, float y_float)
        {
            return m_landManagementModule?.GetLandObject(x_float, y_float);
        }

        /// <summary>
        /// Gets the land object with the specified local ID.
        /// </summary>
        /// <param name="localID">The local ID of the land object.</param>
        /// <returns>The land object with the specified ID, or null if not found.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ILandObject GetLandObject(int localID)
        {
            return m_landManagementModule?.GetLandObject(localID);
        }

        /// <summary>
        /// Gets the land object with the specified global ID.
        /// </summary>
        /// <param name="GlobalID">The global UUID of the land object.</param>
        /// <returns>The land object with the specified ID, or null if not found.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ILandObject GetLandObject(UUID GlobalID)
        {
            return m_landManagementModule?.GetLandObject(GlobalID);
        }

        /// <summary>
        /// Gets the land object at the specified position.
        /// </summary>
        /// <param name="position">The 3D position of the land object.</param>
        /// <returns>The land object at the specified position, or null if not found.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ILandObject GetLandObject(Vector3 position)
        {
            return GetLandObject(position.X, position.Y);
        }

        /// <summary>
        /// Gets the land object at the specified integer coordinates.
        /// </summary>
        /// <param name="x">The x coordinate.</param>
        /// <param name="y">The y coordinate.</param>
        /// <returns>The land object at the specified location, or null if not found.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ILandObject GetLandObject(int x, int y)
        {
            return m_landManagementModule?.GetLandObject(x, y);
        }

        /// <summary>
        /// Gets the land object clipped to the XY coordinates.
        /// </summary>
        /// <param name="x">The x coordinate.</param>
        /// <param name="y">The y coordinate.</param>
        /// <returns>The land object at the clipped coordinates, or null if not found.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ILandObject GetLandObjectClippedXY(float x, float y)
        {
            return m_landManagementModule?.GetLandObjectClippedXY(x, y);
        }

        /// <summary>
        /// Gets all parcels in the scene.
        /// </summary>
        /// <returns>A list of all parcels.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public List<ILandObject> AllParcels()
        {
            return m_landManagementModule is not null ? m_landManagementModule.AllParcels() : [];
        }

        /// <summary>
        /// Clears all land data.
        /// </summary>
        /// <param name="setupDefaultParcel">Whether to set up a default parcel after clearing.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear(bool setupDefaultParcel)
        {
             m_landManagementModule?.Clear(setupDefaultParcel);
        }

        /// <summary>
        /// Gets parcels near the specified position.
        /// </summary>
        /// <param name="position">The position to search near.</param>
        /// <returns>A list of parcels near the specified position.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public List<ILandObject> ParcelsNearPoint(Vector3 position)
        {
            return m_landManagementModule is not null ? m_landManagementModule.ParcelsNearPoint(position) : [];
        }

        /// <summary>
        /// Checks whether forceful bans are allowed.
        /// </summary>
        /// <returns>True if forceful bans are allowed, false otherwise.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsForcefulBansAllowed()
        {
            return m_landManagementModule is not null && m_landManagementModule.AllowedForcefulBans;
        }

        /// <summary>
        /// Updates the land object with the specified data.
        /// </summary>
        /// <param name="localID">The local ID of the land object.</param>
        /// <param name="data">The land data to update.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateLandObject(int localID, LandData data)
        {
            m_landManagementModule?.UpdateLandObject(localID, data);
        }

        /// <summary>
        /// Sends the parcels overlay to the specified client.
        /// </summary>
        /// <param name="client">The client to send the overlay to.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendParcelsOverlay(IClientAPI client)
        {
            m_landManagementModule?.SendParcelOverlay(client);
        }

        /// <summary>
        /// Joins land parcels.
        /// </summary>
        /// <param name="start_x">The starting x coordinate.</param>
        /// <param name="start_y">The starting y coordinate.</param>
        /// <param name="end_x">The ending x coordinate.</param>
        /// <param name="end_y">The ending y coordinate.</param>
        /// <param name="attempting_user_id">The UUID of the user attempting the join.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Join(int start_x, int start_y, int end_x, int end_y, UUID attempting_user_id)
        {
            m_landManagementModule?.Join(start_x, start_y, end_x, end_y, attempting_user_id);
        }

        /// <summary>
        /// Subdivides land parcels.
        /// </summary>
        /// <param name="start_x">The starting x coordinate.</param>
        /// <param name="start_y">The starting y coordinate.</param>
        /// <param name="end_x">The ending x coordinate.</param>
        /// <param name="end_y">The ending y coordinate.</param>
        /// <param name="attempting_user_id">The UUID of the user attempting the subdivision.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Subdivide(int start_x, int start_y, int end_x, int end_y, UUID attempting_user_id)
        {
            m_landManagementModule?.Subdivide(start_x, start_y, end_x, end_y, attempting_user_id);
        }

        /// <summary>
        /// Returns objects in a parcel to their owners.
        /// </summary>
        /// <param name="localID">The local ID of the parcel.</param>
        /// <param name="returnType">The type of objects to return.</param>
        /// <param name="agentIDs">The UUIDs of agents whose objects are being returned.</param>
        /// <param name="taskIDs">The UUIDs of tasks (objects) being returned.</param>
        /// <param name="remoteClient">The client making the request.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReturnObjectsInParcel(int localID, uint returnType, UUID[] agentIDs, UUID[] taskIDs, IClientAPI remoteClient)
        {
            m_landManagementModule?.ReturnObjectsInParcel(localID, returnType, agentIDs, taskIDs, remoteClient);
        }

        /// <summary>
        /// Sets the override delegate for parcel object max prim count.
        /// </summary>
        /// <param name="overrideDel">The delegate to override the max prim count.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void setParcelObjectMaxOverride(overrideParcelMaxPrimCountDelegate overrideDel)
        {
            m_landManagementModule?.setParcelObjectMaxOverride(overrideDel);
        }

        /// <summary>
        /// Sets the override delegate for simulator object max prim count.
        /// </summary>
        /// <param name="overrideDel">The delegate to override the max prim count.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void setSimulatorObjectMaxOverride(overrideSimulatorMaxPrimCountDelegate overrideDel)
        {
            m_landManagementModule?.setSimulatorObjectMaxOverride(overrideDel);
        }

        /// <summary>
        /// Sets the other clean time for a parcel.
        /// </summary>
        /// <param name="remoteClient">The client making the request.</param>
        /// <param name="localID">The local ID of the parcel.</param>
        /// <param name="otherCleanTime">The clean time to set.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetParcelOtherCleanTime(IClientAPI remoteClient, int localID, int otherCleanTime)
        {
            m_landManagementModule?.SetParcelOtherCleanTime(remoteClient, localID, otherCleanTime);
        }

        /// <summary>
        /// Sends the client initial land information.
        /// </summary>
        /// <param name="remoteClient">The client to send information to.</param>
        /// <param name="overlay">Whether to include overlay information.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void sendClientInitialLandInfo(IClientAPI remoteClient, bool overlay)
        {
            m_landManagementModule?.sendClientInitialLandInfo(remoteClient, overlay);
        }

        /// <summary>
        /// Clears all environments from all parcels.
        /// </summary>
        public void ClearAllEnvironments()
        {
            List<ILandObject> parcels = AllParcels();
            for(int i=0; i< parcels.Count; ++i)
                parcels[i].StoreEnvironment(null);
        }
        #endregion
    }
}