using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace OpenSourceToolkit.Security
{
    /// <summary>
    /// Cross-platform secure storage for sensitive data like API keys.
    /// Uses OS-level encryption: DPAPI on Windows, Keychain on macOS, encrypted file on Linux.
    /// </summary>
    public class SecureStorage
    {
        private readonly string _storagePath;
        private readonly string _appIdentifier;
        private readonly object _lock = new object();
        private Dictionary<string, string> _cache;

        /// <summary>
        /// Creates a new SecureStorage instance.
        /// </summary>
        /// <param name="storagePath">Full path to the encrypted storage file.</param>
        /// <param name="appIdentifier">Unique identifier for the application (used for entropy/keychain).</param>
        public SecureStorage(string storagePath, string appIdentifier)
        {
            _storagePath = storagePath ?? throw new ArgumentNullException(nameof(storagePath));
            _appIdentifier = appIdentifier ?? throw new ArgumentNullException(nameof(appIdentifier));
        }

        #region Public API

        /// <summary>
        /// Stores a secret securely using platform-appropriate encryption.
        /// </summary>
        public void Store(string key, string value)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            lock (_lock)
            {
                EnsureCacheLoaded();

                if (string.IsNullOrEmpty(value))
                {
                    _cache.Remove(key);
                }
                else
                {
                    _cache[key] = value;
                }

                SaveSecrets();
            }
        }

        /// <summary>
        /// Retrieves a secret from secure storage.
        /// </summary>
        public string Retrieve(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;

            lock (_lock)
            {
                EnsureCacheLoaded();
                return _cache.TryGetValue(key, out var value) ? value : null;
            }
        }

        /// <summary>
        /// Removes a secret from secure storage.
        /// </summary>
        public void Remove(string key)
        {
            Store(key, null);
        }

        /// <summary>
        /// Checks if a secret exists in secure storage.
        /// </summary>
        public bool Contains(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            lock (_lock)
            {
                EnsureCacheLoaded();
                return _cache.ContainsKey(key);
            }
        }

        /// <summary>
        /// Gets all stored keys (not values).
        /// </summary>
        public IEnumerable<string> GetAllKeys()
        {
            lock (_lock)
            {
                EnsureCacheLoaded();
                return new List<string>(_cache.Keys);
            }
        }

        /// <summary>
        /// Clears all stored secrets.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _cache = new Dictionary<string, string>();
                SaveSecrets();
            }
        }

        /// <summary>
        /// Reloads secrets from disk, discarding any cached values.
        /// </summary>
        public void Reload()
        {
            lock (_lock)
            {
                _cache = null;
                EnsureCacheLoaded();
            }
        }

        #endregion

        #region Platform Detection

        private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        private static bool IsMacOS => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        private static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        #endregion

        #region Cache Management

        private void EnsureCacheLoaded()
        {
            if (_cache != null)
                return;

            _cache = new Dictionary<string, string>();

            try
            {
                if (!File.Exists(_storagePath))
                    return;

                var encryptedData = File.ReadAllBytes(_storagePath);
                if (encryptedData.Length == 0)
                    return;

                var decryptedJson = Decrypt(encryptedData);
                if (!string.IsNullOrEmpty(decryptedJson))
                {
                    _cache = DeserializeSecrets(decryptedJson);
                }
            }
            catch
            {
                // If decryption fails (e.g., different user, corrupted file), start fresh
                _cache = new Dictionary<string, string>();
            }
        }

        private void SaveSecrets()
        {
            try
            {
                var dir = Path.GetDirectoryName(_storagePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = SerializeSecrets(_cache);
                var encryptedData = Encrypt(json);
                File.WriteAllBytes(_storagePath, encryptedData);

                // Set restrictive permissions on the file (Windows)
                if (IsWindows)
                {
                    SetRestrictivePermissions(_storagePath);
                }
            }
            catch
            {
                // Ignore save errors - secrets will be re-entered next time
            }
        }

        // Simple JSON serialization without external dependencies
        private static string SerializeSecrets(Dictionary<string, string> secrets)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            var first = true;
            foreach (var kvp in secrets)
            {
                if (!first) sb.Append(",");
                first = false;
                sb.Append("\"");
                sb.Append(EscapeJsonString(kvp.Key));
                sb.Append("\":\"");
                sb.Append(EscapeJsonString(kvp.Value));
                sb.Append("\"");
            }
            sb.Append("}");
            return sb.ToString();
        }

        private static Dictionary<string, string> DeserializeSecrets(string json)
        {
            var result = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(json) || json.Length < 2)
                return result;

            // Simple JSON object parser for {"key":"value",...} format
            var content = json.Trim();
            if (!content.StartsWith("{") || !content.EndsWith("}"))
                return result;

            content = content.Substring(1, content.Length - 2).Trim();
            if (string.IsNullOrEmpty(content))
                return result;

            var i = 0;
            while (i < content.Length)
            {
                // Skip whitespace
                while (i < content.Length && char.IsWhiteSpace(content[i])) i++;
                if (i >= content.Length) break;

                // Parse key
                if (content[i] != '"') break;
                var key = ParseJsonString(content, ref i);
                if (key == null) break;

                // Skip colon
                while (i < content.Length && char.IsWhiteSpace(content[i])) i++;
                if (i >= content.Length || content[i] != ':') break;
                i++;

                // Parse value
                while (i < content.Length && char.IsWhiteSpace(content[i])) i++;
                if (i >= content.Length || content[i] != '"') break;
                var value = ParseJsonString(content, ref i);
                if (value == null) break;

                result[key] = value;

                // Skip comma
                while (i < content.Length && char.IsWhiteSpace(content[i])) i++;
                if (i < content.Length && content[i] == ',') i++;
            }

            return result;
        }

        private static string ParseJsonString(string json, ref int index)
        {
            if (index >= json.Length || json[index] != '"')
                return null;

            index++; // Skip opening quote
            var sb = new StringBuilder();

            while (index < json.Length)
            {
                var c = json[index];
                if (c == '"')
                {
                    index++; // Skip closing quote
                    return sb.ToString();
                }
                if (c == '\\' && index + 1 < json.Length)
                {
                    index++;
                    var escaped = json[index];
                    switch (escaped)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (index + 4 < json.Length)
                            {
                                var hex = json.Substring(index + 1, 4);
                                if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var code))
                                {
                                    sb.Append((char)code);
                                    index += 4;
                                }
                            }
                            break;
                        default: sb.Append(escaped); break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
                index++;
            }

            return null; // Unterminated string
        }

        private static string EscapeJsonString(string s)
        {
            if (string.IsNullOrEmpty(s))
                return s;

            var sb = new StringBuilder();
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ')
                            sb.AppendFormat("\\u{0:x4}", (int)c);
                        else
                            sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        #endregion

        #region Encryption/Decryption

        private byte[] Encrypt(string plainText)
        {
            if (IsWindows)
                return EncryptWindows(plainText);
            if (IsMacOS)
                return EncryptMacOS(plainText);
            return EncryptLinux(plainText);
        }

        private string Decrypt(byte[] encryptedData)
        {
            if (IsWindows)
                return DecryptWindows(encryptedData);
            if (IsMacOS)
                return DecryptMacOS(encryptedData);
            return DecryptLinux(encryptedData);
        }

        #endregion

        #region Windows DPAPI

        private byte[] EncryptWindows(string plainText)
        {
            // Use DPAPI - encrypts data tied to current Windows user
            var data = Encoding.UTF8.GetBytes(plainText);
            return ProtectedData.Protect(data, GetEntropy(), DataProtectionScope.CurrentUser);
        }

        private string DecryptWindows(byte[] encryptedData)
        {
            var decrypted = ProtectedData.Unprotect(encryptedData, GetEntropy(), DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }

        /// <summary>
        /// Additional entropy to strengthen DPAPI encryption.
        /// Uses the app identifier for app-specific protection.
        /// </summary>
        private byte[] GetEntropy()
        {
            return Encoding.UTF8.GetBytes(_appIdentifier);
        }

        private static void SetRestrictivePermissions(string path)
        {
            try
            {
                // Use icacls to set restrictive permissions (current user only)
                var startInfo = new ProcessStartInfo
                {
                    FileName = "icacls",
                    Arguments = $"\"{path}\" /inheritance:r /grant:r \"%USERNAME%:F\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var process = Process.Start(startInfo))
                {
                    process?.WaitForExit(5000);
                }
            }
            catch
            {
                // Ignore permission errors - file is still encrypted
            }
        }

        #endregion

        #region macOS Keychain

        private byte[] EncryptMacOS(string plainText)
        {
            // On macOS, encrypt locally with AES and store the key in Keychain
            var (key, iv) = GenerateAesKeyAndIv();
            var encrypted = EncryptAes(plainText, key, iv);

            // Store the AES key in Keychain
            var keychainKey = Convert.ToBase64String(CombineArrays(key, iv));
            StoreInKeychain(_appIdentifier, keychainKey);

            return encrypted;
        }

        private string DecryptMacOS(byte[] encryptedData)
        {
            // Retrieve AES key from Keychain
            var keychainKey = RetrieveFromKeychain(_appIdentifier);
            if (string.IsNullOrEmpty(keychainKey))
                return null;

            var keyAndIv = Convert.FromBase64String(keychainKey);
            var key = new byte[32];
            var iv = new byte[16];
            Array.Copy(keyAndIv, 0, key, 0, 32);
            Array.Copy(keyAndIv, 32, iv, 0, 16);

            return DecryptAes(encryptedData, key, iv);
        }

        private void StoreInKeychain(string account, string password)
        {
            try
            {
                // Delete existing entry first
                RunSecurityCommand($"delete-generic-password -a \"{account}\" -s \"{_appIdentifier}\"");

                // Add new entry
                RunSecurityCommand($"add-generic-password -a \"{account}\" -s \"{_appIdentifier}\" -w \"{password}\"");
            }
            catch
            {
                // Ignore errors
            }
        }

        private string RetrieveFromKeychain(string account)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "/usr/bin/security",
                    Arguments = $"find-generic-password -a \"{account}\" -s \"{_appIdentifier}\" -w",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                        return null;

                    var output = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit(5000);

                    return process.ExitCode == 0 ? output : null;
                }
            }
            catch
            {
                return null;
            }
        }

        private static void RunSecurityCommand(string arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "/usr/bin/security",
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (var process = Process.Start(startInfo))
            {
                process?.WaitForExit(5000);
            }
        }

        #endregion

        #region Linux Encrypted File

        private byte[] EncryptLinux(string plainText)
        {
            // On Linux, use AES encryption with a machine-specific key
            var (key, iv) = DeriveLinuxKey();
            return EncryptAes(plainText, key, iv);
        }

        private string DecryptLinux(byte[] encryptedData)
        {
            var (key, iv) = DeriveLinuxKey();
            return DecryptAes(encryptedData, key, iv);
        }

        private (byte[] key, byte[] iv) DeriveLinuxKey()
        {
            // Combine machine ID, user info, and app identifier for key derivation
            var machineId = GetLinuxMachineId();
            var userId = Environment.UserName;
            var combined = $"{_appIdentifier}:{machineId}:{userId}";

            // Use PBKDF2 to derive a strong key
            var derivedBytes = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(combined),
                Encoding.UTF8.GetBytes(_appIdentifier + ".Salt.v1"),
                100000,
                HashAlgorithmName.SHA256,
                48);

            var key = derivedBytes[..32];
            var iv = derivedBytes[32..];
            return (key, iv);
        }

        private static string GetLinuxMachineId()
        {
            try
            {
                // Try /etc/machine-id first (systemd)
                if (File.Exists("/etc/machine-id"))
                    return File.ReadAllText("/etc/machine-id").Trim();

                // Fallback to /var/lib/dbus/machine-id
                if (File.Exists("/var/lib/dbus/machine-id"))
                    return File.ReadAllText("/var/lib/dbus/machine-id").Trim();
            }
            catch
            {
                // Ignore
            }

            // Last resort: use hostname
            return Environment.MachineName;
        }

        #endregion

        #region AES Helpers

        private static (byte[] key, byte[] iv) GenerateAesKeyAndIv()
        {
            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.GenerateKey();
                aes.GenerateIV();
                return (aes.Key, aes.IV);
            }
        }

        private static byte[] EncryptAes(string plainText, byte[] key, byte[] iv)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var encryptor = aes.CreateEncryptor())
                using (var ms = new MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    using (var writer = new StreamWriter(cs, Encoding.UTF8))
                    {
                        writer.Write(plainText);
                    }
                    return ms.ToArray();
                }
            }
        }

        private static string DecryptAes(byte[] encryptedData, byte[] key, byte[] iv)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var decryptor = aes.CreateDecryptor())
                using (var ms = new MemoryStream(encryptedData))
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (var reader = new StreamReader(cs, Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        private static byte[] CombineArrays(byte[] first, byte[] second)
        {
            var result = new byte[first.Length + second.Length];
            Array.Copy(first, 0, result, 0, first.Length);
            Array.Copy(second, 0, result, first.Length, second.Length);
            return result;
        }

        #endregion
    }
}
