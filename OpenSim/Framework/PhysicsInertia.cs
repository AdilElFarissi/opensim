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
using OpenMetaverse;
using System.Text;
using System.IO;
using System.Xml;

namespace OpenSim.Framework
{
    /// <summary>
    /// Represents physics inertia data for a linkset, including mass, center of mass,
    /// inertia tensor, and principal axis rotation.
    /// </summary>
    public class PhysicsInertiaData
    {
        public float TotalMass; // the total mass of a linkset
        public Vector3 CenterOfMass;  // the center of mass position relative to root part position
        public Vector3 Inertia; //  (Ixx, Iyy, Izz) moment of inertia relative to center of mass and principal axis in local coords
        public Vector4 InertiaRotation; // if principal axis don't match local axis, the principal axis rotation
                                        // or the upper triangle of the inertia tensor 
                                        // Ixy (= Iyx), Ixz (= Izx), Iyz (= Izy))

        /// <summary>
        /// Initializes a new instance of the PhysicsInertiaData class with default values.
        /// </summary>
        public PhysicsInertiaData()
        {
        }

        /// <summary>
        /// Initializes a new instance of the PhysicsInertiaData class as a copy of an existing instance.
        /// </summary>
        /// <param name="source">The source PhysicsInertiaData instance to copy from.</param>
        public PhysicsInertiaData(PhysicsInertiaData source)
        {
           TotalMass = source.TotalMass;
           CenterOfMass = source.CenterOfMass;
           Inertia = source.Inertia;
           InertiaRotation = source.InertiaRotation;
        }

        private XmlTextWriter writer;

        private void XWint(string name, int i)
        {
            writer.WriteElementString(name, i.ToString());
        }

        private void XWfloat(string name, float f)
        {
            writer.WriteElementString(name, f.ToString(Culture.FormatProvider));
        }

        private void XWVector(string name, Vector3 vec)
        {
            writer.WriteStartElement(name);
            writer.WriteElementString("X", vec.X.ToString(Culture.FormatProvider));
            writer.WriteElementString("Y", vec.Y.ToString(Culture.FormatProvider));
            writer.WriteElementString("Z", vec.Z.ToString(Culture.FormatProvider));
            writer.WriteEndElement();
        }

        private void XWVector4(string name, Vector4 quat)
        {
            writer.WriteStartElement(name);
            writer.WriteElementString("X", quat.X.ToString(Culture.FormatProvider));
            writer.WriteElementString("Y", quat.Y.ToString(Culture.FormatProvider));
            writer.WriteElementString("Z", quat.Z.ToString(Culture.FormatProvider));
            writer.WriteElementString("W", quat.W.ToString(Culture.FormatProvider));
            writer.WriteEndElement();
        }

        /// <summary>
        /// Writes the physics inertia data to an XmlTextWriter.
        /// </summary>
        /// <param name="twriter">The XmlTextWriter to write to.</param>
        public void ToXml2(XmlTextWriter twriter)
        {
            writer = twriter;
            writer.WriteStartElement("PhysicsInertia");

            XWfloat("MASS", TotalMass);
            XWVector("CM", CenterOfMass);
            XWVector("INERTIA", Inertia);
            XWVector4("IROT", InertiaRotation);

            writer.WriteEndElement();
            writer = null;
        }

        XmlReader reader;

        private int XRint()
        {
            return reader.ReadElementContentAsInt();
        }

        private float XRfloat()
        {
            return reader.ReadElementContentAsFloat();
        }

        /// <summary>
        /// Reads a Vector3 from the XML reader.
        /// </summary>
        /// <returns>The Vector3 read from the XML.</returns>
        public Vector3 XRvector()
        {
            Vector3 vec;
            reader.ReadStartElement();
            vec.X = reader.ReadElementContentAsFloat();
            vec.Y = reader.ReadElementContentAsFloat();
            vec.Z = reader.ReadElementContentAsFloat();
            reader.ReadEndElement();
            return vec;
        }

        /// <summary>
        /// Reads a Vector4 from the XML reader.
        /// </summary>
        /// <returns>The Vector4 read from the XML.</returns>
        public Vector4 XRVector4()
        {
            Vector4 q;
            reader.ReadStartElement();
            q.X = reader.ReadElementContentAsFloat();
            q.Y = reader.ReadElementContentAsFloat();
            q.Z = reader.ReadElementContentAsFloat();
            q.W = reader.ReadElementContentAsFloat();
            reader.ReadEndElement();
            return q;
        }

        /// <summary>
        /// Processes XML nodes using the provided processors dictionary.
        /// </summary>
        /// <param name="processors">Dictionary mapping node names to processing actions.</param>
        /// <param name="xtr">The XmlReader to read from.</param>
        /// <returns>True if errors occurred during processing, false otherwise.</returns>
        public static bool EReadProcessors(
            Dictionary<string, Action> processors,
            XmlReader xtr)
        {
            bool errors = false;

            while (xtr.NodeType != XmlNodeType.EndElement)
            {
                string nodeName = xtr.Name;

                Action p = null;
                if (processors.TryGetValue(xtr.Name, out p))
                {
                    try
                    {
                        p();
                    }
                    catch (Exception)
                    {
                        errors = true;
                        if (xtr.NodeType == XmlNodeType.EndElement)
                            xtr.Read();
                    }
                }
                else
                {
                    xtr.ReadOuterXml(); // ignore
                }
            }

            return errors;
        }

        /// <summary>
        /// Converts the physics inertia data to an XML string.
        /// </summary>
        /// <returns>The XML string representation.</returns>
        public string ToXml2()
        {
            using (StringWriter sw = new())
            {
                using (XmlTextWriter xwriter = new(sw))
                {
                    ToXml2(xwriter);
                }

                return sw.ToString();
            }
        }

        /// <summary>
        /// Creates a PhysicsInertiaData instance from an XML string.
        /// </summary>
        /// <param name="text">The XML string to parse.</param>
        /// <returns>A new PhysicsInertiaData instance, or null if parsing failed or input is empty.</returns>
        public static PhysicsInertiaData FromXml2(string text)
        {
            if (text.Length == 0)
                return null;

            bool error;
            PhysicsInertiaData v;
            UTF8Encoding enc = new();
            using(MemoryStream ms = new(enc.GetBytes(text)))
            {
                using(XmlTextReader xreader = new(ms))
                {
                    xreader.DtdProcessing = DtdProcessing.Ignore;
                    v = new PhysicsInertiaData();
                    v.FromXml2(xreader, out error);
                }
            }
            if (error)
                return null;

            return v;
        }

        /// <summary>
        /// Creates a PhysicsInertiaData instance from an XmlReader.
        /// </summary>
        /// <param name="reader">The XmlReader to read from.</param>
        /// <returns>A new PhysicsInertiaData instance, or null if parsing failed.</returns>
        public static PhysicsInertiaData FromXml2(XmlReader reader)
        {
            PhysicsInertiaData data = new();

            bool errors = false;

            data.FromXml2(reader, out errors);
            if (errors)
                return null;

            return data;
        }

        private void FromXml2(XmlReader _reader, out bool errors)
        {
            errors = false;
            reader = _reader;

            Dictionary<string, Action> m_XmlProcessors = new()
            {
                { "MASS", ProcessXR_Mass },
                { "CM", ProcessXR_CM },
                { "INERTIA", ProcessXR_Inertia },
                { "IROT", ProcessXR_InertiaRotation }
            };

            reader.ReadStartElement("PhysicsInertia", string.Empty);

            errors = EReadProcessors(
                m_XmlProcessors,
                reader);

            reader.ReadEndElement();
            reader = null;
        }

        private void ProcessXR_Mass()
        {
            TotalMass = XRfloat();
        }

        private void ProcessXR_CM()
        {
            CenterOfMass = XRvector();
        }

        private void ProcessXR_Inertia()
        {
            Inertia = XRvector();
        }

        private void ProcessXR_InertiaRotation()
        {
            InertiaRotation = XRVector4();
        }
    }
}