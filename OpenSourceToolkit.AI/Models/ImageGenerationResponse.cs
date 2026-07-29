using System.Collections.Generic;

namespace OpenSourceToolkit.AI.Models
{
    /// <summary>
    /// Response from AI image generation
    /// </summary>
    public class ImageGenerationResponse
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }

        /// <summary>
        /// List of generated images
        /// </summary>
        public List<GeneratedImage> Images { get; set; } = new List<GeneratedImage>();

        /// <summary>
        /// Revised prompt (some models may modify the prompt)
        /// </summary>
        public string RevisedPrompt { get; set; }

        /// <summary>
        /// Total tokens used by GPT Image models
        /// </summary>
        public int? TotalTokens { get; set; }

        /// <summary>
        /// Input/prompt tokens used by GPT Image models
        /// </summary>
        public int? InputTokens { get; set; }

        /// <summary>
        /// Output/image tokens used by GPT Image models
        /// </summary>
        public int? OutputTokens { get; set; }

        public static ImageGenerationResponse Success(List<GeneratedImage> images, string revisedPrompt = null) =>
            new ImageGenerationResponse
            {
                IsSuccess = true,
                Images = images ?? new List<GeneratedImage>(),
                RevisedPrompt = revisedPrompt
            };

        public static ImageGenerationResponse Error(string errorMessage) =>
            new ImageGenerationResponse { ErrorMessage = errorMessage, IsSuccess = false };
    }

    /// <summary>
    /// A single generated image
    /// </summary>
    public class GeneratedImage
    {
        /// <summary>
        /// Base64-encoded image data (when response_format is b64_json)
        /// </summary>
        public byte[] Data { get; set; }

        /// <summary>
        /// URL to the generated image (when response_format is url)
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// MIME type of the image (typically image/png)
        /// </summary>
        public string MimeType { get; set; } = "image/png";

        /// <summary>
        /// Revised prompt for this specific image (if available)
        /// </summary>
        public string RevisedPrompt { get; set; }

        /// <summary>
        /// Returns true if this image has inline data
        /// </summary>
        public bool HasData => Data != null && Data.Length > 0;

        /// <summary>
        /// Returns true if this image has a URL
        /// </summary>
        public bool HasUrl => !string.IsNullOrEmpty(Url);
    }
}
