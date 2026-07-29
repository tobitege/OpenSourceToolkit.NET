#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Flowery.Controls;
using Flowery.Localization;
using Flowery.Services;
using OpenSourceToolkit.NET.Data;
using OpenSourceToolkit.NET.Helpers;
using OpenSourceToolkit.NET.Localization;
using OpenSourceToolkit.NET.Services;
using OpenSourceToolkit.NET.ViewModels;
using OpenSourceToolkit.NET.ViewModels.Tools;

namespace OpenSourceToolkit.NET.Views
{
    /// <summary>
    /// Main application window.
    ///
    /// STARTUP FLICKER PREVENTION:
    /// This window starts hidden (IsVisible=false, ShowInTaskbar=false in AXAML) to prevent
    /// visual flicker during startup. The reveal sequence is:
    ///   1. Constructor: Restore window size and position (while hidden)
    ///   2. App.OnFrameworkInitializationCompleted: Styles load, theme applied
    ///   3. App raises ThemeRestored event
    ///   4. OnThemeRestored: Window becomes visible with correct position AND theme
    ///
    /// See App.axaml.cs OnFrameworkInitializationCompleted for the full startup sequence.
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainWindowViewModel _viewModel;
        
        /// <summary>
        /// Maps sidebar item IDs to tool view model types.
        /// </summary>
        private readonly Dictionary<string, Type> _toolIdToType;

        /// <summary>
        /// Reference to the Favorites category for dynamic population.
        /// </summary>
        private SidebarCategory? _favoritesCategory;

        /// <summary>
        /// Last sidebar item that represents application content. Administrative and
        /// selector items must not replace the startup selection.
        /// </summary>
        private SidebarItem? _lastRememberedSidebarItem;

        private const string SidebarStateKey = "sidebar";

        public MainWindow()
        {
            InitializeComponent();

            _viewModel = new MainWindowViewModel();
            DataContext = _viewModel;

            // Build tool ID to Type mapping from the ViewModel's tool list
            _toolIdToType = BuildToolIdMapping();

            // Restore state/position while window is still hidden (prevents position flicker)
            RestoreWindowState();
            RestoreWindowPosition();
            ApplySavedTheme();

            Opened += OnOpened;
            Closing += OnClosing;

            // Window reveals itself only after theme is fully applied (prevents theme flicker)
            App.ThemeRestored += OnThemeRestored;

            // Initialize the FloweryComponentSidebar
            InitializeSidebar();

            // Subscribe to culture changes to rebuild sidebar categories
            ToolkitLocalization.CultureChanged += OnCultureChanged;
        }

        private void InitializeSidebar()
        {
            if (ComponentSidebar == null) return;

            // Set up categories and languages
            ComponentSidebar.Categories = ToolkitSidebarData.CreateCategories();
            ComponentSidebar.AvailableLanguages = ToolkitSidebarData.CreateLanguages();

            // Initialize favorite states for all tool items
            InitializeSidebarFavorites();

            // Subscribe to favorite toggle events
            ComponentSidebar.FavoriteToggled += ComponentSidebar_FavoriteToggled;

            // Try to restore last viewed item
            var (lastItemId, category) = ComponentSidebar.GetLastViewedItem();
            if (lastItemId != null && category != null)
            {
                var item = category.Items.FirstOrDefault(i => i.Id == lastItemId);
                if (item != null && ShouldRememberSidebarItem(item))
                {
                    _lastRememberedSidebarItem = item;
                    NavigateToItem(item);
                    return;
                }
            }

            // Default to Home, including when an older version persisted Settings.
            var homeItem = FindSidebarItem("welcome");
            if (homeItem != null)
            {
                _lastRememberedSidebarItem = homeItem;
                RestoreSidebarSelection(homeItem);
            }

            _viewModel.GoHome();
        }

        private SidebarItem? FindSidebarItem(string itemId)
        {
            return ComponentSidebar.Categories
                .SelectMany(category => category.Items)
                .FirstOrDefault(item => string.Equals(item.Id, itemId, StringComparison.OrdinalIgnoreCase));
        }

        private static bool ShouldRememberSidebarItem(SidebarItem item)
        {
            return item is not ToolkitSettingsItem and
                   not ToolkitThemeSelectorItem and
                   not ToolkitLanguageSelectorItem;
        }

        private void RestoreSidebarSelection(SidebarItem item)
        {
            ComponentSidebar.SelectedItem = item;

            var state = new List<string> { $"last:{item.Id}" };
            state.AddRange(ComponentSidebar.Categories
                .Where(category => !category.IsExpanded)
                .Select(category => $"collapsed:{category.Name}"));
            StateStorageProvider.Instance.SaveLines(SidebarStateKey, state);
        }

        /// <summary>
        /// Maps sidebar item IDs to tool numeric IDs for favorites tracking.
        /// </summary>
        private static readonly Dictionary<string, int> _sidebarIdToToolId = new(StringComparer.OrdinalIgnoreCase)
        {
            // Media & Files
            ["image-converter"] = 32,
            ["folder-analyzer"] = 17,
            ["ascii-art"] = 18,
            ["pdf-tools"] = 20,
            ["clipboard-image"] = 30,
            ["audio-noise"] = 25,
            ["fonts-viewer"] = 39,
            
            // Generators
            ["uuid"] = 1,
            ["lorem-ipsum"] = 2,
            ["mock-data"] = 3,
            ["privacy-policy"] = 4,
            ["qr-code"] = 5,
            ["password"] = 29,
            ["vcard"] = 27,
            
            // Converters
            ["text-case"] = 6,
            ["timestamp"] = 7,
            ["base64"] = 11,
            ["color"] = 12,
            ["eth-converter"] = 28,
            ["json-formatter"] = 31,
            
            // Security
            ["hash"] = 8,
            ["hmac"] = 9,
            ["jwt"] = 10,
            
            // Networking
            ["uptime"] = 13,
            ["dns"] = 14,
            ["ip-location"] = 15,
            ["ip-calculator"] = 22,
            ["speed-test"] = 35,
            
            // Development
            ["cron"] = 16,
            ["api-tester"] = 23,
            ["nextjs-image"] = 24,
            ["regex"] = 26,
            ["diff-checker"] = 33,
            ["sql-formatter"] = 37,
            ["markdown-editor"] = 38,
            ["theme-testing"] = 40,
            
            // Hardware
            ["hardware"] = 19,
            ["keyboard-tester"] = 34,
            ["stopwatch-timer"] = 36,
            
            // Math
            ["calculator"] = 1100,
            
            // Finance
            ["financial-calculator"] = 21,
        };

        /// <summary>
        /// Initialize favorite states for all tool items in the sidebar.
        /// </summary>
        private void InitializeSidebarFavorites()
        {
            var favoriteIds = AppSettings.Current.FavoriteToolIds;

            foreach (var category in ComponentSidebar.Categories)
            {
                // Find and store reference to Favorites category
                if (category.Name == "Group_Favorites")
                {
                    _favoritesCategory = category;
                    continue; // Will populate below
                }

                foreach (var item in category.Items)
                {
                    // Skip special items (home, theme, language, settings)
                    if (item is ToolkitThemeSelectorItem ||
                        item is ToolkitLanguageSelectorItem ||
                        item is ToolkitSettingsItem ||
                        item.Id == "welcome")
                    {
                        continue;
                    }

                    // Enable favorites for tool items
                    if (_sidebarIdToToolId.TryGetValue(item.Id, out var toolId))
                    {
                        item.ShowFavoriteIcon = true;
                        item.IsFavorite = favoriteIds.Contains(toolId);
                    }
                }
            }

            // Populate the favorites section
            RefreshFavoritesSection();
        }

        /// <summary>
        /// Refreshes the Favorites section in the sidebar with current favorites.
        /// </summary>
        private void RefreshFavoritesSection()
        {
            if (_favoritesCategory == null) return;

            _favoritesCategory.Items.Clear();

            var favoriteIds = AppSettings.Current.FavoriteToolIds;
            if (favoriteIds.Count == 0)
            {
                // Hide the Favorites category when empty (by collapsing it)
                _favoritesCategory.IsExpanded = false;
                return;
            }

            _favoritesCategory.IsExpanded = true;

            // Build reverse mapping: tool ID -> sidebar ID
            var toolIdToSidebarId = _sidebarIdToToolId.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

            // Find all tool items that are favorites and add them to the Favorites section
            foreach (var category in ComponentSidebar.Categories)
            {
                if (category == _favoritesCategory) continue;

                foreach (var item in category.Items)
                {
                    if (_sidebarIdToToolId.TryGetValue(item.Id, out var toolId) && favoriteIds.Contains(toolId))
                    {
                        // Create a copy for the favorites section (same Id, Name, etc.)
                        var favoriteItem = new SidebarItem
                        {
                            Id = item.Id,
                            Name = item.Name,
                            TabHeader = "Group_Favorites",
                            ShowFavoriteIcon = true,
                            IsFavorite = true
                        };
                        _favoritesCategory.Items.Add(favoriteItem);
                    }
                }
            }
        }

        /// <summary>
        /// Handle favorite toggle from sidebar.
        /// </summary>
        private void ComponentSidebar_FavoriteToggled(object? sender, SidebarFavoriteToggledEventArgs e)
        {
            if (!_sidebarIdToToolId.TryGetValue(e.Item.Id, out var toolId))
                return;

            var favorites = AppSettings.Current.FavoriteToolIds;

            if (e.Item.IsFavorite)
            {
                // Add to favorites
                if (!favorites.Contains(toolId))
                {
                    favorites.Add(toolId);
                    AppSettings.Save();
                }
            }
            else
            {
                // Remove from favorites
                if (favorites.Remove(toolId))
                {
                    AppSettings.Save();
                }
            }

            // Sync IsFavorite state across all items with the same Id
            foreach (var category in ComponentSidebar.Categories)
            {
                if (category == _favoritesCategory) continue;

                foreach (var item in category.Items)
                {
                    if (item.Id == e.Item.Id && item != e.Item)
                    {
                        item.IsFavorite = e.Item.IsFavorite;
                    }
                }
            }

            // Refresh the Favorites section
            RefreshFavoritesSection();

            // Refresh home view if it's currently shown
            if (_viewModel.CurrentTool is HomeViewModel homeVm)
            {
                homeVm.RefreshQuickActions();
            }
        }

        private void OnCultureChanged(object? sender, System.Globalization.CultureInfo culture)
        {
            // Note: SidebarCategory and SidebarItem classes already handle DisplayName updates
            // via their built-in PropertyChanged notifications when culture changes.
            // We do NOT recreate categories here as that can cause timing issues with click handling.

            // Save the selected language to settings
            // Use TwoLetterISOLanguageName since sidebar uses short codes like "de", "fr"
            var langCode = culture.TwoLetterISOLanguageName;
            if (culture.Name == "zh-Hans" || culture.Name.StartsWith("zh-Hans"))
                langCode = "zh-Hans"; // Special case for Chinese
            
            AppSettings.Current.Language = langCode;
            AppSettings.Save();
        }

        private Dictionary<string, Type> BuildToolIdMapping()
        {
            // Map sidebar item IDs to tool ViewModel types
            return new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
            {
                // Media & Files
                ["image-converter"] = typeof(ImageConverterToolViewModel),
                ["folder-analyzer"] = typeof(FolderAnalyzerToolViewModel),
                ["ascii-art"] = typeof(AsciiArtToolViewModel),
                ["pdf-tools"] = typeof(PdfToolViewModel),
                ["clipboard-image"] = typeof(ClipboardImageSaverToolViewModel),
                ["audio-noise"] = typeof(AudioNoiseReductionToolViewModel),
                ["fonts-viewer"] = typeof(FontsViewerToolViewModel),
                
                // Generators
                ["uuid"] = typeof(UuidToolViewModel),
                ["lorem-ipsum"] = typeof(LoremIpsumToolViewModel),
                ["mock-data"] = typeof(MockDataToolViewModel),
                ["privacy-policy"] = typeof(PrivacyPolicyToolViewModel),
                ["qr-code"] = typeof(QrCodeToolViewModel),
                ["password"] = typeof(PasswordGeneratorToolViewModel),
                ["vcard"] = typeof(VCardGeneratorToolViewModel),
                
                // Converters
                ["text-case"] = typeof(TextCaseToolViewModel),
                ["timestamp"] = typeof(TimestampToolViewModel),
                ["base64"] = typeof(Base64ToolViewModel),
                ["color"] = typeof(ColorToolViewModel),
                ["eth-converter"] = typeof(EthConverterToolViewModel),
                ["json-formatter"] = typeof(JsonFormatterToolViewModel),
                
                // Security
                ["hash"] = typeof(HashToolViewModel),
                ["hmac"] = typeof(HmacToolViewModel),
                ["jwt"] = typeof(JwtToolViewModel),
                
                // Networking
                ["uptime"] = typeof(UptimeToolViewModel),
                ["dns"] = typeof(DnsToolViewModel),
                ["ip-location"] = typeof(IpLocationToolViewModel),
                ["ip-calculator"] = typeof(IpCalculatorToolViewModel),
                ["speed-test"] = typeof(SpeedTestToolViewModel),
                
                // Development
                ["cron"] = typeof(CronToolViewModel),
                ["api-tester"] = typeof(ApiTesterToolViewModel),
                ["nextjs-image"] = typeof(NextJsImageDecoderToolViewModel),
                ["regex"] = typeof(RegexTesterToolViewModel),
                ["diff-checker"] = typeof(DiffCheckerToolViewModel),
                ["sql-formatter"] = typeof(SqlFormatterToolViewModel),
                ["markdown-editor"] = typeof(MarkdownEditorToolViewModel),
                ["theme-testing"] = typeof(ThemeTestingToolViewModel),
                
                // Hardware
                ["hardware"] = typeof(HardwareToolViewModel),
                ["keyboard-tester"] = typeof(KeyboardTesterToolViewModel),
                ["stopwatch-timer"] = typeof(StopwatchTimerToolViewModel),
                
                // Math
                ["calculator"] = typeof(ScientificCalculatorToolViewModel),
                
                // Finance
                ["financial-calculator"] = typeof(FinancialCalculatorToolViewModel),
            };
        }

        private void ComponentSidebar_ItemSelected(object? sender, SidebarItemSelectedEventArgs e)
        {
            if (e.Item == null)
            {
                return;
            }

            if (e.Item is ToolkitSettingsItem)
            {
                var itemToRestore = _lastRememberedSidebarItem ?? FindSidebarItem("welcome");
                if (itemToRestore != null)
                {
                    _lastRememberedSidebarItem = itemToRestore;
                    RestoreSidebarSelection(itemToRestore);
                }

                OpenSettings();
                return;
            }

            if (ShouldRememberSidebarItem(e.Item))
            {
                _lastRememberedSidebarItem = e.Item;
            }

            NavigateToItem(e.Item);
        }

        private void NavigateToItem(SidebarItem item)
        {
            // Handle special items
            if (item is ToolkitSettingsItem)
            {
                OpenSettings();
                return;
            }

            // Handle home/welcome
            if (item.Id == "welcome")
            {
                _viewModel.GoHome();
                return;
            }

            // Handle theme/language selectors - these are handled by the sidebar itself
            if (item is ToolkitThemeSelectorItem || item is ToolkitLanguageSelectorItem)
            {
                return;
            }

            // Navigate to tool
            if (_toolIdToType.TryGetValue(item.Id, out var toolType))
            {
                _viewModel.NavigateToToolByType(toolType);
            }
        }

        internal async void OpenSettings(SettingsSection initialSection = SettingsSection.General)
        {
            var settingsWindow = new SettingsWindow(initialSection);
            await settingsWindow.ShowDialog(this);

            // Notify all subscribed tools that settings have changed
            ToolViewModel.RaiseSettingsClosed();
        }

        private void ApplySavedTheme()
        {
            var savedTheme = AppSettings.Current.Theme;
            var theme = !string.IsNullOrEmpty(savedTheme) ? savedTheme.ParseThemeVariant() : ThemeVariant.Dark;
            if (Application.Current != null)
            {
                Application.Current.RequestedThemeVariant = theme;
            }
        }

        private void RestoreWindowState()
        {
            var settings = AppSettings.Current;

            if (settings.WindowMaximized)
            {
                WindowState = WindowState.Maximized;
            }
            else if (settings.WindowWidth.HasValue && settings.WindowHeight.HasValue)
            {
                Width = Math.Max(settings.WindowWidth.Value, MinWidth);
                Height = Math.Max(settings.WindowHeight.Value, MinHeight);
            }
        }

        /// <summary>
        /// Set window position early in constructor to prevent flicker.
        /// Position is set directly from saved values; screen validation happens in OnOpened.
        /// </summary>
        private void RestoreWindowPosition()
        {
            var settings = AppSettings.Current;

            if (settings.WindowX.HasValue && settings.WindowY.HasValue && !settings.WindowMaximized)
            {
                // Set position directly - this happens before the window is shown
                Position = new PixelPoint((int)settings.WindowX.Value, (int)settings.WindowY.Value);
            }
        }

        private void OnOpened(object? sender, EventArgs e)
        {
            var settings = AppSettings.Current;

            // Validate and clamp position to visible screen area (in case monitor config changed)
            if (settings.WindowX.HasValue && settings.WindowY.HasValue && !settings.WindowMaximized)
            {
                var x = (int)settings.WindowX.Value;
                var y = (int)settings.WindowY.Value;

                // Find the screen where the window overlaps and clamp if needed
                bool foundScreen = false;
                foreach (var screen in Screens.All)
                {
                    var s = screen.WorkingArea;
                    var pos = ClampWindowToScreen(x, y, Bounds.Width, Bounds.Height, s, screen.Scaling);
                    if (pos.HasValue)
                    {
                        // Only update position if it changed (to avoid unnecessary flicker)
                        if (Position != pos.Value)
                            Position = pos.Value;
                        foundScreen = true;
                        break;
                    }
                }

                // Fallback: place on primary screen if saved position is off all screens
                if (!foundScreen)
                {
                    var primary = Screens.Primary;
                    if (primary != null)
                    {
                        var pos = ClampWindowToScreen(x, y, Bounds.Width, Bounds.Height, primary.WorkingArea, primary.Scaling);
                        if (pos.HasValue)
                            Position = pos.Value;
                    }
                }
            }

            // Note: Window reveal is handled by OnThemeRestored, not here
        }

        /// <summary>
        /// Called when App.RestoreSavedTheme completes. Now safe to reveal the window.
        /// </summary>
        private void OnThemeRestored()
        {
            // Unsubscribe to avoid memory leaks
            App.ThemeRestored -= OnThemeRestored;

            // Reveal the window now that position is set and theme is applied
            ShowInTaskbar = true;
            IsVisible = true;
            Activate(); // Bring to front and focus
        }

        private PixelPoint? ClampWindowToScreen(int x, int y, double boundsW, double boundsH, PixelRect screen, double scaling)
        {
            // Shadow margins (Windows DWM shadow extends beyond window frame)
            // Shadow is minimal at top, ~8px on sides, ~12px at bottom
            var marginLeft = (int)(8 * scaling);
            var marginRight = (int)(8 * scaling);
            var marginTop = (int)(2 * scaling);
            var marginBottom = (int)(12 * scaling);

            // Convert logical pixels to physical pixels
            var w = (int)(boundsW * scaling) + marginLeft + marginRight;
            var h = (int)(boundsH * scaling) + marginTop + marginBottom;

            // Check if window overlaps this screen
            if (!WindowGeometry.RectanglesOverlap(x - marginLeft, y - marginTop, w, h, screen.X, screen.Y, screen.Width, screen.Height))
                return null;

            var clamped = WindowGeometry.ClampRectangleInside(x - marginLeft, y - marginTop, w, h, screen.X, screen.Y, screen.Width, screen.Height);
            return new PixelPoint(clamped.X + marginLeft, clamped.Y + marginTop);
        }

        private void OnClosing(object? sender, WindowClosingEventArgs e)
        {
            var settings = AppSettings.Current;
            settings.WindowMaximized = WindowState == WindowState.Maximized;

            if (WindowState == WindowState.Normal)
            {
                settings.WindowX = Position.X;
                settings.WindowY = Position.Y;
                settings.WindowWidth = Width;
                settings.WindowHeight = Height;
            }

            AppSettings.Save();
        }
    }
}
