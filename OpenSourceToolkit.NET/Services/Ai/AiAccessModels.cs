using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OpenSourceToolkit.NET.Services.Ai
{
    public enum AiAccessMode
    {
        OpenAiApi,
        CodexAppServer,
        CodexOAuth
    }

    public sealed class AiAccessModeOption
    {
        public AiAccessModeOption(AiAccessMode mode, string displayName)
        {
            Mode = mode;
            DisplayName = displayName;
        }

        public AiAccessMode Mode { get; }
        public string DisplayName { get; }
    }

    public sealed class AiAccessCapabilities
    {
        public AiAccessCapabilities(
            bool usesApiConnection,
            bool supportsText,
            bool supportsImageGeneration,
            bool requiresCodexInstallation)
        {
            UsesApiConnection = usesApiConnection;
            SupportsText = supportsText;
            SupportsImageGeneration = supportsImageGeneration;
            RequiresCodexInstallation = requiresCodexInstallation;
        }

        public bool UsesApiConnection { get; }
        public bool SupportsText { get; }
        public bool SupportsImageGeneration { get; }
        public bool RequiresCodexInstallation { get; }
    }

    public sealed class AiSubscriptionAccount
    {
        public AiSubscriptionAccount(string email, string planType)
        {
            Email = email;
            PlanType = planType;
        }

        public string Email { get; }
        public string PlanType { get; }
    }

    public sealed class AiSubscriptionModel
    {
        public AiSubscriptionModel(
            string modelId,
            string displayName,
            string description,
            bool isDefault,
            string defaultReasoningEffort = null,
            IReadOnlyList<AiSubscriptionReasoningEffort> supportedReasoningEfforts = null,
            string defaultServiceTier = null,
            IReadOnlyList<AiSubscriptionServiceTier> serviceTiers = null)
        {
            ModelId = modelId;
            DisplayName = displayName;
            Description = description;
            IsDefault = isDefault;
            DefaultReasoningEffort = defaultReasoningEffort;
            SupportedReasoningEfforts =
                supportedReasoningEfforts ?? Array.Empty<AiSubscriptionReasoningEffort>();
            DefaultServiceTier = defaultServiceTier;
            ServiceTiers = serviceTiers ?? Array.Empty<AiSubscriptionServiceTier>();
        }

        public string ModelId { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public bool IsDefault { get; }
        public string DefaultReasoningEffort { get; }
        public IReadOnlyList<AiSubscriptionReasoningEffort> SupportedReasoningEfforts { get; }
        public string DefaultServiceTier { get; }
        public IReadOnlyList<AiSubscriptionServiceTier> ServiceTiers { get; }
    }

    public sealed class AiSubscriptionReasoningEffort
    {
        public AiSubscriptionReasoningEffort(string id, string description)
        {
            Id = id;
            Description = description;
        }

        public string Id { get; }
        public string Description { get; }
        public string DisplayName =>
            string.IsNullOrWhiteSpace(Id)
                ? string.Empty
                : char.ToUpperInvariant(Id[0]) + Id.Substring(1);
    }

    public sealed class AiSubscriptionServiceTier
    {
        public AiSubscriptionServiceTier(string id, string displayName, string description)
        {
            Id = id;
            DisplayName = displayName;
            Description = description;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public string Description { get; }
    }

    public sealed class AiSubscriptionLoginResult
    {
        public AiSubscriptionLoginResult(bool success, string error)
        {
            Success = success;
            Error = error;
        }

        public bool Success { get; }
        public string Error { get; }
    }

    public interface IAiSubscriptionThread
    {
        string ModelId { get; }

        Task<string> RunAsync(
            string input,
            string reasoningEffort,
            string serviceTier,
            Func<string, Task> onTextDelta,
            CancellationToken cancellationToken);
    }

    public interface IAiSubscriptionSession : IAsyncDisposable
    {
        Task<AiSubscriptionAccount> GetAccountAsync(CancellationToken cancellationToken);

        Task<AiSubscriptionLoginResult> LoginAsync(
            Func<Uri, Task<bool>> openBrowser,
            CancellationToken cancellationToken);

        Task<IReadOnlyList<AiSubscriptionModel>> ListModelsAsync(CancellationToken cancellationToken);

        Task<IAiSubscriptionThread> StartThreadAsync(
            string modelId,
            CancellationToken cancellationToken);

        Task LogoutAsync(CancellationToken cancellationToken);
    }

    public interface IAiSubscriptionSessionFactory
    {
        Task<IAiSubscriptionSession> ConnectAsync(
            AiAccessMode mode,
            CancellationToken cancellationToken);
    }
}
