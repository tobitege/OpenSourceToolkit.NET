using Avalonia.Controls;
using Avalonia.Input.Platform;
using OpenSourceToolkit.NET.ViewModels.Tools;

namespace OpenSourceToolkit.NET.Views.Tools
{
    public partial class SqlFormatterToolView : UserControl
    {
        public SqlFormatterToolView()
        {
            InitializeComponent();
            DataContextChanged += (s, e) =>
            {
                if (DataContext is SqlFormatterToolViewModel vm)
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
