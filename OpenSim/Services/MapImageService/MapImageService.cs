usinglog4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Threading;

namespace OpenSim.Services.MapImageService
{
    public class MapImageService : IMapImageService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
#pragma warning disable 414
        private string LogHeader = "[MAP IMAGE SERVICE]";
#pragma warning restore 414

        private const int ZOOM_LEVELS = 8;
        private const int IMAGE_WIDTH = 256;
        private const int HALF_WIDTH = 128;
        private const int JPEG_QUALITY = 80;

        private static string m_TilesStoragePath = "maptiles";

        private static object m_Sync = new object();
        private static bool m_Initialized = false;
        private static Color m_Watercolor = Color.FromArgb(29, 72, 96);
        private static Bitmap m_WaterBitmap = null;
        private static byte[] m_WaterJPEGBytes = null;

        public MapImageService(IConfigSource config)
        {
            lock (m_Sync)
            {
                if (!m_Initialized)
                {
                    m_Initialized = true;
                    m_log.Debug("[MAP IMAGE SERVICE]: Starting MapImage service");

                    IConfig serviceConfig = config.Configs["MapImageService"];
                    if (serviceConfig is not null)
                    {
                        m_TilesStoragePath = serviceConfig.GetString("TilesStoragePath", m_TilesStoragePath);
                        m_WaterBitmap = new Bitmap(IMAGE_WIDTH, IMAGE_WIDTH, PixelFormat.Format24bppRgb);
                        FillImage(m_WaterBitmap, m_Watercolor);
                        using (MemoryStream ms = new MemoryStream())
                        {
                            m_WaterBitmap.Save(ms, ImageFormat.Jpeg);
                            ms.Seek(0, SeekOrigin.Begin);
                            m_WaterJPEGBytes = ms.ToArray();
                        }
                    }
                }
            }
        }

        #region IMapImageService

        public bool AddMapTile(int x, int y, byte[] imageData, UUID scopeID, out string reason)
        {
            reason = string.Empty;
            try
            {
                ValidateImageData(imageData);
            }
            catch (Exception ex)
            {
                m_log.WarnFormat("[MAP IMAGE SERVICE]: Invalid image data: {0}", ex.Message);
                reason = "Invalid image format or data";
                return false;
            }

            ReadOnlySpan<char> path = GetFolder(scopeID);
            string fileName = GetFileName(1, x, y, path);
            lock (m_Sync)
            {
                try
                {
                    File.WriteAllBytes(fileName, imageData);
                }
                catch (Exception e)
                {
                    m_log.WarnFormat("[MAP IMAGE SERVICE]: Unable to save image file {0}: {1}", fileName, e);
                    reason = e.Message;
                    return false;
                }
            }

            return UpdateMultiResolutionFiles(x, y, scopeID);
        }

        public bool RemoveMapTile(int x, int y, UUID scopeID, out string reason)
        {
            reason = string.Empty;
            string fileName = GetFileName(1, x, y, scopeID);

            lock (m_Sync)
            {
                try
                {
                    File.Delete(fileName);
                }
                catch (Exception e)
                {
                    m_log.Warn($"[MAP IMAGE SERVICE]: Unable to delete file {fileName}: {e.Message}");
                    reason = e.Message;
                    return false;
                }
            }
            return UpdateMultiResolutionFiles(x, y, scopeID);
        }

        private void ValidateImageData(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0)
                throw new ArgumentException("Image data cannot be empty");

            using (var stream = new MemoryStream(imageData))
            {
                using (var image = new Bitmap(stream))
                {
                    if (image.Width != IMAGE_WIDTH || image.Height != IMAGE_WIDTH || image.PixelFormat != PixelFormat.Format24bppRgb)
                        throw new ArgumentException("Invalid image dimensions or format");
                }
            }
        }

        // ... (rest of the methods remain unchanged)
        #endregion

        // ... (other methods)
    }
}