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
    public class OpenAiCompatibleProvider : BaseProvider
    {
        public override AiProviderType ProviderType => Settings.ProviderType;
        public override bool SupportsMultiModal => true;
        public override bool SupportsStreaming => true;
        // OpenAI and OpenRouter support image generation via /images/generations endpoint
        public override bool SupportsImageGeneration =>
            Settings.ProviderType == AiProviderType.OpenAI ||
            Settings.ProviderType == AiProviderType.OpenRouter;

        public OpenAiCompatibleProvider(AiProviderSettings settings) : base(settings)
        {
            if (!string.IsNullOrEmpty(settings.ApiKey))
            {
                HttpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {settings.ApiKey}");
            }

            if (settings.ProviderType == AiProviderType.OpenRouter)
            {
                HttpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://opensourcetoolkit.net");
                HttpClient.DefaultRequestHeaders.Add("X-Title", "OpenSourceToolkit.NET");
            }
        }

        public override async Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken cancellationToken = default)
        {
            var payload = BuildPayload(request, stream: false);
            var endpoint = $"{Settings.Endpoint.TrimEnd('/')}/chat/completions";

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

                        var choices = root.GetProperty("choices");
                        if (choices.GetArrayLength() == 0)
                            return ChatResponse.Error("No response choices returned.");

                        var firstChoice = choices[0];
                        var message = firstChoice.GetProperty("message");
                        var responseContent = message.GetProperty("content").GetString();

                        var result = ChatResponse.Success(responseContent);

                        if (firstChoice.TryGetProperty("finish_reason", out var finishReason))
                            result.FinishReason = finishReason.GetString();

                        if (root.TryGetProperty("usage", out var usage))
                        {
                            if (usage.TryGetProperty("prompt_tokens", out var pt))
                                result.PromptTokens = pt.GetInt32();
                            if (usage.TryGetProperty("completion_tokens", out var ct))
                                result.CompletionTokens = ct.GetInt32();
                        }

                        return result;
                    }
                }
            }
        }

        public override async Task StreamAsync(ChatRequest request, Action<string> onChunk, CancellationToken cancellationToken = default)
        {
            var payload = BuildPayload(request, stream: true);
            var endpoint = $"{Settings.Endpoint.TrimEnd('/')}/chat/completions";

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
                            if (data == "[DONE]")
                                return;

                            using (var doc = JsonDocument.Parse(data))
                            {
                                var root = doc.RootElement;
                                if (!root.TryGetProperty("choices", out var choices))
                                    continue;

                                if (choices.GetArrayLength() == 0)
                                    continue;

                                var delta = choices[0].GetProperty("delta");
                                if (delta.TryGetProperty("content", out var contentEl))
                                {
                                    var chunk = contentEl.GetString();
                                    if (!string.IsNullOrEmpty(chunk))
                                        onChunk(chunk);
                                }
                            }
                        }
                    }
                }
            }
        }

        public override async Task<ImageGenerationResponse> GenerateImageAsync(ImageGenerationRequest request, CancellationToken cancellationToken = default)
        {
            if (!SupportsImageGeneration)
            {
                return ImageGenerationResponse.Error($"Provider '{Settings.ProviderType}' does not support image generation.");
            }

            // OpenRouter uses /chat/completions with modalities parameter
            if (Settings.ProviderType == AiProviderType.OpenRouter)
            {
                return await GenerateImageViaOpenRouterAsync(request, cancellationToken).ConfigureAwait(false);
            }

            // OpenAI: use /images/edits if input image provided, otherwise /images/generations
            if (request.InputImage != null && request.InputImage.Length > 0)
            {
                return await EditImageViaOpenAiAsync(request, cancellationToken).ConfigureAwait(false);
            }

            return await GenerateImageViaOpenAiAsync(request, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// OpenAI image editing via /images/edits endpoint (multipart/form-data).
        /// Used when an input image is provided for editing.
        /// </summary>
        private async Task<ImageGenerationResponse> EditImageViaOpenAiAsync(ImageGenerationRequest request, CancellationToken cancellationToken)
        {
            var modelId = request.Model ?? Settings.ModelId;
            var endpoint = $"{Settings.Endpoint.TrimEnd('/')}/images/edits";

            using (var formContent = new MultipartFormDataContent())
            {
                // Required: image file(s) to edit
                var imageContent = new ByteArrayContent(request.InputImage);
                var mimeType = request.InputImageMimeType ?? "image/png";
                var extension = mimeType == "image/jpeg" ? "jpg" : mimeType == "image/webp" ? "webp" : "png";
                imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mimeType);
                formContent.Add(imageContent, "image", $"image.{extension}");

                // Required: prompt
                formContent.Add(new StringContent(request.Prompt), "prompt");

                // Optional: model
                if (!string.IsNullOrEmpty(modelId))
                {
                    formContent.Add(new StringContent(modelId), "model");
                }

                // Optional: n (number of images)
                if (request.Count > 1)
                {
                    formContent.Add(new StringContent(request.Count.ToString()), "n");
                }

                // Optional: size
                if (!string.IsNullOrEmpty(request.Size))
                {
                    formContent.Add(new StringContent(request.Size), "size");
                }

                // GPT Image model parameters
                var isGptImage = !string.IsNullOrEmpty(modelId) && modelId.StartsWith("gpt-image");
                if (isGptImage)
                {
                    if (!string.IsNullOrEmpty(request.Quality))
                    {
                        formContent.Add(new StringContent(request.Quality), "quality");
                    }
                    if (!string.IsNullOrEmpty(request.Background))
                    {
                        formContent.Add(new StringContent(request.Background), "background");
                    }
                    if (!string.IsNullOrEmpty(request.OutputFormat))
                    {
                        formContent.Add(new StringContent(request.OutputFormat), "output_format");
                    }
                    if (request.OutputCompression.HasValue)
                    {
                        formContent.Add(new StringContent(request.OutputCompression.Value.ToString()), "output_compression");
                    }
                }

                using (var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint))
                {
                    httpRequest.Content = formContent;

                    using (var response = await HttpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false))
                    {
                        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        if (!response.IsSuccessStatusCode)
                        {
                            return ImageGenerationResponse.Error($"HTTP {(int)response.StatusCode}: {content}");
                        }

                        // Response format is same as /images/generations
                        return ParseOpenAiImageResponse(content, request);
                    }
                }
            }
        }

        /// <summary>
        /// OpenAI image generation via /images/generations endpoint
        /// </summary>
        private async Task<ImageGenerationResponse> GenerateImageViaOpenAiAsync(ImageGenerationRequest request, CancellationToken cancellationToken)
        {
            var modelId = request.Model ?? Settings.ModelId;
            var isGptImage = !string.IsNullOrEmpty(modelId) && modelId.StartsWith("gpt-image");

            var payload = new Dictionary<string, object>
            {
                ["prompt"] = request.Prompt,
                ["n"] = request.Count
            };

            if (!string.IsNullOrEmpty(modelId))
            {
                payload["model"] = modelId;
            }

            if (!string.IsNullOrEmpty(request.Size))
            {
                payload["size"] = request.Size;
            }

            if (!string.IsNullOrEmpty(request.Quality))
            {
                payload["quality"] = request.Quality;
            }

            // GPT Image model parameters
            if (isGptImage)
            {
                // GPT Image models return b64_json and use output_format instead
                if (!string.IsNullOrEmpty(request.OutputFormat))
                {
                    payload["output_format"] = request.OutputFormat;
                }
                if (!string.IsNullOrEmpty(request.Background))
                {
                    payload["background"] = request.Background;
                }
                if (request.OutputCompression.HasValue)
                {
                    payload["output_compression"] = request.OutputCompression.Value;
                }
                if (!string.IsNullOrEmpty(request.Moderation))
                {
                    payload["moderation"] = request.Moderation;
                }
            }
            else
            {
                // Legacy/fallback: use response_format for base64
                payload["response_format"] = "b64_json";

                if (!string.IsNullOrEmpty(request.Style))
                {
                    payload["style"] = request.Style;
                }
            }

            var endpoint = $"{Settings.Endpoint.TrimEnd('/')}/images/generations";

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

                    return ParseOpenAiImageResponse(content, request);
                }
            }
        }

        /// <summary>
        /// Parses OpenAI image generation/edit response JSON.
        /// Both /images/generations and /images/edits return the same format.
        /// </summary>
        private ImageGenerationResponse ParseOpenAiImageResponse(string jsonContent, ImageGenerationRequest request)
        {
            using (var doc = JsonDocument.Parse(jsonContent))
            {
                var root = doc.RootElement;

                if (root.TryGetProperty("error", out var errorElement))
                {
                    var errorMsg = errorElement.TryGetProperty("message", out var msgEl)
                        ? msgEl.GetString()
                        : jsonContent;
                    return ImageGenerationResponse.Error(errorMsg);
                }

                var images = new List<GeneratedImage>();
                string firstRevisedPrompt = null;

                // Determine mime type from response output_format or request
                var mimeType = "image/png";
                if (root.TryGetProperty("output_format", out var outFmtEl))
                {
                    var fmt = outFmtEl.GetString();
                    if (fmt == "jpeg") mimeType = "image/jpeg";
                    else if (fmt == "webp") mimeType = "image/webp";
                }
                else if (!string.IsNullOrEmpty(request.OutputFormat))
                {
                    if (request.OutputFormat == "jpeg") mimeType = "image/jpeg";
                    else if (request.OutputFormat == "webp") mimeType = "image/webp";
                }

                if (root.TryGetProperty("data", out var dataArray))
                {
                    foreach (var item in dataArray.EnumerateArray())
                    {
                        var genImage = new GeneratedImage { MimeType = mimeType };

                        if (item.TryGetProperty("b64_json", out var b64El))
                        {
                            var b64String = b64El.GetString();
                            if (!string.IsNullOrEmpty(b64String))
                            {
                                genImage.Data = Convert.FromBase64String(b64String);
                            }
                        }

                        if (item.TryGetProperty("url", out var urlEl))
                        {
                            genImage.Url = urlEl.GetString();
                        }

                        if (item.TryGetProperty("revised_prompt", out var revisedEl))
                        {
                            genImage.RevisedPrompt = revisedEl.GetString();
                            if (firstRevisedPrompt == null)
                                firstRevisedPrompt = genImage.RevisedPrompt;
                        }

                        images.Add(genImage);
                    }
                }

                var result = ImageGenerationResponse.Success(images, firstRevisedPrompt);

                // Parse GPT Image usage information
                if (root.TryGetProperty("usage", out var usageEl))
                {
                    if (usageEl.TryGetProperty("total_tokens", out var totalEl))
                        result.TotalTokens = totalEl.GetInt32();
                    if (usageEl.TryGetProperty("input_tokens", out var inputEl))
                        result.InputTokens = inputEl.GetInt32();
                    if (usageEl.TryGetProperty("output_tokens", out var outputEl))
                        result.OutputTokens = outputEl.GetInt32();
                }

                return result;
            }
        }

        /// <summary>
        /// OpenRouter image generation via /chat/completions with modalities parameter.
        /// Response contains images in message.images[].image_url.url as base64 data URLs.
        /// Supports optional input image for image-to-image generation.
        /// </summary>
        private async Task<ImageGenerationResponse> GenerateImageViaOpenRouterAsync(ImageGenerationRequest request, CancellationToken cancellationToken)
        {
            // Build user message content - either simple text or multi-part with image
            object userContent;
            if (request.InputImage != null && request.InputImage.Length > 0)
            {
                // Multi-part content: text + input image for the AI to see/edit
                var mimeType = request.InputImageMimeType ?? "image/png";
                var base64 = Convert.ToBase64String(request.InputImage);
                var dataUrl = $"data:{mimeType};base64,{base64}";

                userContent = new object[]
                {
                    new { type = "text", text = request.Prompt },
                    new { type = "image_url", image_url = new { url = dataUrl } }
                };
            }
            else
            {
                userContent = request.Prompt;
            }

            var payload = new Dictionary<string, object>
            {
                ["model"] = request.Model ?? Settings.ModelId,
                ["messages"] = new[]
                {
                    new { role = "user", content = userContent }
                },
                ["modalities"] = new[] { "image", "text" }
            };

            // Add image_config.aspect_ratio for Gemini models if specified
            if (!string.IsNullOrEmpty(request.AspectRatio))
            {
                payload["image_config"] = new { aspect_ratio = request.AspectRatio };
            }

            var endpoint = $"{Settings.Endpoint.TrimEnd('/')}/chat/completions";

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

                        // OpenRouter response: choices[0].message.images[].image_url.url
                        if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                        {
                            var message = choices[0].GetProperty("message");

                            if (message.TryGetProperty("images", out var imagesArray))
                            {
                                foreach (var imgItem in imagesArray.EnumerateArray())
                                {
                                    if (imgItem.TryGetProperty("image_url", out var imageUrl) &&
                                        imageUrl.TryGetProperty("url", out var urlEl))
                                    {
                                        var dataUrl = urlEl.GetString();
                                        if (!string.IsNullOrEmpty(dataUrl))
                                        {
                                            var genImage = ParseBase64DataUrl(dataUrl);
                                            if (genImage != null)
                                            {
                                                images.Add(genImage);
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        if (images.Count == 0)
                        {
                            return ImageGenerationResponse.Error("No images returned in response. Ensure the model supports image generation.");
                        }

                        return ImageGenerationResponse.Success(images);
                    }
                }
            }
        }

        /// <summary>
        /// Parses a base64 data URL (e.g. "data:image/png;base64,iVBOR...") into a GeneratedImage
        /// </summary>
        private GeneratedImage ParseBase64DataUrl(string dataUrl)
        {
            // Format: data:image/png;base64,iVBORw0KGgo...
            const string dataPrefix = "data:";
            const string base64Marker = ";base64,";

            if (!dataUrl.StartsWith(dataPrefix))
                return null;

            var base64Index = dataUrl.IndexOf(base64Marker);
            if (base64Index < 0)
                return null;

            var mimeType = dataUrl.Substring(dataPrefix.Length, base64Index - dataPrefix.Length);
            var base64Data = dataUrl.Substring(base64Index + base64Marker.Length);

            try
            {
                return new GeneratedImage
                {
                    Data = Convert.FromBase64String(base64Data),
                    MimeType = mimeType
                };
            }
            catch
            {
                return null;
            }
        }

        private object BuildPayload(ChatRequest request, bool stream)
        {
            var messages = new List<object>();

            foreach (var msg in request.Messages)
            {
                if (msg.Images != null && msg.Images.Count > 0)
                {
                    var contentParts = new List<object>
                    {
                        new { type = "text", text = msg.Content }
                    };

                    foreach (var img in msg.Images)
                    {
                        contentParts.Add(new
                        {
                            type = "image_url",
                            image_url = new { url = img.ToBase64DataUrl() }
                        });
                    }

                    messages.Add(new
                    {
                        role = msg.Role.ToString().ToLowerInvariant(),
                        content = contentParts
                    });
                }
                else
                {
                    messages.Add(new
                    {
                        role = msg.Role.ToString().ToLowerInvariant(),
                        content = msg.Content
                    });
                }
            }

            return new
            {
                model = Settings.ModelId,
                messages = messages,
                max_tokens = request.MaxTokens ?? Settings.MaxTokens,
                temperature = request.Temperature ?? Settings.Temperature,
                stream = stream
            };
        }
    }
}
