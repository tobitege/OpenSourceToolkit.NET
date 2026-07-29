using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSourceToolkit.NET.Services;

namespace OpenSourceToolkit.Tests
{
    [TestClass]
    public class SettingsFileStoreTests
    {
        [TestMethod]
        public void Save_RotatesThreeTimestampedBackups()
        {
            var directory = CreateTestDirectory();
            var settingsPath = Path.Combine(directory, "settings.json");
            var store = CreateStore(settingsPath);

            var settings = store.Load(() => new SettingsData());
            for (var version = 1; version <= 5; version++)
            {
                settings.EditorFontFamily = $"Font {version}";
                Assert.IsTrue(store.Save(settings));
            }

            var backups = Directory.GetFiles(directory, "settings.backup-*.json");
            Assert.AreEqual(3, backups.Length);

            for (var slot = 1; slot <= 3; slot++)
            {
                using var document = JsonDocument.Parse(File.ReadAllText(
                    Path.Combine(directory, $"settings.backup-{slot}.json")));
                Assert.IsTrue(document.RootElement.TryGetProperty("BackupTimestampUtc", out var timestamp));
                Assert.AreEqual(TimeSpan.Zero, timestamp.GetDateTimeOffset().Offset);
                Assert.IsTrue(document.RootElement.TryGetProperty("Settings", out var backedUpSettings));
                Assert.AreEqual($"Font {5 - slot}", backedUpSettings.GetProperty("EditorFontFamily").GetString());
            }
        }

        [TestMethod]
        public void Load_RestoresNewestBackupWhenSettingsAreInvalid()
        {
            var directory = CreateTestDirectory();
            var settingsPath = Path.Combine(directory, "settings.json");
            var store = CreateStore(settingsPath);
            var settings = store.Load(() => new SettingsData());

            settings.EditorFontFamily = "Recover me";
            Assert.IsTrue(store.Save(settings));
            settings.EditorFontFamily = "Current";
            Assert.IsTrue(store.Save(settings));
            File.WriteAllText(settingsPath, "{ invalid json");

            var reloadedStore = CreateStore(settingsPath);
            var recovered = reloadedStore.Load(() => new SettingsData());

            Assert.AreEqual("Recover me", recovered.EditorFontFamily);
            Assert.IsTrue(Directory.GetFiles(directory, "settings.corrupt-*.json").Any());
        }

        [TestMethod]
        public void Load_SkipsInvalidNewestBackupAndUsesNextValidBackup()
        {
            var directory = CreateTestDirectory();
            var settingsPath = Path.Combine(directory, "settings.json");
            var store = CreateStore(settingsPath);
            var settings = store.Load(() => new SettingsData());

            settings.EditorFontFamily = "Oldest valid";
            Assert.IsTrue(store.Save(settings));
            settings.EditorFontFamily = "Newest backup";
            Assert.IsTrue(store.Save(settings));
            settings.EditorFontFamily = "Current";
            Assert.IsTrue(store.Save(settings));

            File.WriteAllText(Path.Combine(directory, "settings.backup-1.json"), "{ broken backup");
            File.WriteAllText(settingsPath, "{ truncated settings");

            var recovered = CreateStore(settingsPath).Load(() => new SettingsData());

            Assert.AreEqual("Oldest valid", recovered.EditorFontFamily);
        }

        [TestMethod]
        public void Load_InvalidFileWithoutBackupPreservesItAndAllowsSafeReplacement()
        {
            var directory = CreateTestDirectory();
            var settingsPath = Path.Combine(directory, "settings.json");
            File.WriteAllText(settingsPath, "{ truncated settings");
            var store = CreateStore(settingsPath);

            var settings = store.Load(() => new SettingsData { EditorFontFamily = "Default" });

            Assert.AreEqual("Default", settings.EditorFontFamily);
            Assert.IsTrue(Directory.GetFiles(directory, "settings.corrupt-*.json").Any());

            settings.EditorFontFamily = "Replacement";
            Assert.IsTrue(store.Save(settings));
            Assert.AreEqual(
                "Replacement",
                CreateStore(settingsPath).Load(() => new SettingsData()).EditorFontFamily);
        }

        [TestMethod]
        public void Load_RecoveryFollowedByLocaleSavePreservesFavoritesAndAiConnections()
        {
            var directory = CreateTestDirectory();
            var settingsPath = Path.Combine(directory, "settings.json");
            var store = CreateStore(settingsPath);
            var settings = store.Load(() => new SettingsData());
            settings.Language = "en-US";
            settings.Locale = "en-US";
            settings.FavoriteToolIds.Add(32);
            settings.AiSettings.Connections.Add(new AiConnectionData
            {
                Id = "NanoBanana",
                Name = "Nano Banana",
                ProviderType = "OpenRouter",
                ModelId = "google/gemini-3.1-flash-image",
                SupportsImageGeneration = true
            });
            Assert.IsTrue(store.Save(settings));

            settings.Theme = "changed-after-backup";
            Assert.IsTrue(store.Save(settings));
            File.WriteAllText(settingsPath, "{ interrupted shutdown write");

            var recoveredStore = CreateStore(settingsPath);
            var recovered = recoveredStore.Load(() => new SettingsData());
            recovered.Locale = "de-DE";
            Assert.IsTrue(recoveredStore.Save(recovered));

            var persisted = CreateStore(settingsPath).Load(() => new SettingsData());
            CollectionAssert.Contains(persisted.FavoriteToolIds, 32);
            Assert.AreEqual(1, persisted.AiSettings.Connections.Count);
            Assert.AreEqual("NanoBanana", persisted.AiSettings.Connections[0].Id);
            Assert.AreEqual("google/gemini-3.1-flash-image", persisted.AiSettings.Connections[0].ModelId);
            Assert.AreEqual("de-DE", persisted.Locale);
        }

        [TestMethod]
        public void Load_MissingMainFileIgnoresOrphanedTemporaryWrite()
        {
            var directory = CreateTestDirectory();
            var settingsPath = Path.Combine(directory, "settings.json");
            File.WriteAllText($"{settingsPath}.tmp-crashed-process", "{ partial write");
            var store = CreateStore(settingsPath);

            var settings = store.Load(() => new SettingsData { EditorFontFamily = "First start" });
            Assert.AreEqual("First start", settings.EditorFontFamily);

            Assert.IsTrue(store.Save(settings));
            Assert.AreEqual(
                "First start",
                CreateStore(settingsPath).Load(() => new SettingsData()).EditorFontFamily);
        }

        [TestMethod]
        public void Save_DoesNotOverwriteChangesFromAnotherProcess()
        {
            var directory = CreateTestDirectory();
            var settingsPath = Path.Combine(directory, "settings.json");
            var firstStore = CreateStore(settingsPath);
            var initial = firstStore.Load(() => new SettingsData());
            initial.EditorFontFamily = "Initial";
            Assert.IsTrue(firstStore.Save(initial));

            var staleStore = CreateStore(settingsPath);
            var stale = staleStore.Load(() => new SettingsData());
            var currentStore = CreateStore(settingsPath);
            var current = currentStore.Load(() => new SettingsData());
            current.EditorFontFamily = "Current";
            Assert.IsTrue(currentStore.Save(current));

            stale.EditorFontFamily = "Stale";
            Assert.IsFalse(staleStore.Save(stale));

            var verifier = CreateStore(settingsPath);
            Assert.AreEqual("Current", verifier.Load(() => new SettingsData()).EditorFontFamily);
            Assert.IsTrue(Directory.GetFiles(directory, "settings.conflict-*.json").Any());
        }

        [TestMethod]
        public async Task Save_ConcurrentInstancesProduceOneWinnerAndOneConflict()
        {
            var directory = CreateTestDirectory();
            var settingsPath = Path.Combine(directory, "settings.json");
            var mutexName = $@"Local\OpenSourceToolkit.NET.Tests.{Guid.NewGuid():N}";
            var initializer = CreateStore(settingsPath, mutexName);
            var initial = initializer.Load(() => new SettingsData { EditorFontFamily = "Initial" });
            Assert.IsTrue(initializer.Save(initial));

            var firstStore = CreateStore(settingsPath, mutexName);
            var firstSettings = firstStore.Load(() => new SettingsData());
            firstSettings.EditorFontFamily = "First";
            var secondStore = CreateStore(settingsPath, mutexName);
            var secondSettings = secondStore.Load(() => new SettingsData());
            secondSettings.EditorFontFamily = "Second";
            using var start = new ManualResetEventSlim(false);

            var firstSave = Task.Run(() =>
            {
                start.Wait();
                return firstStore.Save(firstSettings);
            });
            var secondSave = Task.Run(() =>
            {
                start.Wait();
                return secondStore.Save(secondSettings);
            });

            start.Set();
            var results = await Task.WhenAll(firstSave, secondSave);

            Assert.AreEqual(1, results.Count(result => result));
            var persisted = CreateStore(settingsPath).Load(() => new SettingsData());
            Assert.IsTrue(persisted.EditorFontFamily is "First" or "Second");
            Assert.IsTrue(Directory.GetFiles(directory, "settings.conflict-*.json").Any());
        }

        [TestMethod]
        public void Load_ContinuesAfterPreviousProcessAbandonedMutex()
        {
            var directory = CreateTestDirectory();
            var settingsPath = Path.Combine(directory, "settings.json");
            var mutexName = $@"Local\OpenSourceToolkit.NET.Tests.{Guid.NewGuid():N}";
            using var abandonedMutex = new Mutex(false, mutexName);
            var ownerThread = new Thread(() => abandonedMutex.WaitOne());
            ownerThread.Start();
            ownerThread.Join();

            var store = CreateStore(settingsPath, mutexName);
            var settings = store.Load(() => new SettingsData { EditorFontFamily = "Recovered owner" });

            Assert.AreEqual("Recovered owner", settings.EditorFontFamily);
            Assert.IsTrue(store.Save(settings));
        }

        private static SettingsFileStore CreateStore(string settingsPath, string mutexName = null)
        {
            return new SettingsFileStore(
                settingsPath,
                mutexName ?? $@"Local\OpenSourceToolkit.NET.Tests.{Guid.NewGuid():N}");
        }

        private static string CreateTestDirectory()
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                "OpenSourceToolkit.NET.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }
    }
}
