using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Flowery.Controls;
using Flowery.Controls.ColorPicker;
using Avalonia.Media;
using OpenSourceToolkit.NET.ViewModels.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace OpenSourceToolkit.NET.Views.Tools
{
    public partial class PdfToolView : ToolViewBase
    {
        public PdfToolView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            if (DataContext is PdfToolViewModel vm)
            {
                vm.ShowNotificationAction = ShowToast;
                vm.PromptPresetNameAction = PromptPresetName;
            }
        }

        private void ShowToast(string message, bool isError)
        {
            var toast = this.FindControl<DaisyToast>("ToastContainer");
            if (toast == null) return;

            var alert = new DaisyAlert
            {
                Content = message,
                Variant = isError ? DaisyAlertVariant.Error : DaisyAlertVariant.Success,
                Margin = new Thickness(0, 4)
            };

            toast.Items.Add(alert);

            // Auto-remove after 4 seconds
            DispatcherTimer.RunOnce(() =>
            {
                toast.Items.Remove(alert);
            }, TimeSpan.FromSeconds(4));
        }

        // --- Preset Handlers ---

        private async void SavePreset_Click(object sender, RoutedEventArgs e)
        {
            // Show dialog to get preset name
            var name = await ShowPresetNameDialog("");
            if (!string.IsNullOrWhiteSpace(name) && DataContext is PdfToolViewModel vm)
            {
                vm.SavePresetWithName(name);
            }
        }

        private string PromptPresetName(string defaultName)
        {
            // This is a fallback if command is triggered without click
            return defaultName;
        }

        private async Task<string> ShowPresetNameDialog(string currentName)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return null;

            var dialog = new Window
            {
                Title = "Save Watermark Preset",
                Width = 400,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                ShowInTaskbar = false
            };

            string result = null;
            var nameTextBox = new DaisyInput
            {
                Text = currentName,
                PlaceholderText = "Enter preset name...",
                Margin = new Thickness(0, 8, 0, 0)
            };

            var cancelButton = new DaisyButton
            {
                Content = "Cancel",
                Variant = DaisyButtonVariant.Ghost,
                Margin = new Thickness(0, 0, 8, 0)
            };

            var saveButton = new DaisyButton
            {
                Content = "Save",
                Variant = DaisyButtonVariant.Primary
            };

            cancelButton.Click += (s, ev) => dialog.Close();
            saveButton.Click += (s, ev) =>
            {
                result = nameTextBox.Text?.Trim();
                dialog.Close();
            };

            // Allow Enter to save
            nameTextBox.KeyDown += (s, ev) =>
            {
                if (ev.Key == Avalonia.Input.Key.Enter)
                {
                    result = nameTextBox.Text?.Trim();
                    dialog.Close();
                }
                else if (ev.Key == Avalonia.Input.Key.Escape)
                {
                    dialog.Close();
                }
            };

            dialog.Content = new StackPanel
            {
                Margin = new Thickness(20),
                Children =
                {
                    new TextBlock
                    {
                        Text = "Enter a name for this watermark preset:",
                        FontWeight = Avalonia.Media.FontWeight.Bold
                    },
                    nameTextBox,
                    new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        Spacing = 8,
                        Margin = new Thickness(0, 16, 0, 0),
                        Children = { cancelButton, saveButton }
                    }
                }
            };

            dialog.Opened += (s, ev) =>
            {
                nameTextBox.Focus();
                // Select all text if there's existing text
                if (!string.IsNullOrEmpty(nameTextBox.Text))
                {
                    // DaisyInput might not have SelectAll, just focus
                }
            };

            await dialog.ShowDialog((Window)topLevel);
            return result;
        }

        // --- Merge Handlers ---

        private async void MergeAddFiles_Click(object sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select PDFs to Merge",
                AllowMultiple = true,
                FileTypeFilter = new List<FilePickerFileType> { FilePickerFileTypes.Pdf },
                SuggestedStartLocation = await GetStartFolderAsync()
            });

            if (files.Count > 0 && DataContext is PdfToolViewModel vm)
            {
                SaveLastFolder(files[0].Path.LocalPath);
                foreach (var file in files)
                {
                    vm.MergeInputFiles.Add(file.Path.LocalPath);
                }
            }
        }

        private async void MergeSaveAs_Click(object sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Merged PDF As...",
                DefaultExtension = "pdf",
                FileTypeChoices = new List<FilePickerFileType> { FilePickerFileTypes.Pdf },
                SuggestedStartLocation = await GetStartFolderAsync()
            });

            if (file != null && DataContext is PdfToolViewModel vm)
            {
                SaveLastFolder(file.Path.LocalPath);
                vm.MergeOutputFile = file.Path.LocalPath;
            }
        }

        // --- Split Handlers ---

        private async void SplitBrowseInput_Click(object sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select PDF to Split",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType> { FilePickerFileTypes.Pdf },
                SuggestedStartLocation = await GetStartFolderAsync()
            });

            if (files.Count > 0 && DataContext is PdfToolViewModel vm)
            {
                SaveLastFolder(files[0].Path.LocalPath);
                vm.SplitInputFile = files[0].Path.LocalPath;
            }
        }

        private async void SplitBrowseOutput_Click(object sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Output Folder for Split Pages",
                AllowMultiple = false,
                SuggestedStartLocation = await GetStartFolderAsync()
            });

            if (folders.Count > 0 && DataContext is PdfToolViewModel vm)
            {
                SaveLastFolderDirect(folders[0].Path.LocalPath);
                vm.SplitOutputDir = folders[0].Path.LocalPath;
            }
        }

         // --- Watermark Handlers ---

         private async void WatermarkBrowseInput_Click(object sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select PDF to Watermark",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType> { FilePickerFileTypes.Pdf },
                SuggestedStartLocation = await GetStartFolderAsync()
            });

            if (files.Count > 0 && DataContext is PdfToolViewModel vm)
            {
                SaveLastFolder(files[0].Path.LocalPath);
                vm.WatermarkInputFile = files[0].Path.LocalPath;
            }
        }

        private async void WatermarkSaveAs_Click(object sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Watermarked PDF As...",
                DefaultExtension = "pdf",
                FileTypeChoices = new List<FilePickerFileType> { FilePickerFileTypes.Pdf },
                SuggestedStartLocation = await GetStartFolderAsync()
            });

            if (file != null && DataContext is PdfToolViewModel vm)
            {
                SaveLastFolder(file.Path.LocalPath);
                vm.WatermarkOutputFile = file.Path.LocalPath;
            }
        }

        private void OpenOutputPdf_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not PdfToolViewModel vm) return;

            var filePath = vm.WatermarkOutputFile;
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                ShowToast("Output file not found. Please add a watermark first.", true);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ShowToast($"Failed to open PDF: {ex.Message}", true);
            }
        }

        private async void OpenColorPicker_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not PdfToolViewModel vm) return;

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is not Window window) return;

            // Convert decimal to Color
            var intValue = (uint)Math.Max(0, Math.Min(16777215, vm.WatermarkColor));
            var initialColor = Color.FromRgb(
                (byte)((intValue >> 16) & 0xFF),
                (byte)((intValue >> 8) & 0xFF),
                (byte)(intValue & 0xFF));

            var result = await DaisyColorPickerDialog.ShowDialogAsync(window, initialColor, showAlphaChannel: false);
            if (result.HasValue)
            {
                // Convert Color to decimal
                vm.WatermarkColor = (result.Value.R << 16) | (result.Value.G << 8) | result.Value.B;
            }
        }
    }
}
