using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using OpenSourceToolkit.NET.ViewModels.Tools;
using OpenSourceToolkit.NET.ViewModels.Tools.ImageConverter.Models;
using OpenSourceToolkit.Converters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OpenSourceToolkit.NET.Views.Tools
{
    public partial class ImageConverterToolView : ToolViewBase
    {
        private static readonly FilePickerFileType SupportedImagesFilter = new FilePickerFileType("Supported Images")
        {
            Patterns = ImageProcessor.SupportedInputPatterns,
            AppleUniformTypeIdentifiers = new[] { "public.image" },
            MimeTypes = new[] { "image/*" }
        };

        // Thumbnail strip drag-scroll state
        private bool _isDraggingThumbnails;
        private Point _dragStartPoint;
        private double _dragStartOffset;

        // Right panel width constraints
        private const double RightPanelMinWidth = 220;
        private const double RightPanelMaxWidth = 400;

        public ImageConverterToolView()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[ImageConverterToolView] Constructor starting...");
                Console.WriteLine("[ImageConverterToolView] Constructor starting...");
                InitializeComponent();
                SetupEventHandlers();
                System.Diagnostics.Debug.WriteLine("[ImageConverterToolView] Constructor completed successfully");
                Console.WriteLine("[ImageConverterToolView] Constructor completed successfully");
            }
            catch (Exception ex)
            {
                var msg = $"[ImageConverterToolView] ERROR in constructor: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}";
                if (ex.InnerException != null)
                    msg += $"\nInner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}";
                System.Diagnostics.Debug.WriteLine(msg);
                Console.WriteLine(msg);
                throw;
            }
        }

        private void SetupEventHandlers()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[ImageConverterToolView] SetupEventHandlers starting...");

                // Wire up fullscreen button in ZoomableImageControl (source-generated from x:Name)
                ZoomableImageView.FullscreenRequested += (s, ev) =>
                {
                    if (DataContext is ImageConverterToolViewModel vm)
                    {
                        OpenFullscreenViewer(vm);
                    }
                };

                // Wire up thumbnail strip drag-scroll and mouse wheel (source-generated from x:Name)
                ThumbnailScrollViewer.PointerPressed += OnThumbnailStripPointerPressed;
                ThumbnailScrollViewer.PointerMoved += OnThumbnailStripPointerMoved;
                ThumbnailScrollViewer.PointerReleased += OnThumbnailStripPointerReleased;
                ThumbnailScrollViewer.PointerCaptureLost += OnThumbnailStripPointerCaptureLost;
                ThumbnailScrollViewer.PointerWheelChanged += OnThumbnailStripPointerWheelChanged;

                // Enforce minimum width on right panel during GridSplitter resize (source-generated)
                RightPanelBorder.SizeChanged += OnRightPanelSizeChanged;

                // Track main content grid size for proportional sidebar width (source-generated)
                MainContentGrid.SizeChanged += OnMainContentGridSizeChanged;

                System.Diagnostics.Debug.WriteLine("[ImageConverterToolView] SetupEventHandlers completed");
            }
            catch (Exception ex)
            {
                var msg = $"[ImageConverterToolView] ERROR in SetupEventHandlers: {ex.GetType().Name}: {ex.Message}";
                if (ex.InnerException != null)
                    msg += $"\nInner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}";
                System.Diagnostics.Debug.WriteLine(msg);
                Console.WriteLine(msg);
                throw;
            }
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            if (DataContext is ImageConverterToolViewModel vm)
            {
                // Single image actions
                vm.OpenSingleImageAction = OpenSingleImage;
                vm.SaveWorkspaceImageAction = SaveWorkspaceImage;
                vm.LoadWatermarkImageAction = LoadWatermarkImage;
                vm.CopyImageToClipboardAction = CopyImageToClipboard;
                vm.CopyToClipboardAction = CopyTextToClipboardAsync;

                // Batch actions
                vm.SelectFilesAction = SelectFiles;
                vm.SelectOutputFolderAction = SelectOutputFolder;
                vm.ShowErrorAction = ShowError;

                // Medium Effort actions
                vm.SaveGifAction = SaveGif;
                vm.SavePdfAction = SavePdf;
                vm.OpenPdfAction = OpenPdf;

                // Thumbnail strip actions
                vm.SaveFullImageAction = SaveFullImage;
                vm.ConfirmDeleteThumbnailAction = ConfirmDeleteThumbnail;
                vm.ConfirmDestructiveActionAsync = ConfirmDestructiveAction;

                // Unsaved changes prompt
                vm.PromptSaveChangesAction = PromptSaveChanges;

                // Session rename dialog
                vm.ShowRenameSessionDialogAction = ShowRenameSessionDialog;

                // Fullscreen viewer action
                vm.OpenFullscreenViewerAction = () => OpenFullscreenViewer(vm);

                // Initialize session management (loads last session or creates new)
                _ = vm.InitializeSessionAsync();
            }
        }

        private void OnOpenAiSettingsClicked(object sender, RoutedEventArgs e)
        {
            if (sender is Control button &&
                TopLevel.GetTopLevel(button) is global::OpenSourceToolkit.NET.Views.MainWindow mainWindow)
            {
                mainWindow.OpenSettings(global::OpenSourceToolkit.NET.Views.SettingsSection.AiConnections);
            }
        }

        private async Task<bool> ConfirmDeleteThumbnail(string label)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return true;

            var dialog = new Avalonia.Controls.Window
            {
                Title = "Delete Image",
                Width = 350,
                Height = 120,
                WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
                CanResize = false,
                ShowInTaskbar = false
            };

            bool result = false;
            var yesButton = new Flowery.Controls.DaisyButton
            {
                Content = "Delete",
                MinWidth = 80,
                Variant = Flowery.Controls.DaisyButtonVariant.Error,
                Size = Flowery.Controls.DaisySize.Small
            };
            var noButton = new Flowery.Controls.DaisyButton
            {
                Content = "Cancel",
                MinWidth = 80,
                Variant = Flowery.Controls.DaisyButtonVariant.Ghost,
                Size = Flowery.Controls.DaisySize.Small
            };

            yesButton.Click += (s, e) => { result = true; dialog.Close(); };
            noButton.Click += (s, e) => { result = false; dialog.Close(); };
            dialog.KeyDown += (s, e) =>
            {
                if (e.Key == Avalonia.Input.Key.Escape)
                {
                    result = false;
                    dialog.Close();
                }
            };

            dialog.Content = new Avalonia.Controls.StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 15,
                Children =
                {
                    new Avalonia.Controls.TextBlock
                    {
                        Text = $"Delete \"{label}\" from the thumbnail strip?",
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    },
                    new Avalonia.Controls.StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        Spacing = 10,
                        Children = { noButton, yesButton }
                    }
                }
            };

            await dialog.ShowDialog((Avalonia.Controls.Window)topLevel);
            return result;
        }

        /// <summary>
        /// Confirms a destructive action when there are unsaved images.
        /// Shows list of unsaved image names and requires checkbox confirmation.
        /// Returns true if user confirms, false if cancelled.
        /// </summary>
        private async Task<bool> ConfirmDestructiveAction(List<string> unsavedImageNames, string actionName)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return false;

            var dialog = new Avalonia.Controls.Window
            {
                Title = $"⚠ {actionName} - Unsaved Images",
                Width = 480,
                SizeToContent = Avalonia.Controls.SizeToContent.Height,
                MaxHeight = 500,
                WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
                CanResize = false,
                ShowInTaskbar = false
            };

            bool result = false;
            var confirmCheckBox = new Avalonia.Controls.CheckBox
            {
                Content = "I understand these images will be permanently lost",
                FontWeight = Avalonia.Media.FontWeight.SemiBold,
                Foreground = Avalonia.Media.Brushes.OrangeRed
            };
            var proceedButton = new Avalonia.Controls.Button
            {
                Content = actionName,
                MinWidth = 130,
                Padding = new Avalonia.Thickness(12, 6),
                IsEnabled = false,
                Background = Avalonia.Media.Brushes.DarkRed,
                Foreground = Avalonia.Media.Brushes.White
            };
            var cancelButton = new Avalonia.Controls.Button { Content = "Cancel", Width = 80 };

            // Enable proceed button only when checkbox is checked
            confirmCheckBox.IsCheckedChanged += (s, e) =>
            {
                proceedButton.IsEnabled = confirmCheckBox.IsChecked == true;
            };

            proceedButton.Click += (s, e) => { result = true; dialog.Close(); };
            cancelButton.Click += (s, e) => { result = false; dialog.Close(); };

            // Build the list of unsaved image names
            var imageListText = string.Join("\n", unsavedImageNames.Select(n => $"  • {n}"));
            if (unsavedImageNames.Count > 10)
            {
                var shown = unsavedImageNames.Take(10).Select(n => $"  • {n}");
                imageListText = string.Join("\n", shown) + $"\n  ... and {unsavedImageNames.Count - 10} more";
            }

            var scrollViewer = new Avalonia.Controls.ScrollViewer
            {
                MaxHeight = 150,
                Content = new Avalonia.Controls.TextBlock
                {
                    Text = imageListText,
                    FontFamily = new Avalonia.Media.FontFamily("Consolas, Courier New, monospace"),
                    FontSize = 11,
                    Foreground = Avalonia.Media.Brushes.OrangeRed
                }
            };

            dialog.Content = new Avalonia.Controls.StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 12,
                Children =
                {
                    new Avalonia.Controls.TextBlock
                    {
                        Text = $"The following {unsavedImageNames.Count} image(s) have NOT been saved outside the session:",
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        FontWeight = Avalonia.Media.FontWeight.SemiBold
                    },
                    new Avalonia.Controls.Border
                    {
                        BorderBrush = Avalonia.Media.Brushes.OrangeRed,
                        BorderThickness = new Avalonia.Thickness(1),
                        CornerRadius = new Avalonia.CornerRadius(4),
                        Padding = new Avalonia.Thickness(10),
                        Child = scrollViewer
                    },
                    new Avalonia.Controls.TextBlock
                    {
                        Text = "If you proceed, these images will be permanently deleted and cannot be recovered.",
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        Opacity = 0.8
                    },
                    confirmCheckBox,
                    new Avalonia.Controls.StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        Spacing = 10,
                        Margin = new Avalonia.Thickness(0, 8, 0, 0),
                        Children = { cancelButton, proceedButton }
                    }
                }
            };

            await dialog.ShowDialog((Avalonia.Controls.Window)topLevel);
            return result;
        }

        /// <summary>
        /// Prompts user to save unsaved changes.
        /// Returns: true = Save, false = Discard, null = Cancel
        /// </summary>
        private async Task<bool?> PromptSaveChanges(string imageName)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return false; // Can't show dialog, discard

            var dialog = new Avalonia.Controls.Window
            {
                Title = "Unsaved Changes",
                Width = 400,
                Height = 140,
                WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
                CanResize = false,
                ShowInTaskbar = false
            };

            bool? result = null;
            var saveButton = new Flowery.Controls.DaisyButton
            {
                Content = "Save",
                MinWidth = 80,
                Variant = Flowery.Controls.DaisyButtonVariant.Primary,
                Size = Flowery.Controls.DaisySize.Small
            };
            var discardButton = new Flowery.Controls.DaisyButton
            {
                Content = "Discard",
                MinWidth = 80,
                Variant = Flowery.Controls.DaisyButtonVariant.Warning,
                Size = Flowery.Controls.DaisySize.Small
            };
            var cancelButton = new Flowery.Controls.DaisyButton
            {
                Content = "Cancel",
                MinWidth = 80,
                Variant = Flowery.Controls.DaisyButtonVariant.Ghost,
                Size = Flowery.Controls.DaisySize.Small
            };

            saveButton.Click += (s, e) => { result = true; dialog.Close(); };
            discardButton.Click += (s, e) => { result = false; dialog.Close(); };
            cancelButton.Click += (s, e) => { result = null; dialog.Close(); };
            dialog.KeyDown += (s, e) =>
            {
                if (e.Key == Avalonia.Input.Key.Escape)
                {
                    result = null;
                    dialog.Close();
                }
            };

            dialog.Content = new Avalonia.Controls.StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 15,
                Children =
                {
                    new Avalonia.Controls.TextBlock
                    {
                        Text = $"You have unsaved changes to \"{imageName}\".\nDo you want to save before continuing?",
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    },
                    new Avalonia.Controls.StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        Spacing = 10,
                        Children = { cancelButton, discardButton, saveButton }
                    }
                }
            };

            await dialog.ShowDialog((Avalonia.Controls.Window)topLevel);
            return result;
        }

        /// <summary>
        /// Shows rename session dialog.
        /// Returns new name or null if cancelled.
        /// </summary>
        private async Task<string> ShowRenameSessionDialog(string currentName)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return null;

            var dialog = new Avalonia.Controls.Window
            {
                Title = "Rename Session",
                Width = 440,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
                CanResize = false,
                ShowInTaskbar = false
            };

            string result = null;
            var nameTextBox = new Avalonia.Controls.TextBox
            {
                Text = currentName,
                MaxLength = ImageConverterToolViewModel.MaxSessionNameLength,
                PlaceholderText = "Enter session name...",
                Height = 32,
                FontSize = 14,
                VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            var charCountText = new Avalonia.Controls.TextBlock
            {
                Text = $"{currentName?.Length ?? 0}/{ImageConverterToolViewModel.MaxSessionNameLength}",
                FontSize = 12,
                Opacity = 0.6,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                Margin = new Avalonia.Thickness(0, 4, 0, 0)
            };
            var errorText = new Avalonia.Controls.TextBlock
            {
                Foreground = Avalonia.Media.Brushes.Red,
                FontSize = 12,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Margin = new Avalonia.Thickness(0, 4, 0, 0)
            };

            var renameButton = new Avalonia.Controls.Button
            {
                Content = "Rename",
                MinWidth = 90,
                Padding = new Avalonia.Thickness(10, 6),
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                IsEnabled = true
            };
            var cancelButton = new Avalonia.Controls.Button
            {
                Content = "Cancel",
                MinWidth = 90,
                Padding = new Avalonia.Thickness(10, 6),
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center
            };

            // Update char count and validate on text change
            nameTextBox.TextChanged += (s, e) =>
            {
                var text = nameTextBox.Text ?? "";
                charCountText.Text = $"{text.Length}/{ImageConverterToolViewModel.MaxSessionNameLength}";
                var validationError = ImageConverterToolViewModel.ValidateSessionName(text);
                errorText.Text = validationError ?? "";
                renameButton.IsEnabled = validationError == null;
            };

            renameButton.Click += (s, e) =>
            {
                var text = nameTextBox.Text?.Trim();
                var validationError = ImageConverterToolViewModel.ValidateSessionName(text);
                if (validationError == null)
                {
                    result = text;
                    dialog.Close();
                }
                else
                {
                    errorText.Text = validationError;
                }
            };
            cancelButton.Click += (s, e) => { result = null; dialog.Close(); };

            dialog.Content = new Avalonia.Controls.Border
            {
                Padding = new Avalonia.Thickness(24),
                Child = new Avalonia.Controls.StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        new Avalonia.Controls.TextBlock
                        {
                            Text = "Session Name",
                            FontWeight = Avalonia.Media.FontWeight.SemiBold,
                            FontSize = 14
                        },
                        nameTextBox,
                        charCountText,
                        errorText,
                        new Avalonia.Controls.StackPanel
                        {
                            Orientation = Avalonia.Layout.Orientation.Horizontal,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                            Spacing = 12,
                            Margin = new Avalonia.Thickness(0, 12, 0, 0),
                            Children = { cancelButton, renameButton }
                        }
                    }
                }
            };

            // Select all text when dialog opens
            dialog.Opened += (s, e) =>
            {
                nameTextBox.Focus();
                nameTextBox.SelectAll();
            };

            await dialog.ShowDialog((Avalonia.Controls.Window)topLevel);
            return result;
        }

        private void OnThumbnailDoubleTapped(object sender, Avalonia.Input.TappedEventArgs e)
        {
            if (sender is Border border && border.Tag is ThumbnailItem item)
            {
                var vm = DataContext as ImageConverterToolViewModel;
                if (vm?.Thumbnails.LoadThumbnailToWorkspaceCommand?.CanExecute(item) == true)
                {
                    vm.Thumbnails.LoadThumbnailToWorkspaceCommand.Execute(item);
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Thumbnail Strip Drag-Scroll and Mouse Wheel Handlers
        // ═══════════════════════════════════════════════════════════════════════════

        private void OnThumbnailStripPointerPressed(object sender, PointerPressedEventArgs e)
        {
            // Only handle middle mouse button or left button with no other modifiers for drag-scroll
            var props = e.GetCurrentPoint(ThumbnailScrollViewer).Properties;
            if (props.IsMiddleButtonPressed || (props.IsLeftButtonPressed && e.KeyModifiers == KeyModifiers.None))
            {
                // Check if we're clicking on an interactive element (button, checkbox, or thumbnail image area)
                // If so, don't start drag - let the element handle the click/double-click
                var source = e.Source as Visual;
                if (e.Source is Button || e.Source is CheckBox || e.Source is Image ||
                    source?.FindAncestorOfType<Button>() != null ||
                    source?.FindAncestorOfType<CheckBox>() != null)
                {
                    return;
                }

                // Also check if clicking on a Border that has a Tag (thumbnail click area)
                var border = e.Source as Border ?? source?.FindAncestorOfType<Border>();
                if (border?.Tag is ThumbnailItem)
                {
                    return; // Let double-tap handle this
                }

                _isDraggingThumbnails = true;
                _dragStartPoint = e.GetPosition(ThumbnailScrollViewer);
                _dragStartOffset = ThumbnailScrollViewer.Offset.X;
                e.Pointer.Capture(ThumbnailScrollViewer);
                ThumbnailScrollViewer.Cursor = new Cursor(StandardCursorType.SizeWestEast);
            }
        }

        private void OnThumbnailStripPointerMoved(object sender, PointerEventArgs e)
        {
            if (!_isDraggingThumbnails) return;

            var currentPoint = e.GetPosition(ThumbnailScrollViewer);
            var deltaX = _dragStartPoint.X - currentPoint.X;
            var newOffset = _dragStartOffset + deltaX;

            // Clamp to valid scroll range
            var maxOffset = Math.Max(0, ThumbnailScrollViewer.Extent.Width - ThumbnailScrollViewer.Viewport.Width);
            newOffset = Math.Max(0, Math.Min(maxOffset, newOffset));

            ThumbnailScrollViewer.Offset = new Vector(newOffset, ThumbnailScrollViewer.Offset.Y);
        }

        private void OnThumbnailStripPointerReleased(object sender, PointerReleasedEventArgs e)
        {
            EndThumbnailDrag(e.Pointer);
        }

        private void OnThumbnailStripPointerCaptureLost(object sender, PointerCaptureLostEventArgs e)
        {
            EndThumbnailDrag(null);
        }

        private void EndThumbnailDrag(IPointer pointer)
        {
            if (!_isDraggingThumbnails) return;

            _isDraggingThumbnails = false;
            pointer?.Capture(null);
            ThumbnailScrollViewer.Cursor = Cursor.Default;
        }

        private void OnThumbnailStripPointerWheelChanged(object sender, PointerWheelEventArgs e)
        {
            // Convert vertical wheel scroll to horizontal scroll for the thumbnail strip
            var scrollAmount = e.Delta.Y * 60; // Adjust multiplier for scroll speed
            var newOffset = ThumbnailScrollViewer.Offset.X + scrollAmount;

            // Clamp to valid scroll range
            var maxOffset = Math.Max(0, ThumbnailScrollViewer.Extent.Width - ThumbnailScrollViewer.Viewport.Width);
            newOffset = Math.Max(0, Math.Min(maxOffset, newOffset));

            ThumbnailScrollViewer.Offset = new Vector(newOffset, ThumbnailScrollViewer.Offset.Y);
            e.Handled = true; // Prevent event from bubbling up
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Right Panel Width Constraint Enforcement
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Enforces min/max width on the right panel when GridSplitter resizes it.
        /// </summary>
        private void OnRightPanelSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!RightPanelBorder.IsVisible) return;
            if (e.NewSize.Width <= 0) return;

            // Clamp to minimum width
            if (e.NewSize.Width < RightPanelMinWidth)
            {
                RightPanelBorder.Width = RightPanelMinWidth;
            }
            // Clamp to maximum width
            else if (e.NewSize.Width > RightPanelMaxWidth)
            {
                RightPanelBorder.Width = RightPanelMaxWidth;
            }
        }

        /// <summary>
        /// Updates the ViewModel with available width for proportional sidebar sizing.
        /// Called when the main content grid resizes (window resize, etc.).
        /// </summary>
        private void OnMainContentGridSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width <= 0) return;

            if (DataContext is ImageConverterToolViewModel vm)
            {
                // Available width = total grid width minus left toolbar (approx 50px) and splitter (8px)
                var availableWidth = e.NewSize.Width - 58;
                vm.UpdateAvailableWidth(availableWidth);
            }
        }

        private async void OpenSingleImage()
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open Image",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    SupportedImagesFilter,
                    FilePickerFileTypes.ImageAll,
                    FilePickerFileTypes.All
                },
                SuggestedStartLocation = await GetStartFolderAsync()
            });

            if (files != null && files.Count > 0)
            {
                var localPath = files[0].TryGetLocalPath();
                if (!string.IsNullOrEmpty(localPath))
                {
                    SaveLastFolder(localPath);
                    (DataContext as ImageConverterToolViewModel)?.SetWorkspaceImage(localPath);
                }
            }
        }

        private async Task<string> SaveWorkspaceImage()
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return null;

            var vm = DataContext as ImageConverterToolViewModel;
            if (vm == null) return null;

            var selectedFormat = vm.Workspace.OutputFormat?.ToLower() ?? "png";
            var defaultName = Path.GetFileNameWithoutExtension(vm.Workspace.WorkspaceFileName) + "_converted." + selectedFormat;

            var fileTypes = BuildImageFileTypeChoices(selectedFormat);

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Converted Image",
                SuggestedFileName = defaultName,
                FileTypeChoices = fileTypes,
                SuggestedStartLocation = await GetStartFolderAsync()
            });

            if (file != null)
            {
                var path = file.TryGetLocalPath();
                if (!string.IsNullOrEmpty(path))
                {
                    SaveLastFolderDirect(Path.GetDirectoryName(path));
                    return path;
                }
            }
            return null;
        }

        private async void SelectFiles()
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Images",
                AllowMultiple = true,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    SupportedImagesFilter,
                    FilePickerFileTypes.ImageAll,
                    FilePickerFileTypes.All
                },
                SuggestedStartLocation = await GetStartFolderAsync()
            });

            if (files != null && files.Count > 0)
            {
                SaveLastFolder(files[0].TryGetLocalPath());
                var paths = new List<string>();
                foreach (var f in files)
                {
                    if (f.TryGetLocalPath() is string localPath)
                    {
                        paths.Add(localPath);
                    }
                }
                (DataContext as ImageConverterToolViewModel)?.AddFiles(paths.ToArray());
            }
        }

        private async Task<string> SelectOutputFolder()
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return null;

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Output Folder",
                AllowMultiple = false,
                SuggestedStartLocation = await GetStartFolderAsync()
            });

            if (folders != null && folders.Count > 0)
            {
                var path = folders[0].TryGetLocalPath();
                SaveLastFolderDirect(path);
                return path;
            }
            return null;
        }

        private void ShowError(string message)
        {
            Console.WriteLine($"Error: {message}");
        }

        private async Task CopyTextToClipboardAsync(string text)
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard == null) return;

            await clipboard.SetTextAsync(text);
            await clipboard.FlushAsync();
        }

        /// <summary>
        /// Copies image bytes to clipboard. Uses System.Windows.Forms.Clipboard which is Windows-only.
        /// Avalonia's IClipboard doesn't support image copy in v11 for .NET Framework 4.7.2.
        /// </summary>
        private void CopyImageToClipboard(byte[] imageBytes)
        {
            if (imageBytes == null || imageBytes.Length == 0) return;

            try
            {
                using (var stream = new MemoryStream(imageBytes))
                using (var image = System.Drawing.Image.FromStream(stream))
                {
                    System.Windows.Forms.Clipboard.SetImage(image);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to copy image to clipboard: {ex.Message}");
            }
        }

        private async void LoadWatermarkImage()
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var customFilter = new FilePickerFileType("Images")
            {
                Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.gif", "*.webp" },
                AppleUniformTypeIdentifiers = new[] { "public.image" },
                MimeTypes = new[] { "image/*" }
            };

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Watermark Image",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    customFilter,
                    FilePickerFileTypes.ImageAll
                },
                SuggestedStartLocation = await GetStartFolderAsync()
            });

            if (files != null && files.Count > 0)
            {
                var localPath = files[0].TryGetLocalPath();
                if (!string.IsNullOrEmpty(localPath))
                {
                    SaveLastFolder(localPath);
                    var bytes = File.ReadAllBytes(localPath);
                    (DataContext as ImageConverterToolViewModel)?.SetWatermarkImage(bytes);
                }
            }
        }

        private async Task<string> SaveGif()
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return null;

            var fileTypes = new List<FilePickerFileType>
            {
                new FilePickerFileType("GIF") { Patterns = new[] { "*.gif" } }
            };

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Animated GIF",
                SuggestedFileName = "animation.gif",
                FileTypeChoices = fileTypes,
                SuggestedStartLocation = await GetStartFolderAsync()
            });

            if (file != null)
            {
                var path = file.TryGetLocalPath();
                if (!string.IsNullOrEmpty(path))
                {
                    SaveLastFolderDirect(Path.GetDirectoryName(path));
                    return path;
                }
            }
            return null;
        }

        private async Task<string> SavePdf()
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return null;

            var fileTypes = new List<FilePickerFileType>
            {
                new FilePickerFileType("PDF") { Patterns = new[] { "*.pdf" } }
            };

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save PDF",
                SuggestedFileName = "images.pdf",
                FileTypeChoices = fileTypes,
                SuggestedStartLocation = await GetStartFolderAsync()
            });

            if (file != null)
            {
                var path = file.TryGetLocalPath();
                if (!string.IsNullOrEmpty(path))
                {
                    SaveLastFolderDirect(Path.GetDirectoryName(path));
                    return path;
                }
            }
            return null;
        }

        private async void OpenPdf()
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var pdfFilter = new FilePickerFileType("PDF Files")
            {
                Patterns = new[] { "*.pdf" },
                MimeTypes = new[] { "application/pdf" }
            };

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select PDF to Extract",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType> { pdfFilter },
                SuggestedStartLocation = await GetStartFolderAsync()
            });

            if (files != null && files.Count > 0)
            {
                var localPath = files[0].TryGetLocalPath();
                if (!string.IsNullOrEmpty(localPath))
                {
                    SaveLastFolder(localPath);
                    var vm = DataContext as ImageConverterToolViewModel;
                    if (vm != null)
                    {
                        await vm.ExtractPdfPages(localPath);
                    }
                }
            }
        }

        private async Task<string> SaveFullImage(string suggestedName)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return null;

            var fileTypes = new List<FilePickerFileType>
            {
                new FilePickerFileType("PNG Image") { Patterns = new[] { "*.png" } },
                new FilePickerFileType("JPEG Image") { Patterns = new[] { "*.jpg", "*.jpeg" } },
                new FilePickerFileType("WebP Image") { Patterns = new[] { "*.webp" } }
            };

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Image",
                SuggestedFileName = (suggestedName ?? "image") + ".png",
                FileTypeChoices = fileTypes,
                SuggestedStartLocation = await GetStartFolderAsync()
            });

            if (file != null)
            {
                var path = file.TryGetLocalPath();
                if (!string.IsNullOrEmpty(path))
                {
                    SaveLastFolderDirect(Path.GetDirectoryName(path));
                    return path;
                }
            }
            return null;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Helper Methods
        // ═══════════════════════════════════════════════════════════════════════════

        private static List<FilePickerFileType> BuildImageFileTypeChoices(string selectedFormat = null)
        {
            var fileTypes = new List<FilePickerFileType>();

            // Add selected format first if specified
            if (!string.IsNullOrEmpty(selectedFormat))
            {
                var patterns = ImageProcessor.FormatPatterns.TryGetValue(selectedFormat, out var p) ? p : new[] { "*." + selectedFormat };
                fileTypes.Add(new FilePickerFileType(selectedFormat.ToUpper()) { Patterns = patterns });
            }

            // Add remaining formats
            foreach (var fmt in ImageProcessor.SupportedFormats)
            {
                if (fmt != selectedFormat)
                {
                    var patterns = ImageProcessor.FormatPatterns.TryGetValue(fmt, out var p) ? p : new[] { "*." + fmt };
                    fileTypes.Add(new FilePickerFileType(fmt.ToUpper()) { Patterns = patterns });
                }
            }

            return fileTypes;
        }

        /// <summary>
        /// Opens the fullscreen image viewer with the current workspace image.
        /// </summary>
        private void OpenFullscreenViewer(ImageConverterToolViewModel vm)
        {
            if (vm.Workspace.WorkspaceFile?.RawBytes == null) return;

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var viewer = new ImageFullscreenViewer();
            viewer.SetImageFromBytes(vm.Workspace.WorkspaceFile.RawBytes);

            // Show as dialog (blocks interaction with main window)
            if (topLevel is Window parentWindow)
            {
                viewer.ShowDialog(parentWindow);
            }
            else
            {
                viewer.Show();
            }
        }
    }
}
