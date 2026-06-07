using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using OpenSourceToolkit.NET.ViewModels.Tools;
using System.Linq;

namespace OpenSourceToolkit.NET.Views.Tools
{
    public partial class FontsViewerToolView : ToolViewBase
    {
        public FontsViewerToolView()
        {
            AvaloniaXamlLoader.Load(this);
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, System.EventArgs e)
        {
            if (DataContext is FontsViewerToolViewModel vm)
            {
                // Wire up clipboard action
                vm.CopyToClipboardAction = async text =>
                {
                    var topLevel = TopLevel.GetTopLevel(this);
                    if (topLevel?.Clipboard != null)
                    {
                        await topLevel.Clipboard.SetTextAsync(text);
                    }
                };

                // Wire up folder selection action
                vm.SelectDownloadFolderAction = async lastPath =>
                {
                    var topLevel = TopLevel.GetTopLevel(this);
                    if (topLevel == null) return null;

                    var startFolder = await GetStartFolderAsync();

                    var result = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                    {
                        Title = "Select Download Folder",
                        AllowMultiple = false,
                        SuggestedStartLocation = startFolder
                    });

                    if (result != null && result.Count > 0)
                    {
                        var folder = result.First();
                        var path = folder.TryGetLocalPath();
                        if (!string.IsNullOrEmpty(path))
                        {
                            SaveLastFolderDirect(path);
                            return path;
                        }
                    }

                    return null;
                };
            }
        }
    }
}
