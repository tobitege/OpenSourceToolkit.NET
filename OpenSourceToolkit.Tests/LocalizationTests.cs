using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSourceToolkit.NET.Localization;
using System.Globalization;

namespace OpenSourceToolkit.Tests
{
    [TestClass]
    [DoNotParallelize]
    public class LocalizationTests
    {
        private CultureInfo _originalCulture;

        [TestInitialize]
        public void Setup()
        {
            _originalCulture = ToolkitLocalization.CurrentCulture;
        }

        [TestCleanup]
        public void Cleanup()
        {
            ToolkitLocalization.SetCulture(_originalCulture);
        }

        [TestMethod]
        public void ToolkitLocalization_SwitchesToGerman_UpdatesStrings()
        {
            // Arrange - Force English first (system locale may be anything)
            ToolkitLocalization.SetCulture(new CultureInfo("en-US"));
            
            // Act - Get English text, then switch to German and get German text
            var englishText = ToolkitLocalization.GetString("Button_Cancel");
            ToolkitLocalization.SetCulture(new CultureInfo("de-DE"));
            var germanText = ToolkitLocalization.GetString("Button_Cancel");

            // Assert - Verify both languages work correctly
            Assert.AreEqual("Cancel", englishText, "English string should be 'Cancel'");
            Assert.AreEqual("Abbrechen", germanText, "German string should be 'Abbrechen'");
        }

        [TestMethod]
        public void ToolkitLocalization_CultureChanged_FiresEvent()
        {
            // Arrange - Start from English so switch to German triggers event
            ToolkitLocalization.SetCulture(new CultureInfo("en-US"));
            
            bool eventFired = false;
            CultureInfo newCulture = null;
            
            ToolkitLocalization.CultureChanged += Handler;
            void Handler(object s, CultureInfo c)
            {
                eventFired = true;
                newCulture = c;
            }

            try
            {
                // Act - Switch to German (different from current English)
                ToolkitLocalization.SetCulture(new CultureInfo("de-DE"));

                // Assert
                Assert.IsTrue(eventFired, "CultureChanged event should fire when culture changes");
                Assert.AreEqual("de-DE", newCulture.Name, "New culture should be de-DE");
            }
            finally
            {
                // Cleanup - Unsubscribe to prevent side effects on other tests
                ToolkitLocalization.CultureChanged -= Handler;
            }
        }

        [TestMethod]
        public void ToolkitLocalization_GetString_ReturnsKeyForMissingResource()
        {
            // Arrange
            var key = "NonExistentKey_12345";

            // Act
            var value = ToolkitLocalization.GetString(key);

            // Assert
            Assert.AreEqual(key, value);
        }

        [TestMethod]
        public void ToolkitLocalization_RevertToMessage_UsesGermanLabel()
        {
            ToolkitLocalization.SetCulture(new CultureInfo("en-US"));
            Assert.AreEqual("Revert to", ToolkitLocalization.GetString("AiAssistant_RevertTo"));

            ToolkitLocalization.SetCulture(new CultureInfo("de-DE"));
            Assert.AreEqual("Rückgängig bis", ToolkitLocalization.GetString("AiAssistant_RevertTo"));
        }
    }
}
