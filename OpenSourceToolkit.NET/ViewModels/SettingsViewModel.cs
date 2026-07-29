using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LlmTornado;
using LlmTornado.Code;
using OpenSourceToolkit.NET.Helpers;
using OpenSourceToolkit.NET.Localization;
using OpenSourceToolkit.NET.Services;
using OpenSourceToolkit.NET.Services.Ai;

namespace OpenSourceToolkit.NET.ViewModels
{
    public class SettingsViewModel : ViewModelBase, IDisposable
    {
        private const int MaxConnections = 50;
        private readonly AiAccessManager _aiAccessManager;

        /// <summary>
        /// Action to show exception details in a popup (DEBUG builds only).
        /// </summary>
        public Action<Exception> ShowDebugExceptionAction { get; set; }

        /// <summary>
        /// Action to prompt user to save/discard unsaved changes. Returns true if user wants to proceed (save or discard), false to cancel.
        /// Parameters: (message, callback with result: true=Save, false=Discard, null=Cancel)
        /// </summary>
        public Func<string, Task<bool?>> PromptSaveChangesAction { get; set; }

        #region General Settings

        private string _audioInputDeviceName;
        public string AudioInputDeviceName
        {
            get => _audioInputDeviceName;
            set
            {
                if (SetProperty(ref _audioInputDeviceName, value))
                    AppSettings.Current.AudioInputDeviceName = value;
            }
        }

        private string _audioExportFormat;
        public string AudioExportFormat
        {
            get => _audioExportFormat;
            set
            {
                if (SetProperty(ref _audioExportFormat, value))
                    AppSettings.Current.AudioExportFormat = value;
            }
        }

        private int _audioMp3Bitrate;
        public int AudioMp3Bitrate
        {
            get => _audioMp3Bitrate;
            set
            {
                if (SetProperty(ref _audioMp3Bitrate, value))
                    AppSettings.Current.AudioMp3Bitrate = value;
            }
        }

        private ThemeVariant _activeTheme = ThemeVariant.Dark;
        public ThemeVariant ActiveTheme
        {
            get => _activeTheme;
            private set => SetProperty(ref _activeTheme, value);
        }

        public string[] AudioFormats { get; } = new[] { "WAV", "MP3" };
        public int[] Mp3Bitrates { get; } = new[] { 128, 192, 256, 320 };

        private string _gitHubToken;
        public string GitHubToken
        {
            get => _gitHubToken;
            set
            {
                if (SetProperty(ref _gitHubToken, value))
                {
                    AppSettings.SetGitHubToken(value);
                    OnPropertyChanged(nameof(HasGitHubToken));
                }
            }
        }

        public bool HasGitHubToken => !string.IsNullOrEmpty(GitHubToken);

        // Locale selector for Semi theme
        public LocaleItem[] AvailableLocales { get; } = new[]
        {
            new LocaleItem("English (US)", "en-US"),
            new LocaleItem("English (UK)", "en-GB"),
            new LocaleItem("Deutsch", "de-DE"),
            new LocaleItem("Español", "es-ES"),
            new LocaleItem("Français", "fr-FR"),
            new LocaleItem("Italiano", "it-IT"),
            new LocaleItem("Nederlands", "nl-NL"),
            new LocaleItem("Polski", "pl-PL"),
            new LocaleItem("Русский", "ru-RU"),
            new LocaleItem("Українська", "uk-UA"),
            new LocaleItem("日本語", "ja-JP"),
            new LocaleItem("한국어", "ko-KR"),
            new LocaleItem("简体中文", "zh-CN"),
            new LocaleItem("繁體中文", "zh-TW"),
        };

        private LocaleItem _selectedLocale;
        public LocaleItem SelectedLocale
        {
            get => _selectedLocale;
            set
            {
                if (SetProperty(ref _selectedLocale, value) && value != null)
                {
                    AppSettings.Current.Locale = value.Code;
                    ApplyLocale(value.Code);
                    AppSettings.Save();
                }
            }
        }

        private static void ApplyLocale(string localeCode)
        {
            // Locale setting is stored for future use
        }

        // UI Language selector for ToolkitLocalization
        public LocaleItem[] AvailableLanguages { get; } = new[]
        {
            new LocaleItem("English", "en-US"),
            new LocaleItem("Deutsch", "de-DE"),
            new LocaleItem("Français", "fr-FR"),
            new LocaleItem("Español", "es-ES"),
            new LocaleItem("Italiano", "it-IT"),
            new LocaleItem("中文 (简体)", "zh-Hans"),
            new LocaleItem("한국어", "ko-KR"),
            new LocaleItem("日本語", "ja-JP"),
            new LocaleItem("العربية", "ar-SA"),
            new LocaleItem("Türkçe", "tr-TR"),
            new LocaleItem("Українська", "uk-UA"),
        };

        private LocaleItem _selectedLanguage;
        public LocaleItem SelectedLanguage
        {
            get => _selectedLanguage;
            set
            {
                if (SetProperty(ref _selectedLanguage, value) && value != null)
                {
                    // Apply UI language change
                    ToolkitLocalization.SetCulture(value.Code);
                    RefreshOpenAiAccessModes();
                    SynchronizeOpenAiAccessState();
                    
                    // Save to settings
                    AppSettings.Current.Language = value.Code;
                    AppSettings.Save();
                }
            }
        }

        /// <summary>
        /// Applies the saved locale setting. Call this on app startup.
        /// </summary>
        public static void ApplySavedLocale()
        {
            var locale = AppSettings.Current.Locale;
            if (string.IsNullOrEmpty(locale))
            {
                // First start - detect and save system locale
                locale = DetectSystemLocale();
                AppSettings.Current.Locale = locale;
                AppSettings.Save();
            }
            ApplyLocale(locale);
        }

        /// <summary>
        /// Detects the system locale and returns a matching supported locale code,
        /// or "en-US" if no match is found.
        /// </summary>
        private static string DetectSystemLocale()
        {
            var supportedLocales = new[]
            {
                "en-US", "en-GB", "de-DE", "es-ES", "fr-FR", "it-IT", "zh-Hans",
                "ko-KR", "ja-JP", "ar-SA", "tr-TR", "uk-UA"
            };

            try
            {
                var systemCulture = CultureInfo.CurrentUICulture;

                // Try exact match first (e.g., "de-DE")
                var exactMatch = supportedLocales.FirstOrDefault(l =>
                    l.Equals(systemCulture.Name, StringComparison.OrdinalIgnoreCase));
                if (exactMatch != null)
                    return exactMatch;

                // Try language-only match (e.g., "de" matches "de-DE")
                var langCode = systemCulture.TwoLetterISOLanguageName;
                var langMatch = supportedLocales.FirstOrDefault(l =>
                    l.StartsWith(langCode + "-", StringComparison.OrdinalIgnoreCase));
                if (langMatch != null)
                    return langMatch;
            }
            catch
            {
                // Ignore culture detection errors
            }

            return "en-US";
        }

        #endregion

        #region AI Connections

        public ObservableCollection<AiConnectionViewModel> Connections { get; } = new ObservableCollection<AiConnectionViewModel>();

        private AiConnectionViewModel _selectedConnection;
        private bool _isProcessingSelection;
        private bool _suppressSelectionChange;

        public AiConnectionViewModel SelectedConnection
        {
            get => _selectedConnection;
            set
            {
                if (_suppressSelectionChange || _isProcessingSelection || value == _selectedConnection) return;
                _ = HandleConnectionSelectionAsync(value);
            }
        }

        private async Task HandleConnectionSelectionAsync(AiConnectionViewModel newSelection)
        {
            if (_isProcessingSelection) return;
            _isProcessingSelection = true;

            var previousSelection = _selectedConnection;

            try
            {
                if (HasUnsavedConnectionChanges && PromptSaveChangesAction != null)
                {
                    var result = await PromptSaveChangesAction("You have unsaved changes. Do you want to save them?");
                    if (result == null)
                    {
                        // Cancel - restore previous selection in UI
                        _suppressSelectionChange = true;
                        try
                        {
                            // Force ListBox to re-select by toggling through null
                            _selectedConnection = null;
                            OnPropertyChanged(nameof(SelectedConnection));
                            _selectedConnection = previousSelection;
                            OnPropertyChanged(nameof(SelectedConnection));
                        }
                        finally
                        {
                            _suppressSelectionChange = false;
                        }
                        return;
                    }
                    if (result == true)
                        SaveConnection();
                    else
                        ResetDirtyTracking(); // Discard - clear dirty state
                }

                _selectedConnection = newSelection;
                OnPropertyChanged(nameof(SelectedConnection));
                ((RelayCommand)EditConnectionCommand)?.NotifyCanExecuteChanged();
                ((RelayCommand)DeleteConnectionCommand)?.NotifyCanExecuteChanged();
                if (newSelection != null)
                    StartEditConnection();
            }
            finally
            {
                _isProcessingSelection = false;
            }
        }

        private bool _isEditingConnection;
        public bool IsEditingConnection
        {
            get => _isEditingConnection;
            set
            {
                if (SetProperty(ref _isEditingConnection, value))
                {
                    NotifyConnectionEditChanged();
                    OnPropertyChanged(nameof(IsConnectionNameMissing));
                }
            }
        }

        private bool _isAddingConnection;
        public bool IsAddingConnection
        {
            get => _isAddingConnection;
            set
            {
                if (SetProperty(ref _isAddingConnection, value))
                    NotifyConnectionEditChanged();
            }
        }

        // Edit form fields
        private string _editConnectionName;
        public string EditConnectionName
        {
            get => _editConnectionName;
            set
            {
                if (SetProperty(ref _editConnectionName, value))
                {
                    NotifyConnectionEditChanged();
                    OnPropertyChanged(nameof(IsConnectionNameMissing));
                }
            }
        }

        public bool IsConnectionNameMissing =>
            IsEditingConnection && string.IsNullOrWhiteSpace(EditConnectionName);

        private string _editSelectedProvider;
        public string EditSelectedProvider
        {
            get => _editSelectedProvider;
            set
            {
                if (SetProperty(ref _editSelectedProvider, value))
                {
                    OnPropertyChanged(nameof(IsEditingCodexConnection));
                    OnPropertyChanged(nameof(IsEditingOpenAICompatibleConnection));
                    OnPropertyChanged(nameof(IsEditingApiConnection));
                    if (IsEditingCodexConnection)
                    {
                        EditShowCustomApiKey = false;
                        EditCustomApiKey = "";
                        EditCustomEndpoint = "";
                        EditSupportsMultiModal = false;
                        EditSupportsImageGeneration = false;
                    }
                    else
                    {
                        EditShowCustomApiKey = IsEditingOpenAICompatibleConnection;
                        if (!IsEditingOpenAICompatibleConnection)
                            EditCustomEndpoint = "";
                    }
                    UpdateEditAvailableModels();
                    // Select first model by default
                    EditSelectedModel = IsEditingCodexConnection
                        ? _aiAccessManager.SelectedSubscriptionModelId ?? EditAvailableModels?.FirstOrDefault()
                        : EditAvailableModels?.FirstOrDefault();
                    NotifyConnectionEditChanged();
                }
            }
        }

        public bool IsEditingCodexConnection =>
            string.Equals(EditSelectedProvider, "Codex", StringComparison.Ordinal);

        public bool IsEditingOpenAICompatibleConnection =>
            string.Equals(EditSelectedProvider, "OpenAI-Compatible", StringComparison.Ordinal);

        public bool IsEditingApiConnection => !IsEditingCodexConnection;
        public bool ShowTestConnectionAction =>
            IsEditingApiConnection || IsOpenAiSubscriptionAuthenticated;
        public bool CanTestConnection
        {
            get
            {
                if (IsTestingConnection ||
                    !ShowTestConnectionAction ||
                    string.IsNullOrWhiteSpace(EditSelectedProvider))
                {
                    return false;
                }

                if (IsEditingCodexConnection)
                    return true;

                return (!IsEditingOpenAICompatibleConnection || TryGetOpenAICompatibleEndpoint(out _)) &&
                       !string.IsNullOrWhiteSpace(EditSelectedModel) &&
                       (ApiProviderCanConnectWithoutKey() ||
                        !string.IsNullOrWhiteSpace(GetEffectiveConnectionTestApiKey()));
            }
        }

        private string _editSelectedModel;
        public string EditSelectedModel
        {
            get => _editSelectedModel;
            set
            {
                if (SetProperty(ref _editSelectedModel, value))
                    NotifyConnectionEditChanged();
            }
        }

        private string _editCustomEndpoint;
        public string EditCustomEndpoint
        {
            get => _editCustomEndpoint;
            set
            {
                if (SetProperty(ref _editCustomEndpoint, value))
                    NotifyConnectionEditChanged();
            }
        }

        private bool _editShowCustomApiKey;
        public bool EditShowCustomApiKey
        {
            get => _editShowCustomApiKey;
            set
            {
                if (SetProperty(ref _editShowCustomApiKey, value))
                    NotifyConnectionEditChanged();
            }
        }

        private string _editCustomApiKey;
        public string EditCustomApiKey
        {
            get => _editCustomApiKey;
            set
            {
                if (SetProperty(ref _editCustomApiKey, value))
                    NotifyConnectionEditChanged();
            }
        }

        private int _editMaxTokens = 4096;
        public int EditMaxTokens
        {
            get => _editMaxTokens;
            set
            {
                if (SetProperty(ref _editMaxTokens, value))
                    NotifyConnectionEditChanged();
            }
        }

        private double _editTemperature = 0.7;
        public double EditTemperature
        {
            get => _editTemperature;
            set
            {
                if (SetProperty(ref _editTemperature, value))
                    NotifyConnectionEditChanged();
            }
        }

        private bool _editSupportsMultiModal;
        public bool EditSupportsMultiModal
        {
            get => _editSupportsMultiModal;
            set
            {
                if (SetProperty(ref _editSupportsMultiModal, value))
                    NotifyConnectionEditChanged();
            }
        }

        private bool _editSupportsImageGeneration;
        public bool EditSupportsImageGeneration
        {
            get => _editSupportsImageGeneration;
            set
            {
                if (SetProperty(ref _editSupportsImageGeneration, value))
                    NotifyConnectionEditChanged();
            }
        }

        private List<string> _editAvailableModels = new List<string>();
        public List<string> EditAvailableModels
        {
            get => _editAvailableModels;
            set => SetProperty(ref _editAvailableModels, value);
        }

        private List<AiModelOption> _editAvailableModelOptions = new List<AiModelOption>();
        public List<AiModelOption> EditAvailableModelOptions
        {
            get => _editAvailableModelOptions;
            set => SetProperty(ref _editAvailableModelOptions, value);
        }

        private string _connectionTestStatus;
        public string ConnectionTestStatus
        {
            get => _connectionTestStatus;
            set => SetProperty(ref _connectionTestStatus, value);
        }

        private bool _isTestingConnection;
        public bool IsTestingConnection
        {
            get => _isTestingConnection;
            set
            {
                if (SetProperty(ref _isTestingConnection, value))
                    NotifyTestConnectionStateChanged();
            }
        }

        public bool CanAddConnection => Connections.Count < MaxConnections;

        // Original values for dirty tracking
        private string _originalConnectionName;
        private string _originalProvider;
        private string _originalModel;
        private string _originalCustomEndpoint;
        private int _originalMaxTokens;
        private double _originalTemperature;
        private bool _originalSupportsMultiModal;
        private bool _originalSupportsImageGeneration;
        public bool HasUnsavedConnectionChanges
        {
            get
            {
                if (!IsEditingConnection) return false;

                return EditConnectionName != _originalConnectionName ||
                       EditSelectedProvider != _originalProvider ||
                       EditSelectedModel != _originalModel ||
                       EditCustomEndpoint != _originalCustomEndpoint ||
                       EditMaxTokens != _originalMaxTokens ||
                       Math.Abs(EditTemperature - _originalTemperature) > 0.001 ||
                       EditSupportsMultiModal != _originalSupportsMultiModal ||
                       EditSupportsImageGeneration != _originalSupportsImageGeneration ||
                       EditShowCustomApiKey && !string.IsNullOrEmpty(EditCustomApiKey);
            }
        }

        private void NotifyConnectionEditChanged()
        {
            OnPropertyChanged(nameof(HasUnsavedConnectionChanges));
            (SaveConnectionCommand as RelayCommand)?.NotifyCanExecuteChanged();
            NotifyTestConnectionStateChanged();
        }

        private void NotifyTestConnectionStateChanged()
        {
            OnPropertyChanged(nameof(ShowTestConnectionAction));
            OnPropertyChanged(nameof(CanTestConnection));
            (TestConnectionCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        }

        private void ResetDirtyTracking()
        {
            IsEditingConnection = false;
            IsAddingConnection = false;
            _originalConnectionName = null;
        }

        #endregion

        #region AI Provider API Keys

        public ObservableCollection<ProviderApiKeyViewModel> ProviderApiKeys { get; } = new ObservableCollection<ProviderApiKeyViewModel>();

        private ProviderApiKeyViewModel _selectedProviderApiKey;
        public ProviderApiKeyViewModel SelectedProviderApiKey
        {
            get => _selectedProviderApiKey;
            set
            {
                if (SetProperty(ref _selectedProviderApiKey, value))
                {
                    _textModelSearchQuery = "";
                    _imageModelSearchQuery = "";
                    OnPropertyChanged(nameof(TextModelSearchQuery));
                    OnPropertyChanged(nameof(ImageModelSearchQuery));
                    UpdateProviderModels();
                    NotifyOpenAiAccessProperties();
                }
            }
        }

        public ObservableCollection<AiAccessModeOption> OpenAiAccessModes { get; } =
            new ObservableCollection<AiAccessModeOption>();

        private AiAccessModeOption _selectedOpenAiAccessMode;
        public AiAccessModeOption SelectedOpenAiAccessMode
        {
            get => _selectedOpenAiAccessMode;
            set
            {
                if (value != null && SetProperty(ref _selectedOpenAiAccessMode, value))
                    _ = ChangeOpenAiAccessModeAsync(value.Mode);
            }
        }

        public bool IsOpenAiProviderSelected =>
            string.Equals(SelectedProviderApiKey?.ProviderType, "OpenAI", StringComparison.Ordinal);

        public bool IsOpenAiApiMode => _aiAccessManager.Mode == AiAccessMode.OpenAiApi;
        public bool IsOpenAiSubscriptionMode => !IsOpenAiApiMode;
        public bool ShowProviderApiConfiguration => !IsOpenAiProviderSelected || IsOpenAiApiMode;
        public bool ShowOpenAiSubscriptionConfiguration =>
            IsOpenAiProviderSelected && IsOpenAiSubscriptionMode;
        public bool ShowOpenAiSubscriptionConnectAction =>
            ShowOpenAiSubscriptionConfiguration;
        public bool ShowOpenAiSubscriptionSetupActions =>
            ShowOpenAiSubscriptionConfiguration && !IsOpenAiSubscriptionAuthenticated;
        public bool ShowOpenAiSubscriptionLogoutAction =>
            ShowOpenAiSubscriptionConfiguration && IsOpenAiSubscriptionAuthenticated;
        public bool OpenAiRequiresCodexInstallation =>
            _aiAccessManager.Capabilities.RequiresCodexInstallation;
        public bool IsOpenAiSubscriptionAuthenticated => _aiAccessManager.IsAuthenticated;
        public bool ShowOpenAiSubscriptionReasoningEffort =>
            ShowOpenAiSubscriptionConfiguration &&
            OpenAiSubscriptionReasoningEfforts.Count > 0;
        public bool ShowOpenAiSubscriptionServiceTier =>
            ShowOpenAiSubscriptionConfiguration &&
            OpenAiSubscriptionServiceTiers.Count > 1;

        public ObservableCollection<AiSubscriptionModel> OpenAiSubscriptionModels { get; } =
            new ObservableCollection<AiSubscriptionModel>();
        public ObservableCollection<AiSubscriptionReasoningEffort> OpenAiSubscriptionReasoningEfforts { get; } =
            new ObservableCollection<AiSubscriptionReasoningEffort>();
        public ObservableCollection<AiSubscriptionServiceTier> OpenAiSubscriptionServiceTiers { get; } =
            new ObservableCollection<AiSubscriptionServiceTier>();

        private AiSubscriptionModel _selectedOpenAiSubscriptionModel;
        public AiSubscriptionModel SelectedOpenAiSubscriptionModel
        {
            get => _selectedOpenAiSubscriptionModel;
            set
            {
                if (SetProperty(ref _selectedOpenAiSubscriptionModel, value) &&
                    value != null &&
                    IsOpenAiSubscriptionMode)
                {
                    _aiAccessManager.SelectSubscriptionModel(value.ModelId);
                }
            }
        }

        private AiSubscriptionReasoningEffort _selectedOpenAiSubscriptionReasoningEffort;
        public AiSubscriptionReasoningEffort SelectedOpenAiSubscriptionReasoningEffort
        {
            get => _selectedOpenAiSubscriptionReasoningEffort;
            set
            {
                if (SetProperty(ref _selectedOpenAiSubscriptionReasoningEffort, value) &&
                    value != null &&
                    IsOpenAiSubscriptionMode)
                {
                    _aiAccessManager.SelectSubscriptionReasoningEffort(value.Id);
                }
            }
        }

        private AiSubscriptionServiceTier _selectedOpenAiSubscriptionServiceTier;
        public AiSubscriptionServiceTier SelectedOpenAiSubscriptionServiceTier
        {
            get => _selectedOpenAiSubscriptionServiceTier;
            set
            {
                if (SetProperty(ref _selectedOpenAiSubscriptionServiceTier, value) &&
                    value != null &&
                    IsOpenAiSubscriptionMode)
                {
                    _aiAccessManager.SelectSubscriptionServiceTier(value.Id);
                }
            }
        }

        private string _openAiSubscriptionStatus;
        public string OpenAiSubscriptionStatus
        {
            get => _openAiSubscriptionStatus;
            private set => SetProperty(ref _openAiSubscriptionStatus, value);
        }

        private bool _isOpenAiSubscriptionBusy;
        public bool IsOpenAiSubscriptionBusy
        {
            get => _isOpenAiSubscriptionBusy;
            private set
            {
                if (SetProperty(ref _isOpenAiSubscriptionBusy, value))
                    NotifyOpenAiSubscriptionCommands();
            }
        }

        private Func<Uri, Task<bool>> _openAiBrowserAction;
        public Func<Uri, Task<bool>> OpenAiBrowserAction
        {
            get => _openAiBrowserAction;
            set
            {
                _openAiBrowserAction = value;
                (LoginOpenAiSubscriptionCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
            }
        }

        private readonly List<AiModelOption> _providerModelOptions = new List<AiModelOption>();

        public ObservableCollection<AiModelOption> TextProviderModels { get; } = new ObservableCollection<AiModelOption>();
        public ObservableCollection<AiModelOption> ImageProviderModels { get; } = new ObservableCollection<AiModelOption>();

        private string _textModelSearchQuery = "";
        public string TextModelSearchQuery
        {
            get => _textModelSearchQuery;
            set
            {
                if (SetProperty(ref _textModelSearchQuery, value))
                    RefreshTextProviderModels();
            }
        }

        private string _imageModelSearchQuery = "";
        public string ImageModelSearchQuery
        {
            get => _imageModelSearchQuery;
            set
            {
                if (SetProperty(ref _imageModelSearchQuery, value))
                    RefreshImageProviderModels();
            }
        }

        private string _newModelName;
        public string NewModelName
        {
            get => _newModelName;
            set => SetProperty(ref _newModelName, value);
        }

        private string _providerTestStatus;
        public string ProviderTestStatus
        {
            get => _providerTestStatus;
            set => SetProperty(ref _providerTestStatus, value);
        }

        private bool _isTestingProviderConnection;
        public bool IsTestingProviderConnection
        {
            get => _isTestingProviderConnection;
            set => SetProperty(ref _isTestingProviderConnection, value);
        }

        #endregion

        public string[] AiProviders { get; } = AiSettingsManager.SupportedProviders;
        public string[] ConnectionProviders { get; } = AiSettingsManager.SupportedConnectionProviders;

        #region Commands

        public ICommand SaveCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand ChangeThemeCommand { get; }

        // Connection commands
        public ICommand AddConnectionCommand { get; }
        public ICommand EditConnectionCommand { get; }
        public ICommand DeleteConnectionCommand { get; }
        public ICommand SaveConnectionCommand { get; }
        public ICommand CancelConnectionCommand { get; }
        public ICommand ShowCustomApiKeyCommand { get; }
        public ICommand TestConnectionCommand { get; }

        // Provider model commands
        public ICommand AddModelCommand { get; }
        public ICommand RemoveModelCommand { get; }
        public ICommand ResetModelsCommand { get; }
        public ICommand TestProviderConnectionCommand { get; }
        public ICommand ConnectOpenAiSubscriptionCommand { get; }
        public ICommand LoginOpenAiSubscriptionCommand { get; }
        public ICommand LogoutOpenAiSubscriptionCommand { get; }

        #endregion

        public SettingsViewModel() : this(AiAccessServices.Current)
        {
        }

        public SettingsViewModel(AiAccessManager aiAccessManager)
        {
            _aiAccessManager = aiAccessManager ?? throw new ArgumentNullException(nameof(aiAccessManager));
            RefreshOpenAiAccessModes();
            LoadSettings();

            SaveCommand = new RelayCommand(Save);
            ResetCommand = new RelayCommand(Reset);
            ChangeThemeCommand = new RelayCommand<ThemeVariant>(ChangeTheme);

            AddConnectionCommand = new RelayCommand(StartAddConnection, () => CanAddConnection);
            EditConnectionCommand = new RelayCommand(StartEditConnection, () => SelectedConnection != null);
            DeleteConnectionCommand = new RelayCommand(DeleteConnection, () => SelectedConnection != null);
            SaveConnectionCommand = new RelayCommand(SaveConnection, () => HasUnsavedConnectionChanges);
            CancelConnectionCommand = new AsyncRelayCommand(CancelConnectionEditAsync);
            ShowCustomApiKeyCommand = new RelayCommand(() => EditShowCustomApiKey = true);
            TestConnectionCommand = new AsyncRelayCommand(
                TestConnectionAsync,
                () => CanTestConnection);

            AddModelCommand = new RelayCommand(AddModel);
            RemoveModelCommand = new RelayCommand<AiModelOption>(RemoveModel);
            ResetModelsCommand = new RelayCommand(ResetModelsToDefault);
            TestProviderConnectionCommand = new AsyncRelayCommand(TestProviderConnectionAsync);
            ConnectOpenAiSubscriptionCommand = new AsyncRelayCommand(
                ConnectOpenAiSubscriptionAsync,
                CanConnectOpenAiSubscription);
            LoginOpenAiSubscriptionCommand = new AsyncRelayCommand(
                LoginOpenAiSubscriptionAsync,
                CanLoginOpenAiSubscription);
            LogoutOpenAiSubscriptionCommand = new AsyncRelayCommand(
                LogoutOpenAiSubscriptionAsync,
                CanLogoutOpenAiSubscription);

            _aiAccessManager.StateChanged += OnAiAccessManagerStateChanged;
            SynchronizeOpenAiAccessState();
        }

        private async Task ChangeOpenAiAccessModeAsync(AiAccessMode mode)
        {
            var aiSettings = AppSettings.Current.AiSettings ??= new AiSettingsData();
            aiSettings.OpenAiAccessMode = mode;
            AppSettings.Save();
            await Task.Yield();

            IsOpenAiSubscriptionBusy = true;
            OpenAiSubscriptionStatus = mode == AiAccessMode.OpenAiApi
                ? ToolkitLocalization.GetString("Settings_Providers_OpenAiApiStatus")
                : ToolkitLocalization.GetString("Settings_Providers_CodexConnecting");

            try
            {
                await _aiAccessManager.SwitchModeAsync(mode);
                SynchronizeOpenAiAccessState();
            }
            catch (Exception ex)
            {
                OpenAiSubscriptionStatus = mode == AiAccessMode.CodexAppServer
                    ? ToolkitLocalization.GetString("Settings_Providers_CodexAppServerFailed")
                    : $"{ToolkitLocalization.GetString("Settings_Providers_CodexConnectionFailed")} {SanitizeAuthenticationError(ex.Message)}";
                SynchronizeOpenAiAccessState(preserveStatus: true);
            }
            finally
            {
                IsOpenAiSubscriptionBusy = false;
            }
        }

        private Task ConnectOpenAiSubscriptionAsync()
            => ChangeOpenAiAccessModeAsync(_selectedOpenAiAccessMode.Mode);

        private async Task LoginOpenAiSubscriptionAsync()
        {
            if (OpenAiBrowserAction == null)
                return;

            IsOpenAiSubscriptionBusy = true;
            OpenAiSubscriptionStatus = ToolkitLocalization.GetString("Settings_Providers_CodexWaitingForLogin");
            try
            {
                var result = await _aiAccessManager.LoginAsync(OpenAiBrowserAction);
                OpenAiSubscriptionStatus = result.Success
                    ? ToolkitLocalization.GetString("Settings_Providers_CodexConnected")
                    : $"{ToolkitLocalization.GetString("Settings_Providers_CodexLoginFailed")} {SanitizeAuthenticationError(result.Error)}";
                SynchronizeOpenAiAccessState(preserveStatus: !result.Success);
            }
            catch (Exception ex)
            {
                var failureKey = _aiAccessManager.IsAuthenticated
                    ? "Settings_Providers_CodexConnectionFailed"
                    : "Settings_Providers_CodexLoginFailed";
                OpenAiSubscriptionStatus =
                    $"{ToolkitLocalization.GetString(failureKey)} {SanitizeAuthenticationError(ex.Message)}";
                SynchronizeOpenAiAccessState(preserveStatus: true);
            }
            finally
            {
                IsOpenAiSubscriptionBusy = false;
            }
        }

        private async Task LogoutOpenAiSubscriptionAsync()
        {
            IsOpenAiSubscriptionBusy = true;
            try
            {
                await _aiAccessManager.LogoutAsync();
                OpenAiSubscriptionStatus = ToolkitLocalization.GetString("Settings_Providers_CodexSignedOut");
            }
            catch (Exception ex)
            {
                OpenAiSubscriptionStatus =
                    $"{ToolkitLocalization.GetString("Settings_Providers_CodexLogoutFailed")} {SanitizeAuthenticationError(ex.Message)}";
            }
            finally
            {
                SynchronizeOpenAiAccessState(preserveStatus: true);
                IsOpenAiSubscriptionBusy = false;
            }
        }

        private bool CanConnectOpenAiSubscription()
            => ShowOpenAiSubscriptionConnectAction &&
               !IsOpenAiSubscriptionAuthenticated &&
               !IsOpenAiSubscriptionBusy;

        private bool CanLoginOpenAiSubscription()
            => ShowOpenAiSubscriptionSetupActions &&
               OpenAiBrowserAction != null &&
               !IsOpenAiSubscriptionBusy;

        private bool CanLogoutOpenAiSubscription()
            => ShowOpenAiSubscriptionConfiguration &&
               IsOpenAiSubscriptionAuthenticated &&
               !IsOpenAiSubscriptionBusy;

        private void RefreshOpenAiAccessModes()
        {
            var activeMode = _aiAccessManager.Mode;
            OpenAiAccessModes.Clear();
            OpenAiAccessModes.Add(new AiAccessModeOption(
                AiAccessMode.OpenAiApi,
                ToolkitLocalization.GetString("Settings_Providers_OpenAiApiMode")));
            OpenAiAccessModes.Add(new AiAccessModeOption(
                AiAccessMode.CodexAppServer,
                ToolkitLocalization.GetString("Settings_Providers_CodexAppServerMode")));
            OpenAiAccessModes.Add(new AiAccessModeOption(
                AiAccessMode.CodexOAuth,
                ToolkitLocalization.GetString("Settings_Providers_CodexOAuthMode")));
            _selectedOpenAiAccessMode =
                OpenAiAccessModes.First(option => option.Mode == activeMode);
            OnPropertyChanged(nameof(SelectedOpenAiAccessMode));
        }

        private void SynchronizeOpenAiAccessState(bool preserveStatus = false)
        {
            _selectedOpenAiAccessMode =
                OpenAiAccessModes.First(option => option.Mode == _aiAccessManager.Mode);
            OnPropertyChanged(nameof(SelectedOpenAiAccessMode));

            var openAiProvider = ProviderApiKeys.FirstOrDefault(provider =>
                string.Equals(provider.ProviderType, "OpenAI", StringComparison.Ordinal));
            if (openAiProvider != null)
                openAiProvider.HasSubscriptionAccess = _aiAccessManager.IsAuthenticated;

            OpenAiSubscriptionModels.Clear();
            foreach (var model in _aiAccessManager.SubscriptionModels)
                OpenAiSubscriptionModels.Add(model);

            _selectedOpenAiSubscriptionModel = OpenAiSubscriptionModels.FirstOrDefault(model =>
                string.Equals(
                    model.ModelId,
                    _aiAccessManager.SelectedSubscriptionModelId,
                    StringComparison.Ordinal));
            OnPropertyChanged(nameof(SelectedOpenAiSubscriptionModel));

            OpenAiSubscriptionReasoningEfforts.Clear();
            if (_selectedOpenAiSubscriptionModel != null)
            {
                foreach (var effort in _selectedOpenAiSubscriptionModel.SupportedReasoningEfforts)
                    OpenAiSubscriptionReasoningEfforts.Add(effort);
            }

            _selectedOpenAiSubscriptionReasoningEffort =
                OpenAiSubscriptionReasoningEfforts.FirstOrDefault(effort =>
                    string.Equals(
                        effort.Id,
                        _aiAccessManager.SelectedSubscriptionReasoningEffort,
                        StringComparison.Ordinal));
            OnPropertyChanged(nameof(SelectedOpenAiSubscriptionReasoningEffort));

            OpenAiSubscriptionServiceTiers.Clear();
            if (_selectedOpenAiSubscriptionModel?.ServiceTiers.Count > 0)
            {
                OpenAiSubscriptionServiceTiers.Add(new AiSubscriptionServiceTier(
                    null,
                    ToolkitLocalization.GetString("Settings_Providers_CodexSpeedStandard"),
                    string.Empty));
                foreach (var serviceTier in _selectedOpenAiSubscriptionModel.ServiceTiers)
                    OpenAiSubscriptionServiceTiers.Add(serviceTier);
            }

            _selectedOpenAiSubscriptionServiceTier =
                OpenAiSubscriptionServiceTiers.FirstOrDefault(serviceTier =>
                    string.Equals(
                        serviceTier.Id,
                        _aiAccessManager.SelectedSubscriptionServiceTier,
                        StringComparison.Ordinal));
            OnPropertyChanged(nameof(SelectedOpenAiSubscriptionServiceTier));

            NotifyOpenAiAccessProperties();

            if (!preserveStatus)
            {
                if (IsOpenAiApiMode)
                {
                    OpenAiSubscriptionStatus =
                        ToolkitLocalization.GetString("Settings_Providers_OpenAiApiStatus");
                }
                else if (_aiAccessManager.Account == null)
                {
                    OpenAiSubscriptionStatus =
                        ToolkitLocalization.GetString("Settings_Providers_CodexNotSignedIn");
                }
                else if (string.IsNullOrWhiteSpace(_aiAccessManager.Account.Email))
                {
                    OpenAiSubscriptionStatus =
                        ToolkitLocalization.GetString("Settings_Providers_CodexConnected");
                }
                else
                {
                    OpenAiSubscriptionStatus = string.Format(
                        CultureInfo.CurrentCulture,
                        ToolkitLocalization.GetString("Settings_Providers_CodexConnectedAs"),
                        _aiAccessManager.Account.Email);
                }
            }
        }

        private void NotifyOpenAiAccessProperties()
        {
            OnPropertyChanged(nameof(IsOpenAiProviderSelected));
            OnPropertyChanged(nameof(IsOpenAiApiMode));
            OnPropertyChanged(nameof(IsOpenAiSubscriptionMode));
            OnPropertyChanged(nameof(ShowProviderApiConfiguration));
            OnPropertyChanged(nameof(ShowOpenAiSubscriptionConfiguration));
            OnPropertyChanged(nameof(ShowOpenAiSubscriptionConnectAction));
            OnPropertyChanged(nameof(ShowOpenAiSubscriptionSetupActions));
            OnPropertyChanged(nameof(ShowOpenAiSubscriptionLogoutAction));
            OnPropertyChanged(nameof(OpenAiRequiresCodexInstallation));
            OnPropertyChanged(nameof(IsOpenAiSubscriptionAuthenticated));
            OnPropertyChanged(nameof(ShowOpenAiSubscriptionReasoningEffort));
            OnPropertyChanged(nameof(ShowOpenAiSubscriptionServiceTier));
            NotifyOpenAiSubscriptionCommands();
            NotifyTestConnectionStateChanged();
        }

        private void NotifyOpenAiSubscriptionCommands()
        {
            (ConnectOpenAiSubscriptionCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
            (LoginOpenAiSubscriptionCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
            (LogoutOpenAiSubscriptionCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        }

        private void OnAiAccessManagerStateChanged(object sender, EventArgs e)
        {
            var preserveStatus = IsOpenAiSubscriptionBusy;
            Dispatcher.UIThread.Post(
                () => SynchronizeOpenAiAccessState(preserveStatus));
        }

        private static string SanitizeAuthenticationError(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return ToolkitLocalization.GetString("Settings_Providers_CodexAuthenticationFailed");

            if (message.Contains("sk-", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("API key", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("access_token", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("refresh_token", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("id_token", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("authorization:", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return ToolkitLocalization.GetString("Settings_Providers_CodexAuthenticationFailed");
            }

            return message;
        }

        public void Dispose()
        {
            _aiAccessManager.StateChanged -= OnAiAccessManagerStateChanged;
            OpenAiBrowserAction = null;
        }

        private void LoadSettings()
        {
            var settings = AppSettings.Current;

            // General settings
            _audioInputDeviceName = settings.AudioInputDeviceName;
            _audioExportFormat = settings.AudioExportFormat ?? "WAV";
            _audioMp3Bitrate = settings.AudioMp3Bitrate;
            _gitHubToken = AppSettings.GetGitHubToken();

            // Load locale setting (detect from system if not set)
            var savedLocale = settings.Locale;
            if (string.IsNullOrEmpty(savedLocale))
            {
                // First start - detect from Windows system locale
                savedLocale = DetectSystemLocale();
                settings.Locale = savedLocale;
            }
            _selectedLocale = AvailableLocales.FirstOrDefault(l => l.Code == savedLocale)
                              ?? AvailableLocales.First();

            // Load UI language setting (use current culture or default to English)
            var savedLanguage = settings.Language;
            if (string.IsNullOrEmpty(savedLanguage))
            {
                // Use current ToolkitLocalization culture
                savedLanguage = ToolkitLocalization.CurrentCulture.Name;
            }
            _selectedLanguage = AvailableLanguages.FirstOrDefault(l => l.Code == savedLanguage)
                                ?? AvailableLanguages.First();

            // Sync ActiveTheme with current app theme (already applied at startup in App.axaml.cs)
            var savedTheme = settings.Theme;
            _activeTheme = !string.IsNullOrEmpty(savedTheme) ? savedTheme.ParseThemeVariant() : ThemeVariant.Dark;

            // Load AI settings
            LoadAiSettings();
        }

        private void LoadAiSettings()
        {
            var aiManager = AppSettings.AiManager;

            // Load provider API keys (retrieving actual keys from secure storage)
            ProviderApiKeys.Clear();
            foreach (var provider in AiProviders)
            {
                var vm = new ProviderApiKeyViewModel
                {
                    ProviderType = provider,
                    ApiKey = aiManager.GetProviderApiKey(provider) ?? "",
                    CustomEndpoint = aiManager.GetProviderEndpoint(provider)
                };
                ProviderApiKeys.Add(vm);
            }

            // Wire up save callbacks after loading to avoid saving during load
            foreach (var vm in ProviderApiKeys)
            {
                vm.OnChanged = Save;
            }

            // Load connections
            Connections.Clear();
            foreach (var conn in aiManager.Connections)
            {
                Connections.Add(new AiConnectionViewModel
                {
                    Id = conn.Id,
                    Name = conn.Name,
                    ProviderType = conn.ProviderType,
                    ModelId = conn.ModelId,
                    CustomEndpoint = conn.CustomEndpoint,
                    HasCustomApiKey = !string.IsNullOrEmpty(conn.CustomApiKey),
                    MaxTokens = conn.MaxTokens,
                    Temperature = conn.Temperature,
                    SupportsMultiModalInput = conn.SupportsMultiModalInput,
                    SupportsImageGeneration = conn.SupportsImageGeneration
                });
            }

            if (ProviderApiKeys.Count > 0)
                SelectedProviderApiKey = ProviderApiKeys[0];
        }

        private void UpdateProviderModels()
        {
            _providerModelOptions.Clear();
            if (SelectedProviderApiKey == null)
            {
                RefreshProviderModelFilters();
                return;
            }

            var models = AppSettings.AiManager.GetProviderModels(SelectedProviderApiKey.ProviderType);
            var providerType = ParseProviderType(SelectedProviderApiKey.ProviderType);
            foreach (var model in models)
            {
                _providerModelOptions.Add(new AiModelOption(
                    model,
                    AiConnectionConfig.IsImageGenerationModel(providerType, model)));
            }

            RefreshProviderModelFilters();
        }

        private void RefreshProviderModelFilters()
        {
            RefreshTextProviderModels();
            RefreshImageProviderModels();
        }

        private void RefreshTextProviderModels()
        {
            ReplaceProviderModels(
                TextProviderModels,
                _providerModelOptions
                    .Where(model => model.IsTextOnly && MatchesModelQuery(model, TextModelSearchQuery)));
        }

        private void RefreshImageProviderModels()
        {
            ReplaceProviderModels(
                ImageProviderModels,
                _providerModelOptions
                    .Where(model => model.IsImageGeneration && MatchesModelQuery(model, ImageModelSearchQuery)));
        }

        private static bool MatchesModelQuery(AiModelOption model, string query)
        {
            return string.IsNullOrWhiteSpace(query)
                || model.ModelId.IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void ReplaceProviderModels(
            ObservableCollection<AiModelOption> target,
            IEnumerable<AiModelOption> models)
        {
            target.Clear();
            foreach (var model in models.OrderBy(item => item.ModelId, StringComparer.OrdinalIgnoreCase))
                target.Add(model);
        }

        private void UpdateEditAvailableModels()
        {
            if (string.IsNullOrEmpty(EditSelectedProvider))
            {
                EditAvailableModels = new List<string>();
                EditAvailableModelOptions = new List<AiModelOption>();
                return;
            }

            var models = IsEditingCodexConnection
                ? _aiAccessManager.SubscriptionModels
                    .Select(model => model.ModelId)
                    .Where(modelId => !string.IsNullOrWhiteSpace(modelId))
                    .Distinct(StringComparer.Ordinal)
                    .ToList()
                : AppSettings.AiManager.GetProviderModels(EditSelectedProvider);
            var providerType = ParseProviderType(EditSelectedProvider);

            EditAvailableModels = models;
            EditAvailableModelOptions = models
                .Select(model => new AiModelOption(
                    model,
                    AiConnectionConfig.IsImageGenerationModel(providerType, model)))
                .OrderByDescending(model => model.IsImageGeneration)
                .ThenBy(model => model.ModelId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        #region Connection Management

        private void StartAddConnection()
        {
            if (!CanAddConnection) return;

            IsAddingConnection = true;
            IsEditingConnection = true;

            EditConnectionName = "";
            EditSelectedProvider = "OpenAI";
            EditCustomEndpoint = "";
            EditShowCustomApiKey = false;
            EditCustomApiKey = "";
            EditMaxTokens = 4096;
            EditTemperature = 0.7;
            EditSupportsMultiModal = true;
            EditSupportsImageGeneration = false;
            ConnectionTestStatus = "";

            UpdateEditAvailableModels();
            EditSelectedModel = EditAvailableModels?.FirstOrDefault();

            // Reset original values for dirty tracking
            _originalConnectionName = EditConnectionName;
            _originalProvider = EditSelectedProvider;
            _originalModel = EditSelectedModel;
            _originalCustomEndpoint = EditCustomEndpoint;
            _originalMaxTokens = EditMaxTokens;
            _originalTemperature = EditTemperature;
            _originalSupportsMultiModal = EditSupportsMultiModal;
            _originalSupportsImageGeneration = EditSupportsImageGeneration;
            NotifyConnectionEditChanged();
        }

        private void StartEditConnection()
        {
            if (SelectedConnection == null) return;

            IsAddingConnection = false;
            IsEditingConnection = true;

            EditConnectionName = SelectedConnection.Name;
            EditSelectedProvider = SelectedConnection.ProviderType;
            EditCustomEndpoint = SelectedConnection.CustomEndpoint;
            EditShowCustomApiKey =
                IsEditingOpenAICompatibleConnection &&
                !SelectedConnection.HasCustomApiKey;
            EditCustomApiKey = "";
            EditMaxTokens = SelectedConnection.MaxTokens;
            EditTemperature = SelectedConnection.Temperature;
            EditSupportsMultiModal = SelectedConnection.SupportsMultiModalInput;
            EditSupportsImageGeneration = SelectedConnection.SupportsImageGeneration;
            ConnectionTestStatus = "";

            UpdateEditAvailableModels();
            EditSelectedModel = SelectedConnection.ModelId;

            // Store original values for dirty tracking
            _originalConnectionName = EditConnectionName;
            _originalProvider = EditSelectedProvider;
            _originalModel = EditSelectedModel;
            _originalCustomEndpoint = EditCustomEndpoint;
            _originalMaxTokens = EditMaxTokens;
            _originalTemperature = EditTemperature;
            _originalSupportsMultiModal = EditSupportsMultiModal;
            _originalSupportsImageGeneration = EditSupportsImageGeneration;
            NotifyConnectionEditChanged();
        }

        private void SaveConnection()
        {
            if (string.IsNullOrWhiteSpace(EditConnectionName))
            {
                ConnectionTestStatus = "Please enter a connection name.";
                return;
            }

            if (string.IsNullOrEmpty(EditSelectedProvider))
            {
                ConnectionTestStatus = "Please select a provider.";
                return;
            }

            if (string.IsNullOrEmpty(EditSelectedModel))
            {
                ConnectionTestStatus = "Please select a model.";
                return;
            }

            string customEndpoint = null;
            if (IsEditingOpenAICompatibleConnection &&
                !TryGetOpenAICompatibleEndpoint(out customEndpoint))
            {
                ConnectionTestStatus = "Please enter a valid HTTP or HTTPS Base URL.";
                return;
            }

            var aiManager = AppSettings.AiManager;
            AiConnectionViewModel savedConnection = null;

            if (IsAddingConnection)
            {
                // Add via AiSettingsManager (handles secure storage)
                var customApiKey = EditShowCustomApiKey ? EditCustomApiKey : null;
                var newAiConn = aiManager.AddConnection(
                    EditConnectionName.Trim(),
                    EditSelectedProvider,
                    EditSelectedModel,
                    customApiKey,
                    customEndpoint);

                newAiConn.MaxTokens = EditMaxTokens;
                newAiConn.Temperature = EditTemperature;
                newAiConn.SupportsMultiModalInput = EditSupportsMultiModal;
                newAiConn.SupportsImageGeneration = EditSupportsImageGeneration;

                // Add to ViewModel collection
                savedConnection = new AiConnectionViewModel
                {
                    Id = newAiConn.Id,
                    Name = newAiConn.Name,
                    ProviderType = newAiConn.ProviderType,
                    ModelId = newAiConn.ModelId,
                    CustomEndpoint = newAiConn.CustomEndpoint,
                    HasCustomApiKey = !string.IsNullOrEmpty(customApiKey),
                    MaxTokens = newAiConn.MaxTokens,
                    Temperature = newAiConn.Temperature,
                    SupportsMultiModalInput = newAiConn.SupportsMultiModalInput,
                    SupportsImageGeneration = newAiConn.SupportsImageGeneration
                };
                Connections.Add(savedConnection);
            }
            else if (SelectedConnection != null)
            {
                savedConnection = SelectedConnection;

                // Update ViewModel
                SelectedConnection.Name = EditConnectionName.Trim();
                SelectedConnection.ProviderType = EditSelectedProvider;
                SelectedConnection.ModelId = EditSelectedModel;
                SelectedConnection.CustomEndpoint = customEndpoint;
                SelectedConnection.MaxTokens = EditMaxTokens;
                SelectedConnection.Temperature = EditTemperature;
                SelectedConnection.SupportsMultiModalInput = EditSupportsMultiModal;
                SelectedConnection.SupportsImageGeneration = EditSupportsImageGeneration;

                // Update in AiSettingsManager
                var existing = aiManager.Connections.FirstOrDefault(c => c.Id == SelectedConnection.Id);
                if (existing != null)
                {
                    existing.Name = SelectedConnection.Name;
                    existing.ProviderType = SelectedConnection.ProviderType;
                    existing.ModelId = SelectedConnection.ModelId;
                    existing.CustomEndpoint = SelectedConnection.CustomEndpoint;
                    existing.MaxTokens = SelectedConnection.MaxTokens;
                    existing.Temperature = SelectedConnection.Temperature;
                    existing.SupportsMultiModalInput = SelectedConnection.SupportsMultiModalInput;
                    existing.SupportsImageGeneration = SelectedConnection.SupportsImageGeneration;

                    // Store custom API key securely via manager
                    if (EditShowCustomApiKey)
                    {
                        aiManager.SetConnectionApiKey(existing.Id, EditCustomApiKey);
                        SelectedConnection.HasCustomApiKey = !string.IsNullOrEmpty(EditCustomApiKey);
                    }
                }
            }

            Save();
            CompleteConnectionSave(savedConnection);
            OnPropertyChanged(nameof(CanAddConnection));
        }

        private void CompleteConnectionSave(AiConnectionViewModel savedConnection)
        {
            IsAddingConnection = false;
            IsEditingConnection = savedConnection != null;

            if (savedConnection == null)
                return;

            _selectedConnection = savedConnection;
            OnPropertyChanged(nameof(SelectedConnection));
            ((RelayCommand)EditConnectionCommand)?.NotifyCanExecuteChanged();
            ((RelayCommand)DeleteConnectionCommand)?.NotifyCanExecuteChanged();

            _originalConnectionName = EditConnectionName;
            _originalProvider = EditSelectedProvider;
            _originalModel = EditSelectedModel;
            _originalCustomEndpoint = EditCustomEndpoint;
            _originalMaxTokens = EditMaxTokens;
            _originalTemperature = EditTemperature;
            _originalSupportsMultiModal = EditSupportsMultiModal;
            _originalSupportsImageGeneration = EditSupportsImageGeneration;
            EditShowCustomApiKey =
                IsEditingOpenAICompatibleConnection &&
                !savedConnection.HasCustomApiKey;
            EditCustomApiKey = "";
            NotifyConnectionEditChanged();
        }

        private async Task CancelConnectionEditAsync()
        {
            if (HasUnsavedConnectionChanges)
            {
                if (PromptSaveChangesAction == null)
                    return;

                var result = await PromptSaveChangesAction("You have unsaved changes. Do you want to save them?");
                if (result == null)
                    return;

                if (result == true)
                {
                    SaveConnection();
                    if (HasUnsavedConnectionChanges)
                        return;
                }
            }

            ResetDirtyTracking();
            ConnectionTestStatus = "";
        }

        /// <summary>
        /// Checks for unsaved changes before closing the dialog. Returns true if safe to close.
        /// </summary>
        public async Task<bool> CanCloseAsync()
        {
            if (!HasUnsavedConnectionChanges || PromptSaveChangesAction == null)
                return true;

            var result = await PromptSaveChangesAction("You have unsaved changes. Do you want to save them?");
            if (result == null)
                return false; // Cancel close
            if (result == true)
                SaveConnection();
            else
            {
                ResetDirtyTracking();
                return false;
            }
            return true;
        }

        private void DeleteConnection()
        {
            if (SelectedConnection == null) return;

            // Remove via AiSettingsManager (handles secure storage cleanup)
            AppSettings.AiManager.RemoveConnection(SelectedConnection.Id);

            Connections.Remove(SelectedConnection);
            SelectedConnection = null;
            Save();
            OnPropertyChanged(nameof(CanAddConnection));
        }

        private async Task TestConnectionAsync()
        {
            if (IsTestingConnection) return;
            if (string.IsNullOrEmpty(EditSelectedProvider))
            {
                ConnectionTestStatus = "Please select a provider.";
                return;
            }
            if (!IsEditingCodexConnection && string.IsNullOrEmpty(EditSelectedModel))
            {
                ConnectionTestStatus = "Please select a model.";
                return;
            }
            if (IsEditingOpenAICompatibleConnection &&
                !TryGetOpenAICompatibleEndpoint(out _))
            {
                ConnectionTestStatus = "Please enter a valid HTTP or HTTPS Base URL.";
                return;
            }

            IsTestingConnection = true;
            ConnectionTestStatus = "Testing...";

            try
            {
                if (IsEditingCodexConnection)
                {
                    if (_aiAccessManager.Mode == AiAccessMode.OpenAiApi)
                    {
                        ConnectionTestStatus = "Select a Codex authentication mode in AI Providers first.";
                        return;
                    }

                    var currentModel = EditSelectedModel;
                    await _aiAccessManager.SwitchModeAsync(_aiAccessManager.Mode);
                    UpdateEditAvailableModels();

                    if (!_aiAccessManager.IsAuthenticated)
                    {
                        ConnectionTestStatus = "Sign in to ChatGPT in AI Providers first.";
                        return;
                    }

                    EditSelectedModel = EditAvailableModels.FirstOrDefault(model =>
                        string.Equals(model, currentModel, StringComparison.Ordinal))
                        ?? _aiAccessManager.SelectedSubscriptionModelId
                        ?? EditAvailableModels.FirstOrDefault();
                    ConnectionTestStatus = EditAvailableModels.Count > 0
                        ? $"Connection successful! ({EditAvailableModels.Count} models loaded)"
                        : "Connection failed: No Codex subscription models returned.";
                    return;
                }

                var aiManager = AppSettings.AiManager;
                var providerType = ParseProviderType(EditSelectedProvider);

                string apiKey = GetEffectiveConnectionTestApiKey();

                string endpoint = IsEditingOpenAICompatibleConnection
                    ? EditCustomEndpoint.Trim()
                    : aiManager.GetProviderEndpoint(EditSelectedProvider);

                var api = CreateTornadoApi(providerType, apiKey, endpoint);
                if (api == null)
                {
                    ConnectionTestStatus = "Failed to create API client.";
                    return;
                }

                var modelIds = await GetProviderModelIdsAsync(api, providerType, apiKey, endpoint);
                if (modelIds.Count > 0)
                {
                    var currentModel = EditSelectedModel;
                    AppSettings.AiManager.SetProviderModels(EditSelectedProvider, modelIds);
                    UpdateEditAvailableModels();
                    var matchingModel = modelIds.FirstOrDefault(m => string.Equals(m, currentModel, StringComparison.OrdinalIgnoreCase));
                    EditSelectedModel = matchingModel ?? modelIds.FirstOrDefault();
                    ConnectionTestStatus = $"Connection successful! ({modelIds.Count} models loaded)";
                }
                else
                {
                    ConnectionTestStatus = "Connection failed: No models returned.";
                }
            }
            catch (Exception ex)
            {
                ConnectionTestStatus = GetUserFriendlyMessage(ex);
#if DEBUG
                ShowDebugExceptionAction?.Invoke(ex);
#endif
            }
            finally
            {
                IsTestingConnection = false;
            }
        }

        private string GetEffectiveConnectionTestApiKey()
        {
            if (EditShowCustomApiKey && !string.IsNullOrWhiteSpace(EditCustomApiKey))
                return EditCustomApiKey;

            var aiManager = AppSettings.AiManager;
            if (!IsAddingConnection &&
                SelectedConnection != null &&
                string.Equals(
                    SelectedConnection.ProviderType,
                    EditSelectedProvider,
                    StringComparison.Ordinal))
            {
                var connectionApiKey = aiManager.GetEffectiveApiKey(SelectedConnection.Id);
                if (!string.IsNullOrWhiteSpace(connectionApiKey))
                    return connectionApiKey;
            }

            return aiManager.GetProviderApiKey(EditSelectedProvider);
        }

        private bool ApiProviderCanConnectWithoutKey()
        {
            return IsEditingOpenAICompatibleConnection ||
                   string.Equals(EditSelectedProvider, "Ollama", StringComparison.Ordinal) ||
                   string.Equals(EditSelectedProvider, "LMStudio", StringComparison.Ordinal);
        }

        private bool TryGetOpenAICompatibleEndpoint(out string endpoint)
        {
            endpoint = EditCustomEndpoint?.Trim();
            return Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) &&
                   (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        private static TornadoApi CreateTornadoApi(AiProviderType providerType, string apiKey, string endpoint)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                if (providerType == AiProviderType.OpenAICompatible ||
                    providerType == AiProviderType.Ollama ||
                    providerType == AiProviderType.LMStudio)
                {
                    return new TornadoApi(new Uri(endpoint));
                }
                return null;
            }

            switch (providerType)
            {
                case AiProviderType.OpenAI:
                    return new TornadoApi(LLmProviders.OpenAi, apiKey);
                case AiProviderType.OpenAICompatible:
                    return new TornadoApi(new Uri(endpoint), apiKey, LLmProviders.Custom);
                case AiProviderType.OpenRouter:
                    return new TornadoApi(LLmProviders.OpenRouter, apiKey);
                case AiProviderType.HuggingFace:
                    return new TornadoApi(new Uri(endpoint), apiKey);
                case AiProviderType.Anthropic:
                    return new TornadoApi(LLmProviders.Anthropic, apiKey);
                case AiProviderType.Google:
                    return new TornadoApi(LLmProviders.Google, apiKey);
                case AiProviderType.Ollama:
                    return new TornadoApi(new Uri(endpoint), apiKey);
                case AiProviderType.LMStudio:
                    return new TornadoApi(new Uri(endpoint), apiKey);
                default:
                    return new TornadoApi(LLmProviders.OpenAi, apiKey);
            }
        }

        private static LLmProviders MapToLlmProvider(AiProviderType providerType)
        {
            switch (providerType)
            {
                case AiProviderType.OpenAI: return LLmProviders.OpenAi;
                case AiProviderType.OpenAICompatible: return LLmProviders.Custom;
                case AiProviderType.OpenRouter: return LLmProviders.OpenRouter;
                case AiProviderType.HuggingFace: return LLmProviders.Custom;
                case AiProviderType.Anthropic: return LLmProviders.Anthropic;
                case AiProviderType.Google: return LLmProviders.Google;
                case AiProviderType.Ollama: return LLmProviders.OpenAi;
                case AiProviderType.LMStudio: return LLmProviders.OpenAi;
                default: return LLmProviders.OpenAi;
            }
        }

        private static async Task<List<string>> GetProviderModelIdsAsync(
            TornadoApi api,
            AiProviderType providerType,
            string apiKey,
            string endpoint)
        {
            if (providerType == AiProviderType.HuggingFace)
                return await HuggingFaceApiClient.GetModelIdsAsync(apiKey, endpoint);

            var models = await api.Models.GetModels(MapToLlmProvider(providerType));
            var modelIds = models?
                .Select(model => model.Id)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToList() ?? new List<string>();

            if (providerType == AiProviderType.OpenRouter)
            {
                try
                {
                    modelIds.AddRange(await OpenRouterModelCatalog.GetImageGenerationModelIdsAsync());
                }
                catch (System.Net.Http.HttpRequestException)
                {
                    // Keep the provider's regular catalog and the built-in fallback models.
                    modelIds.AddRange(AiConnectionConfig.GetDefaultImageModels(AiProviderType.OpenRouter));
                }
                catch (TaskCanceledException)
                {
                    modelIds.AddRange(AiConnectionConfig.GetDefaultImageModels(AiProviderType.OpenRouter));
                }
            }

            return modelIds
                .Where(id => !AiConnectionConfig.IsExcludedModel(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string GetUserFriendlyMessage(Exception ex)
        {
            if (ex is System.Net.Http.HttpRequestException)
                return "Unable to connect to the AI provider. Please check your network connection and endpoint URL.";

            if (ex is TaskCanceledException || ex is OperationCanceledException)
                return "The request was cancelled or timed out.";

            var message = ex.Message ?? "An unexpected error occurred.";
            message = System.Text.RegularExpressions.Regex.Replace(
                message,
                @"(sk-[a-zA-Z0-9]{20,}|key[=:]\s*[""']?[a-zA-Z0-9\-_]{20,}[""']?|Bearer\s+[a-zA-Z0-9\-_\.]+|api[_-]?key[=:]\s*[^\s,}]+)",
                "[REDACTED]",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (message.Length > 500)
                return "An error occurred while communicating with the AI provider.";

            return message;
        }

        #endregion

        #region Provider Model Management

        private void AddModel()
        {
            if (SelectedProviderApiKey == null || string.IsNullOrWhiteSpace(NewModelName))
                return;

            var modelName = NewModelName.Trim();
            var existingModels = AppSettings.AiManager.GetProviderModels(SelectedProviderApiKey.ProviderType);
            if (existingModels.Any(model => string.Equals(model, modelName, StringComparison.OrdinalIgnoreCase)))
                return;

            AppSettings.AiManager.AddProviderModel(SelectedProviderApiKey.ProviderType, modelName);
            NewModelName = "";
            UpdateProviderModels();
            Save();
        }

        private void RemoveModel(AiModelOption model)
        {
            if (SelectedProviderApiKey == null || model == null)
                return;

            AppSettings.AiManager.RemoveProviderModel(SelectedProviderApiKey.ProviderType, model.ModelId);
            UpdateProviderModels();
            Save();
        }

        private void ResetModelsToDefault()
        {
            if (SelectedProviderApiKey == null) return;

            AppSettings.AiManager.ResetProviderModels(SelectedProviderApiKey.ProviderType);
            UpdateProviderModels();
            Save();
        }

        private async Task TestProviderConnectionAsync()
        {
            if (SelectedProviderApiKey == null) return;

            IsTestingProviderConnection = true;
            ProviderTestStatus = "Testing...";

            try
            {
                var providerType = ParseProviderType(SelectedProviderApiKey.ProviderType);
                var apiKey = SelectedProviderApiKey.ApiKey;
                var endpoint = SelectedProviderApiKey.CustomEndpoint;

                var api = CreateTornadoApi(providerType, apiKey, endpoint);
                if (api == null)
                {
                    ProviderTestStatus = "Failed: No API key configured.";
                    return;
                }

                var modelIds = await GetProviderModelIdsAsync(api, providerType, apiKey, endpoint);
                if (modelIds.Count > 0)
                {
                    AppSettings.AiManager.SetProviderModels(SelectedProviderApiKey.ProviderType, modelIds);
                    UpdateProviderModels();
                    Save();
                    ProviderTestStatus = $"Success! {modelIds.Count} models loaded.";
                }
                else
                {
                    ProviderTestStatus = "Failed: No models returned.";
                }
            }
            catch (Exception ex)
            {
                ProviderTestStatus = GetUserFriendlyMessage(ex);
#if DEBUG
                ShowDebugExceptionAction?.Invoke(ex);
#endif
            }
            finally
            {
                IsTestingProviderConnection = false;
            }
        }

        #endregion

        private void ChangeTheme(ThemeVariant theme)
        {
            var app = Application.Current;
            if (app?.Styles == null) return;

            app.RequestedThemeVariant = theme ?? ThemeVariant.Dark;
            ActiveTheme = theme ?? ThemeVariant.Dark;

            AppSettings.Current.Theme = theme.ToSettingsString();
            AppSettings.Save();
        }

        private void Save()
        {
            // Save provider API keys via AiSettingsManager (handles secure storage)
            var aiManager = AppSettings.AiManager;

            foreach (var vm in ProviderApiKeys)
            {
                aiManager.SetProviderApiKey(vm.ProviderType, vm.ApiKey);
                aiManager.SetProviderEndpoint(vm.ProviderType, vm.CustomEndpoint);
            }

            AppSettings.Save();
        }

        private void Reset()
        {
            AudioInputDeviceName = null;
            AudioExportFormat = "WAV";
            AudioMp3Bitrate = 192;
            SelectedLocale = AvailableLocales.First(); // Reset to en-US
            ChangeTheme(ThemeVariant.Dark);

            // Reset AI settings via manager (clears secure storage)
            AppSettings.AiManager.Reset();

            LoadAiSettings();
            AppSettings.Save();
        }

        private static AiProviderType ParseProviderType(string provider)
        {
            if (string.Equals(provider, "OpenAI-Compatible", StringComparison.Ordinal))
                return AiProviderType.OpenAICompatible;

            if (Enum.TryParse<AiProviderType>(provider, out var result))
                return result;
            return AiProviderType.OpenAI;
        }
    }

    public class AiConnectionViewModel : ObservableObject
    {
        public string Id { get; set; }

        private string _name;
        public string Name
        {
            get => _name;
            set
            {
                if (SetProperty(ref _name, value))
                    OnPropertyChanged(nameof(DisplayText));
            }
        }

        private string _providerType;
        public string ProviderType
        {
            get => _providerType;
            set
            {
                if (SetProperty(ref _providerType, value))
                    OnPropertyChanged(nameof(DisplayText));
            }
        }

        private string _modelId;
        public string ModelId
        {
            get => _modelId;
            set
            {
                if (SetProperty(ref _modelId, value))
                    OnPropertyChanged(nameof(DisplayText));
            }
        }

        private string _customEndpoint;
        public string CustomEndpoint
        {
            get => _customEndpoint;
            set => SetProperty(ref _customEndpoint, value);
        }

        private bool _hasCustomApiKey;
        public bool HasCustomApiKey
        {
            get => _hasCustomApiKey;
            set => SetProperty(ref _hasCustomApiKey, value);
        }

        private int _maxTokens = 4096;
        public int MaxTokens
        {
            get => _maxTokens;
            set => SetProperty(ref _maxTokens, value);
        }

        private double _temperature = 0.7;
        public double Temperature
        {
            get => _temperature;
            set => SetProperty(ref _temperature, value);
        }

        private bool _supportsMultiModalInput;
        public bool SupportsMultiModalInput
        {
            get => _supportsMultiModalInput;
            set => SetProperty(ref _supportsMultiModalInput, value);
        }

        private bool _supportsImageGeneration;
        public bool SupportsImageGeneration
        {
            get => _supportsImageGeneration;
            set => SetProperty(ref _supportsImageGeneration, value);
        }

        public string DisplayText => $"{Name} ({ProviderType}: {ModelId})";
    }

    public class ProviderApiKeyViewModel : ObservableObject
    {
        public string ProviderType { get; set; }

        public Action OnChanged { get; set; }

        private string _apiKey;
        public string ApiKey
        {
            get => _apiKey;
            set
            {
                if (SetProperty(ref _apiKey, value))
                {
                    OnPropertyChanged(nameof(HasApiKey));
                    OnPropertyChanged(nameof(HasConfiguredAccess));
                    OnChanged?.Invoke();
                }
            }
        }

        private bool _hasSubscriptionAccess;
        public bool HasSubscriptionAccess
        {
            get => _hasSubscriptionAccess;
            set
            {
                if (SetProperty(ref _hasSubscriptionAccess, value))
                    OnPropertyChanged(nameof(HasConfiguredAccess));
            }
        }

        private string _customEndpoint;
        public string CustomEndpoint
        {
            get => _customEndpoint;
            set
            {
                if (SetProperty(ref _customEndpoint, value))
                    OnChanged?.Invoke();
            }
        }

        public bool HasApiKey => !string.IsNullOrEmpty(ApiKey);
        public bool HasConfiguredAccess => HasApiKey || HasSubscriptionAccess;

        public override string ToString() => ProviderType ?? "";
    }

    /// <summary>
    /// Represents a locale option for the Semi theme.
    /// </summary>
    public class LocaleItem
    {
        public string DisplayName { get; }
        public string Code { get; }

        public LocaleItem(string displayName, string code)
        {
            DisplayName = displayName;
            Code = code;
        }

        public override string ToString() => DisplayName;
    }
}
