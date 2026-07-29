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
    internal static class HuggingFaceApiClient
    {
        private const string WhoAmIUrl = "https://huggingface.co/api/whoami-v2";
        private const string ImageModelsUrl =
            "https://huggingface.co/api/models?inference_provider=hf-inference&pipeline_tag=text-to-image&limit=100";
        private const string ImageInferenceBaseUrl =
            "https://router.huggingface.co/hf-inference/models";

        private static readonly HttpClient HttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)
        };

        private static readonly object ImageModelsLock = new object();
        private static readonly HashSet<string> ImageModelIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "stabilityai/stable-diffusion-3-medium-diffusers"
        };

        public static async Task<List<string>> GetModelIdsAsync(
            string apiKey,
            string endpoint,
            CancellationToken cancellationToken = default)
        {
            await ValidateTokenAsync(apiKey, cancellationToken);

            var chatModels = await GetChatModelIdsAsync(apiKey, endpoint, cancellationToken);
            var imageModels = await GetImageModelIdsAsync(apiKey, cancellationToken);
            chatModels.AddRange(imageModels);

            return chatModels
                .Where(modelId => !AiConnectionConfig.IsExcludedModel(modelId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(modelId => modelId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static async Task<HuggingFaceGeneratedImage> GenerateImageAsync(
            string apiKey,
            string modelId,
            string prompt,
            string size,
            CancellationToken cancellationToken = default)
        {
            var parameters = new Dictionary<string, object>();
            if (TryParseSize(size, out var width, out var height))
            {
                parameters["width"] = width;
                parameters["height"] = height;
            }

            var payload = new Dictionary<string, object>
            {
                ["inputs"] = prompt
            };
            if (parameters.Count > 0)
                payload["parameters"] = parameters;

            var endpoint = $"{ImageInferenceBaseUrl}/{EscapeModelId(modelId)}";
            using var request = CreateRequest(HttpMethod.Post, endpoint, apiKey);
            request.Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            using var response = await HttpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessAsync(response);

            return new HuggingFaceGeneratedImage
            {
                Data = await response.Content.ReadAsByteArrayAsync(cancellationToken),
                MimeType = response.Content.Headers.ContentType?.MediaType ?? "image/png"
            };
        }

        public static bool IsImageGenerationModel(string modelId)
        {
            lock (ImageModelsLock)
            {
                if (ImageModelIds.Contains(modelId))
                    return true;
            }

            return modelId.Contains("stable-diffusion", StringComparison.OrdinalIgnoreCase)
                || modelId.Contains("/flux", StringComparison.OrdinalIgnoreCase)
                || modelId.Contains("qwen-image", StringComparison.OrdinalIgnoreCase);
        }

        private static async Task ValidateTokenAsync(string apiKey, CancellationToken cancellationToken)
        {
            using var request = CreateRequest(HttpMethod.Get, WhoAmIUrl, apiKey);
            using var response = await HttpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessAsync(response);
        }

        private static async Task<List<string>> GetChatModelIdsAsync(
            string apiKey,
            string endpoint,
            CancellationToken cancellationToken)
        {
            var modelsUrl = $"{endpoint.TrimEnd('/')}/models";
            using var request = CreateRequest(HttpMethod.Get, modelsUrl, apiKey);
            using var response = await HttpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessAsync(response);

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var result = new List<string>();
            if (!document.RootElement.TryGetProperty("data", out var data) ||
                data.ValueKind != JsonValueKind.Array)
            {
                return result;
            }

            foreach (var model in data.EnumerateArray())
            {
                if (model.TryGetProperty("id", out var idProperty))
                {
                    var modelId = idProperty.GetString();
                    if (!string.IsNullOrWhiteSpace(modelId))
                        result.Add(modelId);
                }
            }
            return result;
        }

        private static async Task<List<string>> GetImageModelIdsAsync(
            string apiKey,
            CancellationToken cancellationToken)
        {
            using var request = CreateRequest(HttpMethod.Get, ImageModelsUrl, apiKey);
            using var response = await HttpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessAsync(response);

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var result = new List<string>();
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return result;

            foreach (var model in document.RootElement.EnumerateArray())
            {
                if (model.TryGetProperty("id", out var idProperty))
                {
                    var modelId = idProperty.GetString();
                    if (!string.IsNullOrWhiteSpace(modelId))
                        result.Add(modelId);
                }
            }

            lock (ImageModelsLock)
            {
                foreach (var modelId in result)
                    ImageModelIds.Add(modelId);
            }
            return result;
        }

        private static HttpRequestMessage CreateRequest(HttpMethod method, string url, string apiKey)
        {
            var request = new HttpRequestMessage(method, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            return request;
        }

        private static async Task EnsureSuccessAsync(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
                return;

            var content = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Hugging Face returned HTTP {(int)response.StatusCode}: {content}");
        }

        private static string EscapeModelId(string modelId)
        {
            return string.Join("/", modelId.Split('/').Select(Uri.EscapeDataString));
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

    internal sealed class HuggingFaceGeneratedImage
    {
        public byte[] Data { get; set; }
        public string MimeType { get; set; }
    }
}
