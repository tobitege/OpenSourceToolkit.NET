#nullable enable
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Styling;
using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Flowery.Controls;
using Flowery.Localization;
using OpenSourceToolkit.NET.Helpers;
using OpenSourceToolkit.NET.Localization;
using OpenSourceToolkit.NET.Services;
using OpenSourceToolkit.NET.ViewModels;
using OpenSourceToolkit.NET.Views;
using PdfSharp.Fonts;

namespace OpenSourceToolkit.NET
{
    public partial class App : Application
    {
        /// <summary>
        /// Event raised when the initial theme restoration is complete.
        /// MainWindow listens to this to reveal itself after theme is applied.
        /// </summary>
        public static event Action? ThemeRestored;

        /// <summary>
        /// Flag to prevent theme saves during initialization.
        /// DaisyThemeDropdown controls in MainWindow and HomeView trigger ApplyTheme during construction,
        /// which would overwrite the saved theme. This flag blocks saves until initialization is complete.
        /// </summary>
        public static bool IsInitializing { get; private set; } = true;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);

            // Wire up localization for Flowery.NET components (FloweryComponentSidebar, etc.)
            // This allows sidebar category and item names to be resolved via ToolkitLocalization
            FloweryLocalization.CustomResolver = key => ToolkitLocalization.GetString(key);

            // Restore saved language BEFORE MainWindow is created
            // This ensures FloweryComponentSidebar gets the correct language at initialization
            RestoreSavedLanguage();

            // Add global exception handlers for all unhandled exceptions
            SetupGlobalExceptionHandlers();
        }

        private void SetupGlobalExceptionHandlers()
        {
            // Handle exceptions from the current AppDomain (non-UI thread crashes)
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                LogException("AppDomain.UnhandledException", ex);
            };

            // Handle exceptions from background tasks (Task.Run, async void, etc.)
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                LogException("TaskScheduler.UnobservedTaskException", e.Exception);
                e.SetObserved(); // Prevent the process from terminating
            };

            // Handle exceptions from the Avalonia UI thread / Dispatcher
            // This is the most important one for preventing UI crashes
            Dispatcher.UIThread.UnhandledException += (s, e) =>
            {
                LogException("Dispatcher.UnhandledException", e.Exception);
                e.Handled = true; // Prevent the app from crashing
            };
        }

        private static void LogException(string source, Exception? ex)
        {
            var msg = $"[{source}] {ex?.GetType().Name}: {ex?.Message}";
            if (ex?.InnerException != null)
                msg += $"\nInner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}";
            msg += $"\nStack: {ex?.StackTrace}";

            System.Diagnostics.Debug.WriteLine(msg);
            Console.WriteLine(msg);
            
            // Log to DebugLogger if active
            if (Services.DebugLogger.IsEnabled)
                Services.DebugLogger.Log(source, msg);

            // Also log to a file for post-mortem debugging
            try
            {
                var logPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "OpenSourceToolkit",
                    "crash.log");
                var logDir = System.IO.Path.GetDirectoryName(logPath);
                if (logDir != null && !System.IO.Directory.Exists(logDir))
                    System.IO.Directory.CreateDirectory(logDir);

                var logEntry = $"\n\n=== {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n{msg}";
                System.IO.File.AppendAllText(logPath, logEntry);
            }
            catch
            {
                // Ignore logging failures
            }
        }

        /// <summary>
        /// Application startup sequence - carefully ordered to prevent visual flicker.
        ///
        /// FLICKER PREVENTION STRATEGY:
        /// The MainWindow starts hidden (IsVisible=false, ShowInTaskbar=false in AXAML).
        /// This allows us to:
        ///   1. Create the window (hidden) and restore its position
        ///   2. Wait for framework/styles to fully load
        ///   3. Apply DaisyUI theme overlay (requires styles to be loaded)
        ///   4. Only THEN reveal the window via ThemeRestored event
        ///
        /// If theme is applied BEFORE styles load: colors won't apply correctly.
        /// If window is shown BEFORE theme applies: user sees theme "pop in" (flicker).
        /// This sequence ensures both work correctly.
        /// </summary>
        public override void OnFrameworkInitializationCompleted()
        {
             // Register global font resolver for PdfSharp
            if (GlobalFontSettings.FontResolver == null)
            {
                GlobalFontSettings.FontResolver = new AppFontResolver();
            }

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Avoid duplicate validations from both Avalonia and the CommunityToolkit.
                // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
                DisableAvaloniaDataAnnotationValidation();

                // Step 1: Create window (starts hidden - see MainWindow.axaml IsVisible/ShowInTaskbar)
                // Window constructor also restores saved position to prevent position flicker
                // NOTE: MainWindow.cs constructor creates its own MainWindowViewModel and sets DataContext
                // Do NOT create a new MainWindowViewModel here - it would overwrite the one created in MainWindow!
                desktop.MainWindow = new MainWindow();
            }

            // Step 2: Complete framework initialization (styles are now fully loaded)
            base.OnFrameworkInitializationCompleted();

            // Step 3: Apply saved theme (must be AFTER styles load, but BEFORE wiring CustomThemeApplicator)
            RestoreSavedTheme();

            // Step 4: Now wire up custom theme applicator for Flowery.NET controls
            // This ensures subsequent theme changes via DaisyThemeDropdown, DaisyThemeController, etc.
            // use our in-place update method AND persist to settings
            DaisyThemeManager.CustomThemeApplicator = themeName => ApplyThemeInPlace(themeName);

            // Step 5: Sync dropdowns with the restored theme (won't save since theme hasn't changed)
            var currentTheme = AppSettings.Current.DaisyUiTheme;
            if (!string.IsNullOrEmpty(currentTheme))
            {
                DaisyThemeManager.SetCurrentTheme(currentTheme);
            }

            // Step 3b: Apply saved locale for Semi theme controls
            SettingsViewModel.ApplySavedLocale();

            // Note: RestoreSavedLanguage() is now called in Initialize() before MainWindow is created,
            // ensuring FloweryComponentSidebar gets the correct language at initialization

            // Step 3c: Apply saved editor font
            RestoreEditorFont();

            // Subscribe to editor font changes
            AppSettings.EditorFontChanged += OnEditorFontChanged;

            // Step 4: Signal MainWindow to reveal itself (it subscribes to this event)
            ThemeRestored?.Invoke();

            // Step 5: Initialization complete - now allow theme saves
            // (DaisyThemeDropdown controls in HomeView will have already been constructed by now)
            IsInitializing = false;

            // Step 6: Re-apply saved theme and sync dropdowns
            // The dropdown constructors overwrote the visual theme with "Dark" during construction,
            // so we need to re-apply the correct theme now.
            var savedTheme = AppSettings.Current.DaisyUiTheme;
            if (!string.IsNullOrEmpty(savedTheme))
            {
                ApplyThemeInPlace(savedTheme, saveToSettings: false);
                DaisyThemeManager.SetCurrentTheme(savedTheme);
            }
        }

        /// <summary>
        /// Restore the saved theme settings on app startup.
        /// Uses in-place resource updates for proper DynamicResource refreshing.
        /// </summary>
        private void RestoreSavedTheme()
        {
            try
            {
                var settings = AppSettings.Current;

                // Restore DaisyUI theme (or default to Dark)
                var daisyThemeName = settings.DaisyUiTheme;
                if (string.IsNullOrEmpty(daisyThemeName))
                    daisyThemeName = "Dark";

                // System.Diagnostics.Debug.WriteLine($"Restoring theme: {daisyThemeName}");
                ApplyThemeInPlace(daisyThemeName, saveToSettings: false);
            }
            catch
            {
                // System.Diagnostics.Debug.WriteLine($"Theme restore failed: {ex.Message}");
                // Fallback to default dark mode
                RequestedThemeVariant = ThemeVariant.Dark;
            }
        }

        /// <summary>
        /// Apply a theme by loading its palette and updating app.Resources in-place.
        /// This triggers DynamicResource bindings to refresh throughout the app.
        /// </summary>
        /// <param name="themeName">Theme name (e.g., "Dark", "Abyss", "Business")</param>
        /// <param name="saveToSettings">Whether to persist the theme to settings (default: true)</param>
        /// <returns>True if successful</returns>
        public static bool ApplyThemeInPlace(string themeName, bool saveToSettings = true)
        {
            if (string.IsNullOrEmpty(themeName))
                return false;

            var themeInfo = DaisyThemeManager.GetThemeInfo(themeName);
            if (themeInfo == null)
            {
                // System.Diagnostics.Debug.WriteLine($"Unknown theme: {themeName}");
                return false;
            }

            var app = Application.Current;
            if (app?.Resources == null) return false;

            try
            {
                // Load the palette file
                var paletteUri = new Uri($"avares://Flowery.NET/Themes/Palettes/Daisy{themeInfo.Name}.axaml");
                var palette = (Avalonia.Controls.ResourceDictionary)AvaloniaXamlLoader.Load(paletteUri);

                // Determine target theme variant
                var targetVariant = themeInfo.IsDark ? ThemeVariant.Dark : ThemeVariant.Light;

                // Apply palette resources to app.Resources
                // This updates DynamicResource bindings throughout the app
                foreach (var kvp in palette)
                {
                    app.Resources[kvp.Key] = kvp.Value;
                }

                // Set the theme variant to trigger Avalonia's built-in refresh
                app.RequestedThemeVariant = targetVariant;

                // Persist to settings only if:
                // 1. saveToSettings is true
                // 2. Not during initialization (dropdowns trigger ApplyTheme during construction)
                // 3. Theme actually changed
                if (saveToSettings && !IsInitializing && AppSettings.Current.DaisyUiTheme != themeName)
                {
                    AppSettings.Current.DaisyUiTheme = themeName;
                    AppSettings.Save();
                    // System.Diagnostics.Debug.WriteLine($"Theme saved: {themeName}");
                }

                // System.Diagnostics.Debug.WriteLine($"Applied theme: {themeName}");
                return true;
            }
            catch
            {
                // System.Diagnostics.Debug.WriteLine($"Failed to apply theme {themeName}: {ex.Message}");
                return false;
            }
        }

        private void DisableAvaloniaDataAnnotationValidation()
        {
            var bindingPluginsType = typeof(Application).Assembly.GetType("Avalonia.Data.Core.Plugins.BindingPlugins");
            var dataValidatorsProperty = bindingPluginsType?.GetProperty(
                "DataValidators",
                BindingFlags.Static | BindingFlags.Public);
            var dataValidators = dataValidatorsProperty?.GetValue(null) as System.Collections.IList;
            if (dataValidators == null)
                return;

            var dataValidationPluginsToRemove = dataValidators
                .Cast<object>()
                .Where(plugin => plugin.GetType().FullName == "Avalonia.Data.Core.Plugins.DataAnnotationsValidationPlugin")
                .ToArray();

            foreach (var plugin in dataValidationPluginsToRemove)
            {
                dataValidators.Remove(plugin);
            }
        }

        /// <summary>
        /// Restore the saved editor font on app startup.
        /// </summary>
        private void RestoreEditorFont()
        {
            var fontFamily = AppSettings.Current.EditorFontFamily;
            if (!string.IsNullOrEmpty(fontFamily))
            {
                ApplyEditorFont(fontFamily);
            }
        }

        /// <summary>
        /// Handler for editor font changes at runtime.
        /// </summary>
        private void OnEditorFontChanged(string fontFamily)
        {
            Dispatcher.UIThread.Post(() => ApplyEditorFont(fontFamily));
        }

        /// <summary>
        /// Applies the editor font as a dynamic resource.
        /// </summary>
        private void ApplyEditorFont(string fontFamily)
        {
            if (string.IsNullOrEmpty(fontFamily))
                return;

            try
            {
                // Update the EditorFont resource dynamically
                Resources["EditorFont"] = new FontFamily(fontFamily);
            }
            catch
            {
                // Ignore font application errors
            }
        }

        /// <summary>
        /// Restore the saved UI language on app startup.
        /// Falls back to system locale or English if not set.
        /// </summary>
        private static void RestoreSavedLanguage()
        {
            try
            {
                var savedLanguage = AppSettings.Current.Language;
                if (!string.IsNullOrEmpty(savedLanguage))
                {
                    // Saved language could be short code ("de") or full code ("de-DE")
                    // Use FloweryLocalization as the source of truth - ToolkitLocalization subscribes to its changes
                    FloweryLocalization.SetCulture(savedLanguage);
                }
                else
                {
                    // First start - use system locale if supported, otherwise English
                    var systemCulture = CultureInfo.CurrentUICulture;
                    var langCode = systemCulture.TwoLetterISOLanguageName;

                    // Check against the single source of truth for supported languages
                    var supportedCodes = ToolkitLocalization.SupportedLanguages.Select(l => l.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    
                    if (supportedCodes.Contains(langCode))
                    {
                        FloweryLocalization.SetCulture(langCode);
                    }
                    else if (langCode == "zh" && supportedCodes.Contains("zh-Hans"))
                    {
                        // Chinese locale - use Simplified Chinese
                        FloweryLocalization.SetCulture("zh-Hans");
                    }
                    // else: unsupported locale - FloweryLocalization defaults to English
                }
            }
            catch
            {
                // Ignore language restore errors - default to English
            }
        }
    }
}
