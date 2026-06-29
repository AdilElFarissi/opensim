using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace OpenSim.Services.MapImageService
{
    public class MapImageService : IMapImageService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private const int ZOOM_LEVELS = 8;
        private const int IMAGE_WIDTH = 256;
        private const int HALF_WIDTH = 128;
        private const int JPEG_QUALITY = 80;

        private static string m_TilesStoragePath = "maptiles";

        private static readonly object m_Sync = new object();
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

                    var serviceConfig = config.Configs["MapImageService"];
                    if (serviceConfig != null)
                    {
                        m_TilesStoragePath = serviceConfig.GetString("TilesStoragePath", m_TilesStoragePath);

                        m_WaterBitmap = new Bitmap(IMAGE_WIDTH, IMAGE_WIDTH, PixelFormat.Format24bppRgb);
                        FillImage(m_WaterBitmap, m_Watercolor);

                        using var ms = new MemoryStream();
                        m_WaterBitmap.Save(ms, ImageFormat.Jpeg);
                        ms.Seek(0, SeekOrigin.Begin);
                        m_WaterJPEGBytes = ms.ToArray();
                    }
                }
            }
        }

        #region IMapImageService

        public bool AddMapTile(int x, int y, byte[] imageData, UUID scopeID, out string reason)
        {
            reason = string.Empty;
            var path = GetFolder(scopeID);
            var fileName = GetFileName(1, x, y, path);
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
            var fileName = GetFileName(1, x, y, scopeID);

            lock (m_Sync)
            {
                try
                {
                    File.Delete(fileName);
                }
                catch (Exception e)
                {
                    m_log.Warn($"[MAP IMAGE SERVICE]: Unable to save delete file {fileName}: {e.Message}");
                    reason = e.Message;
                    return false;
                }
            }
            return UpdateMultiResolutionFiles(x, y, scopeID);
        }

        #endregion

        private bool UpdateMultiResolutionFiles(int x, int y, UUID scopeID)
        {
            lock (m_Sync)
            {
                Task.Run(async () =>
                {
                    await Task.Delay(60 * 1000);
                    while (true)
                    {
                        if (TryRemoveItemFromQueue(out var item))
                            await DoUpdateMultiResolutionFilesAsync(item);
                        else
                            break;
                    }
                });
            }
            return true;
        }

        private async Task DoUpdateMultiResolutionFilesAsync(MapToMultiRez item)
        {
            var path = GetFolder(item.scopeID);
            for (int zoomLevel = 2; zoomLevel <= ZOOM_LEVELS; zoomLevel++)
            {
                if (!CreateTile(zoomLevel, item.x, item.y, path))
                {
                    m_log.WarnFormat("[MAP IMAGE SERVICE]: Unable to create tile for {0},{1} at zoom level {1}", item.x, item.y, zoomLevel);
                    return;
                }
            }
        }

        private class MapToMultiRez
        {
            public int x;
            public int y;
            public UUID scopeID;
        }

        private readonly ConcurrentQueue<MapToMultiRez> m_MultiRezToBuild = new ConcurrentQueue<MapToMultiRez>();

        private bool TryRemoveItemFromQueue(out MapToMultiRez item)
        {
            if (m_MultiRezToBuild.TryDequeue(out item))
                return true;
            return m_MultiRezToBuild.TryPeek(out item) ? false : true;
        }

        public byte[] GetMapTile(string fileName, UUID scopeID, out string format)
        {
            var fullName = Path.Combine(m_TilesStoragePath, scopeID.ToString());
            fullName = Path.Combine(fullName, fileName);
            try
            {
                lock (m_Sync)
                {
                    format = Path.GetExtension(fileName).ToLower();
                    return File.ReadAllBytes(fullName);
                }
            }
            catch
            {
                format = ".jpg";
                return m_WaterJPEGBytes ?? Array.Empty<byte>();
            }
        }

        private string GetFileName(int zoomLevel, int x, int y, UUID scopeID)
        {
            return $"{zoomLevel}-{x}-{y}-objects.jpg";
        }

        private string GetFolder(UUID scopeID)
        {
            var path = Path.Combine(m_TilesStoragePath, scopeID.ToString());
            Directory.CreateDirectory(path);
            return path;
        }

        private Bitmap GetInputTileImage(string fileName)
        {
            try
            {
                lock (m_Sync)
                {
                    if (File.Exists(fileName))
                    {
                        var bm = new Bitmap(fileName);
                        return bm.Width == IMAGE_WIDTH && bm.Height == IMAGE_WIDTH && bm.PixelFormat == PixelFormat.Format24bppRgb ? bm : null;
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Warn($"[MAP IMAGE SERVICE]: Unable to read image data from {fileName}: {e.Message}");
            }

            return null;
        }

        private Bitmap GetOutputTileImage(string fileName)
        {
            try
            {
                lock (m_Sync)
                {
                    return File.Exists(fileName) ? new Bitmap(fileName) : new Bitmap(IMAGE_WIDTH, IMAGE_WIDTH, PixelFormat.Format24bppRgb);
                }
            }
            catch (Exception e)
            {
                m_log.WarnFormat("[MAP IMAGE SERVICE]: Unable to read image data from {0}: {1}", fileName, e);
            }

            return null;
        }

        private bool CreateTile(int zoomLevel, int inx, int iny, string path)
        {
            int previusLevel = zoomLevel - 1;
            int prevStep = 1 << previusLevel - 1;

            int mask = unchecked((int)0xffffffff) << previusLevel;

            // Convert x and y to the bottom left of current tile
            int x = inx & mask;
            int y = iny & mask;

            int ntiles = 0;
            var output = m_WaterBitmap is not null ? m_WaterBitmap.Clone() as Bitmap : new Bitmap(IMAGE_WIDTH, IMAGE_WIDTH, PixelFormat.Format24bppRgb);

            var input = GetInputTileImage($"{(previusLevel - 1)}.{x}.{y}.jpg");
            if (input is not null)
            {
                ImageCopyResampled(output, input, 0, HALF_WIDTH);
                input.Dispose();
                ntiles++;
            }
            input = GetInputTileImage($"{(previusLevel - 1)}.{x + prevStep}.{y}.jpg");
            if (input is not null)
            {
                ImageCopyResampled(output, input, HALF_WIDTH, HALF_WIDTH);
                input.Dispose();
                ntiles++;
            }
            input = GetInputTileImage($"{(previusLevel - 1)}.{x}.{y + prevStep}.jpg");
            if (input is not null)
            {
                ImageCopyResampled(output, input, 0, 0);
                input.Dispose();
                ntiles++;
            }
            input = GetInputTileImage($"{(previusLevel - 1)}.{x + prevStep}.{y + prevStep}.jpg");
            if (input is not null)
            {
                ImageCopyResampled(output, input, HALF_WIDTH, 0);
                input.Dispose();
                ntiles++;
            }

            var outputFile = $"{zoomLevel}-{x}-{y}-objects.jpg";
            try
            {
                lock (m_Sync)
                {
                    File.Delete(outputFile);
                    if (ntiles > 0)
                        output.Save(outputFile, ImageFormat.Jpeg);
                }
            }
            catch (Exception e)
            {
                m_log.Warn($"[MAP IMAGE SERVICE]: Unable to save image {outputFile}: {e.Message}");
            }

            output.Dispose();
            return true;
        }

        private void FillImage(Bitmap bm, Color c)
        {
            var srcData = bm.LockBits(new Rectangle(0, 0, bm.Width, bm.Height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
            var r = c.R;
            var g = c.G;
            var b = c.B;
            try
            {
                var ptr = (byte*)srcData.Scan0;
                for (var y = 0; y < bm.Height; y++)
                {
                    for (var x = 0; x < bm.Width; x++)
                    {
                        *ptr++ = b;
                        *ptr++ = g;
                        *ptr++ = r;