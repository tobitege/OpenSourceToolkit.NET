using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using OpenSourceToolkit.AI.Models;

namespace OpenSourceToolkit.AI.Providers
{
    public sealed class HuggingFaceProvider : OpenAiCompatibleProvider
    {
        private const string ImageInferenceBaseUrl =
            "https://router.huggingface.co/hf-inference/models";

        public override bool SupportsImageGeneration => true;

        public HuggingFaceProvider(AiProviderSettings settings) : base(settings)
        {
        }

        public override async Task<ImageGenerationResponse> GenerateImageAsync(
            ImageGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            var modelId = request.Model;
            if (string.IsNullOrWhiteSpace(modelId))
                return ImageGenerationResponse.Error("A Hugging Face image model is required.");

            var parameters = new Dictionary<string, object>();
            if (TryParseSize(request.Size, out var width, out var height))
            {
                parameters["width"] = width;
                parameters["height"] = height;
            }

            var payload = new Dictionary<string, object>
            {
                ["inputs"] = request.Prompt
            };
            if (parameters.Count > 0)
                payload["parameters"] = parameters;

            var endpoint = $"{ImageInferenceBaseUrl}/{EscapeModelId(modelId)}";
            using (var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint))
            {
                httpRequest.Content = CreateJsonContent(payload);

                using (var response = await HttpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        return ImageGenerationResponse.Error($"HTTP {(int)response.StatusCode}: {error}");
                    }

                    var imageData = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    var mimeType = response.Content.Headers.ContentType?.MediaType ?? "image/png";
                    return ImageGenerationResponse.Success(new List<GeneratedImage>
                    {
                        new GeneratedImage
                        {
                            Data = imageData,
                            MimeType = mimeType
                        }
                    });
                }
            }
        }

        private static string EscapeModelId(string modelId)
        {
            var segments = modelId.Split('/');
            for (var index = 0; index < segments.Length; index++)
                segments[index] = Uri.EscapeDataString(segments[index]);
            return string.Join("/", segments);
        }

        private static bool TryParseSize(string size, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (string.IsNullOrWhiteSpace(size))
                return false;

            var dimensions = size.Split('x');
            return dimensions.Length == 2
                && int.TryParse(dimensions[0], out width)
                && int.TryParse(dimensions[1], out height)
                && width > 0
                && height > 0;
        }
    }
}
