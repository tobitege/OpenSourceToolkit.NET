using System;
using OpenSourceToolkit.AI.Models;
using OpenSourceToolkit.AI.Providers;

namespace OpenSourceToolkit.AI
{
    public static class AiProviderFactory
    {
        public static IAiProvider Create(AiProviderSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            switch (settings.ProviderType)
            {
                case AiProviderType.OpenAI:
                case AiProviderType.OpenRouter:
                case AiProviderType.LMStudio:
                    return new OpenAiCompatibleProvider(settings);

                case AiProviderType.HuggingFace:
                    return new HuggingFaceProvider(settings);

                case AiProviderType.Anthropic:
                    return new AnthropicProvider(settings);

                case AiProviderType.Google:
                    return new GoogleProvider(settings);

                case AiProviderType.Ollama:
                    return new OllamaProvider(settings);

                default:
                    throw new NotSupportedException($"Provider type '{settings.ProviderType}' is not supported.");
            }
        }
    }
}
