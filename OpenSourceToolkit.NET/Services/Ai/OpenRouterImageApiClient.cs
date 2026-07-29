using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OpenSourceToolkit.NET.Services.Ai
{
    internal static class OpenRouterImageApiClient
    {
        private static readonly HttpClient HttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)
        };

        public static async Task<OpenRouterGeneratedImage> GenerateImageAsync(
            string apiKey,
            string endpoint,
            string modelId,
            string prompt,
            string size,
            string quality,
            IReadOnlyList<(byte[] Data, string MimeType)> inputImages,
            CancellationToken cancellationToken = default)
        {
            var supportedParameters = await GetSupportedParametersAsync(
                apiKey,
                endpoint,
                modelId,
                cancellationToken);
            var payload = CreatePayload(
                modelId,
                prompt,
                size,
                quality,
                inputImages,
                supportedParameters);

            var imagesEndpoint = $"{endpoint.TrimEnd('/')}/images";
            using var request = CreateRequest(HttpMethod.Post, imagesEndpoint, apiKey);
            request.Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            using var response = await HttpClient.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"OpenRouter returned HTTP {(int)response.StatusCode}: {content}");
            }

            return ParseGeneratedImage(content);
        }

        internal static Dictionary<string, object> CreatePayload(
            string modelId,
            string prompt,
            string size,
            string quality,
            IReadOnlyList<(byte[] Data, string MimeType)> inputImages,
            ISet<string> supportedParameters)
        {
            var payload = new Dictionary<string, object>
            {
                ["model"] = modelId,
                ["prompt"] = prompt
            };

            if (!string.IsNullOrWhiteSpace(size) &&
                !string.Equals(size, "auto", StringComparison.OrdinalIgnoreCase))
            {
                if (supportedParameters.Contains("size"))
                {
                    payload["size"] = size;
                }
                else
                {
                    if (supportedParameters.Contains("resolution"))
                        payload["resolution"] = MapResolution(size);

                    if (supportedParameters.Contains("aspect_ratio"))
                        payload["aspect_ratio"] = MapAspectRatio(size);
                }
            }

            if (supportedParameters.Contains("quality") &&
                !string.IsNullOrWhiteSpace(quality))
            {
                payload["quality"] = quality;
            }

            if (supportedParameters.Contains("input_references") && inputImages?.Count > 0)
            {
                payload["input_references"] = inputImages.Select(image =>
                    new Dictionary<string, object>
                    {
                        ["type"] = "image_url",
                        ["image_url"] = new Dictionary<string, string>
                        {
                            ["url"] = $"data:{image.MimeType};base64,{Convert.ToBase64String(image.Data)}"
                        }
                    }).ToArray();
            }

            return payload;
        }

        private static async Task<HashSet<string>> GetSupportedParametersAsync(
            string apiKey,
            string endpoint,
            string modelId,
            CancellationToken cancellationToken)
        {
            var modelsEndpoint = $"{endpoint.TrimEnd('/')}/images/models";
            using var request = CreateRequest(HttpMethod.Get, modelsEndpoint, apiKey);
            using var response = await HttpClient.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"OpenRouter image model discovery returned HTTP {(int)response.StatusCode}: {content}");
            }

            using var document = JsonDocument.Parse(content);
            if (!document.RootElement.TryGetProperty("data", out var data) ||
                data.ValueKind != JsonValueKind.Array)
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            foreach (var model in data.EnumerateArray())
            {
                if (!model.TryGetProperty("id", out var idProperty) ||
                    !string.Equals(idProperty.GetString(), modelId, StringComparison.OrdinalIgnoreCase) ||
                    !model.TryGetProperty("supported_parameters", out var parameters) ||
                    parameters.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                return new HashSet<string>(
                    parameters.EnumerateObject().Select(parameter => parameter.Name),
                    StringComparer.OrdinalIgnoreCase);
            }

            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        private static OpenRouterGeneratedImage ParseGeneratedImage(string content)
        {
            using var document = JsonDocument.Parse(content);
            if (!document.RootElement.TryGetProperty("data", out var data) ||
                data.ValueKind != JsonValueKind.Array ||
                data.GetArrayLength() == 0)
            {
                throw new HttpRequestException("OpenRouter returned no generated image data.");
            }

            foreach (var image in data.EnumerateArray())
            {
                if (!image.TryGetProperty("b64_json", out var base64Property))
                    continue;

                var base64 = base64Property.GetString();
                if (string.IsNullOrWhiteSpace(base64))
                    continue;

                var mimeType = image.TryGetProperty("media_type", out var mediaTypeProperty)
                    ? mediaTypeProperty.GetString()
                    : null;

                if (base64.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    var commaIndex = base64.IndexOf(',');
                    if (commaIndex > 0)
                    {
                        var header = base64.Substring(5, commaIndex - 5);
                        var semicolonIndex = header.IndexOf(';');
                        mimeType = semicolonIndex > 0 ? header.Substring(0, semicolonIndex) : header;
                        base64 = base64.Substring(commaIndex + 1);
                    }
                }

                return new OpenRouterGeneratedImage
                {
                    Data = Convert.FromBase64String(base64),
                    MimeType = string.IsNullOrWhiteSpace(mimeType) ? "image/png" : mimeType
                };
            }

            throw new HttpRequestException("OpenRouter returned no generated image data.");
        }

        private static HttpRequestMessage CreateRequest(HttpMethod method, string url, string apiKey)
        {
            var request = new HttpRequestMessage(method, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            return request;
        }

        private static string MapResolution(string size)
        {
            if (!TryParseSize(size, out var width, out var height))
                return "1K";

            var longestSide = Math.Max(width, height);
            if (longestSide <= 1024)
                return "1K";
            if (longestSide <= 2048)
                return "2K";
            return "4K";
        }

        private static string MapAspectRatio(string size)
        {
            if (!TryParseSize(size, out var width, out var height))
                return "1:1";

            var divisor = GreatestCommonDivisor(width, height);
            return $"{width / divisor}:{height / divisor}";
        }

        private static bool TryParseSize(string size, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (string.IsNullOrWhiteSpace(size))
                return false;

            var dimensions = size.Split('x');
            return dimensions.Length == 2 &&
                   int.TryParse(dimensions[0], out width) &&
                   int.TryParse(dimensions[1], out height) &&
                   width > 0 &&
                   height > 0;
        }

        private static int GreatestCommonDivisor(int left, int right)
        {
            while (right != 0)
            {
                var remainder = left % right;
                left = right;
                right = remainder;
            }
            return Math.Abs(left);
        }
    }

    internal sealed class OpenRouterGeneratedImage
    {
        public byte[] Data { get; set; }
        public string MimeType { get; set; }
    }
}
