using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Markup.Xaml;
using OpenSourceToolkit.NET.ViewModels.Tools;

namespace OpenSourceToolkit.NET.Views.Tools
{
    public partial class ColorToolView : UserControl
    {
        public ColorToolView()
        {
            AvaloniaXamlLoader.Load(this);
            DataContextChanged += (s, e) =>
            {
                if (DataContext is ColorToolViewModel vm)
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
            };
        }
    }
}
