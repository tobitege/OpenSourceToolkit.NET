using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;

namespace OpenSourceToolkit.NET.Services
{
    public sealed class SettingsFileStore
    {
        private const int BackupCount = 3;
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        private readonly string _settingsPath;
        private readonly Mutex _processMutex;
        private readonly object _syncRoot = new object();
        private string _knownFileHash;

        public SettingsFileStore(string settingsPath, string mutexName)
        {
            _settingsPath = settingsPath ?? throw new ArgumentNullException(nameof(settingsPath));
            _processMutex = new Mutex(false, mutexName ?? throw new ArgumentNullException(nameof(mutexName)));
        }

        public SettingsData Load(Func<SettingsData> createDefaultSettings)
        {
            if (createDefaultSettings == null)
                throw new ArgumentNullException(nameof(createDefaultSettings));

            lock (_syncRoot)
            {
                EnterProcessMutex();
                try
                {
                    if (!File.Exists(_settingsPath))
                    {
                        _knownFileHash = null;
                        return createDefaultSettings();
                    }

                    var bytes = File.ReadAllBytes(_settingsPath);
                    _knownFileHash = ComputeHash(bytes);
                    if (TryDeserializeSettings(bytes, out var settings))
                        return settings;

                    PreserveInvalidSettingsFile(bytes);
                    if (TryLoadNewestBackup(out settings))
                    {
                        WriteSettingsAtomically(settings);
                        _knownFileHash = ComputeFileHash(_settingsPath);
                        return settings;
                    }

                    return createDefaultSettings();
                }
                finally
                {
                    _processMutex.ReleaseMutex();
                }
            }
        }

        public bool Save(SettingsData settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            lock (_syncRoot)
            {
                EnterProcessMutex();
                try
                {
                    var currentHash = ComputeFileHash(_settingsPath);
                    if (!string.Equals(currentHash, _knownFileHash, StringComparison.Ordinal))
                    {
                        WriteConflictSnapshot(settings);
                        return false;
                    }

                    CreateBackupOfCurrentSettings();
                    WriteSettingsAtomically(settings);
                    _knownFileHash = ComputeFileHash(_settingsPath);
                    return true;
                }
                finally
                {
                    _processMutex.ReleaseMutex();
                }
            }
        }

        private void EnterProcessMutex()
        {
            try
            {
                _processMutex.WaitOne();
            }
            catch (AbandonedMutexException)
            {
                // Ownership is granted when the previous process ended unexpectedly.
            }
        }

        private void CreateBackupOfCurrentSettings()
        {
            if (!File.Exists(_settingsPath))
                return;

            var currentBytes = File.ReadAllBytes(_settingsPath);
            if (!TryDeserializeSettings(currentBytes, out var currentSettings))
                return;

            for (var slot = BackupCount; slot > 1; slot--)
            {
                var previousPath = GetBackupPath(slot - 1);
                if (File.Exists(previousPath))
                    File.Copy(previousPath, GetBackupPath(slot), true);
            }

            var envelope = new SettingsBackupEnvelope
            {
                BackupTimestampUtc = DateTimeOffset.UtcNow,
                Settings = currentSettings
            };
            WriteJsonAtomically(GetBackupPath(1), envelope);
        }

        private bool TryLoadNewestBackup(out SettingsData settings)
        {
            for (var slot = 1; slot <= BackupCount; slot++)
            {
                var backupPath = GetBackupPath(slot);
                if (!File.Exists(backupPath))
                    continue;

                try
                {
                    var envelope = JsonSerializer.Deserialize<SettingsBackupEnvelope>(
                        File.ReadAllText(backupPath),
                        JsonOptions);
                    if (envelope?.Settings != null)
                    {
                        settings = envelope.Settings;
                        return true;
                    }
                }
                catch (JsonException)
                {
                    // Continue with the next older backup.
                }
            }

            settings = null;
            return false;
        }

        private void PreserveInvalidSettingsFile(byte[] bytes)
        {
            var path = GetTimestampedSidecarPath("corrupt");
            File.WriteAllBytes(path, bytes);
        }

        private void WriteConflictSnapshot(SettingsData settings)
        {
            var envelope = new SettingsBackupEnvelope
            {
                BackupTimestampUtc = DateTimeOffset.UtcNow,
                Settings = settings
            };
            WriteJsonAtomically(GetTimestampedSidecarPath("conflict"), envelope);
        }

        private void WriteSettingsAtomically(SettingsData settings)
        {
            WriteJsonAtomically(_settingsPath, settings);
        }

        private static bool TryDeserializeSettings(byte[] bytes, out SettingsData settings)
        {
            try
            {
                settings = JsonSerializer.Deserialize<SettingsData>(bytes, JsonOptions);
                return settings != null;
            }
            catch (JsonException)
            {
                settings = null;
                return false;
            }
        }

        private static string ComputeHash(byte[] bytes)
        {
            return Convert.ToHexString(SHA256.HashData(bytes));
        }

        private static string ComputeFileHash(string path)
        {
            return File.Exists(path) ? ComputeHash(File.ReadAllBytes(path)) : null;
        }

        private string GetBackupPath(int slot)
        {
            return Path.Combine(
                Path.GetDirectoryName(_settingsPath) ?? string.Empty,
                $"settings.backup-{slot}.json");
        }

        private string GetTimestampedSidecarPath(string kind)
        {
            var directory = Path.GetDirectoryName(_settingsPath) ?? string.Empty;
            var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmssfff");
            return Path.Combine(directory, $"settings.{kind}-{timestamp}-{Environment.ProcessId}.json");
        }

        private static void WriteJsonAtomically<T>(string path, T value)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var tempPath = $"{path}.tmp-{Environment.ProcessId}-{Guid.NewGuid():N}";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(value, JsonOptions));

            if (File.Exists(path))
                File.Replace(tempPath, path, null);
            else
                File.Move(tempPath, path);
        }

        private sealed class SettingsBackupEnvelope
        {
            public DateTimeOffset BackupTimestampUtc { get; set; }
            public SettingsData Settings { get; set; }
        }
    }
}
