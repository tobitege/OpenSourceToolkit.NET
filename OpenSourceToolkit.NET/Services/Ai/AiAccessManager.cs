using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OpenSourceToolkit.NET.Services.Ai
{
    public sealed class AiAccessManager : IDisposable, IAsyncDisposable
    {
        private readonly IAiSubscriptionSessionFactory _sessionFactory;
        private readonly SemaphoreSlim _transitionLock = new SemaphoreSlim(1, 1);
        private IAiSubscriptionSession _session;
        private IAiSubscriptionThread _thread;
        private string _threadModelId;
        private bool _disposed;

        public AiAccessManager(IAiSubscriptionSessionFactory sessionFactory)
            : this(sessionFactory, AiAccessMode.OpenAiApi)
        {
        }

        public AiAccessManager(
            IAiSubscriptionSessionFactory sessionFactory,
            AiAccessMode initialMode)
        {
            _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
            if (!Enum.IsDefined(typeof(AiAccessMode), initialMode))
                throw new ArgumentOutOfRangeException(nameof(initialMode));

            Mode = initialMode;
            SubscriptionModels = Array.Empty<AiSubscriptionModel>();
        }

        public event EventHandler StateChanged;

        public AiAccessMode Mode { get; private set; }
        public AiSubscriptionAccount Account { get; private set; }
        public IReadOnlyList<AiSubscriptionModel> SubscriptionModels { get; private set; }
        public string SelectedSubscriptionModelId { get; private set; }
        public string SelectedSubscriptionReasoningEffort { get; private set; }
        public string SelectedSubscriptionServiceTier { get; private set; }
        public bool IsAuthenticated => Account != null;

        public AiAccessCapabilities Capabilities
        {
            get
            {
                switch (Mode)
                {
                    case AiAccessMode.OpenAiApi:
                        return new AiAccessCapabilities(true, true, true, false);
                    case AiAccessMode.CodexAppServer:
                        return new AiAccessCapabilities(false, true, false, true);
                    case AiAccessMode.CodexOAuth:
                        return new AiAccessCapabilities(false, true, false, false);
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public async Task SwitchModeAsync(
            AiAccessMode mode,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            await _transitionLock.WaitAsync(cancellationToken);
            try
            {
                if (Mode != mode)
                {
                    await DisposeSessionAsync();
                    Mode = mode;
                    ClearSubscriptionState();
                }

                if (Mode != AiAccessMode.OpenAiApi)
                {
                    if (_session == null)
                        _session = await _sessionFactory.ConnectAsync(Mode, cancellationToken);

                    await RefreshAccountAndModelsAsync(cancellationToken);
                }

                OnStateChanged();
            }
            catch
            {
                await DisposeSessionAsync();
                ClearSubscriptionState();
                OnStateChanged();
                throw;
            }
            finally
            {
                _transitionLock.Release();
            }
        }

        public async Task<AiSubscriptionLoginResult> LoginAsync(
            Func<Uri, Task<bool>> openBrowser,
            CancellationToken cancellationToken = default)
        {
            if (openBrowser == null)
                throw new ArgumentNullException(nameof(openBrowser));

            ThrowIfDisposed();
            await _transitionLock.WaitAsync(cancellationToken);
            try
            {
                EnsureSubscriptionMode();
                if (_session == null)
                    _session = await _sessionFactory.ConnectAsync(Mode, cancellationToken);

                var result = await _session.LoginAsync(openBrowser, cancellationToken);
                if (result.Success)
                {
                    await RefreshAccountAndModelsAsync(cancellationToken);
                    OnStateChanged();
                }
                return result;
            }
            finally
            {
                _transitionLock.Release();
            }
        }

        public async Task LogoutAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            await _transitionLock.WaitAsync(cancellationToken);
            try
            {
                try
                {
                    if (_session != null)
                        await _session.LogoutAsync(cancellationToken);
                }
                finally
                {
                    try
                    {
                        await DisposeSessionAsync();
                    }
                    finally
                    {
                        ClearSubscriptionState();
                        OnStateChanged();
                    }
                }
            }
            finally
            {
                _transitionLock.Release();
            }
        }

        public void SelectSubscriptionModel(string modelId)
        {
            ThrowIfDisposed();
            EnsureSubscriptionMode();

            if (!SubscriptionModels.Any(model =>
                    string.Equals(model.ModelId, modelId, StringComparison.Ordinal)))
            {
                throw new ArgumentException("The model is not in the active subscription catalog.", nameof(modelId));
            }

            if (!string.Equals(SelectedSubscriptionModelId, modelId, StringComparison.Ordinal))
            {
                SelectedSubscriptionModelId = modelId;
                SelectModelDefaults(FindSelectedSubscriptionModel());
                ResetThread();
                OnStateChanged();
            }
        }

        public void SelectSubscriptionReasoningEffort(string reasoningEffort)
        {
            ThrowIfDisposed();
            EnsureSubscriptionMode();

            var model = FindSelectedSubscriptionModel();
            if (model == null ||
                !model.SupportedReasoningEfforts.Any(effort =>
                    string.Equals(effort.Id, reasoningEffort, StringComparison.Ordinal)))
            {
                throw new ArgumentException(
                    "The reasoning effort is not supported by the selected subscription model.",
                    nameof(reasoningEffort));
            }

            if (!string.Equals(
                    SelectedSubscriptionReasoningEffort,
                    reasoningEffort,
                    StringComparison.Ordinal))
            {
                SelectedSubscriptionReasoningEffort = reasoningEffort;
                OnStateChanged();
            }
        }

        public void SelectSubscriptionServiceTier(string serviceTier)
        {
            ThrowIfDisposed();
            EnsureSubscriptionMode();

            var normalizedServiceTier =
                string.IsNullOrWhiteSpace(serviceTier) ? null : serviceTier;
            var model = FindSelectedSubscriptionModel();
            if (model == null ||
                (normalizedServiceTier != null &&
                 !model.ServiceTiers.Any(tier =>
                     string.Equals(tier.Id, normalizedServiceTier, StringComparison.Ordinal))))
            {
                throw new ArgumentException(
                    "The service tier is not supported by the selected subscription model.",
                    nameof(serviceTier));
            }

            if (!string.Equals(
                    SelectedSubscriptionServiceTier,
                    normalizedServiceTier,
                    StringComparison.Ordinal))
            {
                SelectedSubscriptionServiceTier = normalizedServiceTier;
                OnStateChanged();
            }
        }

        public async Task<string> RunSubscriptionTurnAsync(
            string input,
            Func<string, Task> onTextDelta,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            EnsureSubscriptionMode();
            if (_session == null || !IsAuthenticated)
                throw new InvalidOperationException("Sign in to ChatGPT before sending a Codex message.");
            if (string.IsNullOrWhiteSpace(SelectedSubscriptionModelId))
                throw new InvalidOperationException("Select a Codex subscription model first.");

            if (_thread == null ||
                !string.Equals(_threadModelId, SelectedSubscriptionModelId, StringComparison.Ordinal))
            {
                _thread = await _session.StartThreadAsync(SelectedSubscriptionModelId, cancellationToken);
                _threadModelId = SelectedSubscriptionModelId;
            }

            return await _thread.RunAsync(
                input,
                SelectedSubscriptionReasoningEffort,
                SelectedSubscriptionServiceTier,
                onTextDelta,
                cancellationToken);
        }

        public void ResetThread()
        {
            _thread = null;
            _threadModelId = null;
        }

        private async Task RefreshAccountAndModelsAsync(CancellationToken cancellationToken)
        {
            Account = await _session.GetAccountAsync(cancellationToken);
            ResetThread();

            if (Account == null)
            {
                SubscriptionModels = Array.Empty<AiSubscriptionModel>();
                SelectedSubscriptionModelId = null;
                SelectedSubscriptionReasoningEffort = null;
                SelectedSubscriptionServiceTier = null;
                return;
            }

            SubscriptionModels = await _session.ListModelsAsync(cancellationToken);
            SelectedSubscriptionModelId =
                SubscriptionModels.FirstOrDefault(model => model.IsDefault)?.ModelId
                ?? SubscriptionModels.FirstOrDefault()?.ModelId;
            SelectModelDefaults(FindSelectedSubscriptionModel());
        }

        private AiSubscriptionModel FindSelectedSubscriptionModel()
            => SubscriptionModels.FirstOrDefault(model =>
                string.Equals(
                    model.ModelId,
                    SelectedSubscriptionModelId,
                    StringComparison.Ordinal));

        private void SelectModelDefaults(AiSubscriptionModel model)
        {
            SelectedSubscriptionReasoningEffort =
                model?.SupportedReasoningEfforts.FirstOrDefault(effort =>
                    string.Equals(
                        effort.Id,
                        model.DefaultReasoningEffort,
                        StringComparison.Ordinal))?.Id
                ?? model?.SupportedReasoningEfforts.FirstOrDefault()?.Id;
            SelectedSubscriptionServiceTier =
                model?.ServiceTiers.FirstOrDefault(tier =>
                    string.Equals(
                        tier.Id,
                        model.DefaultServiceTier,
                        StringComparison.Ordinal))?.Id;
        }

        private void EnsureSubscriptionMode()
        {
            if (Mode == AiAccessMode.OpenAiApi)
                throw new InvalidOperationException("The OpenAI API mode does not use a ChatGPT subscription session.");
        }

        private void ClearSubscriptionState()
        {
            Account = null;
            SubscriptionModels = Array.Empty<AiSubscriptionModel>();
            SelectedSubscriptionModelId = null;
            SelectedSubscriptionReasoningEffort = null;
            SelectedSubscriptionServiceTier = null;
            ResetThread();
        }

        private async ValueTask DisposeSessionAsync()
        {
            ResetThread();
            if (_session == null)
                return;

            var session = _session;
            _session = null;
            await session.DisposeAsync();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AiAccessManager));
        }

        private void OnStateChanged()
            => StateChanged?.Invoke(this, EventArgs.Empty);

        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;

            _disposed = true;
            await _transitionLock.WaitAsync();
            try
            {
                await DisposeSessionAsync();
                ClearSubscriptionState();
            }
            finally
            {
                _transitionLock.Release();
                _transitionLock.Dispose();
            }
        }
    }
}
