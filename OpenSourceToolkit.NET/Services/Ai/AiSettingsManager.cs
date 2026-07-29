using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenSourceToolkit.NET.Services.Ai
{
    /// <summary>
    /// Manages AI provider configurations and connections with secure API key storage.
    /// </summary>
    public class AiSettingsManager
    {
        private readonly ISecretStorage _secretStorage;

        public const string SecureKeyPrefix = "secure:";

        public List<AiProviderConfig> Providers { get; set; } = new List<AiProviderConfig>();
        public List<AiConnection> Connections { get; set; } = new List<AiConnection>();
        public Dictionary<string, List<string>> ProviderModels { get; set; } = new Dictionary<string, List<string>>();

        public AiSettingsManager(ISecretStorage secretStorage)
        {
            _secretStorage = secretStorage ?? throw new ArgumentNullException(nameof(secretStorage));
        }

        #region Provider Management

        public AiProviderConfig GetOrCreateProvider(string providerType)
        {
            var provider = Providers.FirstOrDefault(p => p.ProviderType == providerType);
            if (provider == null)
            {
                provider = new AiProviderConfig
                {
                    ProviderType = providerType,
                    Endpoint = GetDefaultEndpoint(providerType)
                };
                Providers.Add(provider);
            }
            return provider;
        }

        public string GetProviderApiKey(string providerType)
        {
            var provider = Providers.FirstOrDefault(p => p.ProviderType == providerType);
            if (provider == null || string.IsNullOrEmpty(provider.ApiKey))
                return null;

            if (provider.ApiKey.StartsWith(SecureKeyPrefix))
            {
                var secureKey = provider.ApiKey.Substring(SecureKeyPrefix.Length);
                return _secretStorage.Retrieve(secureKey);
            }

            return provider.ApiKey;
        }

        public void SetProviderApiKey(string providerType, string apiKey)
        {
            var provider = GetOrCreateProvider(providerType);

            if (!string.IsNullOrEmpty(provider.ApiKey) && provider.ApiKey.StartsWith(SecureKeyPrefix))
            {
                var oldSecureKey = provider.ApiKey.Substring(SecureKeyPrefix.Length);
                _secretStorage.Remove(oldSecureKey);
            }

            if (string.IsNullOrEmpty(apiKey))
            {
                provider.ApiKey = null;
                return;
            }

            var storageKey = $"provider.{providerType}.apikey";
            _secretStorage.Store(storageKey, apiKey);
            provider.ApiKey = SecureKeyPrefix + storageKey;
        }

        public string GetProviderEndpoint(string providerType)
        {
            var provider = Providers.FirstOrDefault(p => p.ProviderType == providerType);
            return provider?.Endpoint ?? GetDefaultEndpoint(providerType);
        }

        public void SetProviderEndpoint(string providerType, string endpoint)
        {
            var provider = GetOrCreateProvider(providerType);
            provider.Endpoint = endpoint;
        }

        #endregion

        #region Connection Management

        public string GetConnectionApiKey(string connectionId)
        {
            var connection = Connections.FirstOrDefault(c => c.Id == connectionId);
            if (connection == null || string.IsNullOrEmpty(connection.CustomApiKey))
                return null;

            if (connection.CustomApiKey.StartsWith(SecureKeyPrefix))
            {
                var secureKey = connection.CustomApiKey.Substring(SecureKeyPrefix.Length);
                return _secretStorage.Retrieve(secureKey);
            }

            return connection.CustomApiKey;
        }

        public void SetConnectionApiKey(string connectionId, string apiKey)
        {
            var connection = Connections.FirstOrDefault(c => c.Id == connectionId);
            if (connection == null)
                return;

            if (!string.IsNullOrEmpty(connection.CustomApiKey) && connection.CustomApiKey.StartsWith(SecureKeyPrefix))
            {
                var oldSecureKey = connection.CustomApiKey.Substring(SecureKeyPrefix.Length);
                _secretStorage.Remove(oldSecureKey);
            }

            if (string.IsNullOrEmpty(apiKey))
            {
                connection.CustomApiKey = null;
                return;
            }

            var storageKey = $"connection.{connectionId}.apikey";
            _secretStorage.Store(storageKey, apiKey);
            connection.CustomApiKey = SecureKeyPrefix + storageKey;
        }

        public AiConnection AddConnection(
            string name,
            string providerType,
            string modelId,
            string customApiKey = null,
            string customEndpoint = null)
        {
            var connection = new AiConnection
            {
                Name = name,
                ProviderType = providerType,
                ModelId = modelId,
                CustomEndpoint = customEndpoint
            };
            Connections.Add(connection);

            if (!string.IsNullOrEmpty(customApiKey))
                SetConnectionApiKey(connection.Id, customApiKey);

            return connection;
        }

        public bool RemoveConnection(string connectionId)
        {
            var connection = Connections.FirstOrDefault(c => c.Id == connectionId);
            if (connection == null)
                return false;

            if (!string.IsNullOrEmpty(connection.CustomApiKey) && connection.CustomApiKey.StartsWith(SecureKeyPrefix))
            {
                var secureKey = connection.CustomApiKey.Substring(SecureKeyPrefix.Length);
                _secretStorage.Remove(secureKey);
            }

            return Connections.Remove(connection);
        }

        public string GetEffectiveApiKey(string connectionId)
        {
            var connection = Connections.FirstOrDefault(c => c.Id == connectionId);
            if (connection == null)
                return null;

            var customKey = GetConnectionApiKey(connectionId);
            if (!string.IsNullOrEmpty(customKey))
                return customKey;

            return GetProviderApiKey(connection.ProviderType);
        }

        public AiConnectionConfig CreateConfigFromConnection(string connectionId)
        {
            var connection = Connections.FirstOrDefault(c => c.Id == connectionId);
            if (connection == null)
                return null;

            var providerType = ParseProviderType(connection.ProviderType);
            var apiKey = GetEffectiveApiKey(connectionId);
            var endpoint = !string.IsNullOrEmpty(connection.CustomEndpoint)
                ? connection.CustomEndpoint
                : GetProviderEndpoint(connection.ProviderType);

            return new AiConnectionConfig
            {
                ProviderType = providerType,
                ApiKey = apiKey,
                Endpoint = endpoint,
                ModelId = connection.ModelId,
                MaxTokens = connection.MaxTokens,
                Temperature = connection.Temperature
            };
        }

        #endregion

        #region Model Management

        public List<string> GetProviderModels(string providerType)
        {
            var type = ParseProviderType(providerType);
            if (ProviderModels.TryGetValue(providerType, out var models) && models.Count > 0)
                return models.FindAll(model => !AiConnectionConfig.IsExcludedModel(model));

            return GetDefaultProviderModels(type);
        }

        public void SetProviderModels(string providerType, List<string> models)
        {
            ProviderModels[providerType] = models.FindAll(model => !AiConnectionConfig.IsExcludedModel(model));
        }

        public void ResetProviderModels(string providerType)
        {
            var type = ParseProviderType(providerType);
            ProviderModels[providerType] = GetDefaultProviderModels(type);
        }

        public void AddProviderModel(string providerType, string modelId)
        {
            if (AiConnectionConfig.IsExcludedModel(modelId))
                return;

            if (!ProviderModels.ContainsKey(providerType))
                ProviderModels[providerType] = GetProviderModels(providerType);

            if (!ProviderModels[providerType].Contains(modelId))
                ProviderModels[providerType].Add(modelId);
        }

        public void RemoveProviderModel(string providerType, string modelId)
        {
            if (ProviderModels.ContainsKey(providerType))
                ProviderModels[providerType].Remove(modelId);
        }

        #endregion

        #region Migration

        public bool MigrateToSecureStorage()
        {
            var migrated = false;

            foreach (var provider in Providers)
            {
                if (!string.IsNullOrEmpty(provider.ApiKey) && !provider.ApiKey.StartsWith(SecureKeyPrefix))
                {
                    var plainKey = provider.ApiKey;
                    var storageKey = $"provider.{provider.ProviderType}.apikey";
                    _secretStorage.Store(storageKey, plainKey);
                    provider.ApiKey = SecureKeyPrefix + storageKey;
                    migrated = true;
                }
            }

            foreach (var connection in Connections)
            {
                if (!string.IsNullOrEmpty(connection.CustomApiKey) && !connection.CustomApiKey.StartsWith(SecureKeyPrefix))
                {
                    var plainKey = connection.CustomApiKey;
                    var storageKey = $"connection.{connection.Id}.apikey";
                    _secretStorage.Store(storageKey, plainKey);
                    connection.CustomApiKey = SecureKeyPrefix + storageKey;
                    migrated = true;
                }
            }

            return migrated;
        }

        #endregion

        #region Reset

        public void Reset()
        {
            _secretStorage.Clear();
            Providers.Clear();
            Connections.Clear();
            ProviderModels.Clear();
        }

        #endregion

        #region Helpers

        private static string GetDefaultEndpoint(string providerType)
        {
            var type = ParseProviderType(providerType);
            return AiConnectionConfig.GetDefaultEndpoint(type);
        }

        private static List<string> GetDefaultProviderModels(AiProviderType providerType)
        {
            var models = AiConnectionConfig.GetDefaultModels(providerType);
            foreach (var imageModel in AiConnectionConfig.GetDefaultImageModels(providerType))
            {
                if (!models.Contains(imageModel))
                    models.Add(imageModel);
            }
            return models;
        }

        private static AiProviderType ParseProviderType(string providerType)
        {
            if (string.Equals(providerType, "OpenAI-Compatible", StringComparison.Ordinal))
                return AiProviderType.OpenAICompatible;

            if (Enum.TryParse<AiProviderType>(providerType, out var result))
                return result;
            return AiProviderType.OpenAI;
        }

        public static readonly string[] SupportedProviders = new[]
        {
            "OpenAI", "OpenRouter", "HuggingFace", "Anthropic", "Google", "Ollama", "LMStudio"
        };

        public static readonly string[] SupportedConnectionProviders = new[]
        {
            "OpenAI", "OpenAI-Compatible", "Codex", "OpenRouter", "HuggingFace", "Anthropic", "Google", "Ollama", "LMStudio"
        };

        #endregion
    }
}
