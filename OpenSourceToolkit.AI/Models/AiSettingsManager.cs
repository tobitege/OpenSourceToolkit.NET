using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenSourceToolkit.AI.Models
{
    /// <summary>
    /// Interface for secure secret storage. Implementations should use platform-appropriate
    /// encryption (DPAPI on Windows, Keychain on macOS, etc.)
    /// </summary>
    public interface ISecretStorage
    {
        void Store(string key, string value);
        string Retrieve(string key);
        void Remove(string key);
        bool Contains(string key);
        void Clear();
    }

    /// <summary>
    /// Manages AI provider configurations and connections with secure API key storage.
    /// </summary>
    public class AiSettingsManager
    {
        private readonly ISecretStorage _secretStorage;

        // Prefix for keys stored in secure storage
        public const string SecureKeyPrefix = "secure:";

        /// <summary>
        /// Provider configurations (API keys stored securely, endpoints, available models).
        /// </summary>
        public List<AiProviderConfig> Providers { get; set; } = new List<AiProviderConfig>();

        /// <summary>
        /// Named connections (max 50 recommended).
        /// </summary>
        public List<AiConnection> Connections { get; set; } = new List<AiConnection>();

        /// <summary>
        /// Per-provider model lists (user-editable).
        /// </summary>
        public Dictionary<string, List<string>> ProviderModels { get; set; } = new Dictionary<string, List<string>>();

        /// <summary>
        /// Creates a new AiSettingsManager with secure storage.
        /// </summary>
        /// <param name="secretStorage">Implementation of secure storage for API keys.</param>
        public AiSettingsManager(ISecretStorage secretStorage)
        {
            _secretStorage = secretStorage ?? throw new ArgumentNullException(nameof(secretStorage));
        }

        #region Provider Management

        /// <summary>
        /// Gets or creates a provider configuration for the specified type.
        /// </summary>
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

        /// <summary>
        /// Gets the actual API key for a provider, retrieving from secure storage if necessary.
        /// </summary>
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

        /// <summary>
        /// Sets the API key for a provider, storing it securely.
        /// </summary>
        public void SetProviderApiKey(string providerType, string apiKey)
        {
            var provider = GetOrCreateProvider(providerType);

            // Remove old key from secure storage if it was stored there
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

            // Store in secure storage
            var storageKey = $"provider.{providerType}.apikey";
            _secretStorage.Store(storageKey, apiKey);
            provider.ApiKey = SecureKeyPrefix + storageKey;
        }

        /// <summary>
        /// Gets the endpoint for a provider.
        /// </summary>
        public string GetProviderEndpoint(string providerType)
        {
            var provider = Providers.FirstOrDefault(p => p.ProviderType == providerType);
            return provider?.Endpoint ?? GetDefaultEndpoint(providerType);
        }

        /// <summary>
        /// Sets the endpoint for a provider.
        /// </summary>
        public void SetProviderEndpoint(string providerType, string endpoint)
        {
            var provider = GetOrCreateProvider(providerType);
            provider.Endpoint = endpoint;
        }

        #endregion

        #region Connection Management

        /// <summary>
        /// Gets the actual custom API key for a connection, retrieving from secure storage if necessary.
        /// </summary>
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

        /// <summary>
        /// Sets the custom API key for a connection, storing it securely.
        /// </summary>
        public void SetConnectionApiKey(string connectionId, string apiKey)
        {
            var connection = Connections.FirstOrDefault(c => c.Id == connectionId);
            if (connection == null)
                return;

            // Remove old key from secure storage if it was stored there
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

            // Store in secure storage
            var storageKey = $"connection.{connectionId}.apikey";
            _secretStorage.Store(storageKey, apiKey);
            connection.CustomApiKey = SecureKeyPrefix + storageKey;
        }

        /// <summary>
        /// Adds a new connection with optional custom API key.
        /// </summary>
        public AiConnection AddConnection(string name, string providerType, string modelId, string customApiKey = null)
        {
            var connection = new AiConnection
            {
                Name = name,
                ProviderType = providerType,
                ModelId = modelId
            };
            Connections.Add(connection);

            if (!string.IsNullOrEmpty(customApiKey))
                SetConnectionApiKey(connection.Id, customApiKey);

            return connection;
        }

        /// <summary>
        /// Removes a connection and its secure API key.
        /// </summary>
        public bool RemoveConnection(string connectionId)
        {
            var connection = Connections.FirstOrDefault(c => c.Id == connectionId);
            if (connection == null)
                return false;

            // Remove API key from secure storage
            if (!string.IsNullOrEmpty(connection.CustomApiKey) && connection.CustomApiKey.StartsWith(SecureKeyPrefix))
            {
                var secureKey = connection.CustomApiKey.Substring(SecureKeyPrefix.Length);
                _secretStorage.Remove(secureKey);
            }

            return Connections.Remove(connection);
        }

        /// <summary>
        /// Gets the effective API key for a connection (custom or provider default).
        /// </summary>
        public string GetEffectiveApiKey(string connectionId)
        {
            var connection = Connections.FirstOrDefault(c => c.Id == connectionId);
            if (connection == null)
                return null;

            // Try custom API key first
            var customKey = GetConnectionApiKey(connectionId);
            if (!string.IsNullOrEmpty(customKey))
                return customKey;

            // Fall back to provider API key
            return GetProviderApiKey(connection.ProviderType);
        }

        /// <summary>
        /// Creates AiProviderSettings from a connection for use with AiService.
        /// </summary>
        public AiProviderSettings CreateSettingsFromConnection(string connectionId)
        {
            var connection = Connections.FirstOrDefault(c => c.Id == connectionId);
            if (connection == null)
                return null;

            var providerType = ParseProviderType(connection.ProviderType);
            var apiKey = GetEffectiveApiKey(connectionId);
            var endpoint = !string.IsNullOrEmpty(connection.CustomEndpoint)
                ? connection.CustomEndpoint
                : GetProviderEndpoint(connection.ProviderType);

            return new AiProviderSettings
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

        /// <summary>
        /// Gets the available models for a provider (user-customized or defaults).
        /// </summary>
        public List<string> GetProviderModels(string providerType)
        {
            var type = ParseProviderType(providerType);
            if (ProviderModels.TryGetValue(providerType, out var models) && models.Count > 0)
                return models.FindAll(model => !AiProviderSettings.IsExcludedModel(model));

            return GetDefaultProviderModels(type);
        }

        /// <summary>
        /// Sets the available models for a provider.
        /// </summary>
        public void SetProviderModels(string providerType, List<string> models)
        {
            ProviderModels[providerType] = models.FindAll(model => !AiProviderSettings.IsExcludedModel(model));
        }

        /// <summary>
        /// Resets the models for a provider to defaults.
        /// </summary>
        public void ResetProviderModels(string providerType)
        {
            var type = ParseProviderType(providerType);
            ProviderModels[providerType] = GetDefaultProviderModels(type);
        }

        /// <summary>
        /// Adds a model to a provider's list.
        /// </summary>
        public void AddProviderModel(string providerType, string modelId)
        {
            if (AiProviderSettings.IsExcludedModel(modelId))
                return;

            if (!ProviderModels.ContainsKey(providerType))
                ProviderModels[providerType] = GetProviderModels(providerType);

            if (!ProviderModels[providerType].Contains(modelId))
                ProviderModels[providerType].Add(modelId);
        }

        /// <summary>
        /// Removes a model from a provider's list.
        /// </summary>
        public void RemoveProviderModel(string providerType, string modelId)
        {
            if (ProviderModels.ContainsKey(providerType))
                ProviderModels[providerType].Remove(modelId);
        }

        #endregion

        #region Migration

        /// <summary>
        /// Migrates plain-text API keys to secure storage.
        /// Call this after loading settings from JSON to upgrade old configurations.
        /// </summary>
        /// <returns>True if any keys were migrated.</returns>
        public bool MigrateToSecureStorage()
        {
            var migrated = false;

            // Migrate provider API keys
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

            // Migrate connection custom API keys
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

        /// <summary>
        /// Clears all settings and secure storage.
        /// </summary>
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
            return AiProviderSettings.GetDefaultEndpoint(type);
        }

        private static List<string> GetDefaultProviderModels(AiProviderType providerType)
        {
            var models = AiProviderSettings.GetDefaultModels(providerType);
            foreach (var imageModel in AiProviderSettings.GetDefaultImageModels(providerType))
            {
                if (!models.Contains(imageModel))
                    models.Add(imageModel);
            }
            return models;
        }

        private static AiProviderType ParseProviderType(string providerType)
        {
            if (Enum.TryParse<AiProviderType>(providerType, out var result))
                return result;
            return AiProviderType.OpenAI;
        }

        /// <summary>
        /// List of all supported provider type names.
        /// </summary>
        public static readonly string[] SupportedProviders = new[]
        {
            "OpenAI", "OpenRouter", "HuggingFace", "Anthropic", "Google", "Ollama", "LMStudio"
        };

        #endregion
    }
}
