// /* The MIT License
//  *
//  * Copyright (c) 2010 Intel Corporation.
//  * All rights reserved.
//  *
//  * Based on the convexdecomposition library from
//  * <http://codesuppository.googlecode.com> by John W. Ratcliff and Stan Melax.
//  *
//  * Permission is hereby granted, free of charge, to any person obtaining a copy
//  * of this software and associated documentation files (the "Software"), to deal
//  * in the Software without restriction, including without limitation the rights
//  * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//  * copies of the Software, and to permit persons to whom the Software is
//  * furnished to do so, subject to the following conditions:
//  *
//  * The above copyright notice and this permission notice shall be included in
//  * all copies or substantial portions of the Software.
//  *
//  * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//  * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//  * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//  * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//  * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//  * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
//  * THE SOFTWARE.
//  */

using System.Collections.Generic;
using System.Diagnostics;

namespace OpenSim.Region.PhysicsModules.ConvexDecompositionDotNet
{
    /// <summary>
    /// Callback delegate for receiving convex decomposition results.
    /// </summary>
    public delegate void ConvexDecompositionCallback(ConvexResult result);

    /// <summary>
    /// Represents a triangle face with three vertices.
    /// </summary>
    public class FaceTri
    {
        public float3 P1;
        public float3 P2;
        public float3 P3;

        /// <summary>
        /// Initializes a new instance of the FaceTri class with default values.
        /// </summary>
        public FaceTri() { }

        /// <summary>
        /// Initializes a new instance of the FaceTri class from a list of vertices and indices.
        /// </summary>
        /// <param name="vertices">The list of vertices.</param>
        /// <param name="i1">Index of the first vertex.</param>
        /// <param name="i2">Index of the second vertex.</param>
        /// <param name="i3">Index of the third vertex.</param>
        public FaceTri(List<float3> vertices, int i1, int i2, int i3)
        {
            P1 = new float3(vertices[i1]);
            P2 = new float3(vertices[i2]);
            P3 = new float3(vertices[i3]);
        }
    }

    /// <summary>
    /// Provides methods for decomposing a mesh into convex hulls.
    /// </summary>
    public static class ConvexDecomposition
    {
        /// <summary>
        /// Adds a triangle to the specified list using the vertex pool.
        /// </summary>
        /// <param name="vl">The vertex pool.</param>
        /// <param name="list">The list to add indices to.</param>
        /// <param name="p1">First vertex of the triangle.</param>
        /// <param name="p2">Second vertex of the triangle.</param>
        /// <param name="p3">Third vertex of the triangle.</param>
        private static void addTri(VertexPool vl, List<int> list, float3 p1, float3 p2, float3 p3)
        {
            int i1 = vl.getIndex(p1);
            int i2 = vl.getIndex(p2);
            int i3 = vl.getIndex(p3);

            // do *not* process degenerate triangles!
            if ( i1 != i2 && i1 != i3 && i2 != i3 )
            {
                list.Add(i1);
                list.Add(i2);
                list.Add(i3);
            }
        }

        /// <summary>
        /// Recursively calculates the convex decomposition of the given mesh.
        /// </summary>
        /// <param name="vertices">The list of vertices.</param>
        /// <param name="indices">The list of indices.</param>
        /// <param name="callback">The callback to invoke with each convex result.</param>
        /// <param name="masterVolume">The master volume for concavity calculation.</param>
        /// <param name="depth">Current recursion depth.</param>
        /// <param name="maxDepth">Maximum recursion depth.</param>
        /// <param name="concavePercent">Percentage threshold for concavity.</param>
        /// <param name="mergePercent">Percentage for merging.</param>
        public static void calcConvexDecomposition(List<float3> vertices, List<int> indices, ConvexDecompositionCallback callback, float masterVolume, int depth,
            int maxDepth, float concavePercent, float mergePercent)
        {
            float4 plane = new();
            bool split = false;

            if (depth < maxDepth)
            {
                float volume = 0f;
                float c = Concavity.computeConcavity(vertices, indices, ref plane, ref volume);

                if (depth == 0)
                {
                    masterVolume = volume;
                }

                float percent = (c * 100.0f) / masterVolume;

                if (percent > concavePercent) // if great than 5% of the total volume is concave, go ahead and keep splitting.
                {
                    split = true;
                }
            }

            if (depth >= maxDepth || !split)
            {
                HullResult result = new();
                HullDesc desc = new();

                desc.SetHullFlag(HullFlag.QF_TRIANGLES);

                desc.Vertices = vertices;

                HullError ret = HullUtils.CreateConvexHull(desc, ref result);

                if (ret == HullError.QE_OK)
                {
                    ConvexResult r = new(result.OutputVertices, result.Indices);
                    callback(r);
                }

                return;
            }

            List<int> ifront = [];
            List<int> iback = [];

            VertexPool vfront = new();
            VertexPool vback = new();

            // ok..now we are going to 'split' all of the input triangles against this plane!
            for (int i = 0; i < indices.Count / 3; i++)
            {
                int i1 = indices[i * 3 + 0];
                int i2 = indices[i * 3 + 1];
                int i3 = indices[i * 3 + 2];

                FaceTri t = new(vertices, i1, i2, i3);

                float3[] front = new float3[4];
                float3[] back = new float3[4];

                int fcount = 0;
                int bcount = 0;

                PlaneTriResult result = PlaneTri.planeTriIntersection(plane, t, 0.00001f, ref front, out fcount, ref back, out bcount);

                if (fcount > 4 || bcount > 4)
                {
                    result = PlaneTri.planeTriIntersection(plane, t, 0.00001f, ref front, out fcount, ref back, out bcount);
                }

                switch (result)
                {
                    case PlaneTriResult.PTR_FRONT:
                        Debug.Assert(fcount == 3);
                        addTri(vfront, ifront, front[0], front[1], front[2]);
                        break;
                    case PlaneTriResult.PTR_BACK:
                        Debug.Assert(bcount == 3);
                        addTri(vback, iback, back[0], back[1], back[2]);
                        break;
                    case PlaneTriResult.PTR_SPLIT:
                        Debug.Assert(fcount >= 3 && fcount <= 4);
                        Debug.Assert(bcount >= 3 && bcount <= 4);

                        addTri(vfront, ifront, front[0], front[1], front[2]);
                        addTri(vback, iback, back[0], back[1], back[2]);

                        if (fcount == 4)
                        {
                            addTri(vfront, ifront, front[0], front[2], front[3]);
                        }

                        if (bcount == 4)
                        {
                            addTri(vback, iback, back[0], back[2], back[3]);
                        }

                        break;
                }
            }

            // ok... here we recursively call
            if (ifront.Count > 0)
            {
                List<float3> vertices2 = vfront.GetVertices();
                for (int i = 0; i < vertices2.Count; i++)
                    vertices2[i] = new float3(vertices2[i]);

                calcConvexDecomposition(vertices2, ifront, callback, masterVolume, depth + 1, maxDepth, concavePercent, mergePercent);
            }

            ifront.Clear();
            vfront.Clear();

            if (iback.Count > 0)
            {
                List<float3> vertices2 = vback.GetVertices();

                calcConvexDecomposition(vertices2, iback, callback, masterVolume, depth + 1, maxDepth, concavePercent, mergePercent);
            }

            iback.Clear();
            vback.Clear();
        }
    }
}