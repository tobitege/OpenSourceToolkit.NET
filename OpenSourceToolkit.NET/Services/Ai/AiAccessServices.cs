using System;
using System.Threading.Tasks;

namespace OpenSourceToolkit.NET.Services.Ai
{
    public static class AiAccessServices
    {
        private static readonly Lazy<AiAccessManager> CurrentManager =
            new Lazy<AiAccessManager>(CreateCurrentManager);

        public static AiAccessManager Current => CurrentManager.Value;

        public static async Task RestoreSavedModeAsync()
        {
            var manager = Current;
            if (manager.Mode == AiAccessMode.OpenAiApi)
                return;

            try
            {
                await manager.SwitchModeAsync(manager.Mode);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[AiAccessServices.RestoreSavedModeAsync] {ex.Message}");
            }
        }

        public static void DisposeCurrent()
        {
            if (CurrentManager.IsValueCreated)
                CurrentManager.Value.Dispose();
        }

        private static AiAccessManager CreateCurrentManager()
        {
            var settings = global::OpenSourceToolkit.NET.Services.AppSettings.Current;
            var aiSettings = settings.AiSettings ??=
                new global::OpenSourceToolkit.NET.Services.AiSettingsData();
            var secretStorage =
                global::OpenSourceToolkit.NET.Services.SecureStorage.Default;
            var savedMode = ResolveSavedMode(
                aiSettings.OpenAiAccessMode,
                CodexOAuthSecureCredentialStore.HasStoredCredentials(secretStorage));
            if (aiSettings.OpenAiAccessMode != savedMode)
            {
                aiSettings.OpenAiAccessMode = savedMode;
                global::OpenSourceToolkit.NET.Services.AppSettings.Save();
            }

            return new AiAccessManager(
                new LlmTornadoSubscriptionSessionFactory(
                    secretStorage),
                savedMode);
        }

        private static AiAccessMode ResolveSavedMode(
            AiAccessMode? savedMode,
            bool hasStoredOAuthCredentials)
        {
            if (savedMode.HasValue &&
                Enum.IsDefined(typeof(AiAccessMode), savedMode.Value))
            {
                return savedMode.Value;
            }

            return hasStoredOAuthCredentials
                ? AiAccessMode.CodexOAuth
                : AiAccessMode.OpenAiApi;
        }
    }
}
