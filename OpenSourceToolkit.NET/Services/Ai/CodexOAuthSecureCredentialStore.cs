using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LlmTornado.Codex;

namespace OpenSourceToolkit.NET.Services.Ai
{
    public sealed class CodexOAuthSecureCredentialStore : ICodexOAuthCredentialStore
    {
        private const string CredentialKey = "codex.oauth.credentials";
        private readonly ISecretStorage _secretStorage;

        public CodexOAuthSecureCredentialStore(ISecretStorage secretStorage)
        {
            _secretStorage = secretStorage;
        }

        public static bool HasStoredCredentials(ISecretStorage secretStorage)
        {
            if (secretStorage == null)
                throw new System.ArgumentNullException(nameof(secretStorage));

            return secretStorage.Contains(CredentialKey);
        }

        public Task<CodexOAuthCredentials> LoadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var serialized = _secretStorage.Retrieve(CredentialKey);
            if (string.IsNullOrWhiteSpace(serialized))
                return Task.FromResult<CodexOAuthCredentials>(null);

            try
            {
                return Task.FromResult(JsonSerializer.Deserialize<CodexOAuthCredentials>(serialized));
            }
            catch (JsonException)
            {
                _secretStorage.Remove(CredentialKey);
                return Task.FromResult<CodexOAuthCredentials>(null);
            }
        }

        public Task SaveAsync(
            CodexOAuthCredentials credentials,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _secretStorage.Store(CredentialKey, JsonSerializer.Serialize(credentials));
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _secretStorage.Remove(CredentialKey);
            return Task.CompletedTask;
        }
    }
}
