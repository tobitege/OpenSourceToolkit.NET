using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenSourceToolkit.NET.Services.Ai;

namespace OpenSourceToolkit.NET.Services
{
    public static class AppSettings
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenSourceToolkit.NET",
            "settings.json"
        );
        private static readonly SettingsFileStore SettingsStore = new SettingsFileStore(
            SettingsPath,
            @"Local\OpenSourceToolkit.NET.Settings.v1");

        private static SettingsData _settings;
        private static AiSettingsManager _aiManager;

        public static SettingsData Current
        {
            get
            {
                if (_settings == null)
                    Load();
                return _settings;
            }
        }

        /// <summary>
        /// Gets the AI settings manager with secure storage integration.
        /// </summary>
        public static AiSettingsManager AiManager
        {
            get
            {
                if (_aiManager == null)
                {
                    _aiManager = new AiSettingsManager(SecureStorage.Default);
                    SyncAiManagerFromSettings();
                }
                return _aiManager;
            }
        }

        public static void Load()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[AppSettings.Load] Reading from: {SettingsPath}");
                _settings = SettingsStore.Load(CreateDefaultSettings);
                System.Diagnostics.Debug.WriteLine($"[AppSettings.Load] DaisyUiTheme after load: '{_settings.DaisyUiTheme}'");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppSettings.Load] ERROR: {ex.Message}");
                _settings = CreateDefaultSettings();
                return;
            }

            // A provider-specific sync failure must never discard settings that were loaded successfully.
            if (_aiManager != null)
            {
                try
                {
                    SyncAiManagerFromSettings();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AppSettings.Load] AI sync error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Syncs the AiSettingsManager from the loaded JSON settings and performs migration.
        /// </summary>
        private static void SyncAiManagerFromSettings()
        {
            if (_settings?.AiSettings == null || _aiManager == null)
                return;

            // Sync providers
            _aiManager.Providers.Clear();
            if (_settings.AiSettings.ProviderApiKeys != null)
            {
                foreach (var p in _settings.AiSettings.ProviderApiKeys)
                {
                    _aiManager.Providers.Add(new AiProviderConfig
                    {
                        ProviderType = p.ProviderType,
                        ApiKey = p.ApiKey,
                        Endpoint = p.CustomEndpoint
                    });
                }
            }

            // Sync connections
            _aiManager.Connections.Clear();
            if (_settings.AiSettings.Connections != null)
            {
                foreach (var c in _settings.AiSettings.Connections)
                {
                    _aiManager.Connections.Add(new AiConnection
                    {
                        Id = c.Id,
                        Name = c.Name,
                        ProviderType = c.ProviderType,
                        ModelId = c.ModelId,
                        CustomApiKey = c.CustomApiKey,
                        CustomEndpoint = c.CustomEndpoint,
                        MaxTokens = c.MaxTokens,
                        Temperature = c.Temperature,
                        SupportsMultiModalInput = c.SupportsMultiModalInput,
                        SupportsImageGeneration = c.SupportsImageGeneration
                    });
                }
            }

            // Sync provider models
            _aiManager.ProviderModels = _settings.AiSettings.ProviderModels != null
                ? new Dictionary<string, List<string>>(_settings.AiSettings.ProviderModels)
                : new Dictionary<string, List<string>>();
        }

        /// <summary>
        /// Syncs the JSON settings from the AiSettingsManager (for saving).
        /// </summary>
        private static void SyncSettingsFromAiManager()
        {
            if (_settings?.AiSettings == null || _aiManager == null)
                return;

            // Sync providers back
            _settings.AiSettings.ProviderApiKeys = _aiManager.Providers.Select(p => new AiProviderApiKey
            {
                ProviderType = p.ProviderType,
                ApiKey = p.ApiKey,
                CustomEndpoint = p.Endpoint
            }).ToList();

            // Sync connections back
            _settings.AiSettings.Connections = _aiManager.Connections.Select(c => new AiConnectionData
            {
                Id = c.Id,
                Name = c.Name,
                ProviderType = c.ProviderType,
                ModelId = c.ModelId,
                CustomApiKey = c.CustomApiKey,
                CustomEndpoint = c.CustomEndpoint,
                MaxTokens = c.MaxTokens,
                Temperature = c.Temperature,
                SupportsMultiModalInput = c.SupportsMultiModalInput,
                SupportsImageGeneration = c.SupportsImageGeneration
            }).ToList();

            // Sync provider models back
            _settings.AiSettings.ProviderModels = new Dictionary<string, List<string>>(_aiManager.ProviderModels);
        }

        private static SettingsData CreateDefaultSettings()
        {
            return new SettingsData
            {
                // Default favorite tool IDs:
                // 1 = UUID Generator, 12 = Color Toolkit, 20 = PDF Tools,
                // 29 = Password Generator, 31 = JSON Formatter, 32 = Image Editor
                FavoriteToolIds = new List<int> { 1, 12, 20, 29, 31, 32 }
            };
        }

        public static void Save()
        {
            try
            {
                // Sync from AI manager before saving
                if (_aiManager != null)
                    SyncSettingsFromAiManager();

                if (SettingsStore.Save(_settings))
                    System.Diagnostics.Debug.WriteLine($"[AppSettings.Save] Saved to: {SettingsPath}");
                else
                    System.Diagnostics.Debug.WriteLine("[AppSettings.Save] Skipped because settings were changed by another process.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppSettings.Save] ERROR: {ex.Message}");
            }
        }

        #region Editor Font

        /// <summary>
        /// Event raised when the editor font changes.
        /// </summary>
        public static event Action<string> EditorFontChanged;

        /// <summary>
        /// Sets the editor font family and raises the EditorFontChanged event.
        /// </summary>
        public static void SetEditorFont(string fontFamily)
        {
            Current.EditorFontFamily = fontFamily;
            Save();
            EditorFontChanged?.Invoke(fontFamily);
        }

        #endregion

        #region Secure Token Storage

        private const string GitHubTokenKey = "github_token";

        /// <summary>
        /// Gets the GitHub Personal Access Token from secure storage.
        /// </summary>
        public static string GetGitHubToken()
        {
            try
            {
                return SecureStorage.Retrieve(GitHubTokenKey);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Sets the GitHub Personal Access Token in secure storage.
        /// </summary>
        public static void SetGitHubToken(string token)
        {
            try
            {
                if (string.IsNullOrEmpty(token))
                    SecureStorage.Remove(GitHubTokenKey);
                else
                    SecureStorage.Store(GitHubTokenKey, token);
            }
            catch
            {
                // Ignore storage errors
            }
        }

        #endregion
    }

    public class SettingsData
    {
        public int SettingsSchemaVersion { get; set; } = 1;

        // Audio Noise Reduction settings
        public string AudioInputDeviceName { get; set; }
        public string AudioExportFormat { get; set; } = "WAV";
        public int AudioMp3Bitrate { get; set; } = 192;

        // Window position/size settings
        public double? WindowX { get; set; }
        public double? WindowY { get; set; }
        public double? WindowWidth { get; set; }
        public double? WindowHeight { get; set; }
        public bool WindowMaximized { get; set; }

        // Generic tool settings storage (key = "ToolName.PropertyName", value = JSON)
        public Dictionary<string, string> ToolSettings { get; set; } = new Dictionary<string, string>();

        // Sidebar group collapsed states (key = group name, value = isExpanded)
        public Dictionary<string, bool> GroupExpandedStates { get; set; } = new Dictionary<string, bool>();

        // Favorite tool IDs
        public List<int> FavoriteToolIds { get; set; } = new List<int>();

        // Image Converter - Watermark settings
        public string WatermarkText { get; set; } = "";
        public string WatermarkPosition { get; set; } = "BottomRight";
        public int WatermarkOpacity { get; set; } = 50;
        public int WatermarkFontSize { get; set; } = 24;
        public string WatermarkColor { get; set; } = "#FFFFFF";
        public int WatermarkPadding { get; set; } = 10;

        // Theme setting
        public string Theme { get; set; } = "dark";

        // Semi theme locale (e.g., "en-US", "de-DE", "zh-CN")
        // null = not set yet, will auto-detect from system on first start
        public string Locale { get; set; }

        // DaisyUI theme overlay (null = no overlay, use Semi theme only)
        public string DaisyUiTheme { get; set; }

        // UI language for ToolkitLocalization (e.g., "en-US", "de-DE")
        // null = auto-detect from system on first start
        public string Language { get; set; }

        // Custom editor font for multi-line textboxes (null = use system default)
        public string EditorFontFamily { get; set; }

        // AI Provider settings
        public AiSettingsData AiSettings { get; set; } = new AiSettingsData();

        // Image Editor Session settings
        public ImageEditorSessionSettings ImageEditorSessions { get; set; } = new ImageEditorSessionSettings();
    }

    /// <summary>
    /// Settings for Image Editor session management.
    /// </summary>
    public class ImageEditorSessionSettings
    {
        /// <summary>ID of the last active session to restore on startup</summary>
        public string LastActiveSessionId { get; set; }

        /// <summary>Whether to automatically save sessions (default: true)</summary>
        public bool AutoSaveSessions { get; set; } = true;

        /// <summary>Auto-save delay in milliseconds (default: 5000ms)</summary>
        public int AutoSaveDelayMs { get; set; } = 5000;

        /// <summary>Whether the thumbnail strip is collapsed (default: false)</summary>
        public bool ThumbnailStripCollapsed { get; set; } = false;

        /// <summary>AI chat font size in points (default: 14)</summary>
        public int AiChatFontSize { get; set; } = 14;

        /// <summary>Sidebar width as percentage of available space (0.0-1.0, default: 0.35 = 35%)</summary>
        public double SidebarWidthPercent { get; set; } = 0.35;
    }

    public class AiSettingsData
    {
        public AiAccessMode? OpenAiAccessMode { get; set; }

        // Provider API keys (one per provider type)
        public List<AiProviderApiKey> ProviderApiKeys { get; set; } = new List<AiProviderApiKey>();

        // Named connections (max 50)
        public List<AiConnectionData> Connections { get; set; } = new List<AiConnectionData>();

        // Per-provider model lists (user-editable)
        public Dictionary<string, List<string>> ProviderModels { get; set; } = new Dictionary<string, List<string>>();
    }

    /// <summary>
    /// JSON serialization class for provider API keys.
    /// Actual key retrieval/storage is handled by AiSettingsManager.
    /// </summary>
    public class AiProviderApiKey
    {
        public string ProviderType { get; set; }
        public string ApiKey { get; set; }
        public string CustomEndpoint { get; set; }
    }

    /// <summary>
    /// JSON serialization class for AI connections.
    /// Actual key retrieval/storage is handled by AiSettingsManager.
    /// </summary>
    public class AiConnectionData
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ProviderType { get; set; }
        public string ModelId { get; set; }
        public string CustomApiKey { get; set; }
        public string CustomEndpoint { get; set; }
        public int MaxTokens { get; set; } = 4096;
        public double Temperature { get; set; } = 0.7;
        public bool SupportsMultiModalInput { get; set; }
        public bool SupportsImageGeneration { get; set; }
    }
}
