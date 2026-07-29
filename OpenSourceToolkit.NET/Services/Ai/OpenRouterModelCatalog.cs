using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OpenSourceToolkit.NET.Services.Ai
{
    internal static class OpenRouterModelCatalog
    {
        private const string ImageModelsUrl =
            "https://openrouter.ai/api/v1/models?input_modalities=image&output_modalities=image";

        private static readonly HttpClient HttpClient = new HttpClient();
        private static readonly object ImageModelsLock = new object();
        private static readonly HashSet<string> KnownImageModelIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static async Task<List<string>> GetImageGenerationModelIdsAsync(
            CancellationToken cancellationToken = default)
        {
            using var response = await HttpClient.GetAsync(ImageModelsUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var modelIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!document.RootElement.TryGetProperty("data", out var data) ||
                data.ValueKind != JsonValueKind.Array)
            {
                return new List<string>();
            }

            foreach (var model in data.EnumerateArray())
            {
                if (!model.TryGetProperty("id", out var idProperty) ||
                    !model.TryGetProperty("architecture", out var architecture) ||
                    !HasModality(architecture, "input_modalities", "image") ||
                    !HasModality(architecture, "output_modalities", "image"))
                {
                    continue;
                }

                var id = idProperty.GetString();
                if (!string.IsNullOrWhiteSpace(id) &&
                    !AiConnectionConfig.IsExcludedModel(id))
                {
                    modelIds.Add(id);
                }
            }

            var result = new List<string>(modelIds);
            result.Sort(StringComparer.OrdinalIgnoreCase);

            lock (ImageModelsLock)
            {
                KnownImageModelIds.Clear();
                KnownImageModelIds.UnionWith(result);
            }

            return result;
        }

        public static bool IsKnownImageGenerationModelId(string modelId)
        {
            if (string.IsNullOrWhiteSpace(modelId))
                return false;

            lock (ImageModelsLock)
            {
                return KnownImageModelIds.Contains(modelId);
            }
        }

        private static bool HasModality(JsonElement architecture, string propertyName, string modality)
        {
            if (!architecture.TryGetProperty(propertyName, out var modalities) ||
                modalities.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var item in modalities.EnumerateArray())
            {
                if (string.Equals(item.GetString(), modality, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
