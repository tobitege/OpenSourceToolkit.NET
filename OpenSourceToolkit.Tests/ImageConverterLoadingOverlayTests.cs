using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSourceToolkit.Converters;
using OpenSourceToolkit.NET.ViewModels.Tools.ImageConverter;

namespace OpenSourceToolkit.Tests
{
    [TestClass]
    public class ImageConverterLoadingOverlayTests
    {
        [TestMethod]
        public void LoadingState_RaisesPropertyChanged()
        {
            var viewModel = new WorkspaceEditorViewModel(new ImageProcessor());
            var changedProperties = new List<string>();
            viewModel.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

            viewModel.IsLoadingWorkspaceImage = true;

            Assert.IsTrue(viewModel.IsLoadingWorkspaceImage);
            CollectionAssert.Contains(changedProperties, nameof(viewModel.IsLoadingWorkspaceImage));
        }

        [TestMethod]
        public void MainEditor_HasThemeAwareLoadingShadeBoundToWorkspaceState()
        {
            var viewPath = FindViewPath("ImageConverterToolView.axaml");
            var document = XDocument.Load(viewPath);
            var xamlNamespace = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");
            var shade = document
                .Descendants()
                .Single(element =>
                    (string)element.Attribute(xamlNamespace + "Name") == "WorkspaceImageLoadingShade");

            Assert.AreEqual("{Binding Workspace.IsLoadingWorkspaceImage}", (string)shade.Attribute("IsVisible"));
            Assert.AreEqual("True", (string)shade.Attribute("IsHitTestVisible"));

            var shadeBackground = shade.Elements().Single(element => element.Name.LocalName == "Border");
            Assert.AreEqual("{DynamicResource DaisyNeutralBrush}", (string)shadeBackground.Attribute("Background"));

            var progress = shade.Elements().Single(element => element.Name.LocalName == "ProgressBar");
            Assert.AreEqual("True", (string)progress.Attribute("IsIndeterminate"));
            Assert.AreEqual("{DynamicResource DaisyPrimaryBrush}", (string)progress.Attribute("Foreground"));
        }

        private static string FindViewPath(params string[] relativePath)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory != null)
            {
                var pathParts = new[]
                {
                    directory.FullName,
                    "OpenSourceToolkit.NET",
                    "Views",
                    "Tools"
                }.Concat(relativePath).ToArray();
                var candidate = Path.Combine(pathParts);

                if (File.Exists(candidate))
                    return candidate;

                directory = directory.Parent;
            }

            Assert.Fail($"Could not locate {Path.Combine(relativePath)} from the test output directory.");
            return null;
        }
    }
}
