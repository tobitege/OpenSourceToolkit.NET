using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using OpenSourceToolkit.NET.Localization;
using OpenSourceToolkit.NET.ViewModels.Tools.ImageConverter;
using OpenSourceToolkit.NET.ViewModels.Tools.ImageConverter.Models;

namespace OpenSourceToolkit.NET.Views.Tools.ImageConverter
{
    /// <summary>
    /// AI Assistant panel for the Image Converter tool.
    /// Expects DataContext to be an AiAssistantViewModel.
    /// </summary>
    public partial class AiAssistantPanel : UserControl
    {
        private ScrollViewer _chatScrollViewer;
        private AiAssistantViewModel _attachedViewModel;
        private bool _isLoaded;
        
        public AiAssistantPanel()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            _chatScrollViewer = this.FindControl<ScrollViewer>("ChatScrollViewer");
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            _isLoaded = true;
            AttachViewModel(DataContext as AiAssistantViewModel);
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            _isLoaded = false;
            AttachViewModel(null);
            base.OnUnloaded(e);
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            if (_isLoaded)
                AttachViewModel(DataContext as AiAssistantViewModel);
        }

        private void AttachViewModel(AiAssistantViewModel viewModel)
        {
            if (ReferenceEquals(_attachedViewModel, viewModel))
                return;

            if (_attachedViewModel != null)
            {
                _attachedViewModel.ChatMessages.CollectionChanged -= OnChatMessagesChanged;
                var confirmationAction = (Func<int, Task<bool>>)ConfirmRevertToMessage;
                if (_attachedViewModel.ConfirmRevertToMessageAction == confirmationAction)
                    _attachedViewModel.ConfirmRevertToMessageAction = null;
            }

            _attachedViewModel = viewModel;
            if (_attachedViewModel != null)
            {
                _attachedViewModel.ChatMessages.CollectionChanged += OnChatMessagesChanged;
                _attachedViewModel.ConfirmRevertToMessageAction = ConfirmRevertToMessage;
            }
        }

        private void OnChatMessagesChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Auto-scroll to bottom when new messages are added
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add ||
                e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    _chatScrollViewer?.ScrollToEnd();
                }, Avalonia.Threading.DispatcherPriority.Background);
            }
        }

        /// <summary>
        /// Handles double-tap on chat bubble to copy message content.
        /// </summary>
        private void OnMessageDoubleTapped(object sender, TappedEventArgs e)
        {
            CopyMessage(sender);
        }

        /// <summary>
        /// Handles the visible per-message copy action.
        /// </summary>
        private void OnCopyMessageClicked(object sender, RoutedEventArgs e)
        {
            CopyMessage(sender);
        }

        /// <summary>
        /// Handles the visible per-message delete action.
        /// </summary>
        private void OnDeleteMessageClicked(object sender, RoutedEventArgs e)
        {
            if (sender is Control control &&
                control.Tag is ChatMessageItem message &&
                DataContext is AiAssistantViewModel vm)
            {
                vm.DeleteMessageCommand.Execute(message);
            }
        }

        private void CopyMessage(object sender)
        {
            if (sender is Control control &&
                control.Tag is ChatMessageItem message &&
                DataContext is AiAssistantViewModel vm)
                vm.CopyMessageCommand.Execute(message);
        }

        private async Task<bool> ConfirmRevertToMessage(int followingMessageCount)
        {
            if (TopLevel.GetTopLevel(this) is not Window owner)
                return false;

            var bodyKey = followingMessageCount == 0
                ? "AiAssistant_RevertConfirmSingle"
                : "AiAssistant_RevertConfirmMultiple";
            var body = string.Format(
                ToolkitLocalization.CurrentCulture,
                ToolkitLocalization.GetString(bodyKey),
                followingMessageCount);
            var dialog = new Window
            {
                Title = ToolkitLocalization.GetString("AiAssistant_RevertConfirmTitle"),
                Width = 420,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                ShowInTaskbar = false
            };

            var result = false;
            var cancelButton = new Flowery.Controls.DaisyButton
            {
                Content = ToolkitLocalization.GetString("Button_Cancel"),
                MinWidth = 90,
                Variant = Flowery.Controls.DaisyButtonVariant.Ghost,
                Size = Flowery.Controls.DaisySize.Small
            };
            var revertButton = new Flowery.Controls.DaisyButton
            {
                Content = ToolkitLocalization.GetString("AiAssistant_RevertTo"),
                MinWidth = 90,
                Variant = Flowery.Controls.DaisyButtonVariant.Warning,
                Size = Flowery.Controls.DaisySize.Small
            };

            cancelButton.Click += (_, _) => dialog.Close();
            revertButton.Click += (_, _) =>
            {
                result = true;
                dialog.Close();
            };
            dialog.KeyDown += (_, args) =>
            {
                if (args.Key == Key.Escape)
                    dialog.Close();
            };

            dialog.Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        Text = body,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    },
                    new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        Spacing = 10,
                        Children = { cancelButton, revertButton }
                    }
                }
            };

            await dialog.ShowDialog(owner);
            return result;
        }

        private void OnOpenSettingsClicked(object sender, RoutedEventArgs e)
        {
            if (TopLevel.GetTopLevel(this) is global::OpenSourceToolkit.NET.Views.MainWindow mainWindow)
                mainWindow.OpenSettings(global::OpenSourceToolkit.NET.Views.SettingsSection.AiProviders);
        }
    }
}
