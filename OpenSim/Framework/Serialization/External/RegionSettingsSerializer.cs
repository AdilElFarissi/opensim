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

using System.IO;
using System.Text;
using System.Xml;
using OpenMetaverse;
using OpenSim.Framework;
using log4net;
using System.Reflection;
namespace OpenSim.Framework.Serialization.External
{
/// <summary>
/// Serialize and deserialize region settings as an external format.
/// <summary>
public class RegionSettingsSerializer
{
    /// <summary>
    /// Deserialize settings
/// <summary>
/// Deserialize settings
/// </summary>
/// <param name="serializedSettings"></param>
/// <param name="regionEnv"></param>
/// <param name="estateSettings">The Estate Settings stored in the archive will be merged into this object</param>
/// <returns></returns>
/// <exception cref="System.Xml.XmlException"></exception>
public static RegionSettings Deserialize(string serializedSettings, out ViewerEnvironment regionEnv, EstateSettings estateSettings)
{
    RegionSettings settings = new RegionSettings();
    regionEnv = null;
var xmlReaderSettings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
using (var stringReader = new StringReader(serializedSettings))
{
using (var xmlReader = XmlReader.Create(stringReader, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore }))
{
    while (xmlReader.Read())
{
    if (xmlReader.NodeType == XmlNodeType.Element)
    {
switch (xmlReader.Name)
{
    case "AllowDamage":
using (var stringReader = new StringReader(xmlReader.ReadOuterXml()))
{
    using (var xmlReader = XmlReader.Create(stringReader))
switch (xtr.Name)
{
    case "Texture1":
XmlReaderSettings settings = new XmlReaderSettings();
settings.DtdProcessing = DtdProcessing.Ignore;

using (XmlReader xtr = XmlReader.Create(xmlString, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore }))
{
    // Rest of the code
private static readonly XmlReaderSettings _xmlReaderSettings = new XmlReaderSettings
{
    DtdProcessing = DtdProcessing.Ignore
};

public static RegionSettings Deserialize(string xmlString)
{
    using (StringReader sr = new StringReader(xmlString))
{
    using (XmlReader xtr = XmlReader.Create(sr, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore }))
    {
        RegionSettings settings = new RegionSettings();
        ViewerEnvironment regionEnv = null;
XmlReaderSettings settings = new XmlReaderSettings();
settings.DtdProcessing = DtdProcessing.Ignore;
using (XmlReader xtr = XmlReader.Create(xmlString, settings))
XmlReaderSettings settings = new XmlReaderSettings();
settings.DtdProcessing = DtdProcessing.Ignore;

// Rest of the code...

if (xtr.IsStartElement("Estate"))
{
    if (xtr.IsEmptyElement)
        xtr.Read();
else
{
    xtr.ReadStartElement("Estate");
XmlReaderSettings settings = new XmlReaderSettings();
settings.DtdProcessing = DtdProcessing.Ignore;
settings.CheckCharacters = true; // Ensure XML is well-formed
settings.XmlResolver = null; 
settings.ProhibitDtd = true; 
settings.CheckCharacters = true; // Ensure well-formed XML
using (XmlReader xr = XmlReader.Create(new StringReader(xmlString), new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore }))
{
    // Rest of the code...
XmlWriterSettings xtwSettings = new XmlWriterSettings();
xtwSettings.Indent = true;
xtwSettings.CheckCharacters = true; // Ensure well-formed XML
using (XmlWriter xtw = XmlWriter.Create(sw, new XmlWriterSettings { DtdProcessing = DtdProcessing.Ignore }))
{
    xtw.WriteStartElement("Environment");
xtw.WriteElementString("data", ViewerEnvironment.ToOSDString(RegionEnv));
xtw.WriteEndElement();

var xmlDoc = new XmlDocument();
var xmlDeclaration = xmlDoc.CreateXmlDeclaration("1.0", "utf-8", null);
xmlDoc.AppendChild(xmlDeclaration);
var xmlDoc = new XmlDocument();
var root = xmlDoc.CreateElement("data");
xmlDoc.AppendChild(root);
var xmlDoc = new XmlDocument();
var root = xmlDoc.CreateElement("Estate");
xmlDoc.AppendChild(root);
var allowDirectTeleport = xmlDoc.CreateElement("AllowDirectTeleport");
allowDirectTeleport.InnerText = estateSettings.AllowDirectTeleport.ToString();
root.AppendChild(allowDirectTeleport);
var allowEnvironmentOverride = xmlDoc.CreateElement("AllowEnvironmentOverride");
allowEnvironmentOverride.InnerText = estateSettings.AllowEnvironmentOverride.ToString();
root.AppendChild(allowEnvironmentOverride);

// Ensure the XmlDocument is properly serialized to prevent resource leaks
var xmlDoc = new XmlDocument();
var xmlDoc = new XmlDocument();
xmlDoc.LoadXml(xmlString, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore });
// Return the string representation of the XML document
return xmlDoc.OuterXml;
XmlReaderSettings settings = new XmlReaderSettings();
settings.DtdProcessing = DtdProcessing.Parse;
XmlReader reader = XmlReader.Create(new StringReader(xmlString), settings);

// Replace the above code with the following:
XmlReaderSettings settings = new XmlReaderSettings();
settings.DtdProcessing = DtdProcessing.Ignore;
settings.XmlResolver = null;
XmlReader reader = XmlReader.Create(new StringReader(xmlString), settings);
XmlReaderSettings settings = new XmlReaderSettings();
settings.DtdProcessing = DtdProcessing.Parse;
using (XmlReader reader = XmlReader.Create(xmlString, settings))
{
    // ...
    XmlReaderSettings settings = new XmlReaderSettings();
settings.DtdProcessing = DtdProcessing.Ignore;
using (XmlReader reader = XmlReader.Create(xmlString, settings))
{
reader.MoveToContent();
if (reader.NodeType == XmlNodeType.Element)
{
    using (XmlReader innerReader = reader.ReadSubtree())
    {
// ...
}
}
else
{
m_log.Error("Invalid XML: " + xmlString);
try
{
var settings = new XmlReaderSettings
{
    DtdProcessing = DtdProcessing.Ignore,
    XmlResolver = null,
    ProhibitDtd = true
};
using (XmlReader xmlReader = XmlReader.Create(new StringReader(xmlString), new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore }))
{
    // Handle the XML string as an XML document
catch (XmlException ex) when (ex.LineNumber == 212)
{
    m_log.Error($"XML Exception: {ex.Message}");
}
XmlReaderSettings settings = new XmlReaderSettings();
settings.DtdProcessing = DtdProcessing.Parse;
using (XmlReader reader = XmlReader.Create(xmlString, settings))
{
    // ...
}
private void LoadXml(string xmlString)
{
    var settings = new XmlReaderSettings();
settings.DtdProcessing = DtdProcessing.Ignore;
using (var reader = XmlReader.Create(new StringReader(xmlString), settings))
{
settings.DtdProcessing = DtdProcessing.Ignore;
settings.DtdProcessing = DtdProcessing.Ignore;
using (var reader = XmlReader.Create(new StringReader(xmlString), settings))
{
    var xmlDoc = new XmlDocument();
    xmlDoc.Load(reader);
settings.DtdProcessing = DtdProcessing.Ignore;
using (var reader = XmlReader.Create(new StringReader(xmlString), settings))
{
    var xmlDoc = new XmlDocument();
settings.DtdProcessing = DtdProcessing.Ignore;
using (var reader = XmlReader.Create(new StringReader(xmlString), settings))
{
    var xmlDoc = new XmlDocument();
settings.DtdProcessing = DtdProcessing.Ignore;
using (var reader = XmlReader.Create(new StringReader(xmlString), settings))
{
settings.DtdProcessing = DtdProcessing.Ignore;
using (var reader = XmlReader.Create(new StringReader(xmlString), settings))
{
settings.DtdProcessing = DtdProcessing.Ignore;
settings.DtdProcessing = DtdProcessing.Ignore;
using (var reader = XmlReader.Create(new StringReader(xmlString), settings))
{
    var xmlDoc = new XmlDocument();
    xmlDoc.Load(reader);
settings.DtdProcessing = DtdProcessing.Ignore;
using (var reader = XmlReader.Create(new StringReader(xmlString), settings))
{
var xmlDoc = new XmlDocument();
xmlDoc.LoadXml(reader.ReadOuterXml());
// Process xmlDoc

var xmlDoc = new XmlDocument();
xmlDoc.LoadXml(reader.ReadString(), new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore });
// Process xmlDoc
XmlReaderSettings settings = new XmlReaderSettings();
settings.DtdProcessing = DtdProcessing.Parse;
using (XmlReader reader = XmlReader.Create(xmlString, settings))
{
    // ...
}
