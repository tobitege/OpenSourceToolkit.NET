using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using OpenSourceToolkit.NET.ViewModels.Tools;

namespace OpenSourceToolkit.NET.Views.Tools
{
    public partial class AsciiArtToolView : ToolViewBase
    {
        public AsciiArtToolView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private async void Browse_Click(object sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Image",
                AllowMultiple = false,
                FileTypeFilter = new[] { FilePickerFileTypes.ImageAll },
                SuggestedStartLocation = await GetStartFolderAsync()
            });

            if (files.Count >= 1 && DataContext is AsciiArtToolViewModel vm)
            {
                SaveLastFolder(files[0].Path.LocalPath);
                vm.Path = files[0].Path.LocalPath;
            }
        }
        private async void Copy_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is AsciiArtToolViewModel vm && !string.IsNullOrEmpty(vm.Output))
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel?.Clipboard != null)
                {
                    await topLevel.Clipboard.SetTextAsync(vm.Output);
                }
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is AsciiArtToolViewModel vm)
            {
                vm.Output = string.Empty;
                vm.Path = string.Empty;
            }
        }
    }
}
