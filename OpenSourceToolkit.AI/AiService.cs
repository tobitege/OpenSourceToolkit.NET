using System;
using System.Threading;
using System.Threading.Tasks;
using OpenSourceToolkit.AI.Models;

namespace OpenSourceToolkit.AI
{
    public class AiService : IAiService
    {
        private IAiProvider _provider;
        private AiProviderSettings _settings;
        private readonly Func<AiProviderSettings, IAiProvider> _providerFactory;

        public AiService(Func<AiProviderSettings, IAiProvider> providerFactory = null)
        {
            _providerFactory = providerFactory ?? AiProviderFactory.Create;
        }

        public bool IsConfigured => _provider != null && _settings != null;
        public AiProviderType? CurrentProvider => _settings?.ProviderType;
        public bool SupportsImageGeneration => _provider?.SupportsImageGeneration ?? false;

        public void Configure(AiProviderSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _provider = _providerFactory(settings);
        }

        public async Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default)
        {
            EnsureConfigured();
            var request = new ChatRequest(prompt)
            {
                MaxTokens = _settings.MaxTokens,
                Temperature = _settings.Temperature
            };
            var response = await _provider.CompleteAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccess)
                throw new AiException(response.ErrorMessage);
            return response.Content;
        }

        public async Task<string> CompleteAsync(string prompt, byte[] imageData, string mimeType = "image/png", CancellationToken cancellationToken = default)
        {
            EnsureConfigured();
            if (!_provider.SupportsMultiModal)
                throw new NotSupportedException($"Provider '{_settings.ProviderType}' does not support multi-modal input.");

            var request = new ChatRequest(prompt)
            {
                MaxTokens = _settings.MaxTokens,
                Temperature = _settings.Temperature
            };
            request.WithImage(imageData, mimeType);

            var response = await _provider.CompleteAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccess)
                throw new AiException(response.ErrorMessage);
            return response.Content;
        }

        public async Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken cancellationToken = default)
        {
            EnsureConfigured();
            request.MaxTokens = request.MaxTokens ?? _settings.MaxTokens;
            request.Temperature = request.Temperature ?? _settings.Temperature;
            return await _provider.CompleteAsync(request, cancellationToken).ConfigureAwait(false);
        }

        public Task StreamAsync(string prompt, Action<string> onChunk, CancellationToken cancellationToken = default)
        {
            EnsureConfigured();
            var request = new ChatRequest(prompt)
            {
                MaxTokens = _settings.MaxTokens,
                Temperature = _settings.Temperature,
                Stream = true
            };
            return _provider.StreamAsync(request, onChunk, cancellationToken);
        }

        public Task StreamAsync(ChatRequest request, Action<string> onChunk, CancellationToken cancellationToken = default)
        {
            EnsureConfigured();
            request.MaxTokens = request.MaxTokens ?? _settings.MaxTokens;
            request.Temperature = request.Temperature ?? _settings.Temperature;
            request.Stream = true;
            return _provider.StreamAsync(request, onChunk, cancellationToken);
        }

        public async Task<ImageGenerationResponse> GenerateImageAsync(string prompt, CancellationToken cancellationToken = default)
        {
            EnsureConfigured();
            if (!_provider.SupportsImageGeneration)
                return ImageGenerationResponse.Error($"Provider '{_settings.ProviderType}' does not support image generation.");

            var request = new ImageGenerationRequest(prompt);
            ApplyDefaultImageModel(request);
            return await _provider.GenerateImageAsync(request, cancellationToken).ConfigureAwait(false);
        }

        public async Task<ImageGenerationResponse> GenerateImageAsync(ImageGenerationRequest request, CancellationToken cancellationToken = default)
        {
            EnsureConfigured();
            if (!_provider.SupportsImageGeneration)
                return ImageGenerationResponse.Error($"Provider '{_settings.ProviderType}' does not support image generation.");

            ApplyDefaultImageModel(request);
            return await _provider.GenerateImageAsync(request, cancellationToken).ConfigureAwait(false);
        }

        private void ApplyDefaultImageModel(ImageGenerationRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.Model))
                return;

            var imageModels = AiProviderSettings.GetDefaultImageModels(_settings.ProviderType);
            if (imageModels.Count > 0)
                request.Model = imageModels[0];
        }

        public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
        {
            EnsureConfigured();
            return await _provider.TestConnectionAsync(cancellationToken).ConfigureAwait(false);
        }

        private void EnsureConfigured()
        {
            if (!IsConfigured)
                throw new InvalidOperationException("AI service is not configured. Call Configure() first.");
        }
    }

    public class AiException : Exception
    {
        public string SafeMessage { get; }

        public AiException(string message) : base(message)
        {
            SafeMessage = SanitizeMessage(message);
        }

        public AiException(string message, Exception innerException) : base(message, innerException)
        {
            SafeMessage = SanitizeMessage(message);
        }

        private static string SanitizeMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return "An error occurred while communicating with the AI provider.";

            // Remove potential API keys and sensitive data patterns
            var sanitized = System.Text.RegularExpressions.Regex.Replace(
                message,
                @"(sk-[a-zA-Z0-9]{20,}|key[=:]\s*[""']?[a-zA-Z0-9\-_]{20,}[""']?|Bearer\s+[a-zA-Z0-9\-_\.]+|api[_-]?key[=:]\s*[^\s,}]+)",
                "[REDACTED]",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // If the message is too long or contains suspicious patterns, return generic message
            if (sanitized.Length > 500 ||
                (sanitized.IndexOf("authorization", StringComparison.OrdinalIgnoreCase) >= 0 &&
                 sanitized.IndexOf("invalid", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return "Authentication failed. Please check your API key configuration.";
            }

            return sanitized;
        }

        public static string GetUserFriendlyMessage(Exception ex)
        {
            if (ex is AiException aiEx)
                return aiEx.SafeMessage;

            if (ex is System.Net.Http.HttpRequestException)
                return "Unable to connect to the AI provider. Please check your network connection and endpoint URL.";

            if (ex is TaskCanceledException || ex is OperationCanceledException)
                return "The request was cancelled or timed out.";

            return "An unexpected error occurred. Please try again.";
        }
    }
}
