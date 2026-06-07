namespace OpenSourceToolkit.Converters
{
    /// <summary>
    /// Fluent builder for ImageProcessingOptions.
    /// Provides a clean API for constructing options with method chaining.
    /// </summary>
    public class ImageProcessingOptionsBuilder
    {
        private readonly ImageProcessingOptions _options = new ImageProcessingOptions();

        // ═══════════════════════════════════════════════════════════════════════════
        // Output Settings
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Sets the output image format.
        /// </summary>
        /// <param name="format">Output format name, such as "png" or "jpg".</param>
        /// <returns>The current builder instance.</returns>
        public ImageProcessingOptionsBuilder WithFormat(string format)
        {
            _options.Format = format;
            return this;
        }

        /// <summary>
        /// Sets the output image quality.
        /// </summary>
        /// <param name="quality">Output quality value.</param>
        /// <returns>The current builder instance.</returns>
        public ImageProcessingOptionsBuilder WithQuality(int quality)
        {
            _options.Quality = quality;
            return this;
        }

        /// <summary>
        /// Sets resize options for the output image.
        /// </summary>
        /// <param name="width">Optional target width.</param>
        /// <param name="height">Optional target height.</param>
        /// <param name="maintainAspectRatio">Whether to preserve the original aspect ratio.</param>
        /// <returns>The current builder instance.</returns>
        public ImageProcessingOptionsBuilder WithResize(int? width, int? height, bool maintainAspectRatio = true)
        {
            _options.Width = width;
            _options.Height = height;
            _options.MaintainAspectRatio = maintainAspectRatio;
            return this;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Adjustments
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Sets the brightness adjustment.
        /// </summary>
        /// <param name="brightness">Brightness adjustment value.</param>
        /// <returns>The current builder instance.</returns>
        public ImageProcessingOptionsBuilder WithBrightness(int brightness)
        {
            _options.Brightness = brightness;
            return this;
        }

        /// <summary>
        /// Sets the contrast adjustment.
        /// </summary>
        /// <param name="contrast">Contrast adjustment value.</param>
        /// <returns>The current builder instance.</returns>
        public ImageProcessingOptionsBuilder WithContrast(int contrast)
        {
            _options.Contrast = contrast;
            return this;
        }

        /// <summary>
        /// Sets the saturation adjustment.
        /// </summary>
        /// <param name="saturation">Saturation adjustment value.</param>
        /// <returns>The current builder instance.</returns>
        public ImageProcessingOptionsBuilder WithSaturation(int saturation)
        {
            _options.Saturation = saturation;
            return this;
        }

        /// <summary>
        /// Sets brightness, contrast, and saturation adjustments.
        /// </summary>
        /// <param name="brightness">Brightness adjustment value.</param>
        /// <param name="contrast">Contrast adjustment value.</param>
        /// <param name="saturation">Saturation adjustment value.</param>
        /// <returns>The current builder instance.</returns>
        public ImageProcessingOptionsBuilder WithAdjustments(int brightness, int contrast, int saturation)
        {
            _options.Brightness = brightness;
            _options.Contrast = contrast;
            _options.Saturation = saturation;
            return this;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Filters
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Enables or disables grayscale conversion.
        /// </summary>
        /// <param name="enabled">Whether grayscale conversion is enabled.</param>
        /// <returns>The current builder instance.</returns>
        public ImageProcessingOptionsBuilder WithGrayscale(bool enabled = true)
        {
            _options.Grayscale = enabled;
            return this;
        }

        /// <summary>
        /// Enables or disables sepia conversion.
        /// </summary>
        /// <param name="enabled">Whether sepia conversion is enabled.</param>
        /// <returns>The current builder instance.</returns>
        public ImageProcessingOptionsBuilder WithSepia(bool enabled = true)
        {
            _options.Sepia = enabled;
            return this;
        }

        /// <summary>
        /// Enables or disables color inversion.
        /// </summary>
        /// <param name="enabled">Whether color inversion is enabled.</param>
        /// <returns>The current builder instance.</returns>
        public ImageProcessingOptionsBuilder WithInvert(bool enabled = true)
        {
            _options.Invert = enabled;
            return this;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Blur / Sharpen
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Sets the blur radius.
        /// </summary>
        /// <param name="radius">Blur radius value.</param>
        /// <returns>The current builder instance.</returns>
        public ImageProcessingOptionsBuilder WithBlur(int radius)
        {
            _options.BlurRadius = radius;
            return this;
        }

        /// <summary>
        /// Sets the sharpen amount.
        /// </summary>
        /// <param name="amount">Sharpen amount value.</param>
        /// <returns>The current builder instance.</returns>
        public ImageProcessingOptionsBuilder WithSharpen(int amount)
        {
            _options.SharpenAmount = amount;
            return this;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Transform
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Sets the rotation angle.
        /// </summary>
        /// <param name="angle">Rotation angle in degrees.</param>
        /// <returns>The current builder instance.</returns>
        public ImageProcessingOptionsBuilder WithRotation(int angle)
        {
            _options.RotationAngle = angle;
            return this;
        }

        /// <summary>
        /// Enables or disables horizontal flipping.
        /// </summary>
        /// <param name="enabled">Whether horizontal flipping is enabled.</param>
        /// <returns>The current builder instance.</returns>
        public ImageProcessingOptionsBuilder WithFlipHorizontal(bool enabled = true)
        {
            _options.FlipHorizontal = enabled;
            return this;
        }

        /// <summary>
        /// Enables or disables vertical flipping.
        /// </summary>
        /// <param name="enabled">Whether vertical flipping is enabled.</param>
        /// <returns>The current builder instance.</returns>
        public ImageProcessingOptionsBuilder WithFlipVertical(bool enabled = true)
        {
            _options.FlipVertical = enabled;
            return this;
        }

        /// <summary>
        /// Sets rotation and flip options.
        /// </summary>
        /// <param name="rotationAngle">Rotation angle in degrees.</param>
        /// <param name="flipHorizontal">Whether horizontal flipping is enabled.</param>
        /// <param name="flipVertical">Whether vertical flipping is enabled.</param>
        /// <returns>The current builder instance.</returns>
        public ImageProcessingOptionsBuilder WithTransform(int rotationAngle, bool flipHorizontal, bool flipVertical)
        {
            _options.RotationAngle = rotationAngle;
            _options.FlipHorizontal = flipHorizontal;
            _options.FlipVertical = flipVertical;
            return this;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Crop
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Enables cropping and sets the crop rectangle.
        /// </summary>
        /// <param name="x">Crop origin on the x-axis.</param>
        /// <param name="y">Crop origin on the y-axis.</param>
        /// <param name="width">Crop width.</param>
        /// <param name="height">Crop height.</param>
        /// <returns>The current builder instance.</returns>
        public ImageProcessingOptionsBuilder WithCrop(int x, int y, int width, int height)
        {
            _options.CropEnabled = true;
            _options.CropX = x;
            _options.CropY = y;
            _options.CropWidth = width;
            _options.CropHeight = height;
            return this;
        }

        /// <summary>
        /// Disables cropping.
        /// </summary>
        /// <returns>The current builder instance.</returns>
        public ImageProcessingOptionsBuilder WithCropDisabled()
        {
            _options.CropEnabled = false;
            return this;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Watermark
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Enables a text watermark and sets its options.
        /// </summary>
        /// <param name="text">Watermark text.</param>
        /// <param name="position">Watermark position.</param>
        /// <param name="opacity">Watermark opacity.</param>
        /// <param name="fontSize">Watermark font size.</param>
        /// <param name="color">Watermark color.</param>
        /// <param name="padding">Watermark padding.</param>
        /// <returns>The current builder instance.</returns>
        public ImageProcessingOptionsBuilder WithTextWatermark(
            string text,
            WatermarkPosition position = WatermarkPosition.BottomRight,
            int opacity = 50,
            int fontSize = 24,
            string color = "#FFFFFF",
            int padding = 10)
        {
            _options.WatermarkEnabled = true;
            _options.WatermarkText = text;
            _options.WatermarkImageBytes = null;
            _options.WatermarkPosition = position;
            _options.WatermarkOpacity = opacity;
            _options.WatermarkFontSize = fontSize;
            _options.WatermarkColor = color;
            _options.WatermarkPadding = padding;
            return this;
        }

        /// <summary>
        /// Enables an image watermark and sets its options.
        /// </summary>
        /// <param name="imageBytes">Watermark image bytes.</param>
        /// <param name="position">Watermark position.</param>
        /// <param name="opacity">Watermark opacity.</param>
        /// <param name="padding">Watermark padding.</param>
        /// <returns>The current builder instance.</returns>
        public ImageProcessingOptionsBuilder WithImageWatermark(
            byte[] imageBytes,
            WatermarkPosition position = WatermarkPosition.BottomRight,
            int opacity = 50,
            int padding = 10)
        {
            _options.WatermarkEnabled = true;
            _options.WatermarkText = null;
            _options.WatermarkImageBytes = imageBytes;
            _options.WatermarkPosition = position;
            _options.WatermarkOpacity = opacity;
            _options.WatermarkPadding = padding;
            return this;
        }

        /// <summary>
        /// Disables watermarking.
        /// </summary>
        /// <returns>The current builder instance.</returns>
        public ImageProcessingOptionsBuilder WithWatermarkDisabled()
        {
            _options.WatermarkEnabled = false;
            return this;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Phase 3 Effects
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Enables or disables automatic image enhancement.
        /// </summary>
        /// <param name="enabled">Whether automatic enhancement is enabled.</param>
        /// <returns>The current builder instance.</returns>
        public ImageProcessingOptionsBuilder WithAutoEnhance(bool enabled = true)
        {
            _options.AutoEnhance = enabled;
            return this;
        }

        /// <summary>
        /// Enables vignette and sets its options.
        /// </summary>
        /// <param name="radius">Vignette radius.</param>
        /// <param name="softness">Vignette softness.</param>
        /// <returns>The current builder instance.</returns>
        public ImageProcessingOptionsBuilder WithVignette(int radius = 50, int softness = 50)
        {
            _options.Vignette = true;
            _options.VignetteRadius = radius;
            _options.VignetteSoftness = softness;
            return this;
        }

        /// <summary>
        /// Disables vignette.
        /// </summary>
        /// <returns>The current builder instance.</returns>
        public ImageProcessingOptionsBuilder WithVignetteDisabled()
        {
            _options.Vignette = false;
            return this;
        }

        /// <summary>
        /// Enables posterization and sets the number of color levels.
        /// </summary>
        /// <param name="levels">Number of posterization levels.</param>
        /// <returns>The current builder instance.</returns>
        public ImageProcessingOptionsBuilder WithPosterize(int levels = 4)
        {
            _options.Posterize = true;
            _options.PosterizeLevels = levels;
            return this;
        }

        /// <summary>
        /// Disables posterization.
        /// </summary>
        /// <returns>The current builder instance.</returns>
        public ImageProcessingOptionsBuilder WithPosterizeDisabled()
        {
            _options.Posterize = false;
            return this;
        }

        /// <summary>
        /// Enables edge detection and sets its radius.
        /// </summary>
        /// <param name="radius">Edge detection radius.</param>
        /// <returns>The current builder instance.</returns>
        public ImageProcessingOptionsBuilder WithEdgeDetect(int radius = 1)
        {
            _options.EdgeDetect = true;
            _options.EdgeDetectRadius = radius;
            return this;
        }

        /// <summary>
        /// Disables edge detection.
        /// </summary>
        /// <returns>The current builder instance.</returns>
        public ImageProcessingOptionsBuilder WithEdgeDetectDisabled()
        {
            _options.EdgeDetect = false;
            return this;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Background Removal
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Enables background removal and sets its options.
        /// </summary>
        /// <param name="backgroundColor">Background color to remove.</param>
        /// <param name="tolerance">Color matching tolerance.</param>
        /// <returns>The current builder instance.</returns>
        public ImageProcessingOptionsBuilder WithBackgroundRemoval(string backgroundColor = "transparent", int tolerance = 10)
        {
            _options.RemoveBackground = true;
            _options.BackgroundColor = backgroundColor;
            _options.BackgroundTolerance = tolerance;
            return this;
        }

        /// <summary>
        /// Disables background removal.
        /// </summary>
        /// <returns>The current builder instance.</returns>
        public ImageProcessingOptionsBuilder WithBackgroundRemovalDisabled()
        {
            _options.RemoveBackground = false;
            return this;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Metadata
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Enables or disables metadata stripping.
        /// </summary>
        /// <param name="enabled">Whether metadata stripping is enabled.</param>
        /// <returns>The current builder instance.</returns>
        public ImageProcessingOptionsBuilder WithStripMetadata(bool enabled = true)
        {
            _options.StripMetadata = enabled;
            return this;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // ICO Multi-size
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Enables multi-size ICO generation.
        /// </summary>
        /// <param name="sizes">Optional ICO sizes to generate.</param>
        /// <returns>The current builder instance.</returns>
        public ImageProcessingOptionsBuilder WithMultiSizeIco(int[] sizes = null)
        {
            _options.GenerateMultiSizeIco = true;
            if (sizes != null)
            {
                _options.IcoSizes = sizes;
            }
            return this;
        }

        /// <summary>
        /// Disables multi-size ICO generation.
        /// </summary>
        /// <returns>The current builder instance.</returns>
        public ImageProcessingOptionsBuilder WithMultiSizeIcoDisabled()
        {
            _options.GenerateMultiSizeIco = false;
            return this;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Build
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Builds and returns the configured ImageProcessingOptions.
        /// </summary>
        /// <returns>The configured image processing options.</returns>
        public ImageProcessingOptions Build()
        {
            return _options;
        }

        /// <summary>
        /// Creates a new builder instance.
        /// </summary>
        /// <returns>A new image processing options builder.</returns>
        public static ImageProcessingOptionsBuilder Create()
        {
            return new ImageProcessingOptionsBuilder();
        }

        /// <summary>
        /// Creates a builder pre-configured for preview (PNG format, no resize/output settings).
        /// </summary>
        /// <returns>A builder configured for preview output.</returns>
        public static ImageProcessingOptionsBuilder ForPreview()
        {
            return new ImageProcessingOptionsBuilder().WithFormat("png");
        }

        /// <summary>
        /// Creates a builder pre-configured for batch conversion (format and resize only).
        /// </summary>
        /// <param name="format">Output format name.</param>
        /// <param name="quality">Output quality value.</param>
        /// <returns>A builder configured for batch conversion.</returns>
        public static ImageProcessingOptionsBuilder ForBatch(string format, int quality = 90)
        {
            return new ImageProcessingOptionsBuilder()
                .WithFormat(format)
                .WithQuality(quality);
        }

        /// <summary>
        /// Creates options for single image editing with all effect parameters.
        /// </summary>
        /// <param name="format">Output format (e.g., "png", "jpg")</param>
        /// <param name="quality">Output quality value.</param>
        /// <param name="includeResizeAndOutput">Include resize/format/quality settings (false for preview)</param>
        /// <param name="resizeEnabled">Whether resize settings are enabled.</param>
        /// <param name="resizeWidth">Optional resize width.</param>
        /// <param name="resizeHeight">Optional resize height.</param>
        /// <param name="maintainAspectRatio">Whether to preserve the original aspect ratio.</param>
        /// <param name="brightness">Brightness adjustment value.</param>
        /// <param name="contrast">Contrast adjustment value.</param>
        /// <param name="saturation">Saturation adjustment value.</param>
        /// <param name="grayscale">Whether grayscale conversion is enabled.</param>
        /// <param name="sepia">Whether sepia conversion is enabled.</param>
        /// <param name="invert">Whether color inversion is enabled.</param>
        /// <param name="blurRadius">Blur radius value.</param>
        /// <param name="sharpenAmount">Sharpen amount value.</param>
        /// <param name="rotationAngle">Rotation angle in degrees.</param>
        /// <param name="flipHorizontal">Whether horizontal flipping is enabled.</param>
        /// <param name="flipVertical">Whether vertical flipping is enabled.</param>
        /// <param name="cropEnabled">Whether cropping is enabled.</param>
        /// <param name="cropX">Crop origin on the x-axis.</param>
        /// <param name="cropY">Crop origin on the y-axis.</param>
        /// <param name="cropWidth">Crop width.</param>
        /// <param name="cropHeight">Crop height.</param>
        /// <param name="watermarkEnabled">Whether watermarking is enabled.</param>
        /// <param name="watermarkText">Optional watermark text.</param>
        /// <param name="watermarkImageBytes">Optional watermark image bytes.</param>
        /// <param name="watermarkPosition">Watermark position.</param>
        /// <param name="watermarkOpacity">Watermark opacity.</param>
        /// <param name="watermarkFontSize">Watermark font size.</param>
        /// <param name="watermarkColor">Watermark color.</param>
        /// <param name="watermarkPadding">Watermark padding.</param>
        /// <param name="autoEnhance">Whether automatic enhancement is enabled.</param>
        /// <param name="vignette">Whether vignette is enabled.</param>
        /// <param name="vignetteRadius">Vignette radius.</param>
        /// <param name="vignetteSoftness">Vignette softness.</param>
        /// <param name="posterize">Whether posterization is enabled.</param>
        /// <param name="posterizeLevels">Number of posterization levels.</param>
        /// <param name="edgeDetect">Whether edge detection is enabled.</param>
        /// <param name="edgeDetectRadius">Edge detection radius.</param>
        /// <param name="removeBackground">Whether background removal is enabled.</param>
        /// <param name="backgroundColor">Background color to remove.</param>
        /// <param name="backgroundTolerance">Color matching tolerance.</param>
        /// <param name="stripMetadata">Whether metadata stripping is enabled.</param>
        /// <param name="generateMultiSizeIco">Whether multi-size ICO generation is enabled.</param>
        /// <param name="icoSizes">Optional ICO sizes to generate.</param>
        /// <returns>The configured image processing options.</returns>
        public static ImageProcessingOptions BuildSingleImageOptions(
            // Output settings
            string format,
            int quality,
            bool includeResizeAndOutput,
            // Resize
            bool resizeEnabled,
            int? resizeWidth,
            int? resizeHeight,
            bool maintainAspectRatio,
            // Adjustments
            int brightness,
            int contrast,
            int saturation,
            // Filters
            bool grayscale,
            bool sepia,
            bool invert,
            // Blur/Sharpen
            int blurRadius,
            int sharpenAmount,
            // Transform
            int rotationAngle,
            bool flipHorizontal,
            bool flipVertical,
            // Crop
            bool cropEnabled,
            int cropX,
            int cropY,
            int cropWidth,
            int cropHeight,
            // Watermark
            bool watermarkEnabled,
            string watermarkText,
            byte[] watermarkImageBytes,
            WatermarkPosition watermarkPosition,
            int watermarkOpacity,
            int watermarkFontSize,
            string watermarkColor,
            int watermarkPadding,
            // Phase 3 Effects
            bool autoEnhance,
            bool vignette,
            int vignetteRadius,
            int vignetteSoftness,
            bool posterize,
            int posterizeLevels,
            bool edgeDetect,
            int edgeDetectRadius,
            // Background Removal
            bool removeBackground,
            string backgroundColor,
            int backgroundTolerance,
            // Metadata
            bool stripMetadata,
            bool generateMultiSizeIco,
            int[] icoSizes = null)
        {
            var builder = Create()
                .WithAdjustments(brightness, contrast, saturation)
                .WithGrayscale(grayscale)
                .WithSepia(sepia)
                .WithInvert(invert)
                .WithBlur(blurRadius)
                .WithSharpen(sharpenAmount)
                .WithTransform(rotationAngle, flipHorizontal, flipVertical)
                .WithAutoEnhance(autoEnhance);

            // Crop
            if (cropEnabled)
                builder.WithCrop(cropX, cropY, cropWidth, cropHeight);

            // Watermark
            if (watermarkEnabled)
            {
                if (watermarkImageBytes != null && watermarkImageBytes.Length > 0)
                    builder.WithImageWatermark(watermarkImageBytes, watermarkPosition, watermarkOpacity, watermarkPadding);
                else if (!string.IsNullOrEmpty(watermarkText))
                    builder.WithTextWatermark(watermarkText, watermarkPosition, watermarkOpacity, watermarkFontSize, watermarkColor, watermarkPadding);
            }

            // Phase 3 Effects
            if (vignette)
                builder.WithVignette(vignetteRadius, vignetteSoftness);
            if (posterize)
                builder.WithPosterize(posterizeLevels);
            if (edgeDetect)
                builder.WithEdgeDetect(edgeDetectRadius);

            // Background Removal
            if (removeBackground)
                builder.WithBackgroundRemoval(backgroundColor, backgroundTolerance);

            // Output settings
            if (includeResizeAndOutput)
            {
                builder.WithFormat(format)
                       .WithQuality(quality)
                       .WithStripMetadata(stripMetadata);

                if (resizeEnabled)
                    builder.WithResize(resizeWidth, resizeHeight, maintainAspectRatio);

                if (generateMultiSizeIco)
                    builder.WithMultiSizeIco(icoSizes);
            }
            else
            {
                builder.WithFormat("png");
            }

            return builder.Build();
        }
    }
}
