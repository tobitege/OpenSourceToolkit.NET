using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Flowery.Controls;
using OpenSourceToolkit.NET.Services;
using OpenSourceToolkit.NET.ViewModels;

namespace OpenSourceToolkit.NET.Views
{
    internal enum SettingsSection
    {
        General,
        AiConnections,
        AiProviders,
        About
    }

    public partial class SettingsWindow : Window
    {
        private SettingsViewModel _viewModel;

        public SettingsWindow() : this(SettingsSection.General)
        {
        }

        internal SettingsWindow(SettingsSection initialSection)
        {
            AvaloniaXamlLoader.Load(this);
            ConfigureConnectionModelPicker();
            SelectInitialSection(initialSection);
            _viewModel = new SettingsViewModel();
            _viewModel.PromptSaveChangesAction = PromptSaveChangesAsync;
            _viewModel.OpenAiBrowserAction = OpenBrowserAsync;
#if DEBUG
            _viewModel.ShowDebugExceptionAction = ShowDebugException;
#endif
            DataContext = _viewModel;
        }

        private async Task<bool> OpenBrowserAsync(Uri authorizationUri)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            return topLevel != null &&
                   await topLevel.Launcher.LaunchUriAsync(authorizationUri);
        }

        private void SelectInitialSection(SettingsSection section)
        {
            GetSettingsNavigationList().SelectedIndex = (int)section;
        }

        private void SettingsNavigationButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is DaisyButton button &&
                button.Tag is string sectionIndex &&
                int.TryParse(sectionIndex, out var selectedIndex))
            {
                GetSettingsNavigationList().SelectedIndex = selectedIndex;
            }
        }

        private ListBox GetSettingsNavigationList()
        {
            return this.FindControl<ListBox>("SettingsNavigationList")
                ?? throw new InvalidOperationException("Settings navigation was not loaded.");
        }

        private void ConfigureConnectionModelPicker()
        {
            GetConnectionModelPicker().ItemFilter = FilterModelOption;
        }

        private static bool FilterModelOption(string searchText, object item)
        {
            return item is AiModelOption model &&
                   (string.IsNullOrWhiteSpace(searchText) ||
                    model.ModelId.Contains(searchText.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        private AutoCompleteBox GetConnectionModelPicker()
        {
            return this.FindControl<AutoCompleteBox>("ConnectionModelPicker")
                ?? throw new InvalidOperationException("Connection model picker was not loaded.");
        }

        private void OpenConnectionModelList_Click(object sender, RoutedEventArgs e)
        {
            var modelPicker = GetConnectionModelPicker();
            modelPicker.Focus();
            modelPicker.IsDropDownOpen = true;
            e.Handled = true;
        }

        private void RemoveProviderModel_Click(object sender, RoutedEventArgs e)
        {
            if (sender is DaisyButton { Tag: AiModelOption model } &&
                DataContext is SettingsViewModel viewModel &&
                viewModel.RemoveModelCommand.CanExecute(model))
            {
                viewModel.RemoveModelCommand.Execute(model);
                e.Handled = true;
            }
        }

        private async Task<bool?> PromptSaveChangesAsync(string message)
        {
            var dialog = new Window
            {
                Title = "Unsaved Changes",
                Width = 350,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false
            };

            bool? result = null;
            var saveBtn = new DaisyButton
            {
                Content = "Save",
                Width = 80,
                Variant = DaisyButtonVariant.Primary
            };
            var discardBtn = new DaisyButton
            {
                Content = "Discard",
                Width = 80,
                Variant = DaisyButtonVariant.Warning
            };
            var cancelBtn = new DaisyButton
            {
                Content = "Cancel",
                Width = 80,
                Variant = DaisyButtonVariant.Default
            };

            saveBtn.Click += (s, e) => { result = true; dialog.Close(); };
            discardBtn.Click += (s, e) => { result = false; dialog.Close(); };
            cancelBtn.Click += (s, e) => { result = null; dialog.Close(); };

            dialog.Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 16,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        Spacing = 8,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        Children = { saveBtn, discardBtn, cancelBtn }
                    }
                }
            };

            await dialog.ShowDialog(this);
            return result;
        }

#if DEBUG
        private async void ShowDebugException(Exception ex)
        {
            var dialog = new Window
            {
                Title = "Debug: Exception Details",
                Width = 600,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new ScrollViewer
                {
                    Content = new TextBox
                    {
                        Text = ex.ToString(),
                        IsReadOnly = true,
                        AcceptsReturn = true,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    }
                }
            };
            await dialog.ShowDialog(this);
        }
#endif

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            AdjustHeightToScreen();
        }

        /// <summary>
        /// Shrinks window height if it exceeds the screen's working area (excluding taskbar).
        /// Only shrinks, never enlarges the window.
        /// </summary>
        private void AdjustHeightToScreen()
        {
            var screen = Screens.ScreenFromWindow(this);
            if (screen == null) return;

            // Get working area (screen minus taskbar)
            var workingArea = screen.WorkingArea;
            var scaling = screen.Scaling;

            // Convert to DIPs (device-independent pixels)
            var maxHeight = workingArea.Height / scaling;

            // Leave some margin (20px top + 20px bottom)
            var availableHeight = maxHeight - 40;

            // Only shrink if window is too tall (never enlarge)
            if (Height > availableHeight)
            {
                // Respect MinHeight constraint
                Height = Math.Max(availableHeight, MinHeight);

                // Re-center vertically within working area
                var workingAreaTop = workingArea.Y / scaling;
                Position = new PixelPoint(Position.X, (int)((workingAreaTop + 20) * scaling));
            }
        }

        private async void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null && !await _viewModel.CanCloseAsync())
                return;

            AppSettings.Save();
            Close();
        }

        protected override async void OnClosing(WindowClosingEventArgs e)
        {
            if (_viewModel != null && _viewModel.HasUnsavedConnectionChanges)
            {
                e.Cancel = true;
                if (await _viewModel.CanCloseAsync())
                {
                    AppSettings.Save();
                    Close();
                }
                return;
            }
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            _viewModel?.Dispose();
            base.OnClosed(e);
        }
    }
}
