using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSourceToolkit.NET.Services.Ai;

namespace OpenSourceToolkit.Tests
{
    [TestClass]
    public class AiTests
    {
        #region Mocks

        class MockSecretStorage : ISecretStorage
        {
            private readonly Dictionary<string, string> _store = new Dictionary<string, string>();

            public void Store(string key, string value) => _store[key] = value;
            public string Retrieve(string key) => _store.TryGetValue(key, out var val) ? val : null;
            public void Remove(string key) => _store.Remove(key);
            public bool Contains(string key) => _store.ContainsKey(key);
            public void Clear() => _store.Clear();
        }

        #endregion

        [TestMethod]
        public void AiSettingsManager_SecurelyStoresKeys()
        {
            var storage = new MockSecretStorage();
            var manager = new AiSettingsManager(storage);
            var key = "secret-key-123";

            manager.SetProviderApiKey("OpenAI", key);

            Assert.IsTrue(storage.Contains("provider.OpenAI.apikey"));
            Assert.AreEqual(key, storage.Retrieve("provider.OpenAI.apikey"));

            var provider = manager.GetOrCreateProvider("OpenAI");
            Assert.IsTrue(provider.ApiKey.StartsWith(AiSettingsManager.SecureKeyPrefix));
            Assert.IsFalse(provider.ApiKey.Contains(key));

            Assert.AreEqual(key, manager.GetProviderApiKey("OpenAI"));
        }

        [TestMethod]
        public void AiSettingsManager_MigratesPlainKeys()
        {
            var storage = new MockSecretStorage();
            var manager = new AiSettingsManager(storage);

            var provider = manager.GetOrCreateProvider("OpenAI");
            provider.ApiKey = "plain-text-key";

            var migrated = manager.MigrateToSecureStorage();

            Assert.IsTrue(migrated);
            Assert.IsTrue(provider.ApiKey.StartsWith(AiSettingsManager.SecureKeyPrefix));
            Assert.AreEqual("plain-text-key", storage.Retrieve("provider.OpenAI.apikey"));
        }

        [TestMethod]
        public void AiSettingsManager_ConnectionKeys_OverrideProviderKeys()
        {
            var storage = new MockSecretStorage();
            var manager = new AiSettingsManager(storage);

            manager.SetProviderApiKey("OpenAI", "provider-key");
            var conn = manager.AddConnection("Test", "OpenAI", "gpt-4");

            Assert.AreEqual("provider-key", manager.GetEffectiveApiKey(conn.Id));

            manager.SetConnectionApiKey(conn.Id, "connection-key");
            Assert.AreEqual("connection-key", manager.GetEffectiveApiKey(conn.Id));
        }

        [TestMethod]
        public void AiSettingsManager_ManagesProviderModels()
        {
            var storage = new MockSecretStorage();
            var manager = new AiSettingsManager(storage);

            var defaults = manager.GetProviderModels("OpenAI");
            Assert.IsTrue(defaults.Count > 0);

            manager.AddProviderModel("OpenAI", "custom-gpt");
            var updated = manager.GetProviderModels("OpenAI");
            Assert.IsTrue(updated.Contains("custom-gpt"));

            manager.ResetProviderModels("OpenAI");
            var reset = manager.GetProviderModels("OpenAI");
            Assert.IsFalse(reset.Contains("custom-gpt"));

            manager.SetProviderModels("OpenRouter", new List<string>
            {
                "openai/gpt-5.4-image-2",
                "google/gemini-2.5-flash-image",
                "google/gemini-2.5-pro",
                "google/gemini-3.1-flash-image"
            });
            var filtered = manager.GetProviderModels("OpenRouter");
            Assert.IsFalse(filtered.Contains("openai/gpt-5.4-image-2"));
            Assert.IsFalse(filtered.Contains("google/gemini-2.5-flash-image"));
            Assert.IsFalse(filtered.Contains("google/gemini-2.5-pro"));
            Assert.IsTrue(filtered.Contains("google/gemini-3.1-flash-image"));
        }

        [TestMethod]
        public void AiConnectionConfig_CreateDefault_ReturnsCorrectEndpoints()
        {
            var openai = AiConnectionConfig.CreateDefault(AiProviderType.OpenAI);
            Assert.AreEqual("https://api.openai.com/v1", openai.Endpoint);

            var anthropic = AiConnectionConfig.CreateDefault(AiProviderType.Anthropic);
            Assert.AreEqual("https://api.anthropic.com/v1", anthropic.Endpoint);

            var google = AiConnectionConfig.CreateDefault(AiProviderType.Google);
            Assert.AreEqual("https://generativelanguage.googleapis.com/v1beta", google.Endpoint);

            var huggingFace = AiConnectionConfig.CreateDefault(AiProviderType.HuggingFace);
            Assert.AreEqual("https://router.huggingface.co/v1", huggingFace.Endpoint);
            Assert.AreEqual("openai/gpt-oss-120b", huggingFace.ModelId);

            var ollama = AiConnectionConfig.CreateDefault(AiProviderType.Ollama);
            Assert.AreEqual("http://localhost:11434", ollama.Endpoint);
        }

        [TestMethod]
        public void AiConnectionConfig_IsImageGenerationModel_DetectsCorrectly()
        {
            Assert.IsTrue(AiConnectionConfig.IsImageGenerationModel(AiProviderType.OpenAI, "gpt-image-2"));
            Assert.IsFalse(AiConnectionConfig.IsImageGenerationModel(AiProviderType.OpenAI, "gpt-4o"));

            Assert.IsTrue(AiConnectionConfig.IsImageGenerationModel(AiProviderType.OpenRouter, "google/gemini-3.1-flash-image"));
            Assert.IsTrue(AiConnectionConfig.IsImageGenerationModel(AiProviderType.OpenRouter, "black-forest-labs/flux.2-pro"));
            Assert.IsTrue(AiConnectionConfig.IsImageGenerationModel(AiProviderType.OpenRouter, "krea/krea-1"));
            Assert.IsTrue(AiConnectionConfig.IsImageGenerationModel(AiProviderType.OpenRouter, "vendor/custom-image-generator"));
            Assert.IsTrue(AiConnectionConfig.IsImageGenerationModel(AiProviderType.OpenRouter, "recraft/recraft-v4"));
            Assert.IsFalse(AiConnectionConfig.IsImageGenerationModel(AiProviderType.OpenRouter, "anthropic/claude-sonnet-4.5"));
            Assert.IsFalse(AiConnectionConfig.IsImageGenerationModel(AiProviderType.OpenRouter, "openai/gpt-5.4-image-2"));
            Assert.IsFalse(AiConnectionConfig.IsImageGenerationModel(AiProviderType.OpenRouter, "google/gemini-2.5-flash-image"));

            Assert.IsTrue(AiConnectionConfig.IsImageGenerationModel(AiProviderType.Google, "gemini-3.1-flash-image"));
            Assert.IsFalse(AiConnectionConfig.IsImageGenerationModel(AiProviderType.Google, "gemini-2.5-pro"));
            Assert.IsFalse(AiConnectionConfig.IsImageGenerationModel(AiProviderType.Google, "gemini-2.5-flash-image"));

            Assert.IsTrue(AiConnectionConfig.IsImageGenerationModel(
                AiProviderType.HuggingFace,
                "stabilityai/stable-diffusion-3-medium-diffusers"));
            Assert.IsTrue(AiConnectionConfig.IsImageGenerationModel(
                AiProviderType.HuggingFace,
                "stabilityai/sdxl-turbo"));
            Assert.IsFalse(AiConnectionConfig.IsImageGenerationModel(
                AiProviderType.HuggingFace,
                "openai/gpt-oss-120b"));
        }

        [TestMethod]
        public void AiConnectionConfig_DefaultImageModels_ExcludeRetiredModels()
        {
            var openRouterModels = AiConnectionConfig.GetDefaultImageModels(AiProviderType.OpenRouter);
            CollectionAssert.DoesNotContain(openRouterModels, "openai/gpt-5.4-image-2");
            CollectionAssert.DoesNotContain(openRouterModels, "google/gemini-2.5-flash-image");

            var googleModels = AiConnectionConfig.GetDefaultImageModels(AiProviderType.Google);
            CollectionAssert.DoesNotContain(googleModels, "gemini-2.5-flash-image");

            CollectionAssert.Contains(AiSettingsManager.SupportedProviders, "HuggingFace");
            CollectionAssert.Contains(AiSettingsManager.SupportedConnectionProviders, "Codex");
            CollectionAssert.DoesNotContain(AiSettingsManager.SupportedProviders, "Codex");
            var huggingFaceModels = AiConnectionConfig.GetDefaultImageModels(AiProviderType.HuggingFace);
            CollectionAssert.Contains(
                huggingFaceModels,
                "stabilityai/stable-diffusion-3-medium-diffusers");
        }

        [TestMethod]
        public void OpenRouterImagePayload_UsesGeminiCapabilitiesWithoutChatParameters()
        {
            var createPayload = GetOpenRouterImagePayloadFactory();
            var supportedParameters = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "resolution",
                "aspect_ratio",
                "input_references"
            };
            var inputImages = new List<(byte[] Data, string MimeType)>
            {
                (new byte[] { 1, 2, 3 }, "image/png")
            };

            var payload = (Dictionary<string, object>)createPayload.Invoke(
                null,
                new object[]
                {
                    "google/gemini-3.1-flash-image",
                    "A hot cup of coffee",
                    "1024x1024",
                    "auto",
                    inputImages,
                    supportedParameters
                });

            using var document = JsonDocument.Parse(JsonSerializer.Serialize(payload));
            var root = document.RootElement;

            Assert.AreEqual("google/gemini-3.1-flash-image", root.GetProperty("model").GetString());
            Assert.AreEqual("A hot cup of coffee", root.GetProperty("prompt").GetString());
            Assert.AreEqual("1K", root.GetProperty("resolution").GetString());
            Assert.AreEqual("1:1", root.GetProperty("aspect_ratio").GetString());
            Assert.IsFalse(root.TryGetProperty("quality", out _));
            Assert.IsFalse(root.TryGetProperty("max_tokens", out _));
            Assert.IsFalse(root.TryGetProperty("temperature", out _));
            Assert.IsFalse(root.TryGetProperty("modalities", out _));
            Assert.AreEqual(
                "data:image/png;base64,AQID",
                root.GetProperty("input_references")[0]
                    .GetProperty("image_url")
                    .GetProperty("url")
                    .GetString());
        }

        [TestMethod]
        public void OpenRouterImagePayload_IncludesOnlyAdvertisedOptionalSettings()
        {
            var createPayload = GetOpenRouterImagePayloadFactory();
            var supportedParameters = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "size",
                "quality"
            };

            var payload = (Dictionary<string, object>)createPayload.Invoke(
                null,
                new object[]
                {
                    "openai/gpt-image-2",
                    "A landscape",
                    "1536x1024",
                    "high",
                    new List<(byte[] Data, string MimeType)>(),
                    supportedParameters
                });

            using var document = JsonDocument.Parse(JsonSerializer.Serialize(payload));
            var root = document.RootElement;

            Assert.AreEqual("1536x1024", root.GetProperty("size").GetString());
            Assert.AreEqual("high", root.GetProperty("quality").GetString());
            Assert.IsFalse(root.TryGetProperty("resolution", out _));
            Assert.IsFalse(root.TryGetProperty("aspect_ratio", out _));
            Assert.IsFalse(root.TryGetProperty("input_references", out _));
        }

        private static MethodInfo GetOpenRouterImagePayloadFactory()
        {
            var clientType = typeof(AiConnectionConfig).Assembly.GetType(
                "OpenSourceToolkit.NET.Services.Ai.OpenRouterImageApiClient");
            Assert.IsNotNull(clientType);

            var createPayload = clientType.GetMethod(
                "CreatePayload",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(createPayload);
            return createPayload;
        }

        [TestMethod]
        public void AiConnection_Clone_CreatesIndependentCopy()
        {
            var original = new AiConnection
            {
                Name = "Test",
                ProviderType = "OpenAI",
                ModelId = "gpt-4",
                MaxTokens = 8000,
                Temperature = 0.5
            };

            var clone = original.Clone();

            Assert.AreEqual(original.Id, clone.Id);
            Assert.AreEqual(original.Name, clone.Name);
            Assert.AreEqual(original.MaxTokens, clone.MaxTokens);

            clone.Name = "Modified";
            Assert.AreNotEqual(original.Name, clone.Name);
        }

        [TestMethod]
        public void AiSettingsManager_RemoveConnection_CleansUpSecureStorage()
        {
            var storage = new MockSecretStorage();
            var manager = new AiSettingsManager(storage);

            var conn = manager.AddConnection("Test", "OpenAI", "gpt-4", "custom-key");
            var connId = conn.Id;

            Assert.IsTrue(storage.Contains($"connection.{connId}.apikey"));

            manager.RemoveConnection(connId);

            Assert.IsFalse(storage.Contains($"connection.{connId}.apikey"));
            Assert.AreEqual(0, manager.Connections.Count);
        }

        [TestMethod]
        public void AiSettingsManager_CreateConfigFromConnection_ReturnsCorrectConfig()
        {
            var storage = new MockSecretStorage();
            var manager = new AiSettingsManager(storage);

            manager.SetProviderApiKey("OpenAI", "provider-key");
            var conn = manager.AddConnection("Test", "OpenAI", "gpt-4o");
            conn.MaxTokens = 16000;
            conn.Temperature = 0.3;

            var config = manager.CreateConfigFromConnection(conn.Id);

            Assert.IsNotNull(config);
            Assert.AreEqual(AiProviderType.OpenAI, config.ProviderType);
            Assert.AreEqual("provider-key", config.ApiKey);
            Assert.AreEqual("gpt-4o", config.ModelId);
            Assert.AreEqual(16000, config.MaxTokens);
            Assert.AreEqual(0.3, config.Temperature, 0.001);
        }

        [TestMethod]
        public void AiSettingsManager_OpenAICompatibleConnection_PreservesEndpointAndOptionalKey()
        {
            var storage = new MockSecretStorage();
            var manager = new AiSettingsManager(storage);
            var connection = manager.AddConnection(
                "Local gateway",
                "OpenAI-Compatible",
                "custom-model",
                customEndpoint: "http://localhost:8080/v1");

            var config = manager.CreateConfigFromConnection(connection.Id);

            Assert.IsNotNull(config);
            Assert.AreEqual(AiProviderType.OpenAICompatible, config.ProviderType);
            Assert.AreEqual("http://localhost:8080/v1", config.Endpoint);
            Assert.AreEqual("custom-model", config.ModelId);
            Assert.IsNull(config.ApiKey);
            CollectionAssert.Contains(
                AiSettingsManager.SupportedConnectionProviders,
                "OpenAI-Compatible");
            CollectionAssert.DoesNotContain(
                AiSettingsManager.SupportedProviders,
                "OpenAI-Compatible");
        }
    }
}
