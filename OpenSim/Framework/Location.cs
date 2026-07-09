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
using OpenMetaverse;

namespace OpenSim.Framework
{
    /// <summary>
    /// Represents a location with X and Y coordinates.
    /// </summary>
    [Serializable]
    public class Location
    {
        private readonly uint m_x;
        private readonly uint m_y;

        /// <summary>
        /// Initializes a new instance of the Location class with the specified coordinates.
        /// </summary>
        /// <param name="x">The X coordinate.</param>
        /// <param name="y">The Y coordinate.</param>
        public Location(uint x, uint y)
        {
            m_x = x;
            m_y = y;
        }

        /// <summary>
        /// Initializes a new instance of the Location class from a region handle.
        /// </summary>
        /// <param name="regionHandle">The region handle.</param>
        public Location(ulong regionHandle)
        {
            m_x =  (uint)(regionHandle >> 32);
            m_y = (uint)(regionHandle & (ulong)uint.MaxValue);
        }

        /// <summary>
        /// Gets the region handle.
        /// </summary>
        public ulong RegionHandle
        {
            get { return Utils.UIntsToLong(m_x, m_y); }
        }

        /// <summary>
        /// Gets the X coordinate.
        /// </summary>
        public uint X
        {
            get { return m_x; }
        }

        /// <summary>
        /// Gets the Y coordinate.
        /// </summary>
        public uint Y
        {
            get { return m_y; }
        }

        /// <summary>
        /// Determines whether the specified object is equal to this instance.
        /// </summary>
        /// <param name="obj">The object to compare with this instance.</param>
        /// <returns>true if the specified object is equal to this instance; otherwise, false.</returns>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(obj, this))
                return true;

            if (obj == null || obj.GetType() != GetType())
                return false;

            return Equals((Location) obj);
        }

        /// <summary>
        /// Determines whether the specified Location is equal to this instance.
        /// </summary>
        /// <param name="loc">The Location to compare with this instance.</param>
        /// <returns>true if the specified Location is equal to this instance; otherwise, false.</returns>
        public bool Equals(Location loc)
        {
            return loc.X == X && loc.Y == Y;
        }

        /// <summary>
        /// Determines whether the specified coordinates are equal to this instance.
        /// </summary>
        /// <param name="x">The X coordinate.</param>
        /// <param name="y">The Y coordinate.</param>
        /// <returns>true if the specified coordinates are equal to this instance; otherwise, false.</returns>
        public bool Equals(int x, int y)
        {
            return X == x && y == Y;
        }

        /// <summary>
        /// Determines whether either of the two Location objects is equal to the other.
        /// </summary>
        /// <param name="o">The first Location.</param>
        /// <param name="o2">The second Location.</param>
        /// <returns>true if the Locations are equal; otherwise, false.</returns>
        public static bool operator ==(Location o, object o2)
        {
            return o.Equals(o2);
        }

        /// <summary>
        /// Determines whether either of the two Location objects is not equal to the other.
        /// </summary>
        /// <param name="o">The first Location.</param>
        /// <param name="o2">The second Location.</param>
        /// <returns>true if the Locations are not equal; otherwise, false.</returns>
        public static bool operator !=(Location o, object o2)
        {
            return !o.Equals(o2);
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>A hash code for this instance.</returns>
        public override int GetHashCode()
        {
            return X.GetHashCode() ^ Y.GetHashCode();
        }

        /// <summary>
        /// Creates a clone of this instance.
        /// </summary>
        /// <returns>A new Location with the same coordinates.</returns>
        public Location Clone()
        {
            return new Location(X, Y);
        }
    }
}