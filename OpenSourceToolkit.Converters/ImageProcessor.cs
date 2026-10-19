using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using ImageMagick;
using OpenCvSharp;

namespace OpenSourceToolkit.Converters
{
    public class ImageProcessingOptions
    {
        public int? Width { get; set; }
        public int? Height { get; set; }
        public bool MaintainAspectRatio { get; set; } = true;
        public string Format { get; set; } = "png";
        public int Quality { get; set; } = 90;

        // Adjustments
        public int Brightness { get; set; } = 0;      // -100 to +100
        public int Contrast { get; set; } = 0;        // -100 to +100
        public int Saturation { get; set; } = 0;      // -100 to +100

        // Filters
        public bool Grayscale { get; set; }
        public bool Sepia { get; set; }
        public bool Invert { get; set; }

        // Blur/Sharpen
        public int BlurRadius { get; set; } = 0;      // 0 = off, 1-20
        public int SharpenAmount { get; set; } = 0;   // 0 = off, 1-100

        // Transform
        public int RotationAngle { get; set; } = 0;   // 0, 90, 180, 270
        public bool FlipHorizontal { get; set; }
        public bool FlipVertical { get; set; }

        // Crop (Phase 2)
        public bool CropEnabled { get; set; }
        public int CropX { get; set; }
        public int CropY { get; set; }
        public int CropWidth { get; set; }
        public int CropHeight { get; set; }

        // Watermark (Phase 2)
        public bool WatermarkEnabled { get; set; }
        public string WatermarkText { get; set; }
        public byte[] WatermarkImageBytes { get; set; }
        public WatermarkPosition WatermarkPosition { get; set; } = WatermarkPosition.BottomRight;
        public int WatermarkOpacity { get; set; } = 50;  // 0-100
        public int WatermarkFontSize { get; set; } = 24;
        public string WatermarkColor { get; set; } = "#FFFFFF";
        public int WatermarkPadding { get; set; } = 10;

        // Metadata (Phase 2)
        public bool StripMetadata { get; set; }

        // ICO Multi-size (Phase 2)
        public bool GenerateMultiSizeIco { get; set; }
        public int[] IcoSizes { get; set; } = new[] { 16, 32, 48, 64, 128, 256 };

        // Phase 3 Filters
        public bool Vignette { get; set; }
        public int VignetteRadius { get; set; } = 50;       // 0-100 (percentage of image size)
        public int VignetteSoftness { get; set; } = 50;     // 0-100

        public bool AutoEnhance { get; set; }               // Auto-level / normalize

        public bool Posterize { get; set; }
        public int PosterizeLevels { get; set; } = 4;       // 2-16 color levels per channel

        public bool EdgeDetect { get; set; }
        public int EdgeDetectRadius { get; set; } = 1;      // 1-5

        // Background Removal (Phase 3 Advanced)
        public bool RemoveBackground { get; set; }
        public string BackgroundColor { get; set; } = "transparent";  // "transparent", "#FFFFFF", "#000000", etc.
        public int BackgroundTolerance { get; set; } = 10;            // 0-50 edge refinement
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // GIF Creation Options (Medium Effort)
    // ═══════════════════════════════════════════════════════════════════════════

    public class GifCreationOptions
    {
        public int FrameDelay { get; set; } = 100;          // Delay in milliseconds between frames
        public bool Loop { get; set; } = true;              // Loop forever
        public int LoopCount { get; set; } = 0;             // 0 = infinite
        public int? ResizeWidth { get; set; }
        public int? ResizeHeight { get; set; }
        public bool OptimizeForSize { get; set; } = true;   // Optimize GIF for smaller file size
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PDF Options (Medium Effort)
    // ═══════════════════════════════════════════════════════════════════════════

    public class PdfToImagesOptions
    {
        public string OutputFormat { get; set; } = "png";
        public int Dpi { get; set; } = 150;                 // Resolution for rendering
        public int? PageStart { get; set; }                 // null = first page
        public int? PageEnd { get; set; }                   // null = last page
    }

    public class ImagesToPdfOptions
    {
        public bool FitToPage { get; set; } = true;
        public double PageWidth { get; set; } = 595;        // A4 width in points (72 dpi)
        public double PageHeight { get; set; } = 842;       // A4 height in points
        public double Margin { get; set; } = 0;
        public int Quality { get; set; } = 90;
    }

    public enum WatermarkPosition
    {
        TopLeft,
        TopCenter,
        TopRight,
        MiddleLeft,
        MiddleCenter,
        MiddleRight,
        BottomLeft,
        BottomCenter,
        BottomRight,
        Tile
    }

    public class ImageProcessor
    {
        /// <summary>
        /// All supported output image formats
        /// </summary>
        public static readonly string[] SupportedFormats = { "png", "jpg", "webp", "bmp", "gif", "tiff", "ico" };

        /// <summary>
        /// File patterns for file dialogs (format -> patterns)
        /// </summary>
        public static readonly Dictionary<string, string[]> FormatPatterns = new Dictionary<string, string[]>
        {
            { "png", new[] { "*.png" } },
            { "jpg", new[] { "*.jpg", "*.jpeg" } },
            { "webp", new[] { "*.webp" } },
            { "bmp", new[] { "*.bmp" } },
            { "gif", new[] { "*.gif" } },
            { "tiff", new[] { "*.tiff", "*.tif" } },
            { "ico", new[] { "*.ico" } }
        };

        /// <summary>
        /// All supported input image patterns for file dialogs
        /// </summary>
        public static readonly string[] SupportedInputPatterns = { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.webp", "*.svg", "*.ico", "*.tiff", "*.tif" };

        public byte[] ProcessImage(byte[] inputBytes, int? width, int? height, bool maintainAspectRatio, string format, int quality = 90)
        {
            var options = new ImageProcessingOptions
            {
                Width = width,
                Height = height,
                MaintainAspectRatio = maintainAspectRatio,
                Format = format,
                Quality = quality
            };
            return ProcessImage(inputBytes, options);
        }

        public byte[] ProcessImage(byte[] inputBytes, ImageProcessingOptions options)
        {
            using (var image = new MagickImage(inputBytes))
            {
                // 1. Transformations (rotate/flip first, before resize)
                ApplyTransformations(image, options);

                // 2. Crop (before resize so dimensions are predictable)
                ApplyCrop(image, options);

                // 3. Resize
                ApplyResize(image, options);

                // 4. Adjustments (brightness, contrast, saturation)
                ApplyAdjustments(image, options);

                // 5. Filters (grayscale, sepia, invert)
                ApplyFilters(image, options);

                // 6. Blur/Sharpen
                ApplyBlurSharpen(image, options);

                // 7. Phase 3 Filters (Vignette, Auto-Enhance, Posterize, Edge Detect)
                ApplyPhase3Filters(image, options);

                // 7b. Background Removal (Phase 3 Advanced)
                if (options.RemoveBackground)
                {
                    ApplyBackgroundRemoval(image, options);
                }

                // 8. Watermark (after all effects, before format conversion)
                ApplyWatermark(image, options);

                // 9. Strip metadata if requested
                if (options.StripMetadata)
                {
                    image.Strip();
                }

                // 10. Handle ICO multi-size generation
                if (options.GenerateMultiSizeIco && options.Format?.ToLowerInvariant() == "ico")
                {
                    return GenerateMultiSizeIco(image, options);
                }

                // 11. Format and Quality
                var targetFormat = GetMagickFormat(options.Format);
                image.Format = targetFormat;

                // 12. Handle transparency / background
                if (IsFormatIdeallyOpaque(targetFormat))
                {
                    image.BackgroundColor = MagickColors.White;
                    image.Alpha(AlphaAction.Remove);
                }

                // 13. Set Quality
                image.Quality = (uint)options.Quality;

                // 14. Output
                return image.ToByteArray();
            }
        }

        private void ApplyTransformations(MagickImage image, ImageProcessingOptions options)
        {
            if (options.RotationAngle != 0)
            {
                image.Rotate(options.RotationAngle);
            }

            if (options.FlipHorizontal)
            {
                image.Flop();
            }

            if (options.FlipVertical)
            {
                image.Flip();
            }
        }

        private void ApplyResize(MagickImage image, ImageProcessingOptions options)
        {
            if (!options.Width.HasValue && !options.Height.HasValue) return;

            int targetWidth = options.Width ?? 0;
            int targetHeight = options.Height ?? 0;

            var geometry = new MagickGeometry((uint)targetWidth, (uint)targetHeight);

            if (options.MaintainAspectRatio)
            {
                geometry.IgnoreAspectRatio = false;

                if (options.Width.HasValue && !options.Height.HasValue)
                {
                    geometry = new MagickGeometry((uint)targetWidth, 0);
                }
                else if (!options.Width.HasValue && options.Height.HasValue)
                {
                    geometry = new MagickGeometry(0, (uint)targetHeight);
                }
            }
            else
            {
                geometry.IgnoreAspectRatio = true;
                if (!options.Width.HasValue) geometry.Width = image.Width;
                if (!options.Height.HasValue) geometry.Height = image.Height;
            }

            image.Resize(geometry);
        }

        private void ApplyAdjustments(MagickImage image, ImageProcessingOptions options)
        {
            // Brightness: Use Modulate for predictable results
            // Modulate brightness: 100 = no change, <100 = darker, >100 = brighter
            // Input range: -100 to +100, we map to 25-175 for visible but not extreme changes
            if (options.Brightness != 0)
            {
                // Scale: -100 maps to 25%, 0 maps to 100%, +100 maps to 175%
                var brightnessPct = new Percentage(100 + (options.Brightness * 0.75));
                image.Modulate(brightnessPct, new Percentage(100), new Percentage(100));
            }

            // Contrast: Use SigmoidalContrast for smooth, natural-looking adjustments
            if (options.Contrast != 0)
            {
                // SigmoidalContrast(contrast, midpoint) - positive increases, negative decreases
                // Range: -100 to +100 maps to -10 to +10 for visible changes
                double contrastAmount = options.Contrast * 0.10; // Range: -10 to +10
                image.SigmoidalContrast(contrastAmount, new Percentage(50));
            }

            // Saturation: Use Modulate
            // 100 = no change, 0 = grayscale, 200 = double saturation
            if (options.Saturation != 0)
            {
                var saturation = new Percentage(100 + options.Saturation);
                image.Modulate(new Percentage(100), saturation, new Percentage(100));
            }
        }

        private void ApplyFilters(MagickImage image, ImageProcessingOptions options)
        {
            if (options.Grayscale)
            {
                image.Grayscale(PixelIntensityMethod.Rec709Luminance);
            }

            if (options.Sepia)
            {
                image.SepiaTone(new Percentage(80));
            }

            if (options.Invert)
            {
                image.Negate(Channels.RGB);
            }
        }

        private void ApplyBlurSharpen(MagickImage image, ImageProcessingOptions options)
        {
            if (options.BlurRadius > 0)
            {
                image.GaussianBlur(options.BlurRadius, options.BlurRadius / 2.0);
            }

            if (options.SharpenAmount > 0)
            {
                double sigma = options.SharpenAmount / 20.0;
                image.Sharpen(0, sigma);
            }
        }

        private void ApplyPhase3Filters(MagickImage image, ImageProcessingOptions options)
        {
            // Auto-Enhance (normalize / auto-level)
            if (options.AutoEnhance)
            {
                image.Normalize();
            }

            // Vignette effect
            if (options.Vignette)
            {
                // Vignette(radius, sigma, x, y) - x,y are offsets from center (0,0 = centered)
                // radius controls the inner unaffected area, sigma controls the falloff softness
                double maxDim = Math.Max(image.Width, image.Height);
                double radius = maxDim * (options.VignetteRadius / 200.0);
                double sigma = maxDim * (options.VignetteSoftness / 200.0);
                image.Vignette(radius, sigma, 0, 0);
            }

            // Posterize (color quantization)
            if (options.Posterize)
            {
                int levels = Math.Max(2, Math.Min(16, options.PosterizeLevels));
                image.Posterize(levels);
            }

            // Edge Detection
            if (options.EdgeDetect)
            {
                double radius = Math.Max(1, Math.Min(5, options.EdgeDetectRadius));
                image.CannyEdge(radius, radius * 2, new Percentage(10), new Percentage(30));
            }
        }

        private void ApplyBackgroundRemoval(MagickImage image, ImageProcessingOptions options)
        {
            // Convert MagickImage to OpenCV Mat
            byte[] pngBytes = image.ToByteArray(MagickFormat.Png);

            using (var mat = Mat.FromImageData(pngBytes, ImreadModes.Color))
            using (var mask = new Mat())
            using (var bgdModel = new Mat())
            using (var fgdModel = new Mat())
            {
                // Define a rectangle that likely contains the foreground
                // We use a margin from the edges (tolerance controls this)
                int margin = Math.Max(1, (int)(Math.Min(mat.Width, mat.Height) * options.BackgroundTolerance / 100.0));
                var rect = new OpenCvSharp.Rect(
                    margin,
                    margin,
                    mat.Width - margin * 2,
                    mat.Height - margin * 2
                );

                // Ensure rect is valid
                if (rect.Width <= 0 || rect.Height <= 0)
                {
                    rect = new OpenCvSharp.Rect(1, 1, mat.Width - 2, mat.Height - 2);
                }

                // Run GrabCut algorithm
                Cv2.GrabCut(mat, mask, rect, bgdModel, fgdModel, 5, GrabCutModes.InitWithRect);

                // Create foreground mask: keep pixels marked as foreground or probable foreground
                using (var fgMask = new Mat())
                {
                    // GrabCut returns: 0=GC_BGD, 1=GC_FGD, 2=GC_PR_BGD, 3=GC_PR_FGD
                    // We want to keep 1 and 3 (foreground and probable foreground)
                    const int GC_FGD = 1;
                    const int GC_PR_FGD = 3;
                    Cv2.Compare(mask, new Scalar(GC_FGD), fgMask, CmpTypes.EQ);
                    using (var prFgMask = new Mat())
                    {
                        Cv2.Compare(mask, new Scalar(GC_PR_FGD), prFgMask, CmpTypes.EQ);
                        Cv2.BitwiseOr(fgMask, prFgMask, fgMask);
                    }

                    // Convert to BGRA for transparency support
                    using (var bgra = new Mat())
                    {
                        Cv2.CvtColor(mat, bgra, ColorConversionCodes.BGR2BGRA);

                        // Apply the mask to alpha channel
                        var channels = Cv2.Split(bgra);
                        try
                        {
                            // Set alpha channel based on mask
                            fgMask.CopyTo(channels[3]);

                            // Merge channels back
                            using (var result = new Mat())
                            {
                                Cv2.Merge(channels, result);

                                // If a specific background color is requested (not transparent)
                                if (!string.IsNullOrEmpty(options.BackgroundColor) &&
                                    !options.BackgroundColor.Equals("transparent", StringComparison.OrdinalIgnoreCase))
                                {
                                    ApplyBackgroundColor(result, fgMask, options.BackgroundColor);
                                }

                                // Convert back to MagickImage
                                byte[] resultBytes = result.ToBytes(".png");
                                using (var newImage = new MagickImage(resultBytes))
                                {
                                    // Copy the result back to the original image
                                    image.Read(newImage.ToByteArray());
                                }
                            }
                        }
                        finally
                        {
                            foreach (var channel in channels)
                            {
                                channel?.Dispose();
                            }
                        }
                    }
                }
            }
        }

        private void ApplyBackgroundColor(Mat bgra, Mat fgMask, string colorHex)
        {
            // Parse the hex color
            var magickColor = new MagickColor(colorHex);
            byte r = (byte)(magickColor.R / 257); // MagickColor uses 16-bit, convert to 8-bit
            byte g = (byte)(magickColor.G / 257);
            byte b = (byte)(magickColor.B / 257);

            // Create background color mat
            using (var bgColor = new Mat(bgra.Size(), MatType.CV_8UC4, new Scalar(b, g, r, 255)))
            using (var invertedMask = new Mat())
            {
                // Invert the foreground mask to get background mask
                Cv2.BitwiseNot(fgMask, invertedMask);

                // Copy background color where mask is 0 (background)
                bgColor.CopyTo(bgra, invertedMask);
            }
        }

        private void ApplyCrop(MagickImage image, ImageProcessingOptions options)
        {
            if (!options.CropEnabled) return;
            if (options.CropWidth <= 0 || options.CropHeight <= 0) return;

            int x = Math.Max(0, Math.Min(options.CropX, (int)image.Width - 1));
            int y = Math.Max(0, Math.Min(options.CropY, (int)image.Height - 1));
            int w = Math.Min(options.CropWidth, (int)image.Width - x);
            int h = Math.Min(options.CropHeight, (int)image.Height - y);

            if (w > 0 && h > 0)
            {
                image.Crop(new MagickGeometry(x, y, (uint)w, (uint)h));
                image.Page = new MagickGeometry(0, 0, image.Width, image.Height);
            }
        }

        private void ApplyWatermark(MagickImage image, ImageProcessingOptions options)
        {
            if (!options.WatermarkEnabled) return;

            bool hasText = !string.IsNullOrWhiteSpace(options.WatermarkText);
            bool hasImage = options.WatermarkImageBytes != null && options.WatermarkImageBytes.Length > 0;

            if (!hasText && !hasImage) return;

            double opacity = options.WatermarkOpacity / 100.0;

            if (hasImage)
            {
                ApplyImageWatermark(image, options, opacity);
            }
            else if (hasText)
            {
                ApplyTextWatermark(image, options, opacity);
            }
        }

        private void ApplyImageWatermark(MagickImage image, ImageProcessingOptions options, double opacity)
        {
            using (var watermark = new MagickImage(options.WatermarkImageBytes))
            {
                watermark.Evaluate(Channels.Alpha, EvaluateOperator.Multiply, opacity);

                if (options.WatermarkPosition == WatermarkPosition.Tile)
                {
                    ApplyTiledWatermark(image, watermark, options.WatermarkPadding);
                }
                else
                {
                    var (x, y) = CalculateWatermarkPosition(
                        (int)image.Width, (int)image.Height,
                        (int)watermark.Width, (int)watermark.Height,
                        options.WatermarkPosition, options.WatermarkPadding);

                    image.Composite(watermark, x, y, CompositeOperator.Over);
                }
            }
        }

        private void ApplyTextWatermark(MagickImage image, ImageProcessingOptions options, double opacity)
        {
            var color = new MagickColor(options.WatermarkColor ?? "#FFFFFF");
            color.A = (byte)(opacity * 255);

            var settings = new MagickReadSettings
            {
                Font = "Arial",
                FontPointsize = options.WatermarkFontSize,
                BackgroundColor = MagickColors.Transparent,
                FillColor = color
            };

            using (var textImage = new MagickImage($"label:{options.WatermarkText}", settings))
            {
                if (options.WatermarkPosition == WatermarkPosition.Tile)
                {
                    ApplyTiledWatermark(image, textImage, options.WatermarkPadding);
                }
                else
                {
                    var (x, y) = CalculateWatermarkPosition(
                        (int)image.Width, (int)image.Height,
                        (int)textImage.Width, (int)textImage.Height,
                        options.WatermarkPosition, options.WatermarkPadding);

                    image.Composite(textImage, x, y, CompositeOperator.Over);
                }
            }
        }

        private void ApplyTiledWatermark(MagickImage image, MagickImage watermark, int padding)
        {
            int stepX = (int)watermark.Width + padding;
            int stepY = (int)watermark.Height + padding;

            for (int y = padding; y < image.Height; y += stepY)
            {
                for (int x = padding; x < image.Width; x += stepX)
                {
                    image.Composite(watermark, x, y, CompositeOperator.Over);
                }
            }
        }

        private (int x, int y) CalculateWatermarkPosition(int imageWidth, int imageHeight, int wmWidth, int wmHeight, WatermarkPosition position, int padding)
        {
            int x = padding;
            int y = padding;

            switch (position)
            {
                case WatermarkPosition.TopLeft:
                    x = padding;
                    y = padding;
                    break;
                case WatermarkPosition.TopCenter:
                    x = (imageWidth - wmWidth) / 2;
                    y = padding;
                    break;
                case WatermarkPosition.TopRight:
                    x = imageWidth - wmWidth - padding;
                    y = padding;
                    break;
                case WatermarkPosition.MiddleLeft:
                    x = padding;
                    y = (imageHeight - wmHeight) / 2;
                    break;
                case WatermarkPosition.MiddleCenter:
                    x = (imageWidth - wmWidth) / 2;
                    y = (imageHeight - wmHeight) / 2;
                    break;
                case WatermarkPosition.MiddleRight:
                    x = imageWidth - wmWidth - padding;
                    y = (imageHeight - wmHeight) / 2;
                    break;
                case WatermarkPosition.BottomLeft:
                    x = padding;
                    y = imageHeight - wmHeight - padding;
                    break;
                case WatermarkPosition.BottomCenter:
                    x = (imageWidth - wmWidth) / 2;
                    y = imageHeight - wmHeight - padding;
                    break;
                case WatermarkPosition.BottomRight:
                    x = imageWidth - wmWidth - padding;
                    y = imageHeight - wmHeight - padding;
                    break;
            }

            return (Math.Max(0, x), Math.Max(0, y));
        }

        private byte[] GenerateMultiSizeIco(MagickImage sourceImage, ImageProcessingOptions options)
        {
            var sizes = options.IcoSizes ?? new[] { 16, 32, 48, 64, 128, 256 };

            using (var collection = new MagickImageCollection())
            {
                foreach (var size in sizes)
                {
                    using (var clone = sourceImage.Clone())
                    {
                        clone.Resize(new MagickGeometry((uint)size, (uint)size)
                        {
                            IgnoreAspectRatio = false
                        });
                        clone.Format = MagickFormat.Png;
                        collection.Add(new MagickImage(clone.ToByteArray()));
                    }
                }

                using (var ms = new MemoryStream())
                {
                    collection.Write(ms, MagickFormat.Ico);
                    return ms.ToArray();
                }
            }
        }

        public ImageMetadata GetMetadata(byte[] inputBytes)
        {
            using (var image = new MagickImage(inputBytes))
            {
                var metadata = new ImageMetadata
                {
                    Width = (int)image.Width,
                    Height = (int)image.Height,
                    Format = image.Format.ToString(),
                    ColorSpace = image.ColorSpace.ToString(),
                    HasAlpha = image.HasAlpha,
                    Depth = (int)image.Depth
                };

                var profile = image.GetExifProfile();
                if (profile != null)
                {
                    foreach (var value in profile.Values)
                    {
                        var tagValue = value.GetValue();
                        if (tagValue != null)
                        {
                            metadata.ExifData[value.Tag.ToString()] = tagValue.ToString();
                        }
                    }
                }

                return metadata;
            }
        }

        private bool IsFormatIdeallyOpaque(MagickFormat format)
        {
            return format == MagickFormat.Jpeg ||
                   format == MagickFormat.Jpg ||
                   format == MagickFormat.Bmp ||
                   format == MagickFormat.Bmp2 ||
                   format == MagickFormat.Bmp3;
        }

        private MagickFormat GetMagickFormat(string format)
        {
            switch (format?.ToLowerInvariant())
            {
                case "png": return MagickFormat.Png;
                case "jpeg":
                case "jpg": return MagickFormat.Jpeg;
                case "bmp": return MagickFormat.Bmp;
                case "gif": return MagickFormat.Gif;
                case "tiff": return MagickFormat.Tiff;
                case "webp": return MagickFormat.WebP;
                case "ico": return MagickFormat.Ico;
                case "svg": return MagickFormat.Svg;
                default: return MagickFormat.Png;
            }
        }

        /// <summary>
        /// Converts any supported image to a PNG byte array at full resolution.
        /// </summary>
        public byte[] ConvertToPng(byte[] inputBytes)
        {
            using (var image = new MagickImage(inputBytes))
            {
                image.Format = MagickFormat.Png;
                return image.ToByteArray();
            }
        }

        /// <summary>
        /// Converts image to PNG scaled down for AI API calls (max 1024x1024).
        /// </summary>
        public byte[] ConvertToAiPng(byte[] inputBytes, int maxSize = 1024)
        {
            using (var image = new MagickImage(inputBytes))
            {
                if (image.Width > maxSize || image.Height > maxSize)
                {
                    image.Resize((uint)maxSize, (uint)maxSize);
                }

                image.Format = MagickFormat.Png;
                return image.ToByteArray();
            }
        }

        /// <summary>
        /// Helper to convert any supported image to a PNG byte array (useful for UI previews).
        /// Now uses full resolution - use ConvertToAiPng for AI API calls.
        /// </summary>
        public byte[] ConvertToPreviewPng(byte[] inputBytes)
        {
            return ConvertToPng(inputBytes);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Animated GIF Creation (Medium Effort)
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Creates an animated GIF from multiple images
        /// </summary>
        public byte[] CreateAnimatedGif(List<byte[]> imageFrames, GifCreationOptions options)
        {
            if (imageFrames == null || imageFrames.Count == 0)
                throw new ArgumentException("At least one image frame is required");

            using (var collection = new MagickImageCollection())
            {
                foreach (var frameBytes in imageFrames)
                {
                    var frame = new MagickImage(frameBytes);

                    // Resize if specified
                    if (options.ResizeWidth.HasValue || options.ResizeHeight.HasValue)
                    {
                        var geometry = new MagickGeometry(
                            (uint)(options.ResizeWidth ?? 0),
                            (uint)(options.ResizeHeight ?? 0));
                        geometry.IgnoreAspectRatio = false;
                        frame.Resize(geometry);
                    }

                    // Set frame delay (in 1/100ths of a second)
                    frame.AnimationDelay = (uint)(options.FrameDelay / 10);
                    frame.GifDisposeMethod = GifDisposeMethod.Background;

                    collection.Add(frame);
                }

                // Set loop count
                collection[0].AnimationIterations = options.Loop ? (uint)options.LoopCount : 1;

                // Optimize if requested
                if (options.OptimizeForSize)
                {
                    collection.Optimize();
                }

                // Quantize to reduce colors (GIF supports max 256 colors)
                collection.Quantize(new QuantizeSettings { Colors = 256 });

                using (var ms = new MemoryStream())
                {
                    collection.Write(ms, MagickFormat.Gif);
                    return ms.ToArray();
                }
            }
        }

        /// <summary>
        /// Creates an animated GIF from image file paths
        /// </summary>
        public byte[] CreateAnimatedGifFromFiles(List<string> filePaths, GifCreationOptions options)
        {
            var frames = new List<byte[]>();
            foreach (var path in filePaths)
            {
                if (File.Exists(path))
                {
                    frames.Add(File.ReadAllBytes(path));
                }
            }
            return CreateAnimatedGif(frames, options);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // PDF to Images (Medium Effort)
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Extracts pages from a PDF as individual images
        /// </summary>
        public List<byte[]> PdfToImages(byte[] pdfBytes, PdfToImagesOptions options)
        {
            var result = new List<byte[]>();

            var settings = new MagickReadSettings
            {
                Density = new Density(options.Dpi)
            };

            using (var collection = new MagickImageCollection(pdfBytes, settings))
            {
                int startPage = (options.PageStart ?? 1) - 1;
                int endPage = options.PageEnd.HasValue ? Math.Min(options.PageEnd.Value - 1, collection.Count - 1) : collection.Count - 1;

                startPage = Math.Max(0, startPage);
                endPage = Math.Min(endPage, collection.Count - 1);

                for (int i = startPage; i <= endPage; i++)
                {
                    var page = collection[i];
                    page.Format = GetMagickFormat(options.OutputFormat);

                    // Flatten to remove transparency (PDFs often have transparent backgrounds)
                    page.BackgroundColor = MagickColors.White;
                    page.Alpha(AlphaAction.Remove);

                    result.Add(page.ToByteArray());
                }
            }

            return result;
        }

        /// <summary>
        /// Gets the number of pages in a PDF
        /// </summary>
        public int GetPdfPageCount(byte[] pdfBytes)
        {
            var settings = new MagickReadSettings
            {
                Density = new Density(72) // Low density just for counting
            };

            using (var collection = new MagickImageCollection(pdfBytes, settings))
            {
                return collection.Count;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Images to PDF (Medium Effort)
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Combines multiple images into a single PDF
        /// </summary>
        public byte[] ImagesToPdf(List<byte[]> imageBytes, ImagesToPdfOptions options)
        {
            if (imageBytes == null || imageBytes.Count == 0)
                throw new ArgumentException("At least one image is required");

            using (var collection = new MagickImageCollection())
            {
                foreach (var imgBytes in imageBytes)
                {
                    var image = new MagickImage(imgBytes);

                    if (options.FitToPage)
                    {
                        // Calculate available area (page size minus margins)
                        double availableWidth = options.PageWidth - (options.Margin * 2);
                        double availableHeight = options.PageHeight - (options.Margin * 2);

                        // Scale image to fit within available area
                        double scaleX = availableWidth / image.Width;
                        double scaleY = availableHeight / image.Height;
                        double scale = Math.Min(scaleX, scaleY);

                        if (scale < 1.0)
                        {
                            int newWidth = (int)(image.Width * scale);
                            int newHeight = (int)(image.Height * scale);
                            image.Resize(new MagickGeometry((uint)newWidth, (uint)newHeight));
                        }
                    }

                    // Set page geometry for PDF
                    image.Page = new MagickGeometry((int)options.Margin, (int)options.Margin, image.Width, image.Height);

                    // Set quality
                    image.Quality = (uint)options.Quality;

                    collection.Add(image);
                }

                using (var ms = new MemoryStream())
                {
                    collection.Write(ms, MagickFormat.Pdf);
                    return ms.ToArray();
                }
            }
        }

        /// <summary>
        /// Combines image files into a single PDF
        /// </summary>
        public byte[] ImagesToPdfFromFiles(List<string> filePaths, ImagesToPdfOptions options)
        {
            var images = new List<byte[]>();
            foreach (var path in filePaths)
            {
                if (File.Exists(path))
                {
                    images.Add(File.ReadAllBytes(path));
                }
            }
            return ImagesToPdf(images, options);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Batch Rename Pattern (Medium Effort)
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Generates an output filename based on a pattern template
        /// Supported placeholders:
        /// {name} - Original filename without extension
        /// {ext} - Original extension
        /// {width} - Image width after processing
        /// {height} - Image height after processing
        /// {date} - Current date (yyyy-MM-dd)
        /// {time} - Current time (HH-mm-ss)
        /// {index} - Sequential index (for batch operations)
        /// {format} - Output format
        /// </summary>
        public static string GenerateOutputFilename(string pattern, string originalPath, int width, int height, string outputFormat, int index = 0)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                pattern = "{name}_converted";
            }

            string originalName = Path.GetFileNameWithoutExtension(originalPath);
            string originalExt = Path.GetExtension(originalPath).TrimStart('.');
            var now = DateTime.Now;

            string result = pattern
                .Replace("{name}", originalName)
                .Replace("{ext}", originalExt)
                .Replace("{width}", width.ToString())
                .Replace("{height}", height.ToString())
                .Replace("{date}", now.ToString("yyyy-MM-dd"))
                .Replace("{time}", now.ToString("HH-mm-ss"))
                .Replace("{index}", index.ToString("D3"))
                .Replace("{format}", outputFormat);

            // Sanitize filename - remove invalid characters
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                result = result.Replace(c.ToString(), "_");
            }

            return result;
        }

        /// <summary>
        /// Gets available rename pattern placeholders with descriptions
        /// </summary>
        public static Dictionary<string, string> GetRenamePatternPlaceholders()
        {
            return new Dictionary<string, string>
            {
                { "{name}", "Original filename (without extension)" },
                { "{ext}", "Original file extension" },
                { "{width}", "Output image width" },
                { "{height}", "Output image height" },
                { "{date}", "Current date (yyyy-MM-dd)" },
                { "{time}", "Current time (HH-mm-ss)" },
                { "{index}", "Sequential index (001, 002, ...)" },
                { "{format}", "Output format" }
            };
        }
    }

    public class ImageMetadata
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public string Format { get; set; }
        public string ColorSpace { get; set; }
        public bool HasAlpha { get; set; }
        public int Depth { get; set; }
        public System.Collections.Generic.Dictionary<string, string> ExifData { get; set; } = new System.Collections.Generic.Dictionary<string, string>();
    }
}
