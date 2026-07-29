using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using OpenSourceToolkit.NET.Localization;
using SkiaSharp;

namespace OpenSourceToolkit.NET.ViewModels.Tools
{
    /// <summary>
    /// Represents a Google Font family with its metadata.
    /// </summary>
    public class GoogleFontFamily
    {
        public string Family { get; set; }
        public string Category { get; set; }
        public List<string> Variants { get; set; } = new List<string>();
        public List<string> Subsets { get; set; } = new List<string>();
        public string Version { get; set; }
        public string LastModified { get; set; }

        // Computed properties for UI
        public string VariantsDisplay => string.Join(", ", Variants);
        public string CategoryDisplay => Category?.Replace("-", " ") ?? "";
        public int VariantCount => Variants?.Count ?? 0;

        /// <summary>
        /// Whether this font is installed on the system.
        /// </summary>
        public bool IsInstalled { get; set; }
    }

    /// <summary>
    /// Represents a downloadable font file from the Google Fonts GitHub repository.
    /// </summary>
    public class FontFileInfo
    {
        public string Name { get; set; }
        public string DownloadUrl { get; set; }
        public long Size { get; set; }
        public bool IsVariable { get; set; }
        public bool IsCached { get; set; }
        public string CachedPath { get; set; }

        public string SizeDisplay => Size > 0 ? $"{Size / 1024.0:F1} KB" : "";
        public string TypeDisplay => IsCached ? "Cached" : (IsVariable ? "Variable" : "Static");
    }

    /// <summary>
    /// Represents a filter tag for the font tag cloud.
    /// </summary>
    public class FontFilterTag : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        public string Name { get; }
        public string DisplayName { get; }
        public FontFilterTagType TagType { get; }
        public Func<GoogleFontFamily, bool> Filter { get; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public FontFilterTag(string name, string displayName, FontFilterTagType tagType, Func<GoogleFontFamily, bool> filter)
        {
            Name = name;
            DisplayName = displayName;
            TagType = tagType;
            Filter = filter;
        }
    }

    /// <summary>
    /// Tag type for visual grouping/styling in the UI.
    /// </summary>
    public enum FontFilterTagType
    {
        VariantCount,   // Number of styles/weights
        Subset,         // Language/script support
        Feature         // Special features like variable fonts
    }

    public partial class FontsViewerToolViewModel : ToolViewModel
    {
        public override int Id => 39;
        public override string Name => ToolkitLocalization.GetString("Tool_FontsViewer_Name");
        public override string Description => ToolkitLocalization.GetString("Tool_FontsViewer_Description");
        // Font icon (text/typography style)
        public override string IconKey => "FontIcon";

        private static readonly HttpClient _httpClient = new HttpClient();

        // Google Fonts GitHub repository API base URL
        private const string GitHubFontsApiBase = "https://api.github.com/repos/google/fonts/contents/";

        /// <summary>
        /// Cache of installed font family names (lowercase for case-insensitive matching).
        /// </summary>
        private static HashSet<string> _installedFonts;

        /// <summary>
        /// Gets the set of installed font family names on the system.
        /// Uses System.Drawing.Text.InstalledFontCollection on Windows.
        /// </summary>
        private static HashSet<string> GetInstalledFonts()
        {
            if (_installedFonts != null)
                return _installedFonts;

            _installedFonts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (var fontCollection = new InstalledFontCollection())
                {
                    foreach (var family in fontCollection.Families)
                    {
                        _installedFonts.Add(family.Name);
                    }
                }
            }
            catch
            {
                // Font enumeration failed, return empty set
            }
            return _installedFonts;
        }

        /// <summary>
        /// Checks if a font family is installed on the system.
        /// </summary>
        private static bool IsFontInstalled(string familyName)
        {
            return GetInstalledFonts().Contains(familyName);
        }

        /// <summary>
        /// Clears the installed fonts cache so it will be re-read on next access.
        /// Call this when refreshing the font list to detect newly installed fonts.
        /// </summary>
        private static void ClearInstalledFontsCache()
        {
            _installedFonts = null;
        }

        /// <summary>
        /// Creates an HttpRequestMessage with GitHub authentication if a token is configured.
        /// </summary>
        private static HttpRequestMessage CreateGitHubRequest(string url)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "OpenSourceToolkit");

            var token = Services.AppSettings.GetGitHubToken();
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Add("Authorization", $"Bearer {token}");
            }
            return request;
        }

        /// <summary>
        /// Sends a GET request to GitHub API with optional authentication.
        /// </summary>
        private static async Task<string> GetGitHubApiAsync(string url)
        {
            using (var request = CreateGitHubRequest(url))
            {
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
        }
        private CancellationTokenSource _searchCts;

        // --- Actions (set by View for platform-specific operations) ---
        public Action<string> CopyToClipboardAction { get; set; }
        public Func<string, Task<string>> SelectDownloadFolderAction { get; set; }

        // --- Observable Collections ---
        public ObservableCollection<GoogleFontFamily> AllFonts { get; } = new ObservableCollection<GoogleFontFamily>();
        public ObservableCollection<GoogleFontFamily> FilteredFonts { get; } = new ObservableCollection<GoogleFontFamily>();
        public ObservableCollection<FontFileInfo> AvailableFiles { get; } = new ObservableCollection<FontFileInfo>();
        public ObservableCollection<string> Categories { get; } = new ObservableCollection<string>
        {
            "All Categories",
            "Sans Serif",
            "Serif",
            "Display",
            "Handwriting",
            "Monospace"
        };

        // --- Filter Tags (Tag Cloud) ---
        public ObservableCollection<FontFilterTag> FilterTags { get; } = new ObservableCollection<FontFilterTag>();

        private void InitializeFilterTags()
        {
            FilterTags.Clear();

            // Build all tags in a list first, then sort alphabetically
            var tags = new List<FontFilterTag>
            {
                // Variant count tags
                new FontFilterTag("single", "1 Style", FontFilterTagType.VariantCount,
                    f => f.VariantCount == 1),
                new FontFilterTag("few", "2-4 Styles", FontFilterTagType.VariantCount,
                    f => f.VariantCount >= 2 && f.VariantCount <= 4),
                new FontFilterTag("many", "5-10 Styles", FontFilterTagType.VariantCount,
                    f => f.VariantCount >= 5 && f.VariantCount <= 10),
                new FontFilterTag("extensive", "10+ Styles", FontFilterTagType.VariantCount,
                    f => f.VariantCount > 10),

                // Feature tags
                new FontFilterTag("bold", "Has Bold", FontFilterTagType.Feature,
                    f => f.Variants.Any(v => v == "700" || v == "700italic" || v.ToLowerInvariant().Contains("bold"))),
                new FontFilterTag("installed", "Installed", FontFilterTagType.Feature,
                    f => f.IsInstalled),
                new FontFilterTag("italic", "Has Italic", FontFilterTagType.Feature,
                    f => f.Variants.Any(v => v.ToLowerInvariant().Contains("italic"))),
                new FontFilterTag("light", "Has Light", FontFilterTagType.Feature,
                    f => f.Variants.Any(v => v == "300" || v == "300italic" || v == "100" || v == "200")),
                new FontFilterTag("variable", "Variable Font", FontFilterTagType.Feature,
                    f => f.Variants.Any(v => v.Contains("wght") || v.Contains("ital") || v.Contains("opsz"))),

                // Subset/language tags (common ones)
                new FontFilterTag("arabic", "Arabic", FontFilterTagType.Subset,
                    f => f.Subsets.Contains("arabic")),
                new FontFilterTag("cjk", "CJK (Chinese/Japanese/Korean)", FontFilterTagType.Subset,
                    f => f.Subsets.Any(s => s.Contains("chinese") || s.Contains("japanese") || s.Contains("korean"))),
                new FontFilterTag("cyrillic", "Cyrillic", FontFilterTagType.Subset,
                    f => f.Subsets.Contains("cyrillic") || f.Subsets.Contains("cyrillic-ext")),
                new FontFilterTag("devanagari", "Devanagari (Hindi)", FontFilterTagType.Subset,
                    f => f.Subsets.Contains("devanagari")),
                new FontFilterTag("greek", "Greek", FontFilterTagType.Subset,
                    f => f.Subsets.Contains("greek") || f.Subsets.Contains("greek-ext")),
                new FontFilterTag("hebrew", "Hebrew", FontFilterTagType.Subset,
                    f => f.Subsets.Contains("hebrew")),
                new FontFilterTag("latin-ext", "Latin Extended", FontFilterTagType.Subset,
                    f => f.Subsets.Contains("latin-ext")),
                new FontFilterTag("thai", "Thai", FontFilterTagType.Subset,
                    f => f.Subsets.Contains("thai")),
                new FontFilterTag("vietnamese", "Vietnamese", FontFilterTagType.Subset,
                    f => f.Subsets.Contains("vietnamese"))
            };

            // Sort alphabetically by display name and add to collection
            foreach (var tag in tags.OrderBy(t => t.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                FilterTags.Add(tag);
            }

            // Subscribe to tag selection changes
            foreach (var tag in FilterTags)
            {
                tag.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(FontFilterTag.IsSelected))
                    {
                        OnPropertyChanged(nameof(HasActiveFilters));
                        FilterFonts();
                    }
                };
            }
        }

        public bool HasActiveFilters => FilterTags.Any(t => t.IsSelected);

        public IRelayCommand ClearAllFiltersCommand { get; private set; }
        public IRelayCommand<FontFilterTag> ToggleTagCommand { get; private set; }

        private void ClearAllFilters()
        {
            foreach (var tag in FilterTags)
                tag.IsSelected = false;
            SearchText = "";
            SelectedCategory = "All Categories";
        }

        // --- Search & Filter State ---
        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                    FilterFontsDebounced();
            }
        }

        private string _selectedCategory = "Monospace";
        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value))
                    FilterFonts();
            }
        }

        // --- Selected Font State ---
        private GoogleFontFamily _selectedFont;
        public GoogleFontFamily SelectedFont
        {
            get => _selectedFont;
            set
            {
                if (SetProperty(ref _selectedFont, value))
                {
                    OnPropertyChanged(nameof(HasSelectedFont));
                    OnPropertyChanged(nameof(PreviewText));
                    (SetAsEditorFontCommand as RelayCommand)?.NotifyCanExecuteChanged();

                    // Clear any warning from previous font
                    WarningMessage = "";

                    // Cancel any pending preview load when font changes
                    _previewCts?.Cancel();

                    // Reset variant to "regular" for new font selection
                    _selectedPreviewVariant = "regular";
                    OnPropertyChanged(nameof(SelectedPreviewVariant));

                    if (value != null)
                    {
                        _ = LoadFontFilesAsync(value);
                        if (IsLivePreviewEnabled)
                            _ = LoadLivePreviewFontAsync(value);
                    }
                    else
                    {
                        AvailableFiles.Clear();
                        PreviewFontFamily = null;
                        IsLoadingPreview = false;
                    }
                }
            }
        }

        public bool HasSelectedFont => SelectedFont != null;

        // --- Selected Variant for Preview ---
        private string _selectedPreviewVariant = "regular";
        public string SelectedPreviewVariant
        {
            get => _selectedPreviewVariant;
            set
            {
                if (SetProperty(ref _selectedPreviewVariant, value))
                {
                    // Re-download and render preview with new variant
                    if (IsLivePreviewEnabled && SelectedFont != null)
                    {
                        _currentPreviewFontPath = null;
                        _ = LoadLivePreviewFontAsync(SelectedFont);
                    }
                }
            }
        }

        // --- Preview State ---
        private string _customPreviewText = "The quick brown fox jumps over the lazy dog";
        public string CustomPreviewText
        {
            get => _customPreviewText;
            set
            {
                if (SetProperty(ref _customPreviewText, value))
                {
                    OnPropertyChanged(nameof(PreviewText));
                    // Re-render preview if live preview is enabled
                    RefreshPreviewDebounced();
                }
            }
        }

        public string PreviewText => CustomPreviewText;

        private double _previewFontSize = 24;
        public double PreviewFontSize
        {
            get => _previewFontSize;
            set
            {
                if (SetProperty(ref _previewFontSize, value))
                {
                    // Re-render preview if live preview is enabled
                    RefreshPreviewDebounced();
                }
            }
        }

        private CancellationTokenSource _refreshPreviewCts;

        /// <summary>
        /// Debounced refresh of the preview images when text or size changes.
        /// </summary>
        private void RefreshPreviewDebounced()
        {
            if (!IsLivePreviewEnabled || string.IsNullOrEmpty(_currentPreviewFontPath)) return;

            _refreshPreviewCts?.Cancel();
            _refreshPreviewCts = new CancellationTokenSource();
            var token = _refreshPreviewCts.Token;

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(300, token); // Debounce 300ms
                    if (!token.IsCancellationRequested && File.Exists(_currentPreviewFontPath))
                    {
                        await RenderPreviewImagesAsync(_currentPreviewFontPath, token);
                    }
                }
                catch (TaskCanceledException) { }
            });
        }

        // --- Live Preview Option ---
        private bool _isLivePreviewEnabled;
        public bool IsLivePreviewEnabled
        {
            get => _isLivePreviewEnabled;
            set
            {
                if (SetProperty(ref _isLivePreviewEnabled, value))
                {
                    SetSetting("LivePreviewEnabled", value);
                    OnPropertyChanged(nameof(ShowPreviewHint));
                    // Trigger font reload if enabling live preview with a font selected
                    if (value && SelectedFont != null)
                        _ = LoadLivePreviewFontAsync(SelectedFont);
                    else if (!value)
                    {
                        // Cancel any pending preview load and clear font
                        _previewCts?.Cancel();
                        PreviewFontFamily = null;
                        IsLoadingPreview = false;
                    }
                }
            }
        }

        public bool ShowPreviewHint => !IsLivePreviewEnabled;

        // --- Dark Mode Preview Toggle ---
        private bool _isDarkPreview;
        public bool IsDarkPreview
        {
            get => _isDarkPreview;
            set
            {
                if (SetProperty(ref _isDarkPreview, value))
                {
                    SetSetting("DarkPreview", value);
                    OnPropertyChanged(nameof(PreviewBackground));
                    OnPropertyChanged(nameof(PreviewForeground));
                    // Re-render the preview image if live preview is enabled
                    if (IsLivePreviewEnabled && SelectedFont != null)
                        _ = LoadLivePreviewFontAsync(SelectedFont);
                }
            }
        }

        // Background/foreground colors for preview based on dark mode toggle
        public Avalonia.Media.IBrush PreviewBackground => IsDarkPreview
            ? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(30, 30, 30))
            : new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(250, 250, 250));

        public Avalonia.Media.IBrush PreviewForeground => IsDarkPreview
            ? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(240, 240, 240))
            : new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(30, 30, 30));

        private Avalonia.Media.FontFamily _previewFontFamily;
        public Avalonia.Media.FontFamily PreviewFontFamily
        {
            get => _previewFontFamily;
            set => SetProperty(ref _previewFontFamily, value);
        }

        // SkiaSharp-rendered preview image (for live preview of uninstalled fonts)
        private Bitmap _previewImage;
        public Bitmap PreviewImage
        {
            get => _previewImage;
            set => SetProperty(ref _previewImage, value);
        }

        // Multiple size preview images
        private Bitmap _previewImage12;
        public Bitmap PreviewImage12
        {
            get => _previewImage12;
            set => SetProperty(ref _previewImage12, value);
        }

        private Bitmap _previewImage16;
        public Bitmap PreviewImage16
        {
            get => _previewImage16;
            set => SetProperty(ref _previewImage16, value);
        }

        private Bitmap _previewImage20;
        public Bitmap PreviewImage20
        {
            get => _previewImage20;
            set => SetProperty(ref _previewImage20, value);
        }

        private Bitmap _previewImage28;
        public Bitmap PreviewImage28
        {
            get => _previewImage28;
            set => SetProperty(ref _previewImage28, value);
        }

        private bool _isLoadingPreview;
        public bool IsLoadingPreview
        {
            get => _isLoadingPreview;
            set => SetProperty(ref _isLoadingPreview, value);
        }

        // Cache for downloaded preview fonts
        private static readonly string _fontCacheDir = Path.Combine(Path.GetTempPath(), "OpenSourceToolkit", "FontsCache");
        private string _currentPreviewFontPath;
        private CancellationTokenSource _previewCts;

        private int _cachedFontCount;
        public int CachedFontCount
        {
            get => _cachedFontCount;
            set
            {
                if (SetProperty(ref _cachedFontCount, value))
                {
                    OnPropertyChanged(nameof(HasCachedFonts));
                    OnPropertyChanged(nameof(CacheStatusText));
                    ClearCacheCommand?.NotifyCanExecuteChanged();
                    OpenCacheFolderCommand?.NotifyCanExecuteChanged();
                }
            }
        }

        public bool HasCachedFonts => CachedFontCount > 0;
        public string CacheStatusText => CachedFontCount > 0 ? $"{CachedFontCount} cached" : "";

        // --- Loading State ---
        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private bool _isLoadingFiles;
        public bool IsLoadingFiles
        {
            get => _isLoadingFiles;
            set => SetProperty(ref _isLoadingFiles, value);
        }

        private bool _isDownloading;
        public bool IsDownloading
        {
            get => _isDownloading;
            set
            {
                if (SetProperty(ref _isDownloading, value))
                    DownloadSelectedFilesCommand.NotifyCanExecuteChanged();
            }
        }

        private string _statusMessage = "";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private string _errorMessage = "";
        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                if (SetProperty(ref _errorMessage, value))
                    OnPropertyChanged(nameof(HasError));
            }
        }

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        private string _warningMessage = "";
        public string WarningMessage
        {
            get => _warningMessage;
            set
            {
                if (SetProperty(ref _warningMessage, value))
                    OnPropertyChanged(nameof(HasWarning));
            }
        }

        public bool HasWarning => !string.IsNullOrEmpty(WarningMessage);

        // --- Selected Files for Download ---
        private FontFileInfo _selectedFile;
        public FontFileInfo SelectedFile
        {
            get => _selectedFile;
            set
            {
                if (SetProperty(ref _selectedFile, value))
                    DownloadSelectedFilesCommand.NotifyCanExecuteChanged();
            }
        }

        // --- Commands ---
        public IRelayCommand LoadFontsCommand { get; }
        public IRelayCommand RefreshCommand { get; }
        public IRelayCommand<GoogleFontFamily> SelectFontCommand { get; }
        public IRelayCommand<string> SelectVariantCommand { get; }
        public IRelayCommand DownloadSelectedFilesCommand { get; }
        public IRelayCommand DownloadAllFilesCommand { get; }
        public IRelayCommand CopyFontNameCommand { get; }
        public IRelayCommand OpenGoogleFontsCommand { get; }
        public IRelayCommand OpenLicenseInfoCommand { get; }
        public IRelayCommand OpenOflWebsiteCommand { get; }
        public IRelayCommand ClearCacheCommand { get; }
        public IRelayCommand OpenCacheFolderCommand { get; }
        public IRelayCommand SetAsEditorFontCommand { get; }

        public FontsViewerToolViewModel()
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("OpenSourceToolkit-FontsViewer/1.0");

            LoadFontsCommand = new RelayCommand(async () => await LoadFontsAsync());
            RefreshCommand = new RelayCommand(async () =>
            {
                // Clear installed fonts cache to detect newly installed fonts
                ClearInstalledFontsCache();
                await LoadFontsAsync();
            });
            SelectFontCommand = new RelayCommand<GoogleFontFamily>(font => SelectedFont = font);
            SelectVariantCommand = new RelayCommand<string>(variant => SelectedPreviewVariant = variant);
            DownloadSelectedFilesCommand = new RelayCommand(
                async () => await DownloadFilesAsync(false),
                () => SelectedFile != null && !IsDownloading);
            DownloadAllFilesCommand = new RelayCommand(
                async () => await DownloadFilesAsync(true),
                () => AvailableFiles.Count > 0 && !IsDownloading);
            CopyFontNameCommand = new RelayCommand(() =>
            {
                if (SelectedFont != null)
                    CopyToClipboardAction?.Invoke(SelectedFont.Family);
            });
            OpenGoogleFontsCommand = new RelayCommand(() =>
            {
                if (SelectedFont != null)
                {
                    var url = $"https://fonts.google.com/specimen/{SelectedFont.Family.Replace(" ", "+")}";
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = url,
                            UseShellExecute = true
                        });
                    }
                    catch { }
                }
            });
            OpenLicenseInfoCommand = new RelayCommand(() =>
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "https://fonts.google.com/knowledge/glossary/licensing",
                        UseShellExecute = true
                    });
                }
                catch { }
            });
            OpenOflWebsiteCommand = new RelayCommand(() =>
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "https://openfontlicense.org/",
                        UseShellExecute = true
                    });
                }
                catch { }
            });
            ClearCacheCommand = new RelayCommand(ClearFontCache, () => CachedFontCount > 0);
            OpenCacheFolderCommand = new RelayCommand(OpenCacheFolder, () => CachedFontCount > 0);
            ClearAllFiltersCommand = new RelayCommand(ClearAllFilters);
            ToggleTagCommand = new RelayCommand<FontFilterTag>(tag => { if (tag != null) tag.IsSelected = !tag.IsSelected; });
            SetAsEditorFontCommand = new RelayCommand(SetAsEditorFont, () => SelectedFont != null);

            // Initialize filter tags
            InitializeFilterTags();

            // Load saved settings
            _isLivePreviewEnabled = GetSetting("LivePreviewEnabled", false);
            _isDarkPreview = GetSetting("DarkPreview", false);

            // Ensure cache directory exists and count cached fonts
            if (!Directory.Exists(_fontCacheDir))
                Directory.CreateDirectory(_fontCacheDir);
            UpdateCachedFontCount();

            // Load fonts on construction
            Task.Run(async () => await LoadFontsAsync());
        }

        private void UpdateCachedFontCount()
        {
            try
            {
                if (Directory.Exists(_fontCacheDir))
                    CachedFontCount = Directory.GetFiles(_fontCacheDir, "*.ttf").Length;
                else
                    CachedFontCount = 0;
            }
            catch
            {
                CachedFontCount = 0;
            }
        }

        private void SetAsEditorFont()
        {
            if (SelectedFont == null)
                return;

            // Clear any previous warning
            WarningMessage = "";

            if (!SelectedFont.IsInstalled)
            {
                // Show warning that font needs to be installed first
                WarningMessage = $"'{SelectedFont.Family}' is not installed on your system. " +
                    "Download the font below and install it (double-click the .ttf file), then restart the app.";
                StatusMessage = "";
                return;
            }

            Services.AppSettings.SetEditorFont(SelectedFont.Family);
            StatusMessage = $"Editor font set to: {SelectedFont.Family}";
        }

        private void ClearFontCache()
        {
            try
            {
                // Clear current preview font reference first
                PreviewFontFamily = null;
                _currentPreviewFontPath = null;

                if (Directory.Exists(_fontCacheDir))
                {
                    foreach (var file in Directory.GetFiles(_fontCacheDir, "*.ttf"))
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch { /* File may be in use */ }
                    }
                }

                UpdateCachedFontCount();
                StatusMessage = "Font cache cleared";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to clear cache: {ex.Message}";
            }
        }

        private void OpenCacheFolder()
        {
            try
            {
                if (Directory.Exists(_fontCacheDir))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = _fontCacheDir,
                        UseShellExecute = true
                    });
                }
            }
            catch { }
        }

        /// <summary>
        /// Converts a Google Fonts variant name (e.g. "regular", "700italic") to a file suffix (e.g. "Regular", "BoldItalic").
        /// </summary>
        private static string VariantToFileSuffix(string variant)
        {
            if (string.IsNullOrEmpty(variant)) return "Regular";

            var v = variant.ToLowerInvariant();

            // Map weight numbers to names
            var weightMap = new Dictionary<string, string>
            {
                { "100", "Thin" },
                { "200", "ExtraLight" },
                { "300", "Light" },
                { "400", "Regular" },
                { "500", "Medium" },
                { "600", "SemiBold" },
                { "700", "Bold" },
                { "800", "ExtraBold" },
                { "900", "Black" }
            };

            bool isItalic = v.Contains("italic");
            string weight = v.Replace("italic", "").Trim();

            // Handle named variants
            if (weight == "regular" || weight == "") weight = "400";

            string weightName = weightMap.TryGetValue(weight, out var mapped) ? mapped : "Regular";

            // For regular weight italic, it's just "Italic"
            if (isItalic && weightName == "Regular")
                return "Italic";

            return isItalic ? $"{weightName}Italic" : weightName;
        }

        /// <summary>
        /// Searches for a font file matching the given variant suffix in a GitHub API response.
        /// Returns (downloadUrl, isExactMatch) - isExactMatch is false when falling back to any static TTF.
        /// </summary>
        private static (string url, bool exactMatch) FindVariantFile(JsonElement files, string folderName, string variantSuffix)
        {
            // Build possible file name patterns for exact match
            var patterns = new[]
            {
                $"-{variantSuffix}.ttf",
                $"_{variantSuffix}.ttf",
                $"{variantSuffix}.ttf"
            };

            string firstStaticTtf = null;

            foreach (var file in files.EnumerateArray())
            {
                var name = file.GetProperty("name").GetString();
                if (!name.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Skip variable fonts (contain brackets like [wght])
                bool isVariable = name.Contains("[");

                if (!isVariable)
                {
                    // Check for exact variant match
                    foreach (var pattern in patterns)
                    {
                        if (name.EndsWith(pattern, StringComparison.OrdinalIgnoreCase))
                        {
                            return (file.GetProperty("download_url").GetString(), true);
                        }
                    }

                    // Keep track of first static TTF as fallback
                    if (firstStaticTtf == null)
                        firstStaticTtf = file.GetProperty("download_url").GetString();
                }
            }

            // Return first static TTF if no exact match found
            if (firstStaticTtf != null)
                return (firstStaticTtf, false);

            return (null, false);
        }

        /// <summary>
        /// Downloads a font file temporarily and loads it for live preview.
        /// Uses the SelectedPreviewVariant to determine which weight/style to download.
        /// </summary>
        private async Task LoadLivePreviewFontAsync(GoogleFontFamily font)
        {
            if (!IsLivePreviewEnabled) return;

            // Cancel any previous preview load to avoid race conditions
            _previewCts?.Cancel();
            _previewCts = new CancellationTokenSource();
            var token = _previewCts.Token;

            IsLoadingPreview = true;
            PreviewFontFamily = null;

            try
            {
                // Convert font name to GitHub folder format
                var folderName = font.Family.Replace(" ", "").ToLowerInvariant();
                // Include variant in cache filename to allow caching different variants
                var variantSuffix = VariantToFileSuffix(SelectedPreviewVariant);
                var cacheFileName = $"{folderName}-{variantSuffix}.ttf";
                var cachePath = Path.Combine(_fontCacheDir, cacheFileName);

                // Check if already cached
                if (!File.Exists(cachePath))
                {
                    string downloadUrl = null;
                    JsonDocument rootFilesDoc = null;

                    // Try each license folder (ofl, apache, ufl)
                    foreach (var licenseFolder in _licenseFolders)
                    {
                        if (token.IsCancellationRequested) return;

                        try
                        {
                            var apiUrl = $"{GitHubFontsApiBase}{licenseFolder}/{folderName}";
                            var response = await GetGitHubApiAsync(apiUrl);

                            if (token.IsCancellationRequested) return;

                            rootFilesDoc = JsonDocument.Parse(response);

                            // Look for the selected variant in root folder
                            var (url, _) = FindVariantFile(rootFilesDoc.RootElement, folderName, variantSuffix);
                            downloadUrl = url;

                            // If not found in root, try static folder
                            if (downloadUrl == null)
                            {
                                try
                                {
                                    var staticUrl = $"{GitHubFontsApiBase}{licenseFolder}/{folderName}/static";
                                    var staticResponse = await GetGitHubApiAsync(staticUrl);

                                    if (token.IsCancellationRequested) return;

                                    var staticFiles = JsonDocument.Parse(staticResponse);
                                    var (staticUrl2, _) = FindVariantFile(staticFiles.RootElement, folderName, variantSuffix);
                                    downloadUrl = staticUrl2;
                                }
                                catch { /* Static folder doesn't exist */ }
                            }

                            // Found the font folder, stop searching license folders
                            break;
                        }
                        catch (HttpRequestException ex)
                        {
                            // Check for rate limiting (403)
                            if (ex.Message.Contains("403"))
                            {
                                System.Diagnostics.Debug.WriteLine("GitHub API rate limit exceeded");
                                return;
                            }
                            // 404 = Not found in this license folder, try next
                            continue;
                        }
                    }

                    // Fall back to any TTF file in root (usually variable font)
                    if (downloadUrl == null && rootFilesDoc != null)
                    {
                        foreach (var file in rootFilesDoc.RootElement.EnumerateArray())
                        {
                            var name = file.GetProperty("name").GetString();
                            if (name.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase))
                            {
                                downloadUrl = file.GetProperty("download_url").GetString();
                                break;
                            }
                        }
                    }

                    if (downloadUrl == null || token.IsCancellationRequested) return;

                    // Download the font using HttpClient
                    var fontBytes = await _httpClient.GetByteArrayAsync(downloadUrl);
                    await File.WriteAllBytesAsync(cachePath, fontBytes);
                }

                if (token.IsCancellationRequested) return;

                _currentPreviewFontPath = cachePath;

                // Update cache count after downloading
                UpdateCachedFontCount();

                // Render preview images using SkiaSharp
                await RenderPreviewImagesAsync(cachePath, token);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelled, ignore
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load preview font: {ex.Message}");
                ClearPreviewImages();
            }
            finally
            {
                if (!token.IsCancellationRequested)
                    IsLoadingPreview = false;
            }
        }

        /// <summary>
        /// Renders preview images using SkiaSharp with the downloaded font file.
        /// </summary>
        private async Task RenderPreviewImagesAsync(string fontPath, CancellationToken token)
        {
            await Task.Run(() =>
            {
                if (token.IsCancellationRequested) return;

                try
                {
                    // Load the font using SkiaSharp - this works with any TTF file!
                    using (var typeface = SKTypeface.FromFile(fontPath))
                    {
                        if (typeface == null)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to load typeface from {fontPath}");
                            return;
                        }

                        System.Diagnostics.Debug.WriteLine($"Loaded typeface: {typeface.FamilyName} from {fontPath}");

                        // Render the main preview at the user-selected size
                        var mainPreview = RenderTextToBitmap(PreviewText, (float)PreviewFontSize, typeface);

                        // Render sample sizes
                        var preview12 = RenderTextToBitmap(PreviewText, 12f, typeface);
                        var preview16 = RenderTextToBitmap(PreviewText, 16f, typeface);
                        var preview20 = RenderTextToBitmap(PreviewText, 20f, typeface);
                        var preview28 = RenderTextToBitmap(PreviewText, 28f, typeface);

                        if (token.IsCancellationRequested) return;

                        // Update UI on dispatcher thread
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            if (token.IsCancellationRequested) return;

                            // Dispose old images
                            PreviewImage?.Dispose();
                            PreviewImage12?.Dispose();
                            PreviewImage16?.Dispose();
                            PreviewImage20?.Dispose();
                            PreviewImage28?.Dispose();

                            PreviewImage = mainPreview;
                            PreviewImage12 = preview12;
                            PreviewImage16 = preview16;
                            PreviewImage20 = preview20;
                            PreviewImage28 = preview28;
                        });
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to render preview: {ex.Message}");
                }
            }, token);
        }

        /// <summary>
        /// Renders text to an Avalonia Bitmap using SkiaSharp.
        /// </summary>
        private Bitmap RenderTextToBitmap(string text, float fontSize, SKTypeface typeface)
        {
            // Get colors based on dark/light mode
            var bgColor = IsDarkPreview ? new SKColor(30, 30, 30) : new SKColor(250, 250, 250);
            var fgColor = IsDarkPreview ? new SKColor(240, 240, 240) : new SKColor(30, 30, 30);

            using var font = new SKFont(typeface, fontSize);
            using var paint = new SKPaint
            {
                IsAntialias = true,
                Color = fgColor
            };

            // Measure the text
            var bounds = new SKRect();
            font.MeasureText(text, out bounds);

            // Add padding
            int padding = 8;
            int width = Math.Max((int)Math.Ceiling(bounds.Width) + padding * 2, 100);
            int height = Math.Max((int)Math.Ceiling(bounds.Height) + padding * 2, (int)(fontSize * 1.5));

            // Create bitmap
            using var surface = SKSurface.Create(new SKImageInfo(width, height));
            var canvas = surface.Canvas;
            canvas.Clear(bgColor);

            // Draw text centered vertically
            float x = padding;
            float y = height / 2f + bounds.Height / 2f - bounds.Bottom;
            canvas.DrawText(text, x, y, SKTextAlign.Left, font, paint);

            // Convert to Avalonia Bitmap
            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = new MemoryStream(data.ToArray());
            return new Bitmap(stream);
        }

        /// <summary>
        /// Clears all preview images.
        /// </summary>
        private void ClearPreviewImages()
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                PreviewImage?.Dispose();
                PreviewImage12?.Dispose();
                PreviewImage16?.Dispose();
                PreviewImage20?.Dispose();
                PreviewImage28?.Dispose();

                PreviewImage = null;
                PreviewImage12 = null;
                PreviewImage16 = null;
                PreviewImage20 = null;
                PreviewImage28 = null;
            });
        }

        private async Task LoadFontsAsync()
        {
            if (IsLoading) return;

            IsLoading = true;
            ErrorMessage = "";
            StatusMessage = "Loading fonts from Google Fonts...";

            try
            {
                // Use Google Fonts Developer API (free, no key required for font list)
                // This URL returns all font families with metadata
                var apiUrl = "https://www.googleapis.com/webfonts/v1/webfonts?sort=popularity&key=";

                var response = await _httpClient.GetStringAsync(apiUrl);
                var jsonDoc = JsonDocument.Parse(response);
                var items = jsonDoc.RootElement.GetProperty("items");

                AllFonts.Clear();

                foreach (var item in items.EnumerateArray())
                {
                    var familyName = item.GetProperty("family").GetString();

                    // Skip icon fonts - they're not available as downloadable TTFs in the GitHub repo
                    if (IsIconFont(familyName))
                        continue;

                    var font = new GoogleFontFamily
                    {
                        Family = familyName,
                        Category = item.GetProperty("category").GetString(),
                        Version = item.TryGetProperty("version", out var v) ? v.GetString() : "",
                        LastModified = item.TryGetProperty("lastModified", out var lm) ? lm.GetString() : "",
                        IsInstalled = IsFontInstalled(familyName)
                    };

                    if (item.TryGetProperty("variants", out var variants))
                    {
                        foreach (var variant in variants.EnumerateArray())
                        {
                            font.Variants.Add(variant.GetString());
                        }
                    }

                    if (item.TryGetProperty("subsets", out var subsets))
                    {
                        foreach (var subset in subsets.EnumerateArray())
                        {
                            font.Subsets.Add(subset.GetString());
                        }
                    }

                    AllFonts.Add(font);
                }

                StatusMessage = $"Loaded {AllFonts.Count} fonts";
                FilterFonts();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load fonts: {ex.Message}";
                StatusMessage = "";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void FilterFontsDebounced()
        {
            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(200, token);
                    if (!token.IsCancellationRequested)
                    {
                        // Run filter on UI thread
                        Avalonia.Threading.Dispatcher.UIThread.Post(() => FilterFonts());
                    }
                }
                catch (TaskCanceledException) { }
            });
        }

        private void FilterFonts()
        {
            FilteredFonts.Clear();

            var query = SearchText?.Trim().ToLowerInvariant() ?? "";
            var category = SelectedCategory == "All Categories" ? null : SelectedCategory?.ToLowerInvariant().Replace(" ", "-");
            var activeTags = FilterTags.Where(t => t.IsSelected).ToList();

            // Filter and sort alphabetically by family name
            var filtered = AllFonts
                .Where(font =>
                {
                    // Filter by category
                    if (category != null && !string.Equals(font.Category, category, StringComparison.OrdinalIgnoreCase))
                        return false;

                    // Filter by search text
                    if (!string.IsNullOrEmpty(query) &&
                        font.Family.ToLowerInvariant().IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                        return false;

                    // Filter by active tags (font must match ALL selected tags)
                    foreach (var tag in activeTags)
                    {
                        if (!tag.Filter(font))
                            return false;
                    }

                    return true;
                })
                .OrderBy(f => f.Family, StringComparer.OrdinalIgnoreCase);

            foreach (var font in filtered)
            {
                FilteredFonts.Add(font);
            }

            StatusMessage = $"Showing {FilteredFonts.Count} of {AllFonts.Count} fonts";
        }

        /// <summary>
        /// Icon fonts are not available as downloadable TTFs in the Google Fonts GitHub repository.
        /// </summary>
        private static bool IsIconFont(string familyName)
        {
            if (string.IsNullOrEmpty(familyName)) return false;
            var lower = familyName.ToLowerInvariant();
            return lower.Contains("material icons") ||
                   lower.Contains("material symbols") ||
                   lower.StartsWith("noto emoji") ||
                   lower.StartsWith("noto color emoji");
        }

        // License folder prefixes in Google Fonts repository
        private static readonly string[] _licenseFolders = { "ofl", "apache", "ufl" };

        /// <summary>
        /// Gets cached font files for the given font family from the local cache directory.
        /// </summary>
        private List<FontFileInfo> GetCachedFilesForFont(string folderName)
        {
            var cachedFiles = new List<FontFileInfo>();
            try
            {
                if (Directory.Exists(_fontCacheDir))
                {
                    // Cache files are named: {folderName}-{variant}.ttf
                    foreach (var filePath in Directory.GetFiles(_fontCacheDir, $"{folderName}-*.ttf"))
                    {
                        var fileName = Path.GetFileName(filePath);
                        var fileInfo = new FileInfo(filePath);
                        cachedFiles.Add(new FontFileInfo
                        {
                            Name = fileName,
                            DownloadUrl = null, // Already cached, no download URL needed
                            Size = fileInfo.Length,
                            IsVariable = fileName.Contains("["),
                            IsCached = true,
                            CachedPath = filePath
                        });
                    }
                }
            }
            catch { /* Ignore cache read errors */ }
            return cachedFiles;
        }

        private async Task LoadFontFilesAsync(GoogleFontFamily font)
        {
            IsLoadingFiles = true;
            AvailableFiles.Clear();
            ErrorMessage = "";

            try
            {
                // Convert font name to GitHub folder format (lowercase, no spaces)
                var folderName = font.Family.Replace(" ", "").ToLowerInvariant();
                var ttfFiles = new List<FontFileInfo>();
                string foundLicenseFolder = null;
                bool rateLimited = false;

                // Try each license folder (ofl, apache, ufl)
                foreach (var licenseFolder in _licenseFolders)
                {
                    try
                    {
                        var apiUrl = $"{GitHubFontsApiBase}{licenseFolder}/{folderName}";
                        var response = await GetGitHubApiAsync(apiUrl);
                        var files = JsonDocument.Parse(response);
                        foundLicenseFolder = licenseFolder;

                        foreach (var file in files.RootElement.EnumerateArray())
                        {
                            var name = file.GetProperty("name").GetString();
                            if (name.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase))
                            {
                                var isVariable = name.Contains("[");
                                ttfFiles.Add(new FontFileInfo
                                {
                                    Name = name,
                                    DownloadUrl = file.GetProperty("download_url").GetString(),
                                    Size = file.TryGetProperty("size", out var s) ? s.GetInt64() : 0,
                                    IsVariable = isVariable
                                });
                            }
                        }

                        // Found the folder, stop searching
                        break;
                    }
                    catch (HttpRequestException ex)
                    {
                        // Check for rate limiting (403) or other errors
                        if (ex.Message.Contains("403"))
                        {
                            ErrorMessage = "GitHub API rate limit exceeded. Showing cached files only.";
                            rateLimited = true;
                            break;
                        }
                        // 404 = Not found in this license folder, try next
                        continue;
                    }
                }

                // If rate limited, show cached files for this font
                if (rateLimited)
                {
                    var cachedFiles = GetCachedFilesForFont(folderName);
                    foreach (var file in cachedFiles.OrderBy(f => f.Name))
                    {
                        AvailableFiles.Add(file);
                    }
                    if (AvailableFiles.Count > 0)
                    {
                        StatusMessage = $"{AvailableFiles.Count} cached file(s) available";
                    }
                    return;
                }

                if (foundLicenseFolder == null)
                {
                    ErrorMessage = $"Font '{font.Family}' not found in Google Fonts repository.";
                    return;
                }

                // If no static TTFs found, check static subfolder
                if (ttfFiles.All(f => f.IsVariable))
                {
                    try
                    {
                        var staticUrl = $"{GitHubFontsApiBase}{foundLicenseFolder}/{folderName}/static";
                        var staticResponse = await GetGitHubApiAsync(staticUrl);
                        var staticFiles = JsonDocument.Parse(staticResponse);

                        foreach (var file in staticFiles.RootElement.EnumerateArray())
                        {
                            var name = file.GetProperty("name").GetString();
                            if (name.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) && !name.Contains("["))
                            {
                                ttfFiles.Add(new FontFileInfo
                                {
                                    Name = name,
                                    DownloadUrl = file.GetProperty("download_url").GetString(),
                                    Size = file.TryGetProperty("size", out var s) ? s.GetInt64() : 0,
                                    IsVariable = false
                                });
                            }
                        }
                    }
                    catch { /* Static folder doesn't exist */ }
                }

                // Sort: static files first, then by name
                foreach (var file in ttfFiles.OrderBy(f => f.IsVariable).ThenBy(f => f.Name))
                {
                    AvailableFiles.Add(file);
                }

                if (AvailableFiles.Count == 0)
                {
                    StatusMessage = "No TTF files found for this font";
                }
                else
                {
                    StatusMessage = $"Found {AvailableFiles.Count} font file(s)";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load font files: {ex.Message}";
            }
            finally
            {
                IsLoadingFiles = false;
                DownloadAllFilesCommand.NotifyCanExecuteChanged();
            }
        }

        private async Task DownloadFilesAsync(bool downloadAll)
        {
            if (SelectDownloadFolderAction == null) return;

            var filesToDownload = downloadAll
                ? AvailableFiles.ToList()
                : (SelectedFile != null ? new List<FontFileInfo> { SelectedFile } : new List<FontFileInfo>());

            if (filesToDownload.Count == 0) return;

            var folder = await SelectDownloadFolderAction(LastFolderPath);
            if (string.IsNullOrEmpty(folder)) return;

            LastFolderPath = folder;
            IsDownloading = true;
            ErrorMessage = "";

            try
            {
                var downloadedCount = 0;
                foreach (var file in filesToDownload)
                {
                    StatusMessage = $"Downloading {file.Name}...";

                    var destPath = Path.Combine(folder, file.Name);
                    using var httpClient = new HttpClient();
                    httpClient.DefaultRequestHeaders.Add("User-Agent", "OpenSourceToolkit-FontsViewer/1.0");
                    var fileBytes = await httpClient.GetByteArrayAsync(file.DownloadUrl);
                    await File.WriteAllBytesAsync(destPath, fileBytes);
                    downloadedCount++;
                }

                StatusMessage = $"Downloaded {downloadedCount} file(s) to {folder}";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Download failed: {ex.Message}";
            }
            finally
            {
                IsDownloading = false;
            }
        }

        /// <summary>
        /// Extracts the font family name from a TTF file by reading the name table.
        /// </summary>
        private static string GetFontFamilyNameFromFile(string ttfPath)
        {
            try
            {
                using (var fs = new FileStream(ttfPath, FileMode.Open, FileAccess.Read))
                using (var reader = new BinaryReader(fs))
                {
                    // Read TTF header
                    uint sfntVersion = ReadUInt32BE(reader);
                    ushort numTables = ReadUInt16BE(reader);
                    reader.ReadBytes(6); // Skip searchRange, entrySelector, rangeShift

                    // Find 'name' table
                    long nameTableOffset = 0;
                    for (int i = 0; i < numTables; i++)
                    {
                        string tag = new string(reader.ReadChars(4));
                        reader.ReadBytes(4); // Skip checksum
                        uint offset = ReadUInt32BE(reader);
                        reader.ReadBytes(4); // Skip length

                        if (tag == "name")
                        {
                            nameTableOffset = offset;
                            break;
                        }
                    }

                    if (nameTableOffset == 0) return null;

                    // Read name table
                    fs.Seek(nameTableOffset, SeekOrigin.Begin);
                    ushort format = ReadUInt16BE(reader);
                    ushort count = ReadUInt16BE(reader);
                    ushort stringOffset = ReadUInt16BE(reader);

                    // Look for Font Family name (nameID 1) or Typographic Family name (nameID 16)
                    for (int i = 0; i < count; i++)
                    {
                        ushort platformID = ReadUInt16BE(reader);
                        ushort encodingID = ReadUInt16BE(reader);
                        ushort languageID = ReadUInt16BE(reader);
                        ushort nameID = ReadUInt16BE(reader);
                        ushort length = ReadUInt16BE(reader);
                        ushort offset = ReadUInt16BE(reader);

                        // nameID 1 = Font Family, nameID 16 = Typographic Family
                        // Prefer platformID 3 (Windows) with encodingID 1 (Unicode BMP)
                        if ((nameID == 1 || nameID == 16) && platformID == 3 && encodingID == 1)
                        {
                            long currentPos = fs.Position;
                            fs.Seek(nameTableOffset + stringOffset + offset, SeekOrigin.Begin);
                            byte[] nameBytes = reader.ReadBytes(length);
                            fs.Seek(currentPos, SeekOrigin.Begin);

                            // Windows Unicode is UTF-16 BE
                            string name = System.Text.Encoding.BigEndianUnicode.GetString(nameBytes);
                            if (!string.IsNullOrWhiteSpace(name))
                                return name;
                        }
                    }
                }
            }
            catch
            {
                // Ignore parsing errors
            }
            return null;
        }

        private static ushort ReadUInt16BE(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(2);
            return (ushort)((bytes[0] << 8) | bytes[1]);
        }

        private static uint ReadUInt32BE(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(4);
            return (uint)((bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3]);
        }
    }
}
