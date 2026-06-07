using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSourceToolkit.IO;

namespace OpenSourceToolkit.Tests
{
    [TestClass]
    public class IoTests
    {
        [TestMethod]
        public void FolderAnalyzer_Analyze_ReturnsCorrectStructure()
        {
            // Setup temp dir
            string tempPath = Path.Combine(Path.GetTempPath(), "OST_Test_" + Guid.NewGuid());
            Directory.CreateDirectory(tempPath);
            try
            {
                File.WriteAllText(Path.Combine(tempPath, "test.txt"), "content");
                Directory.CreateDirectory(Path.Combine(tempPath, "sub"));
                File.WriteAllText(Path.Combine(tempPath, "sub", "sub.txt"), "content");

                var result = FolderAnalyzer.Analyze(tempPath);

                Assert.IsNotNull(result);
                Assert.IsFalse(result.WasCancelled);
                Assert.AreEqual("directory", result.Root.Type);
                // Expect 2 children: test.txt and sub
                Assert.AreEqual(2, result.Root.Children.Count);

                long expectedSize = "content".Length * 2;
                Assert.AreEqual(expectedSize, result.Root.Size);
            }
            finally
            {
                if (Directory.Exists(tempPath))
                    Directory.Delete(tempPath, true);
            }
        }

        [TestMethod]
        public void FolderAnalyzer_Analyze_NonExistingDirectory_Throws()
        {
            string nonExisting = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Assert.Throws<DirectoryNotFoundException>(() => FolderAnalyzer.Analyze(nonExisting));
        }
    }
}
