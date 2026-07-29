using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSourceToolkit.NET.Services;
using OpenSourceToolkit.NET.Services.Ai;
using System;
using System.IO;
using System.Text.Json;

namespace OpenSourceToolkit.Tests
{
    [TestClass]
    public class WindowGeometryTests
    {
        [TestMethod]
        public void RectanglesOverlap_FullyInside_ReturnsTrue()
        {
            // Window 100x100 at (50,50) inside screen 1920x1080 at (0,0)
            Assert.IsTrue(WindowGeometry.RectanglesOverlap(50, 50, 100, 100, 0, 0, 1920, 1080));
        }

        [TestMethod]
        public void RectanglesOverlap_PartiallyOutsideRight_ReturnsTrue()
        {
            // Window 800x600 at (1600,100) - half outside right edge of 1920-wide screen
            Assert.IsTrue(WindowGeometry.RectanglesOverlap(1600, 100, 800, 600, 0, 0, 1920, 1080));
        }

        [TestMethod]
        public void RectanglesOverlap_PartiallyOutsideLeft_ReturnsTrue()
        {
            // Window 800x600 at (-400,100) - half outside left edge
            Assert.IsTrue(WindowGeometry.RectanglesOverlap(-400, 100, 800, 600, 0, 0, 1920, 1080));
        }

        [TestMethod]
        public void RectanglesOverlap_CompletelyOutside_ReturnsFalse()
        {
            // Window completely to the right of screen
            Assert.IsFalse(WindowGeometry.RectanglesOverlap(2000, 100, 800, 600, 0, 0, 1920, 1080));
        }

        [TestMethod]
        public void RectanglesOverlap_TouchingEdge_ReturnsFalse()
        {
            // Window exactly at right edge (no overlap)
            Assert.IsFalse(WindowGeometry.RectanglesOverlap(1920, 100, 800, 600, 0, 0, 1920, 1080));
        }

        [TestMethod]
        public void ClampRectangleInside_AlreadyInside_NoChange()
        {
            var result = WindowGeometry.ClampRectangleInside(100, 100, 800, 600, 0, 0, 1920, 1080);
            Assert.AreEqual(100, result.X);
            Assert.AreEqual(100, result.Y);
        }

        [TestMethod]
        public void ClampRectangleInside_HalfOutsideRight_ClampsLeft()
        {
            // Window 800x600 at (1600,100) - extends 480px past right edge of 1920-wide screen
            // Should clamp to x=1120 so right edge is at 1920
            var result = WindowGeometry.ClampRectangleInside(1600, 100, 800, 600, 0, 0, 1920, 1080);
            Assert.AreEqual(1120, result.X);
            Assert.AreEqual(100, result.Y);
        }

        [TestMethod]
        public void ClampRectangleInside_HalfOutsideLeft_ClampsRight()
        {
            // Window 800x600 at (-400,100) - left edge outside screen
            var result = WindowGeometry.ClampRectangleInside(-400, 100, 800, 600, 0, 0, 1920, 1080);
            Assert.AreEqual(0, result.X);
            Assert.AreEqual(100, result.Y);
        }

        [TestMethod]
        public void ClampRectangleInside_HalfOutsideBottom_ClampsUp()
        {
            // Window 800x600 at (100,800) - extends past bottom of 1080-high screen
            var result = WindowGeometry.ClampRectangleInside(100, 800, 800, 600, 0, 0, 1920, 1080);
            Assert.AreEqual(100, result.X);
            Assert.AreEqual(480, result.Y);
        }

        [TestMethod]
        public void ClampRectangleInside_OutsideTopLeft_ClampsToOrigin()
        {
            var result = WindowGeometry.ClampRectangleInside(-100, -100, 800, 600, 0, 0, 1920, 1080);
            Assert.AreEqual(0, result.X);
            Assert.AreEqual(0, result.Y);
        }

        [TestMethod]
        public void ClampRectangleInside_OutsideBottomRight_ClampsToMaxPosition()
        {
            var result = WindowGeometry.ClampRectangleInside(2000, 1000, 800, 600, 0, 0, 1920, 1080);
            Assert.AreEqual(1120, result.X); // 1920 - 800
            Assert.AreEqual(480, result.Y);  // 1080 - 600
        }

        [TestMethod]
        public void ClampRectangleInside_SecondMonitor_ClampsWithinThatScreen()
        {
            // Second monitor at x=1920, window half outside its right edge
            var result = WindowGeometry.ClampRectangleInside(3200, 100, 800, 600, 1920, 0, 1920, 1080);
            Assert.AreEqual(3040, result.X); // 1920 + 1920 - 800
            Assert.AreEqual(100, result.Y);
        }

        [TestMethod]
        public void ClampRectangleInside_NegativeScreenCoordinates_WorksCorrectly()
        {
            // Monitor to the left of primary (negative X)
            var result = WindowGeometry.ClampRectangleInside(-2000, 100, 800, 600, -1920, 0, 1920, 1080);
            Assert.AreEqual(-1920, result.X);
            Assert.AreEqual(100, result.Y);
        }
    }

    [TestClass]
    public class AppSettingsTests
    {
        [TestMethod]
        public void SettingsData_DefaultValues_AreCorrect()
        {
            var settings = new SettingsData();

            Assert.IsNull(settings.AudioInputDeviceName);
            Assert.AreEqual("WAV", settings.AudioExportFormat);
            Assert.AreEqual(192, settings.AudioMp3Bitrate);
            Assert.IsNull(settings.WindowX);
            Assert.IsNull(settings.WindowY);
            Assert.IsNull(settings.WindowWidth);
            Assert.IsNull(settings.WindowHeight);
            Assert.IsFalse(settings.WindowMaximized);
        }

        [TestMethod]
        public void SettingsData_WindowPosition_CanBeSetAndRetrieved()
        {
            var settings = new SettingsData
            {
                WindowX = 100.5,
                WindowY = 200.5,
                WindowWidth = 800.0,
                WindowHeight = 600.0,
                WindowMaximized = false
            };

            Assert.AreEqual(100.5, settings.WindowX);
            Assert.AreEqual(200.5, settings.WindowY);
            Assert.AreEqual(800.0, settings.WindowWidth);
            Assert.AreEqual(600.0, settings.WindowHeight);
            Assert.IsFalse(settings.WindowMaximized);
        }

        [TestMethod]
        public void SettingsData_WindowMaximized_CanBeSet()
        {
            var settings = new SettingsData { WindowMaximized = true };

            Assert.IsTrue(settings.WindowMaximized);
        }

        [TestMethod]
        public void SettingsData_Serialization_RoundTrip()
        {
            var original = new SettingsData
            {
                AudioInputDeviceName = "Test Microphone",
                AudioExportFormat = "MP3",
                AudioMp3Bitrate = 320,
                WindowX = 150.0,
                WindowY = 250.0,
                WindowWidth = 1024.0,
                WindowHeight = 768.0,
                WindowMaximized = false,
                AiSettings = new AiSettingsData
                {
                    OpenAiAccessMode = AiAccessMode.CodexOAuth
                }
            };

            var json = JsonSerializer.Serialize(original);
            var deserialized = JsonSerializer.Deserialize<SettingsData>(json);

            Assert.AreEqual(original.AudioInputDeviceName, deserialized.AudioInputDeviceName);
            Assert.AreEqual(original.AudioExportFormat, deserialized.AudioExportFormat);
            Assert.AreEqual(original.AudioMp3Bitrate, deserialized.AudioMp3Bitrate);
            Assert.AreEqual(original.WindowX, deserialized.WindowX);
            Assert.AreEqual(original.WindowY, deserialized.WindowY);
            Assert.AreEqual(original.WindowWidth, deserialized.WindowWidth);
            Assert.AreEqual(original.WindowHeight, deserialized.WindowHeight);
            Assert.AreEqual(original.WindowMaximized, deserialized.WindowMaximized);
            Assert.AreEqual(
                AiAccessMode.CodexOAuth,
                deserialized.AiSettings.OpenAiAccessMode);
        }

        [TestMethod]
        public void SettingsData_Serialization_WithNullWindowValues_RoundTrip()
        {
            var original = new SettingsData
            {
                AudioInputDeviceName = "Mic",
                WindowX = null,
                WindowY = null,
                WindowWidth = null,
                WindowHeight = null,
                WindowMaximized = true
            };

            var json = JsonSerializer.Serialize(original);
            var deserialized = JsonSerializer.Deserialize<SettingsData>(json);

            Assert.IsNull(deserialized.WindowX);
            Assert.IsNull(deserialized.WindowY);
            Assert.IsNull(deserialized.WindowWidth);
            Assert.IsNull(deserialized.WindowHeight);
            Assert.IsTrue(deserialized.WindowMaximized);
        }

        [TestMethod]
        public void SettingsData_Deserialization_FromEmptyJson_UsesDefaults()
        {
            var json = "{}";
            var settings = JsonSerializer.Deserialize<SettingsData>(json);

            Assert.IsNull(settings.AudioInputDeviceName);
            Assert.AreEqual("WAV", settings.AudioExportFormat);
            Assert.AreEqual(192, settings.AudioMp3Bitrate);
            Assert.IsNull(settings.WindowX);
            Assert.IsFalse(settings.WindowMaximized);
            Assert.IsNull(settings.AiSettings.OpenAiAccessMode);
        }

        [TestMethod]
        public void SettingsData_Deserialization_FromPartialJson_PreservesDefaults()
        {
            var json = "{\"WindowX\": 100, \"WindowY\": 200}";
            var settings = JsonSerializer.Deserialize<SettingsData>(json);

            Assert.AreEqual(100.0, settings.WindowX);
            Assert.AreEqual(200.0, settings.WindowY);
            Assert.IsNull(settings.WindowWidth);
            Assert.IsNull(settings.WindowHeight);
            Assert.AreEqual("WAV", settings.AudioExportFormat);
            Assert.AreEqual(192, settings.AudioMp3Bitrate);
        }

        [TestMethod]
        public void SettingsData_WindowPosition_NegativeCoordinates_Supported()
        {
            var settings = new SettingsData
            {
                WindowX = -500.0,
                WindowY = -100.0
            };

            var json = JsonSerializer.Serialize(settings);
            var deserialized = JsonSerializer.Deserialize<SettingsData>(json);

            Assert.AreEqual(-500.0, deserialized.WindowX);
            Assert.AreEqual(-100.0, deserialized.WindowY);
        }

        [TestMethod]
        public void SettingsData_WindowSize_MinimumValues_Supported()
        {
            var settings = new SettingsData
            {
                WindowWidth = 500.0,
                WindowHeight = 500.0
            };

            Assert.AreEqual(500.0, settings.WindowWidth);
            Assert.AreEqual(500.0, settings.WindowHeight);
        }

        [TestMethod]
        public void SettingsData_LargeWindowCoordinates_Supported()
        {
            var settings = new SettingsData
            {
                WindowX = 3840.0,
                WindowY = 2160.0,
                WindowWidth = 1920.0,
                WindowHeight = 1080.0
            };

            var json = JsonSerializer.Serialize(settings);
            var deserialized = JsonSerializer.Deserialize<SettingsData>(json);

            Assert.AreEqual(3840.0, deserialized.WindowX);
            Assert.AreEqual(2160.0, deserialized.WindowY);
            Assert.AreEqual(1920.0, deserialized.WindowWidth);
            Assert.AreEqual(1080.0, deserialized.WindowHeight);
        }
    }
}
