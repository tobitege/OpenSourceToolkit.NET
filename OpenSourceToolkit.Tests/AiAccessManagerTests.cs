using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using LlmTornado.Codex;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSourceToolkit.NET.Services.Ai;

namespace OpenSourceToolkit.Tests
{
    [TestClass]
    public class AiAccessManagerTests
    {
        [TestMethod]
        public async Task SubscriptionModes_UseDynamicModelsAndNeverExposeImageGeneration()
        {
            var session = new FakeSubscriptionSession
            {
                Account = new AiSubscriptionAccount("user@example.com", "plus"),
                Models = new[]
                {
                    new AiSubscriptionModel("codex-default", "Default", "", true),
                    new AiSubscriptionModel("codex-other", "Other", "", false)
                }
            };
            var factory = new FakeSubscriptionSessionFactory(session);
            await using var manager = new AiAccessManager(factory);

            await manager.SwitchModeAsync(AiAccessMode.CodexOAuth);

            Assert.AreEqual(AiAccessMode.CodexOAuth, factory.LastMode);
            Assert.IsTrue(manager.IsAuthenticated);
            Assert.AreEqual(2, manager.SubscriptionModels.Count);
            Assert.AreEqual("codex-default", manager.SelectedSubscriptionModelId);
            Assert.IsFalse(manager.Capabilities.UsesApiConnection);
            Assert.IsTrue(manager.Capabilities.SupportsText);
            Assert.IsFalse(manager.Capabilities.SupportsImageGeneration);
        }

        [TestMethod]
        public async Task Constructor_RestoresSavedAccessModeBeforeSilentReconnect()
        {
            var session = new FakeSubscriptionSession
            {
                Account = new AiSubscriptionAccount("user@example.com", "plus"),
                Models = new[] { new AiSubscriptionModel("model", "Model", "", true) }
            };
            var factory = new FakeSubscriptionSessionFactory(session);
            await using var manager = new AiAccessManager(factory, AiAccessMode.CodexOAuth);

            Assert.AreEqual(AiAccessMode.CodexOAuth, manager.Mode);
            Assert.IsFalse(manager.IsAuthenticated);

            await manager.SwitchModeAsync(manager.Mode);

            Assert.AreEqual(AiAccessMode.CodexOAuth, factory.LastMode);
            Assert.IsTrue(manager.IsAuthenticated);
            Assert.AreEqual("model", manager.SelectedSubscriptionModelId);
        }

        [TestMethod]
        public void MissingSavedMode_UsesStoredOAuthCredentialsOnlyForMigration()
        {
            var resolveSavedMode = typeof(AiAccessServices).GetMethod(
                "ResolveSavedMode",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(resolveSavedMode);
            Assert.AreEqual(
                AiAccessMode.CodexOAuth,
                resolveSavedMode.Invoke(null, new object[] { null, true }));
            Assert.AreEqual(
                AiAccessMode.OpenAiApi,
                resolveSavedMode.Invoke(null, new object[] { null, false }));
            Assert.AreEqual(
                AiAccessMode.OpenAiApi,
                resolveSavedMode.Invoke(
                    null,
                    new object[] { AiAccessMode.OpenAiApi, true }));
        }

        [TestMethod]
        public async Task SubscriptionStateChanges_ArePublishedForSettingsAndAssistantViews()
        {
            var session = new FakeSubscriptionSession
            {
                Account = new AiSubscriptionAccount("user@example.com", "plus"),
                Models = new[]
                {
                    new AiSubscriptionModel("first", "First", "", true),
                    new AiSubscriptionModel("second", "Second", "", false)
                }
            };
            var factory = new FakeSubscriptionSessionFactory(session);
            await using var manager = new AiAccessManager(factory);
            var stateChangeCount = 0;
            manager.StateChanged += (_, _) => stateChangeCount++;

            await manager.SwitchModeAsync(AiAccessMode.CodexOAuth);
            manager.SelectSubscriptionModel("second");
            await manager.LogoutAsync();

            Assert.AreEqual(3, stateChangeCount);
        }

        [TestMethod]
        public async Task SwitchingSubscriptionMode_DisposesOldSessionAndLoadsNewCatalog()
        {
            var oauth = new FakeSubscriptionSession
            {
                Account = new AiSubscriptionAccount("oauth@example.com", "plus"),
                Models = new[] { new AiSubscriptionModel("oauth-model", "OAuth", "", true) }
            };
            var appServer = new FakeSubscriptionSession
            {
                Account = new AiSubscriptionAccount("app@example.com", "team"),
                Models = new[] { new AiSubscriptionModel("app-model", "App", "", true) }
            };
            var factory = new FakeSubscriptionSessionFactory(oauth, appServer);
            await using var manager = new AiAccessManager(factory);

            await manager.SwitchModeAsync(AiAccessMode.CodexOAuth);
            await manager.SwitchModeAsync(AiAccessMode.CodexAppServer);

            Assert.IsTrue(oauth.IsDisposed);
            Assert.AreEqual("app-model", manager.SelectedSubscriptionModelId);
            Assert.IsTrue(manager.Capabilities.RequiresCodexInstallation);
        }

        [TestMethod]
        public async Task ReconnectingCurrentSubscriptionMode_ReloadsModelCatalog()
        {
            var session = new FakeSubscriptionSession
            {
                Account = new AiSubscriptionAccount("user@example.com", "plus"),
                Models = new[] { new AiSubscriptionModel("first", "First", "", true) }
            };
            var factory = new FakeSubscriptionSessionFactory(session);
            await using var manager = new AiAccessManager(factory);
            await manager.SwitchModeAsync(AiAccessMode.CodexOAuth);

            session.Models = new[] { new AiSubscriptionModel("second", "Second", "", true) };
            await manager.SwitchModeAsync(AiAccessMode.CodexOAuth);

            Assert.AreEqual(2, session.ListModelsCallCount);
            Assert.AreEqual("second", manager.SelectedSubscriptionModelId);
        }

        [TestMethod]
        public async Task SubscriptionThread_IsReusedForFollowUpsAndResetForModelChange()
        {
            var session = new FakeSubscriptionSession
            {
                Account = new AiSubscriptionAccount("user@example.com", "plus"),
                Models = new[]
                {
                    new AiSubscriptionModel("first", "First", "", true),
                    new AiSubscriptionModel("second", "Second", "", false)
                }
            };
            var factory = new FakeSubscriptionSessionFactory(session);
            await using var manager = new AiAccessManager(factory);
            await manager.SwitchModeAsync(AiAccessMode.CodexOAuth);
            var deltas = new List<string>();

            await manager.RunSubscriptionTurnAsync("one", delta =>
            {
                deltas.Add(delta);
                return Task.CompletedTask;
            });
            await manager.RunSubscriptionTurnAsync("two", _ => Task.CompletedTask);

            Assert.AreEqual(1, session.StartThreadCount);
            Assert.AreEqual(2, session.Thread.RunCount);
            CollectionAssert.Contains(deltas, "one");

            manager.SelectSubscriptionModel("second");
            await manager.RunSubscriptionTurnAsync("three", _ => Task.CompletedTask);

            Assert.AreEqual(2, session.StartThreadCount);
        }

        [TestMethod]
        public async Task SubscriptionTurn_UsesModelReasoningEffortAndServiceTierSelections()
        {
            var session = new FakeSubscriptionSession
            {
                Account = new AiSubscriptionAccount("user@example.com", "plus"),
                Models = new[]
                {
                    new AiSubscriptionModel(
                        "codex-model",
                        "Codex",
                        "",
                        true,
                        "medium",
                        new[]
                        {
                            new AiSubscriptionReasoningEffort("low", "Fast"),
                            new AiSubscriptionReasoningEffort("medium", "Balanced"),
                            new AiSubscriptionReasoningEffort("high", "Deep")
                        },
                        null,
                        new[]
                        {
                            new AiSubscriptionServiceTier("priority", "Fast", "1.5x speed")
                        })
                }
            };
            var factory = new FakeSubscriptionSessionFactory(session);
            await using var manager = new AiAccessManager(factory);
            await manager.SwitchModeAsync(AiAccessMode.CodexOAuth);

            Assert.AreEqual("medium", manager.SelectedSubscriptionReasoningEffort);
            Assert.IsNull(manager.SelectedSubscriptionServiceTier);

            manager.SelectSubscriptionReasoningEffort("high");
            manager.SelectSubscriptionServiceTier("priority");
            await manager.RunSubscriptionTurnAsync("test", _ => Task.CompletedTask);

            Assert.AreEqual("high", session.Thread.LastReasoningEffort);
            Assert.AreEqual("priority", session.Thread.LastServiceTier);
        }

        [TestMethod]
        public async Task Logout_ClearsSubscriptionStateAndDisposesSession()
        {
            var session = new FakeSubscriptionSession
            {
                Account = new AiSubscriptionAccount("user@example.com", "plus"),
                Models = new[] { new AiSubscriptionModel("model", "Model", "", true) }
            };
            var factory = new FakeSubscriptionSessionFactory(session);
            await using var manager = new AiAccessManager(factory);
            await manager.SwitchModeAsync(AiAccessMode.CodexOAuth);

            await manager.LogoutAsync();

            Assert.IsTrue(session.LogoutCalled);
            Assert.IsTrue(session.IsDisposed);
            Assert.IsFalse(manager.IsAuthenticated);
            Assert.AreEqual(0, manager.SubscriptionModels.Count);
            Assert.IsNull(manager.SelectedSubscriptionModelId);
        }

        [TestMethod]
        public async Task LogoutFailure_StillClearsSubscriptionStateAndDisposesSession()
        {
            var session = new FakeSubscriptionSession
            {
                Account = new AiSubscriptionAccount("user@example.com", "plus"),
                Models = new[] { new AiSubscriptionModel("model", "Model", "", true) },
                LogoutException = new InvalidOperationException("logout failed")
            };
            var factory = new FakeSubscriptionSessionFactory(session);
            await using var manager = new AiAccessManager(factory);
            await manager.SwitchModeAsync(AiAccessMode.CodexOAuth);

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => manager.LogoutAsync());

            Assert.IsTrue(session.LogoutCalled);
            Assert.IsTrue(session.IsDisposed);
            Assert.IsFalse(manager.IsAuthenticated);
            Assert.AreEqual(0, manager.SubscriptionModels.Count);
            Assert.IsNull(manager.SelectedSubscriptionModelId);
        }

        [TestMethod]
        public async Task OAuthCredentialStore_UsesAppSecretStorageAndClearsOnlyItsKey()
        {
            var secrets = new FakeSecretStorage();
            var store = new CodexOAuthSecureCredentialStore(secrets);
            var credentials = new CodexOAuthCredentials
            {
                AccessToken = "access-secret",
                IdToken = "id-secret",
                RefreshToken = "refresh-secret",
                AccountId = "account-1",
                LastRefreshUtc = DateTimeOffset.UtcNow
            };

            await store.SaveAsync(credentials);
            var loaded = await store.LoadAsync();

            Assert.AreEqual("access-secret", loaded.AccessToken);
            Assert.AreEqual("account-1", loaded.AccountId);
            Assert.IsTrue(secrets.Contains("codex.oauth.credentials"));

            await store.ClearAsync();

            Assert.IsFalse(secrets.Contains("codex.oauth.credentials"));
        }

        [TestMethod]
        public void OAuthFactory_LeavesCodexProtocolVersionToLlmTornado()
        {
            var field = typeof(LlmTornadoSubscriptionSessionFactory).GetField(
                "CodexOAuthClientVersion",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNull(field);

            var options = new CodexOAuthOptions();
            Assert.IsFalse(string.IsNullOrWhiteSpace(options.CodexProtocolVersion));
            Assert.AreEqual(
                CodexOAuthOptions.DefaultCodexProtocolVersion,
                options.CodexProtocolVersion);
        }

        private sealed class FakeSubscriptionSessionFactory : IAiSubscriptionSessionFactory
        {
            private readonly Queue<IAiSubscriptionSession> _sessions;

            public FakeSubscriptionSessionFactory(params IAiSubscriptionSession[] sessions)
            {
                _sessions = new Queue<IAiSubscriptionSession>(sessions);
            }

            public AiAccessMode LastMode { get; private set; }

            public Task<IAiSubscriptionSession> ConnectAsync(
                AiAccessMode mode,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                LastMode = mode;
                return Task.FromResult(_sessions.Dequeue());
            }
        }

        private sealed class FakeSubscriptionSession : IAiSubscriptionSession
        {
            public AiSubscriptionAccount Account { get; set; }
            public IReadOnlyList<AiSubscriptionModel> Models { get; set; } =
                Array.Empty<AiSubscriptionModel>();
            public FakeSubscriptionThread Thread { get; } = new FakeSubscriptionThread();
            public int StartThreadCount { get; private set; }
            public int ListModelsCallCount { get; private set; }
            public bool LogoutCalled { get; private set; }
            public bool IsDisposed { get; private set; }
            public Exception LogoutException { get; set; }

            public Task<AiSubscriptionAccount> GetAccountAsync(CancellationToken cancellationToken)
                => Task.FromResult(Account);

            public Task<AiSubscriptionLoginResult> LoginAsync(
                Func<Uri, Task<bool>> openBrowser,
                CancellationToken cancellationToken)
                => Task.FromResult(new AiSubscriptionLoginResult(true, null));

            public Task<IReadOnlyList<AiSubscriptionModel>> ListModelsAsync(
                CancellationToken cancellationToken)
            {
                ListModelsCallCount++;
                return Task.FromResult(Models);
            }

            public Task<IAiSubscriptionThread> StartThreadAsync(
                string modelId,
                CancellationToken cancellationToken)
            {
                StartThreadCount++;
                Thread.ModelIdValue = modelId;
                return Task.FromResult<IAiSubscriptionThread>(Thread);
            }

            public Task LogoutAsync(CancellationToken cancellationToken)
            {
                LogoutCalled = true;
                if (LogoutException != null)
                    return Task.FromException(LogoutException);
                return Task.CompletedTask;
            }

            public ValueTask DisposeAsync()
            {
                IsDisposed = true;
                return default;
            }
        }

        private sealed class FakeSubscriptionThread : IAiSubscriptionThread
        {
            public string ModelIdValue { get; set; }
            public string ModelId => ModelIdValue;
            public int RunCount { get; private set; }
            public string LastReasoningEffort { get; private set; }
            public string LastServiceTier { get; private set; }

            public async Task<string> RunAsync(
                string input,
                string reasoningEffort,
                string serviceTier,
                Func<string, Task> onTextDelta,
                CancellationToken cancellationToken)
            {
                RunCount++;
                LastReasoningEffort = reasoningEffort;
                LastServiceTier = serviceTier;
                await onTextDelta(input);
                return input;
            }
        }

        private sealed class FakeSecretStorage : ISecretStorage
        {
            private readonly Dictionary<string, string> _values = new Dictionary<string, string>();

            public void Store(string key, string value)
            {
                if (value == null)
                    _values.Remove(key);
                else
                    _values[key] = value;
            }

            public string Retrieve(string key)
                => _values.TryGetValue(key, out var value) ? value : null;

            public void Remove(string key)
                => _values.Remove(key);

            public bool Contains(string key)
                => _values.ContainsKey(key);

            public void Clear()
                => _values.Clear();
        }
    }
}
