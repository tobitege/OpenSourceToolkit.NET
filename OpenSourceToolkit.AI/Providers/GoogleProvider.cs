using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OpenSourceToolkit.AI.Models;

namespace OpenSourceToolkit.AI.Providers
{
    public class GoogleProvider : BaseProvider
    {
        public override AiProviderType ProviderType => AiProviderType.Google;
        public override bool SupportsMultiModal => true;
        public override bool SupportsStreaming => true;
        public override bool SupportsImageGeneration => true; // Google Gemini image generation

        public GoogleProvider(AiProviderSettings settings) : base(settings)
        {
        }

        public override async Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken cancellationToken = default)
        {
            var payload = BuildPayload(request);
            var endpoint = $"{Settings.Endpoint.TrimEnd('/')}/models/{Settings.ModelId}:generateContent?key={Settings.ApiKey}";

            using (var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint))
            {
                httpRequest.Content = CreateJsonContent(payload);

                using (var response = await HttpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false))
                {
                    var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        return ChatResponse.Error($"HTTP {(int)response.StatusCode}: {content}");
                    }

                    using (var doc = JsonDocument.Parse(content))
                    {
                        var root = doc.RootElement;

                        if (root.TryGetProperty("error", out var errorElement))
                        {
                            var errorMsg = errorElement.TryGetProperty("message", out var msgEl)
                                ? msgEl.GetString()
                                : content;
                            return ChatResponse.Error(errorMsg);
                        }

                        if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
                            return ChatResponse.Error("No response candidates returned.");

                        var firstCandidate = candidates[0];
                        var responseContent = firstCandidate.GetProperty("content");
                        var parts = responseContent.GetProperty("parts");

                        var responseText = "";
                        foreach (var part in parts.EnumerateArray())
                        {
                            if (part.TryGetProperty("text", out var textEl))
                                responseText += textEl.GetString();
                        }

                        var result = ChatResponse.Success(responseText);

                        if (firstCandidate.TryGetProperty("finishReason", out var finishReason))
                            result.FinishReason = finishReason.GetString();

                        if (root.TryGetProperty("usageMetadata", out var usage))
                        {
                            if (usage.TryGetProperty("promptTokenCount", out var pt))
                                result.PromptTokens = pt.GetInt32();
                            if (usage.TryGetProperty("candidatesTokenCount", out var ct))
                                result.CompletionTokens = ct.GetInt32();
                        }

                        return result;
                    }
                }
            }
        }

        public override async Task StreamAsync(ChatRequest request, Action<string> onChunk, CancellationToken cancellationToken = default)
        {
            var payload = BuildPayload(request);
            var endpoint = $"{Settings.Endpoint.TrimEnd('/')}/models/{Settings.ModelId}:streamGenerateContent?alt=sse&key={Settings.ApiKey}";

            using (var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint))
            {
                httpRequest.Content = CreateJsonContent(payload);

                using (var response = await HttpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        throw new AiException($"HTTP {(int)response.StatusCode}: {errorContent}");
                    }

                    using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var reader = new StreamReader(stream))
                    {
                        string line;
                        while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
                        {
                            if (cancellationToken.IsCancellationRequested)
                                return;

                            if (string.IsNullOrWhiteSpace(line))
                                continue;

                            if (!line.StartsWith("data: "))
                                continue;

                            var data = line.Substring(6);

                            using (var doc = JsonDocument.Parse(data))
                            {
                                var root = doc.RootElement;
                                if (!root.TryGetProperty("candidates", out var candidates))
                                    continue;

                                if (candidates.GetArrayLength() == 0)
                                    continue;

                                var content = candidates[0].GetProperty("content");
                                var parts = content.GetProperty("parts");

                                foreach (var part in parts.EnumerateArray())
                                {
                                    if (part.TryGetProperty("text", out var textEl))
                                    {
                                        var chunk = textEl.GetString();
                                        if (!string.IsNullOrEmpty(chunk))
                                            onChunk(chunk);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private object BuildPayload(ChatRequest request)
        {
            var contents = new List<object>();
            object systemInstruction = null;

            foreach (var msg in request.Messages)
            {
                if (msg.Role == ChatRole.System)
                {
                    systemInstruction = new
                    {
                        parts = new[] { new { text = msg.Content } }
                    };
                    continue;
                }

                var parts = new List<object>();

                if (msg.Images != null && msg.Images.Count > 0)
                {
                    foreach (var img in msg.Images)
                    {
                        parts.Add(new
                        {
                            inline_data = new
                            {
                                mime_type = img.MimeType,
                                data = Convert.ToBase64String(img.Data)
                            }
                        });
                    }
                }

                parts.Add(new { text = msg.Content });

                contents.Add(new
                {
                    role = msg.Role == ChatRole.Assistant ? "model" : "user",
                    parts = parts
                });
            }

            var generationConfig = new
            {
                maxOutputTokens = request.MaxTokens ?? Settings.MaxTokens,
                temperature = request.Temperature ?? Settings.Temperature
            };

            if (systemInstruction != null)
            {
                return new
                {
                    system_instruction = systemInstruction,
                    contents = contents,
                    generationConfig = generationConfig
                };
            }

            return new
            {
                contents = contents,
                generationConfig = generationConfig
            };
        }

        public override async Task<ImageGenerationResponse> GenerateImageAsync(ImageGenerationRequest request, CancellationToken cancellationToken = default)
        {
            var model = string.IsNullOrWhiteSpace(request.Model)
                ? "gemini-3.1-flash-image"
                : request.Model;
            if (!model.StartsWith("imagen", StringComparison.OrdinalIgnoreCase))
                return await GenerateImageWithGeminiAsync(model, request, cancellationToken).ConfigureAwait(false);

            return await GenerateImageWithImagenAsync(model, request, cancellationToken).ConfigureAwait(false);
        }

        private async Task<ImageGenerationResponse> GenerateImageWithGeminiAsync(
            string model,
            ImageGenerationRequest request,
            CancellationToken cancellationToken)
        {
            var payload = new
            {
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[] { new { text = request.Prompt } }
                    }
                },
                generationConfig = new
                {
                    responseModalities = new[] { "TEXT", "IMAGE" },
                    imageConfig = new { aspectRatio = ParseAspectRatio(request.Size) }
                }
            };

            var endpoint = $"{Settings.Endpoint.TrimEnd('/')}/models/{model}:generateContent?key={Settings.ApiKey}";
            using (var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint))
            {
                httpRequest.Content = CreateJsonContent(payload);

                using (var response = await HttpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false))
                {
                    var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                        return ImageGenerationResponse.Error($"HTTP {(int)response.StatusCode}: {content}");

                    using (var doc = JsonDocument.Parse(content))
                    {
                        var images = new List<GeneratedImage>();
                        if (doc.RootElement.TryGetProperty("candidates", out var candidates))
                        {
                            foreach (var candidate in candidates.EnumerateArray())
                            {
                                if (!candidate.TryGetProperty("content", out var candidateContent) ||
                                    !candidateContent.TryGetProperty("parts", out var parts))
                                {
                                    continue;
                                }

                                foreach (var part in parts.EnumerateArray())
                                {
                                    if (!part.TryGetProperty("inlineData", out var inlineData) ||
                                        !inlineData.TryGetProperty("data", out var dataElement))
                                    {
                                        continue;
                                    }

                                    var base64 = dataElement.GetString();
                                    if (string.IsNullOrEmpty(base64))
                                        continue;

                                    var mimeType = inlineData.TryGetProperty("mimeType", out var mimeTypeElement)
                                        ? mimeTypeElement.GetString()
                                        : "image/png";

                                    images.Add(new GeneratedImage
                                    {
                                        Data = Convert.FromBase64String(base64),
                                        MimeType = mimeType ?? "image/png"
                                    });
                                }
                            }
                        }

                        return ImageGenerationResponse.Success(images);
                    }
                }
            }
        }

        private async Task<ImageGenerationResponse> GenerateImageWithImagenAsync(
            string model,
            ImageGenerationRequest request,
            CancellationToken cancellationToken)
        {
            var payload = new
            {
                instances = new[]
                {
                    new { prompt = request.Prompt }
                },
                parameters = new
                {
                    sampleCount = request.Count,
                    aspectRatio = ParseAspectRatio(request.Size)
                }
            };

            var endpoint = $"{Settings.Endpoint.TrimEnd('/')}/models/{model}:predict?key={Settings.ApiKey}";

            using (var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint))
            {
                httpRequest.Content = CreateJsonContent(payload);

                using (var response = await HttpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false))
                {
                    var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        return ImageGenerationResponse.Error($"HTTP {(int)response.StatusCode}: {content}");
                    }

                    using (var doc = JsonDocument.Parse(content))
                    {
                        var root = doc.RootElement;

                        if (root.TryGetProperty("error", out var errorElement))
                        {
                            var errorMsg = errorElement.TryGetProperty("message", out var msgEl)
                                ? msgEl.GetString()
                                : content;
                            return ImageGenerationResponse.Error(errorMsg);
                        }

                        var images = new List<GeneratedImage>();

                        if (root.TryGetProperty("predictions", out var predictions))
                        {
                            foreach (var pred in predictions.EnumerateArray())
                            {
                                if (pred.TryGetProperty("bytesBase64Encoded", out var b64El))
                                {
                                    var b64String = b64El.GetString();
                                    if (!string.IsNullOrEmpty(b64String))
                                    {
                                        images.Add(new GeneratedImage
                                        {
                                            Data = Convert.FromBase64String(b64String),
                                            MimeType = "image/png"
                                        });
                                    }
                                }
                            }
                        }

                        return ImageGenerationResponse.Success(images);
                    }
                }
            }
        }

        private static string ParseAspectRatio(string size)
        {
            // Convert size like "1024x1024" to aspect ratio like "1:1"
            if (string.IsNullOrEmpty(size))
                return "1:1";

            var parts = size.Split('x');
            if (parts.Length != 2)
                return "1:1";

            if (int.TryParse(parts[0], out int width) && int.TryParse(parts[1], out int height))
            {
                if (width == height)
                    return "1:1";
                if (width > height)
                    return "16:9";
                return "9:16";
            }

            return "1:1";
        }
    }
}
