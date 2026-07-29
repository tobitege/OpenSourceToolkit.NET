using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSourceToolkit.NET.ViewModels.Tools;
using OpenSourceToolkit.NET.Views.Tools;
using OpenSourceToolkit.TextData;
using System.Threading;
using System.Windows.Forms;
using Avalonia;

namespace OpenSourceToolkit.Tests
{
    [TestClass]
    public class QrCodeClipboardTests
    {
        [AssemblyInitialize]
        public static void InitializeAvalonia(TestContext context)
        {
            // Initialize basic Avalonia platform services so Bitmap constructor works
            AppBuilder.Configure<OpenSourceToolkit.NET.App>()
                .UseWin32()
                .UseSkia()
                .UseHarfBuzz()
                .SetupWithoutStarting();
        }

        [TestMethod]
        [TestCategory("WindowsOnly")]
        public void CopyPng_PutsImageOnSystemClipboard()
        {
            // Skip test on non-Windows platforms
            if (!System.OperatingSystem.IsWindows())
            {
                Assert.Inconclusive("This test requires Windows.");
                return;
            }

            // Must run on STA thread for OLE clipboard operations
            var thread = new Thread(() =>
            {
                var viewModel = new QrCodeToolViewModel();
                // Force generation so LastPngBytes is populated
                viewModel.GenerateCommand.Execute(null);

                Assert.IsNotNull(viewModel.LastPngBytes, "Bytes should have been generated");

                // Clear clipboard first
                Clipboard.Clear();

                // Act: call our helper
                QrCodeToolView.SetBitmapClipboardData(viewModel.LastPngBytes);

                // Assert: System clipboard has image
                Assert.IsTrue(Clipboard.ContainsImage(), "Clipboard should contain an image");

                // Cleanup
                Clipboard.Clear();
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
        }
    }
}
