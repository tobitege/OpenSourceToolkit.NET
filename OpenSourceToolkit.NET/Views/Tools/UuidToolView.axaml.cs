using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Markup.Xaml;
using OpenSourceToolkit.NET.ViewModels.Tools;

namespace OpenSourceToolkit.NET.Views.Tools
{
    public partial class UuidToolView : UserControl
    {
        public UuidToolView()
        {
            AvaloniaXamlLoader.Load(this);
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, System.EventArgs e)
        {
            if (DataContext is UuidToolViewModel vm)
            {
                vm.CopyToClipboardAction = async text =>
                {
                    var topLevel = TopLevel.GetTopLevel(this);
                    if (topLevel?.Clipboard != null)
                    {
                        await topLevel.Clipboard.SetTextAsync(text);
                    }
                };
            }
        }
    }
}
