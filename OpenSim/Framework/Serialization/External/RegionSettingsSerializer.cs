using System.Globalization;
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
    /// </summary>
    public class RegionSettingsSerializer
    {
        private const int MaxSerializationSize = 1024 * 1024; // 1 MB limit to prevent XML bombs

        /// <summary>
        /// Deserialize settings
        /// </summary>
        /// <param name="serializedSettings"></param>
        /// <param name="regionEnv"></param>
        /// <param name="estateSettings">The Estate Settings stored in the archive will be merged into this object</param>
        /// <returns></returns>
        /// <exception cref="System.Xml.XmlException"></exception>
        public static RegionSettings Deserialize(byte[] serializedSettings, out ViewerEnvironment regionEnv, EstateSettings estateSettings)
        {
            // Ensure we do not process excessively large inputs that could cause resource exhaustion
            if (serializedSettings.Length > MaxSerializationSize)
                throw new XmlException("Serialized settings exceed maximum allowed size.");

            // encoding is wrong. old oars seem to be on utf-16
            return Deserialize(Encoding.ASCII.GetString(serializedSettings, 0, serializedSettings.Length), out regionEnv, estateSettings);
        }

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
            // Limit the size of the string input to prevent denial-of-service via huge XML payloads
            if (serializedSettings.Length > MaxSerializationSize)
                throw new XmlException("Serialized settings exceed maximum allowed size.");

            RegionSettings settings = new RegionSettings();
            regionEnv = null;

            // Create a secure XmlReader that disables DTD processing and external entity expansion
            var readerSettings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                MaxCharactersFromEntity = 1024,
                MaxDepth = 100
            };
            using (var stringReader = new StringReader(serializedSettings))
            using (var xmlReader = XmlReader.Create(stringReader, readerSettings))
            {
                xmlReader.ReadStartElement("RegionSettings");

                xmlReader.ReadStartElement("General");

                while (xmlReader.Read() && xmlReader.NodeType != XmlNodeType.EndElement)
                {
                    switch (xmlReader.Name)
                    {
                        case "AllowDamage":
                            settings.AllowDamage = bool.Parse(xmlReader.ReadElementContentAsString());
                            break;
                        case "AllowLandResell":
                            settings.AllowLandResell = bool.Parse(xmlReader.ReadElementContentAsString());
                            break;
                        case "AllowLandJoinDivide":
                            settings.AllowLandJoinDivide = bool.Parse(xmlReader.ReadElementContentAsString());
                            break;
                        case "BlockFly":
                            settings.BlockFly = bool.Parse(xmlReader.ReadElementContentAsString());
                            break;
                        case "BlockLandShowInSearch":
                            settings.BlockShowInSearch = bool.Parse(xmlReader.ReadElementContentAsString());
                            break;
                        case "BlockTerraform":
                            settings.BlockTerraform = bool.Parse(xmlReader.ReadElementContentAsString());
                            break;
                        case "DisableCollisions":
                            settings.DisableCollisions = bool.Parse(xmlReader.ReadElementContentAsString());
                            break;
                        case "DisablePhysics":
                            settings.DisablePhysics = bool.Parse(xmlReader.ReadElementContentAsString());
                            break;
                        case "DisableScripts":
                            settings.DisableScripts = bool.Parse(xmlReader.ReadElementContentAsString());
                            break;
                        case "MaturityRating":
                            settings.Maturity = int.Parse(xmlReader.ReadElementContentAsString());
                            break;
                        case "RestrictPushing":
                            settings.RestrictPushing = bool.Parse(xmlReader.ReadElementContentAsString());
                            break;
                        case "AgentLimit":
                            settings.AgentLimit = int.Parse(xmlReader.ReadElementContentAsString());
                            break;
                        case "ObjectBonus":
                            settings.ObjectBonus = double.Parse(xmlReader.ReadElementContentAsString(), CultureInfo.NumberFormatInfo);
                            break;
                    }
                }

                xmlReader.ReadEndElement();
                xmlReader.ReadStartElement("GroundTextures");

                while (xmlReader.Read() && xmlReader.NodeType != XmlNodeType.EndElement)
                {
                    switch (xmlReader.Name)
                    {
                        case "Texture1":
                            settings.TerrainTexture1 = UUID.Parse(xmlReader.ReadElementContentAsString());
                            break;
                        case "Texture2":
                            settings.TerrainTexture2 = UUID.Parse(xmlReader.ReadElementContentAsString());
                            break;
                        case "Texture3":
                            settings.TerrainTexture3 = UUID.Parse(xmlReader.ReadElementContentAsString());
                            break;
                        case "Texture4":
                            settings.TerrainTexture4 = UUID.Parse(xmlReader.ReadElementContentAsString());
                            break;
                        case "PBR1":
                            settings.TerrainPBR1 = UUID.Parse(xmlReader.ReadElementContentAsString());
                            break;
                        case "PBR2":
                            settings.TerrainPBR2 = UUID.Parse(xmlReader.ReadElementContentAsString());
                            break;
                        case "PBR3":
                            settings.TerrainPBR3 = UUID.Parse(xmlReader.ReadElementContentAsString());
                            break;
                        case "PBR4":
                            settings.TerrainPBR4 = UUID.Parse(xmlReader.ReadElementContentAsString());
                            break;
                        case "ElevationLowSW":
                            settings.Elevation1SW = double.Parse(xmlReader.ReadElementContentAsString(), CultureInfo.NumberFormatInfo);
                            break;
                        case "ElevationLowNW":
                            settings.Elevation1NW = double.Parse(xmlReader.ReadElementContentAsString(), CultureInfo.NumberFormatInfo);
                            break;
                        case "ElevationLowSE":
                            settings.Elevation1SE = double.Parse(xmlReader.ReadElementContentAsString(), CultureInfo.NumberFormatInfo);
                            break;
                        case "ElevationLowNE":
                            settings.Elevation1NE = double.Parse(xmlReader.ReadElementContentAsString(), CultureInfo.NumberFormatInfo);
                            break;
                        case "ElevationHighSW":
                            settings.Elevation2SW = double.Parse(xmlReader.ReadElementContentAsString(), CultureInfo.NumberFormatInfo);
                            break;
                        case "ElevationHighNW":
                            settings.Elevation2NW = double.Parse(xmlReader.ReadElementContentAsString(), CultureInfo.NumberFormatInfo);
                            break;
                        case "ElevationHighSE":
                            settings.Elevation2SE = double.Parse(xmlReader.ReadElementContentAsString(), CultureInfo.NumberFormatInfo);
                            break;
                        case "ElevationHighNE":
                            settings.Elevation2NE = double.Parse(xmlReader.ReadElementContentAsString(), CultureInfo.NumberFormatInfo);
                            break;
                    }
                }

                xmlReader.ReadEndElement();
                xmlReader.ReadStartElement("Terrain");

                while (xmlReader.Read() && xmlReader.NodeType != XmlNodeType.EndElement)
                {
                    switch (xmlReader.Name)
                    {
                        case "WaterHeight":
                            settings.WaterHeight = double.Parse(xmlReader.ReadElementContentAsString(), CultureInfo.NumberFormatInfo);
                            break;
                        case "TerrainRaiseLimit":
                            settings.TerrainRaiseLimit = double.Parse(xmlReader.ReadElementContentAsString(), CultureInfo.NumberFormatInfo);
                            break;
                        case "TerrainLowerLimit":
                            settings.TerrainLowerLimit = double.Parse(xmlReader.ReadElementContentAsString(), CultureInfo.NumberFormatInfo);
                            break;
                        case "UseEstateSun":
                            settings.UseEstateSun = bool.Parse(xmlReader.ReadElementContentAsString());
                            break;
                        case "FixedSun":
                            settings.FixedSun = bool.Parse(xmlReader.ReadElementContentAsString());
                            break;
                        case "SunPosition":
                            settings.SunPosition = double.Parse(xmlReader.ReadElementContentAsString());
                            break;
                    }
                }

                xmlReader.ReadEndElement();

                if (xmlReader.IsStartElement("Telehub"))
                {
                    if (xmlReader.IsEmptyElement)
                        xmlReader.Read();
                    else
                    {
                        xmlReader.ReadStartElement("Telehub");
                        while (xmlReader.Read() && xmlReader.NodeType != XmlNodeType.EndElement)
                        {
                            switch (xmlReader.Name)
                            {
                                case "TelehubObject":
                                    settings.TelehubObject = UUID.Parse(xmlReader.ReadElementContentAsString());
                                    break;
                                case "SpawnPoint":
                                    string str = xmlReader.ReadElementContentAsString();
                                    SpawnPoint sp = SpawnPoint.Parse(str);
                                    settings.AddSpawnPoint(sp);
                                    break;
                            }
                        }
                        xmlReader.ReadEndElement();
                    }
                }

                if (xmlReader.IsStartElement("Environment"))
                {
                    if (xmlReader.IsEmptyElement)
                        xmlReader.Read();
                    else
                    {
                        xmlReader.ReadStartElement("Environment");
                        while (xmlReader.Read() && xmlReader.NodeType != XmlNodeType.EndElement)
                        {
                            switch (xmlReader.Name)
                            {
                                case "data":
                                    regionEnv = ViewerEnvironment.FromOSDString(xmlReader.ReadElementContentAsString());
                                    break;
                            }
                        }
                        xmlReader.ReadEndElement();
                    }
                }

                if (xmlReader.IsStartElement("Estate"))
                {
                    if (xmlReader.IsEmptyElement)
                        xmlReader.Read();
                    else
                    {
                        xmlReader.ReadStartElement("Estate");
                        while (xmlReader.Read() && xmlReader.NodeType != XmlNodeType.EndElement)
                        {
                            switch (xmlReader.Name)
                            {
                                case "AllowDirectTeleport":
                                    estateSettings.AllowDirectTeleport = bool.Parse(xmlReader.ReadElementContentAsString());
                                    break;
                                case "AllowEnvironmentOverride":
                                    estateSettings.AllowEnvironmentOverride = bool.Parse(xmlReader.ReadElementContentAsString());
                                    break;
                            }
                        }
                        xmlReader.ReadEndElement();
                    }
                }

                xmlReader.Close();
            }

            return settings;
        }

        public static string Serialize(RegionSettings settings, ViewerEnvironment RegionEnv, EstateSettings estateSettings)
        {
            StringWriter sw = new StringWriter();
            XmlTextWriter xtw = new XmlTextWriter(sw);
            xtw.Formatting = Formatting.Indented;
            xtw.WriteStartDocument();

            xtw.WriteStartElement("RegionSettings");

            xtw.WriteStartElement("General");
            xtw.WriteElementString("AllowDamage", settings.AllowDamage.ToString());
            xtw.WriteElementString("AllowLandResell", settings.AllowLandResell.ToString());
            xtw.WriteElementString("AllowLandJoinDivide", settings.AllowLandJoinDivide.ToString());
            xtw.WriteElementString("BlockFly", settings.BlockFly.ToString());
            xtw.WriteElementString("BlockLandShowInSearch", settings.BlockShowInSearch.ToString());
            xtw.WriteElementString("BlockTerraform", settings.BlockTerraform.ToString());
            xtw.WriteElementString("DisableCollisions", settings.DisableCollisions.ToString());
            xtw.WriteElementString("DisablePhysics", settings.DisablePhysics.ToString());
            xtw.WriteElementString("DisableScripts", settings.DisableScripts.ToString());
            xtw.WriteElementString("MaturityRating", settings.Maturity.ToString());
            xtw.WriteElementString("RestrictPushing", settings.RestrictPushing.ToString());
            xtw.WriteElementString("AgentLimit", settings.AgentLimit.ToString());
            xtw.WriteElementString("ObjectBonus", settings.ObjectBonus.ToString());
            xtw.WriteEndElement();

            xtw.WriteStartElement("GroundTextures");
            xtw.WriteElementString("Texture1", settings.TerrainTexture1.ToString());
            xtw.WriteElementString("Texture2", settings.TerrainTexture2.ToString());
            xtw.WriteElementString("Texture3", settings.TerrainTexture3.ToString());
            xtw.WriteElementString("Texture4", settings.TerrainTexture4.ToString());
            xtw.WriteElementString("PBR1", settings.TerrainPBR1.ToString());
            xtw.WriteElementString("PBR2", settings.TerrainPBR2.ToString());
            xtw.WriteElementString("PBR3", settings.TerrainPBR3.ToString());
            xtw.WriteElementString("PBR4", settings.TerrainPBR4.ToString());
            xtw.WriteElementString("ElevationLowSW", settings.Elevation1SW.ToString());
            xtw.WriteElementString("ElevationLowNW", settings.Elevation1NW.ToString());
            xtw.WriteElementString("ElevationLowSE", settings.Elevation1SE.ToString());
            xtw.WriteElementString("ElevationLowNE", settings.Elevation1NE.ToString());
            xtw.WriteElementString("ElevationHighSW", settings.Elevation2SW.ToString());
            xtw.WriteElementString("ElevationHighNW", settings.Elevation2NW.ToString());
            xtw.WriteElementString("ElevationHighSE", settings.Elevation2SE.ToString());
            xtw.WriteElementString("ElevationHighNE", settings.Elevation2NE.ToString());
            xtw.WriteEndElement();

            xtw.WriteStartElement("Terrain");
            xtw.WriteElementString("WaterHeight", settings.WaterHeight.ToString());
            xtw.WriteElementString("TerrainRaiseLimit", settings.TerrainRaiseLimit.ToString());
            xtw.WriteElementString("TerrainLowerLimit", settings.TerrainLowerLimit.ToString());
            xtw.WriteElementString("UseEstateSun", settings.UseEstateSun.ToString());
            xtw.WriteElementString("FixedSun", settings.FixedSun.ToString());
            xtw.WriteElementString("SunPosition", settings.SunPosition.ToString());
            xtw.WriteEndElement();

            xtw.WriteStartElement("Telehub");
            if (!settings.TelehubObject.IsZero())
            {
                xtw.WriteElementString("TelehubObject", settings.TelehubObject.ToString());
                foreach (SpawnPoint sp in settings.SpawnPoints())
                    xtw.WriteElementString("SpawnPoint", sp.ToString());
            }
            xtw.WriteEndElement();

            if (RegionEnv != null)
            {
                xtw.WriteStartElement("Environment");
                xtw.WriteElementString("data", ViewerEnvironment.ToOSDString(RegionEnv));
                xtw.WriteEndElement();
            }

            xtw.WriteStartElement("Estate");
            xtw.WriteElementString("AllowDirectTeleport", estateSettings.AllowDirectTeleport.ToString());
            xtw.WriteElementString("AllowEnvironmentOverride", estateSettings.AllowEnvironmentOverride.ToString());
            xtw.WriteEndElement();

            xtw.WriteEndElement();

            xtw.Close();
            sw.Close();

            return sw.ToString();
        }
    }
}