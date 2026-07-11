/* The MIT License
 *
 * Copyright (c) 2010 Intel Corporation.
 * All rights reserved.
 *
 * Based on the convexdecomposition library from
 * <http://codesuppository.googlecode.com> by John W. Ratcliff and Stan Melax.
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */

using System;

namespace OpenSim.Region.PhysicsModules.ConvexDecompositionDotNet
{
    /// <summary>
    /// Represents a plane in 3D space defined by a normal vector and distance from origin.
    /// </summary>
    public class Plane
    {
        /// <summary>
        /// The normal vector of the plane.
        /// </summary>
        public float3 normal = new();
        
        /// <summary>
        /// The distance from the origin to the plane along the normal vector.
        /// </summary>
        public float dist; // distance below origin - the D from plane equasion Ax+By+Cz+D=0

        private const float EPSILON = 1e-6f;

        /// <summary>
        /// Initializes a new instance of the Plane class with the specified normal and distance.
        /// </summary>
        /// <param name="n">The normal vector of the plane.</param>
        /// <param name="d">The distance from the origin.</param>
        public Plane(float3 n, float d)
        {
            normal = new float3(n);
            dist = d;
        }

        /// <summary>
        /// Initializes a new instance of the Plane class as a copy of another plane.
        /// </summary>
        /// <param name="p">The plane to copy.</param>
        public Plane(Plane p)
        {
            normal = new float3(p.normal);
            dist = p.dist;
        }

        /// <summary>
        /// Initializes a new instance of the Plane class with default values.
        /// </summary>
        public Plane()
        {
            dist = 0;
        }

        /// <summary>
        /// Transforms the plane to the space defined by the given position/orientation.
        /// </summary>
        /// <param name="position">The position for the transformation.</param>
        /// <param name="orientation">The orientation for the transformation.</param>
        public void Transform(float3 position, Quaternion orientation)
        {
            //   Transforms the plane to the space defined by the
            //   given position/orientation
            float3 newNormal = Quaternion.Inverse(orientation) * normal;
            float3 origin = Quaternion.Inverse(orientation) * (-normal * dist - position);

            normal = newNormal;
            dist = -float3.dot(newNormal, origin);
        }

        /// <summary>
        /// Returns a hash code for this plane.
        /// </summary>
        /// <returns>A hash code for the current plane.</returns>
        public override int GetHashCode()
        {
            return normal.GetHashCode() ^ dist.GetHashCode();
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current plane.
        /// </summary>
        /// <param name="obj">The object to compare with the current plane.</param>
        /// <returns>True if the objects are equal; otherwise, false.</returns>
        public override bool Equals(object obj)
        {
            if (obj is Plane p)
                return this == p;
            return false;
        }

        /// <summary>
        /// Determines whether two planes are equal.
        /// </summary>
        /// <param name="a">The first plane.</param>
        /// <param name="b">The second plane.</param>
        /// <returns>True if the planes are equal; otherwise, false.</returns>
        public static bool operator ==(Plane a, Plane b)
        {
            if (ReferenceEquals(a, b))
                return true;
            if (a is null || b is null)
                return false;
            return a.normal == b.normal && Math.Abs(a.dist - b.dist) < EPSILON;
        }

        /// <summary>
        /// Determines whether two planes are not equal.
        /// </summary>
        /// <param name="a">The first plane.</param>
        /// <param name="b">The second plane.</param>
        /// <returns>True if the planes are not equal; otherwise, false.</returns>
        public static bool operator !=(Plane a, Plane b)
        {
            return !(a == b);
        }

        /// <summary>
        /// Returns a new plane that is the result of flipping the given plane.
        /// </summary>
        /// <param name="plane">The plane to flip.</param>
        /// <returns>A new plane with flipped normal and distance.</returns>
        public static Plane PlaneFlip(Plane plane)
        {
            return new Plane(-plane.normal, -plane.dist);
        }

        /// <summary>
        /// Determines whether two planes are coplanar (either equal or flipped versions of each other).
        /// </summary>
        /// <param name="a">The first plane.</param>
        /// <param name="b">The second plane.</param>
        /// <returns>True if the planes are coplanar; otherwise, false.</returns>
        public static bool coplanar(Plane a, Plane b)
        {
            return (a == b || a == PlaneFlip(b));
        }
    }
}