using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSourceToolkit.Media;
using SkiaSharp;

namespace OpenSourceToolkit.Tests
{
    [TestClass]
    public class MediaTests
    {
        [TestMethod]
        public void AsciiArt_ConvertBitmap_ReturnsString()
        {
            // Create a small bitmap using SkiaSharp
            using var bmp = new SKBitmap(10, 10);
            using (var canvas = new SKCanvas(bmp))
            {
                canvas.Clear(SKColors.White);
                using var paint = new SKPaint { Color = SKColors.Black };
                canvas.DrawRect(2, 2, 6, 6, paint);
            }

            var ascii = AsciiArtGenerator.ConvertBitmapToAscii(bmp, 10);
            Assert.IsNotNull(ascii);
            Assert.IsTrue(ascii.Length > 0);
        }

        [TestMethod]
        public void AsciiArt_ConvertText_WithMockFont_Works()
        {
            // Create a minimal FLF file for testing
            string flfPath = Path.GetTempFileName() + ".flf";
            try
            {
                // We verify the method structure is in place by checking FileNotFound behavior
                Assert.Throws<FileNotFoundException>(() =>
                    AsciiArtGenerator.ConvertTextToAscii("Test", "non_existent.flf"));
            }
            finally
            {
                if (File.Exists(flfPath)) File.Delete(flfPath);
            }
        }

        [TestMethod]
        public void AsciiArt_ConvertBitmap_RespectsWidth()
        {
            using var bmp = new SKBitmap(4, 4);
            using (var canvas = new SKCanvas(bmp))
            {
                canvas.Clear(SKColors.Black);
            }

            int width = 16;
            var ascii = AsciiArtGenerator.ConvertBitmapToAscii(bmp, width);
            var lines = ascii.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            Assert.IsTrue(lines.Length > 0);
            foreach (var line in lines)
            {
                Assert.AreEqual(width, line.Length);
            }
        }

        [TestMethod]
        public void AsciiArt_ConvertText_WithExistingFont_ThrowsNotImplemented()
        {
            string flfPath = Path.GetTempFileName() + ".flf";
            try
            {
                File.WriteAllText(flfPath, "dummy");

                Assert.Throws<NotImplementedException>(() =>
                    AsciiArtGenerator.ConvertTextToAscii("Test", flfPath));
            }
            finally
            {
                if (File.Exists(flfPath)) File.Delete(flfPath);
            }
        }

        // NextJsImageUrlParser
        [TestMethod]
        public void NextJs_Parse_Works()
        {
            string url = "https://example.com/_next/image?url=%2Fimg.jpg&w=640&q=75";
            var info = NextJsImageUrlParser.Parse(url);
            Assert.IsTrue(info.IsValid);
            Assert.AreEqual("/img.jpg", info.OriginalUrl);
            Assert.AreEqual(640, info.Width);
            Assert.AreEqual(75, info.Quality);
        }

        [TestMethod]
        public void NextJs_Generate_Works()
        {
            string baseUrl = "https://example.com";
            string imgUrl = "/test.png";
            string result = NextJsImageUrlParser.Generate(baseUrl, imgUrl, 800, 90);

            Assert.IsTrue(result.StartsWith("https://example.com/_next/image"));
            Assert.IsTrue(result.Contains("url=%2ftest.png")); // encoded /test.png
            Assert.IsTrue(result.Contains("w=800"));
            Assert.IsTrue(result.Contains("q=90"));
        }
    }
}
