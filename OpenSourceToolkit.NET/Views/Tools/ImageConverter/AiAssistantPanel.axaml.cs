using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
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
            
            // Subscribe to collection changes to auto-scroll
            if (DataContext is AiAssistantViewModel vm)
            {
                vm.ChatMessages.CollectionChanged += OnChatMessagesChanged;
            }
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);
            
            // Unsubscribe from collection changes
            if (DataContext is AiAssistantViewModel vm)
            {
                vm.ChatMessages.CollectionChanged -= OnChatMessagesChanged;
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

        private void OnOpenSettingsClicked(object sender, RoutedEventArgs e)
        {
            if (TopLevel.GetTopLevel(this) is global::OpenSourceToolkit.NET.Views.MainWindow mainWindow)
                mainWindow.OpenSettings(global::OpenSourceToolkit.NET.Views.SettingsSection.AiProviders);
        }
    }
}
