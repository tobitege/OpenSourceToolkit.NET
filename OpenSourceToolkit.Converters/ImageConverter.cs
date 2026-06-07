using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.Versioning;

namespace OpenSourceToolkit.Converters
{
    /// <summary>
    /// Provides basic image conversion helpers for file and byte-array inputs.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static class ImageConverter
    {
        /// <summary>
        /// Converts an image file to the specified output format.
        /// </summary>
        /// <param name="inputPath">Path to the source image file.</param>
        /// <param name="outputPath">Path where the converted image should be saved.</param>
        /// <param name="format">Image format to use for the output file.</param>
        public static void Convert(string inputPath, string outputPath, ImageFormat format)
        {
            using (var image = Image.FromFile(inputPath))
            {
                image.Save(outputPath, format);
            }
        }

        /// <summary>
        /// Converts image bytes to the specified output format.
        /// </summary>
        /// <param name="inputBytes">Source image bytes.</param>
        /// <param name="format">Image format to use for the converted bytes.</param>
        /// <returns>The converted image bytes.</returns>
        public static byte[] Convert(byte[] inputBytes, ImageFormat format)
        {
            using (var inputStream = new MemoryStream(inputBytes))
            using (var image = Image.FromStream(inputStream))
            using (var outputStream = new MemoryStream())
            {
                image.Save(outputStream, format);
                return outputStream.ToArray();
            }
        }
    }
}
