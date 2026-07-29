namespace OpenSourceToolkit.AI.Models
{
    /// <summary>
    /// Request for AI image generation
    /// </summary>
    public class ImageGenerationRequest
    {
        /// <summary>
        /// The text prompt describing the image to generate (max 32000 chars).
        /// </summary>
        public string Prompt { get; set; }

        /// <summary>
        /// Image size: "1024x1024", "1536x1024" (landscape), "1024x1536" (portrait), or "auto"
        /// </summary>
        public string Size { get; set; }

        /// <summary>
        /// Quality level: "high", "medium", "low", or "auto" (default)
        /// </summary>
        public string Quality { get; set; }

        /// <summary>
        /// Number of images to generate (1-10).
        /// </summary>
        public int Count { get; set; } = 1;

        /// <summary>
        /// Style hint: "vivid" or "natural" (not supported by all models)
        /// </summary>
        public string Style { get; set; }

        /// <summary>
        /// Model to use for generation. If null, uses provider default.
        /// OpenAI: "gpt-image-2"
        /// </summary>
        public string Model { get; set; }

        /// <summary>
        /// Aspect ratio for image generation (OpenRouter/Gemini models).
        /// Supported: "1:1", "2:3", "3:2", "3:4", "4:3", "4:5", "5:4", "9:16", "16:9", "21:9"
        /// </summary>
        public string AspectRatio { get; set; }

        /// <summary>
        /// Background transparency for GPT Image models: "transparent", "opaque", or "auto"
        /// If transparent, OutputFormat should be "png" or "webp".
        /// </summary>
        public string Background { get; set; }

        /// <summary>
        /// Output format for GPT Image models: "png", "jpeg", or "webp"
        /// </summary>
        public string OutputFormat { get; set; }

        /// <summary>
        /// Compression level 0-100% for GPT Image models with webp/jpeg output. Default 100.
        /// </summary>
        public int? OutputCompression { get; set; }

        /// <summary>
        /// Content moderation level for GPT Image models: "low" or "auto"
        /// </summary>
        public string Moderation { get; set; }

        /// <summary>
        /// Optional input image for image-to-image generation or editing.
        /// The AI will see this image along with the prompt.
        /// </summary>
        public byte[] InputImage { get; set; }

        /// <summary>
        /// MIME type of the input image (e.g. "image/png", "image/jpeg")
        /// </summary>
        public string InputImageMimeType { get; set; }

        public ImageGenerationRequest() { }

        public ImageGenerationRequest(string prompt)
        {
            Prompt = prompt;
        }

        public ImageGenerationRequest(string prompt, string size) : this(prompt)
        {
            Size = size;
        }
    }
}
