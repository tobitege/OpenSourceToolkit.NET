using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using OpenSourceToolkit.NET.ViewModels.Tools;
using System.IO;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace OpenSourceToolkit.NET.Views.Tools
{
    public partial class QrCodeToolView : UserControl
    {
        public QrCodeToolView()
        {
            AvaloniaXamlLoader.Load(this);
        }

        [SupportedOSPlatform("windows")]
        public static void SetBitmapClipboardData(byte[] pngBytes)
        {
            if (pngBytes == null || pngBytes.Length == 0) return;

            using (var stream = new MemoryStream(pngBytes))
            using (var image = System.Drawing.Image.FromStream(stream))
            {
                System.Windows.Forms.Clipboard.SetImage(image);
            }
        }

        private async void CopyPng_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as QrCodeToolViewModel;
            if (vm?.LastPngBytes == null) return;

            if (!System.OperatingSystem.IsWindows())
            {
                await vm.ShowCopyStatusAsync("PNG clipboard not supported on this platform.");
                return;
            }

            try
            {
                SetBitmapClipboardData(vm.LastPngBytes);
                await vm.ShowCopyStatusAsync("PNG image copied to clipboard.");
            }
            catch
            {
                await vm.ShowCopyStatusAsync("Failed to copy to clipboard.");
            }
        }

        private async void CopySvg_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as QrCodeToolViewModel;
            if (string.IsNullOrEmpty(vm?.SvgText)) return;

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.Clipboard != null)
            {
                await topLevel.Clipboard.SetTextAsync(vm.SvgText);
                await vm.ShowCopyStatusAsync("SVG code copied to clipboard.");
            }
        }
    }
}
