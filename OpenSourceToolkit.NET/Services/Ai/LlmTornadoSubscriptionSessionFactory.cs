using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LlmTornado;
using LlmTornado.Codex;

namespace OpenSourceToolkit.NET.Services.Ai
{
    public sealed class LlmTornadoSubscriptionSessionFactory : IAiSubscriptionSessionFactory
    {
        private readonly ISecretStorage _secretStorage;

        public LlmTornadoSubscriptionSessionFactory(ISecretStorage secretStorage)
        {
            _secretStorage = secretStorage;
        }

        public async Task<IAiSubscriptionSession> ConnectAsync(
            AiAccessMode mode,
            CancellationToken cancellationToken)
        {
            var api = new TornadoApi();

            switch (mode)
            {
                case AiAccessMode.CodexAppServer:
                    return new AppServerSubscriptionSession(
                        await api.Codex.ConnectAsync(cancellationToken: cancellationToken));
                case AiAccessMode.CodexOAuth:
                    return new OAuthSubscriptionSession(
                        await api.Codex.ConnectOAuthAsync(
                            new CodexOAuthOptions
                            {
                                CredentialStore = new CodexOAuthSecureCredentialStore(_secretStorage)
                            },
                            cancellationToken));
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, "A subscription mode is required.");
            }
        }

        private static AiSubscriptionAccount MapAccount(CodexAccount account)
        {
            if (account == null)
                return null;

            return new AiSubscriptionAccount(account.Email ?? string.Empty, account.PlanType ?? string.Empty);
        }

        private static IReadOnlyList<AiSubscriptionModel> MapModels(IReadOnlyList<CodexModel> models)
        {
            return models
                .Select(model => new AiSubscriptionModel(
                    model.Model,
                    string.IsNullOrWhiteSpace(model.DisplayName) ? model.Model : model.DisplayName,
                    model.Description ?? string.Empty,
                    model.IsDefault,
                    model.DefaultReasoningEffort,
                    model.SupportedReasoningEfforts
                        .Select(effort => new AiSubscriptionReasoningEffort(
                            effort.ReasoningEffort,
                            effort.Description))
                        .ToList(),
                    model.DefaultServiceTier,
                    model.ServiceTiers
                        .Select(tier => new AiSubscriptionServiceTier(
                            tier.Id,
                            tier.DisplayName,
                            tier.Description))
                        .ToList()))
                .ToList();
        }

        private sealed class AppServerSubscriptionSession : IAiSubscriptionSession
        {
            private readonly CodexSession _session;

            public AppServerSubscriptionSession(CodexSession session)
            {
                _session = session;
            }

            public async Task<AiSubscriptionAccount> GetAccountAsync(CancellationToken cancellationToken)
            {
                var result = await _session.GetAccountAsync(cancellationToken: cancellationToken);
                return MapAccount(result.Account);
            }

            public async Task<AiSubscriptionLoginResult> LoginAsync(
                Func<Uri, Task<bool>> openBrowser,
                CancellationToken cancellationToken)
            {
                var login = await _session.StartBrowserLoginAsync(cancellationToken);
                bool opened;
                try
                {
                    opened = await openBrowser(login.AuthorizationUrl);
                }
                catch
                {
                    await login.CancelAsync(CancellationToken.None);
                    throw;
                }

                if (!opened)
                {
                    await login.CancelAsync(CancellationToken.None);
                    return new AiSubscriptionLoginResult(false, "The authorization URL could not be opened.");
                }

                var result = await login.WaitAsync(cancellationToken);
                return new AiSubscriptionLoginResult(result.Success, result.Error);
            }

            public async Task<IReadOnlyList<AiSubscriptionModel>> ListModelsAsync(
                CancellationToken cancellationToken)
                => MapModels(await _session.ListModelsAsync(cancellationToken: cancellationToken));

            public async Task<IAiSubscriptionThread> StartThreadAsync(
                string modelId,
                CancellationToken cancellationToken)
            {
                var thread = await _session.StartThreadAsync(
                    new CodexThreadOptions { Model = modelId },
                    cancellationToken);
                return new AppServerSubscriptionThread(thread);
            }

            public Task LogoutAsync(CancellationToken cancellationToken)
                => _session.LogoutAsync(cancellationToken);

            public ValueTask DisposeAsync()
                => _session.DisposeAsync();
        }

        private sealed class AppServerSubscriptionThread : IAiSubscriptionThread
        {
            private readonly CodexThread _thread;

            public AppServerSubscriptionThread(CodexThread thread)
            {
                _thread = thread;
            }

            public string ModelId => _thread.Model ?? string.Empty;

            public async Task<string> RunAsync(
                string input,
                string reasoningEffort,
                string serviceTier,
                Func<string, Task> onTextDelta,
                CancellationToken cancellationToken)
            {
                var result = await _thread.RunAsync(
                    input,
                    new CodexTurnOptions
                    {
                        ReasoningEffort = reasoningEffort,
                        ServiceTier = serviceTier,
                        OnTextDelta = delta => onTextDelta(delta.Delta)
                    },
                    cancellationToken);
                return result.FinalResponse;
            }
        }

        private sealed class OAuthSubscriptionSession : IAiSubscriptionSession
        {
            private readonly CodexOAuthSession _session;

            public OAuthSubscriptionSession(CodexOAuthSession session)
            {
                _session = session;
            }

            public async Task<AiSubscriptionAccount> GetAccountAsync(CancellationToken cancellationToken)
            {
                var result = await _session.GetAccountAsync(cancellationToken: cancellationToken);
                return MapAccount(result.Account);
            }

            public async Task<AiSubscriptionLoginResult> LoginAsync(
                Func<Uri, Task<bool>> openBrowser,
                CancellationToken cancellationToken)
            {
                var login = await _session.StartBrowserLoginAsync(cancellationToken);
                bool opened;
                try
                {
                    opened = await openBrowser(login.AuthorizationUrl);
                }
                catch
                {
                    await login.CancelAsync();
                    throw;
                }

                if (!opened)
                {
                    await login.CancelAsync();
                    return new AiSubscriptionLoginResult(false, "The authorization URL could not be opened.");
                }

                var result = await login.WaitAsync(cancellationToken);
                return new AiSubscriptionLoginResult(result.Success, result.Error);
            }

            public async Task<IReadOnlyList<AiSubscriptionModel>> ListModelsAsync(
                CancellationToken cancellationToken)
                => MapModels(await _session.ListModelsAsync(cancellationToken: cancellationToken));

            public async Task<IAiSubscriptionThread> StartThreadAsync(
                string modelId,
                CancellationToken cancellationToken)
            {
                var thread = await _session.StartThreadAsync(
                    new CodexOAuthThreadOptions { Model = modelId },
                    cancellationToken);
                return new OAuthSubscriptionThread(thread);
            }

            public Task LogoutAsync(CancellationToken cancellationToken)
                => _session.LogoutAsync(cancellationToken);

            public ValueTask DisposeAsync()
                => _session.DisposeAsync();
        }

        private sealed class OAuthSubscriptionThread : IAiSubscriptionThread
        {
            private readonly CodexOAuthThread _thread;

            public OAuthSubscriptionThread(CodexOAuthThread thread)
            {
                _thread = thread;
            }

            public string ModelId => _thread.Model;

            public async Task<string> RunAsync(
                string input,
                string reasoningEffort,
                string serviceTier,
                Func<string, Task> onTextDelta,
                CancellationToken cancellationToken)
            {
                var result = await _thread.RunAsync(
                    input,
                    new CodexOAuthTurnOptions
                    {
                        ReasoningEffort = reasoningEffort,
                        ServiceTier = serviceTier,
                        OnTextDelta = delta => onTextDelta(delta.Delta)
                    },
                    cancellationToken);
                return result.FinalResponse;
            }
        }
    }
}
