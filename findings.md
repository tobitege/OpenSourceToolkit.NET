# Findings and Decisions

## 2026-07-26 - OpenAI-Compatible connection

- The desktop connection enum and provider lists have no generic Custom/OpenAI-Compatible entry.
- LLMTornado already supports `TornadoApi(Uri, apiKey, LLmProviders.Custom)`, so no LLMTornado source change is needed.
- The provider Settings page stores editable endpoints globally, but the desktop OpenAI route ignores that endpoint and constructs the fixed OpenAI provider.
- The connection data model already contains `CustomEndpoint`, but the connection editor does not expose or persist it.
- The standalone `OpenSourceToolkit.AI` library already sends OpenAI-compatible requests to `Settings.Endpoint`; the desktop app uses a separate LLMTornado-based path.
- The new type should keep official OpenAI and Codex semantics unchanged, store Base URL per connection, allow an empty key for local servers, and use free model input.
- `AiSettingsManager.CreateConfigFromConnection` already resolves `AiConnection.CustomEndpoint` before the provider endpoint, so the persisted connection shape is prepared for per-connection Base URLs.
- `AiSettingsManager.AddConnection` currently accepts only name, provider, model, and key; Settings save/edit does not write `CustomEndpoint`.
- `AiAssistantViewModel` already constructs custom-URI `TornadoApi` instances, with and without keys, whenever a provider maps to `LLmProviders.Custom`.
- The required Assistant routing change is therefore limited to mapping the new provider enum value to `Custom`; existing URI construction can be reused.
- Both runtime `AiConnection` and serialized `AiConnectionData` already carry `CustomEndpoint`, including clone support.
- `AiConnectionViewModel` has no endpoint property, and the connection editor has no endpoint edit/original fields, so create/edit/dirty tracking must be extended end to end.
- The free-text model picker already supports arbitrary model IDs; the new provider should use an empty default catalog rather than introducing invented model names.
- Use `OpenAICompatible` as the enum and persisted identifier, but expose the user-facing connection label as `OpenAI-Compatible`.
- The existing string-based provider selector can persist/display `OpenAI-Compatible` directly; both enum parsers need an explicit mapping to `OpenAICompatible`.
- Keep `OpenAICompatible` out of `SupportedProviders`: it has no meaningful shared provider settings because Base URL and optional key belong to each named connection.
- Existing AppSettings synchronization already round-trips `CustomEndpoint`, so no settings schema migration is required.
- Model discovery can reuse `api.Models.GetModels(...)` once the new provider maps to `LLmProviders.Custom`.
- The first assumed `AiAssistantViewModel.cs` path was invalid and must be resolved with `rg --files` before access.
- The exact Assistant path is `OpenSourceToolkit.NET/ViewModels/Tools/ImageConverter/AiAssistantViewModel.cs`.
- The implemented contract uses persisted/display string `OpenAI-Compatible` and enum `OpenAICompatible`, with explicit parser mappings.
- Compatible connections accept only absolute HTTP or HTTPS Base URLs; the API key remains optional.
- The new enum member is appended to preserve existing numeric enum values.
- Existing compatible connections with a stored key keep the established explicit override flow; new/keyless compatible connections show the optional key input directly.
- Add the Base URL row directly after Provider and shift Model/API/options/actions down one row; the existing two-column form then aligns the Base URL and test action naturally with the model column.

## 2026-07-26: Discard reopens the unsaved-changes dialog

- `SettingsWindow.OnClosing` cancels the first close while awaiting `SettingsViewModel.CanCloseAsync`, then calls `Close()` again after approval.
- The Save branch calls `SaveConnection`, which clears dirty state before the second close.
- The Discard branch returned `true` without clearing `IsEditingConnection` and `IsAddingConnection`, so the second close still detected unsaved changes and displayed the dialog again.
- Discard must reset connection-edit state and return `false` so the warning closes but the Settings window remains open; the edited values are only editor fields and have not yet mutated the persisted connection.
- The first real UI validation exposed an incorrect semantic assumption: it proved the repeated prompt was gone, but also showed that Settings closed after Discard. The corrected requirement is to keep Settings open.
- Final UI validation confirmed that Discard dismisses the warning, exits the connection editor, and leaves Settings open.
- `CancelConnectionCommand` already implemented the required editor reset; only the missing `DaisyButton` binding beside Save caused the UI gap.
- The original cancel command discarded edits synchronously, so wiring it directly to the new button bypassed the prompt.
- New-connection dirty tracking also treated the preselected provider as a change. Capturing the actual initial provider/model/defaults and comparing all editor values now distinguishes pristine initialization from user edits.
- Real UI validation confirmed both cases: pristine Abbrechen returns immediately, while changing the name first opens the Save/Discard/Cancel prompt.
- The connection editor action order is Speichern followed by Abbrechen; Abbrechen uses Daisy's red `Error` variant.

## 2026-07-26: LLMTornado PR branch alignment

- The local LLMTornado fork is clean on `feat_openai_codex_subscription_auth` at commit `afc32fe476fc10d291d54e626c6cb20cccf47d03`.
- The PR API now owns the Codex catalog protocol version through `CodexOAuthOptions.CodexProtocolVersion` and `DefaultCodexProtocolVersion`.
- The Toolkit still assigns the former hardcoded protocol value to `ClientVersion`; that property now correctly represents only backend headers and the user agent.
- Service-tier model metadata and turn options already match the PR API for both app-server and OAuth paths.
- The Toolkit should stop overriding `ClientVersion` and rely on LLMTornado's protocol-version default.
- The final Toolkit implementation leaves `ClientVersion` and `CodexProtocolVersion` untouched, so LLMTornado owns both defaults according to its public API.
- The Release solution resolves and builds the local LLMTornado project successfully; all 225 Toolkit tests pass against it.

## 2026-07-26: Compact provider title and Codex model row

- `Settings_Providers_Title` is used only by the left provider-list card and currently names API keys even though the list also represents subscription access.
- The Codex model ComboBox item template contains separate display-name and model-ID TextBlocks, making the selected value two lines high.
- The requested layout is one localized provider title and one display-name-only Codex model row.
- Real UI validation confirms the German title is `Anbieter` and the closed Codex model selector contains only `GPT-5.6-Sol`, without a second model-ID line.

## 2026-07-26: OpenAI access-mode persistence and blank Fast selection

- OAuth credentials are persisted independently in secure storage, but `AiAccessManager` is recreated in `OpenAiApi` mode on every process start.
- The Settings authentication ComboBox therefore reflects only the manager's current in-memory mode; the selected access mode is not part of `SettingsData`.
- Restoring a subscription mode must also reconnect silently so the persisted OAuth account and model catalog are projected without opening a browser.
- Existing installations do not yet have the new setting. If that field is absent and secure OAuth credentials exist, the one-time migration selects Browser-OAuth; an explicitly saved API choice always remains API.
- Selecting Fast calls `SelectSubscriptionServiceTier`, which raises `StateChanged` synchronously. The Settings handler immediately clears and repopulates the ComboBox's source collection while Avalonia is still committing the selection, leaving the closed selection box empty.
- Defer manager-to-Settings synchronization through the UI dispatcher so the ComboBox selection transaction can finish before its option collection is rebuilt.
- Real restart validation confirmed Browser-OAuth survives two process starts, restores the connected account and model catalog without opening a browser, and writes enum value `2` to `AiSettings.OpenAiAccessMode`.
- Real UI validation confirmed selecting Fast leaves `Fast` visible in the closed speed ComboBox.

## 2026-07-26: Subscription connect state, effort, and speed

- The user confirms direct OAuth models now load.
- `Verbinden` must use the Primary variant and remain visible but disabled while the subscription is already connected.
- Reasoning effort should be driven by the selected model's advertised levels. Speed should be exposed only if the backend catalog and LLMTornado surface a corresponding capability.
- The Assistant must retain the selector for already configured AI connections. Authentication mode, browser login, logout, and OAuth account management must remain exclusive to Settings.
- LLMTornado already exposes each Codex model's supported/default reasoning effort and accepts a turn-level reasoning effort for both app-server and OAuth sessions; the toolkit currently discards that metadata and does not pass the selected value.
- The current Codex model catalog advertises `service_tiers`, but LLMTornado's Codex model and turn option types do not currently surface any service-tier/speed value.
- The official Codex protocol defines model `service_tiers`, a default service tier, and a turn-level service-tier override. LLMTornado now maps those fields and sends `serviceTier` through app-server turns or `service_tier` through direct OAuth Responses requests.
- The Assistant connection selector remains visible in every access mode; it still contains no authentication, login, logout, or subscription-model controls.
- Final validation: 6 LLMTornado Codex tests and all 222 app tests pass. The Release solution build against the local LLMTornado ProjectReference has 0 errors; its warnings are pre-existing LLMTornado warnings.

## 2026-07-25: OAuth subscription model discovery failure

- The screenshot shows direct browser OAuth selected, an authenticated-state check icon and Logout action, but an empty Codex model picker.
- The visible error is `Anmeldung fehlgeschlagen: Codex model discovery failed.` This means OAuth completion produced an account, then model retrieval threw before the catalog was committed.
- `AiAccessManager.RefreshAccountAndModelsAsync` currently assigns `Account` before awaiting `ListModelsAsync`. A model-discovery exception therefore leaves a partially updated state: authenticated UI plus a login-failure message and no models.
- The official Codex implementation and an official issue confirm that the current catalog URL shape is `GET /backend-api/codex/models?client_version=...`; investigation therefore focuses on authentication/account headers, client metadata, and the actual HTTP response rather than changing the endpoint path.
- The user confirms that the same model list was populated before authentication controls were removed from the Assistant. This makes a new backend incompatibility unlikely and identifies the current behavior as a local regression across the UI removal/rebuild/restart.
- The secure credential adapter serializes the complete public `CodexOAuthCredentials` object, including `AccountId`; a persisted-account-header loss remains possible only if the stored payload itself lacks that value and must be checked without exposing token contents.
- The direct OAuth session used the LLMTornado assembly/package version `3.8.64` as the Codex `client_version`. The current local Codex model cache records protocol version `0.146.0`; sending the package version is the API mismatch behind the catalog failure.
- The production adapter now supplies Codex protocol version `0.146.0`. No credential value or response body was logged.
- Selecting the current subscription mode previously skipped refresh whenever its session already existed, and the Connect action disappeared after the account was recognized. Same-mode Connect now reloads account and models, and remains available alongside Logout as an explicit retry.
- A live Release-app check with the existing secure credentials now reports the connected account and loads `GPT-5.6-Sol` (`gpt-5.6-sol`) in the Browser-OAuth model picker.

## 2026-07-25: OAuth completion does not update OpenAI Settings

- The user completed sign-in on the OpenAI browser page, but the Settings subscription card remained visually unchanged: model selection stayed populated and all three actions (`Verbinden`, `Anmelden`, `Abmelden`) kept their previous presentation.
- The defect could be either a missing browser-to-app completion signal or a successful session update that is not projected into the Settings UI. The full browser-return path must be traced before changing view state.
- The follow-up screenshot confirms a separate placement defect: authentication mode, Codex model, account status, connect, sign-in, and logout controls are visible inside the AI Assistant. The user explicitly requires all authentication UI to exist only in OpenAI Settings.
- The Assistant may consume the shared authenticated session and selected model, but it must not own or expose authentication controls.
- The Assistant screenshot shows `Connected as ...` after the browser login. This proves the browser callback reached LLMTornado and the shared `AiAccessManager` account state was updated; the primary defect is not a lost OAuth response.
- Settings currently renders the subscription status in the separate authentication-mode card above the subscription card, while the cropped subscription card keeps Connect, Sign in, and Logout visible together. That makes a successful login appear unchanged.
- The correction should place the account/status signal inside the subscription card and make unauthenticated versus authenticated actions mutually clear: Connect/Sign in before authentication, Logout after authentication.
- The Assistant still needs `AiAccessManager.StateChanged` so mode/account changes made in Settings update send enablement, API-only controls, tooltip, and icon. It no longer needs its own mode/model collections, browser launcher, authentication commands, or authentication status.
- Subscription sending now reads the manager's authenticated account and selected model directly; Settings remains the only writer for mode, login/logout, and subscription-model selection.
- With authentication removed from the Assistant, its settings cog must open `SettingsSection.AiProviders`; leaving it on the General page would make the required configuration route unnecessarily indirect.
- Final validation confirms the correction compiles against the exact local LLMTornado project with 0 warnings/errors. All 34 focused tests and all 219 repository tests pass.
- The corrected executable was started without monitoring. No new browser login was initiated by the agent.
- No additional real login will be started during diagnosis unless the user explicitly requests it.

## Current Follow-up: ChatGPT subscription access through local LLMTornado

### 2026-07-25 correction: missing OpenAI Settings entry point

- The screenshot confirms the visible OpenAI provider page still contains only API key, endpoint, test/model loading, and provider model catalog controls.
- The implemented authentication selector exists only inside `AiAssistantPanel`; that makes the requested subscription extension undiscoverable from the established OpenAI settings workflow.
- The correction must preserve the complete API-key card and add a clearly separated ChatGPT subscription card on the OpenAI provider page, backed by the already implemented app-scoped manager rather than duplicate session state.
- `SettingsWindow` creates `SettingsViewModel` directly and already owns window-scoped UI callbacks, so it must supply the browser launcher through its active `TopLevel` just as the Assistant view does.
- The provider detail page is one scrollable stack: header, API-key section, divider, shared provider-model editor, split text/image lists, and reset action. A mode selector can precede two computed branches: existing API content for every non-OpenAI provider or OpenAI API mode, and subscription account/model/actions only for OpenAI Codex modes.
- `SelectedProviderApiKey` already centralizes provider switching. It needs to notify computed OpenAI visibility properties without changing provider persistence or model catalogs.
- Settings should use the existing singleton `AiAccessManager`; a second session manager would violate mode-switch disposal and would produce conflicting model/account state.
- The shared manager previously had no state notification contract. A small `StateChanged` event lets the long-lived Assistant and the disposable Settings viewmodel project the same active mode/account/model catalog without duplicating sessions.
- Settings command enablement depends on provider selection, active mode, browser-launcher availability, authentication, and busy state; every corresponding transition must refresh all three async commands.
- The provider page now has two computed content branches: API controls remain visible for every non-OpenAI provider and OpenAI API mode, while the ChatGPT card appears only for OpenAI plus a Codex mode.
- All 12 localization files contain the complete 23-key OpenAI subscription Settings surface and parse as valid JSON.
- The provider-list warning previously represented only API-key presence. It must represent either an API key or an authenticated subscription so a valid ChatGPT login is not shown as unconfigured.
- Focused regressions now cover the mode selector, separate API/subscription branches, dynamic subscription model binding, connect/login/logout commands, active `TopLevel` launcher wiring, disposal, and the combined configured-access indicator.
- The final corrected Release build uses the verified local LLMTornado project and completes with 0 warnings and 0 errors.
- All 35 focused OpenAI/Assistant/Settings tests pass. The final full network-enabled suite passes all 220 tests.
- The corrected executable is `D:\github\OpensourceToolkit.NET\bin\release\net10.0-windows\win-x64\OpenSourceToolkit.NET.exe` and was started without monitoring; no login or browser flow was triggered.

- The repository worktree already contains extensive uncommitted AI, settings, XAML, localization, project, and test changes. All work for this follow-up must be hunk-scoped and preserve that state.
- The user requires three distinct access modes: existing OpenAI API key, Codex app-server, and direct Codex browser OAuth. API-key models and subscription models must remain separate, and image generation must remain API-key-only.
- The local LLMTornado checkout and commit, its own rules, public Codex APIs, target frameworks, and build output still need direct verification.
- No interactive OAuth/browser login is authorized in this run.
- `D:\github\LLMTornado` is clean and exactly at commit `9d64bc537051fa5d2568b650a9c593feeb69f381`; it has no repository-local `AGENTS.md`.
- The requested Release solution build reached the LLMTornado projects but failed in `src/LlmTornado.Docs/LlmTornado.Docs.csproj`: its `npm run build` invocation reported that the `build` script is missing. No alternate build was started.
- The Docs project invokes npm from a `ClientApp` working directory that does not exist in this checkout. The only Docs package manifests are at `src/LlmTornado.Docs/package.json` and `src/LlmTornado.Docs/website/package.json`; the root manifest has no scripts. This is the direct cause of the solution-build failure.
- `LlmTornado.csproj` targets `net10.0`, `net8.0`, `net462`, and `netstandard2.0`. The app's exact target is still to be verified.
- Both subscription implementations are present under `src/LlmTornado/Codex`: separate app-server and OAuth session/thread/transport/model types.
- Avalonia guidance for this change requires app-owned service/lifetime ownership, compiled XAML/bindings with typed templates, minimal UI-thread updates for streaming deltas, deterministic disposal of subscriptions/sessions, and thin UI event handlers that dispatch into commands/services.
- Conditional local LLMTornado selection belongs in MSBuild project configuration; it must not change XAML compilation or the existing application bootstrap.
- Opening the direct OAuth authorization URI should be supplied by the attached view through the active `TopLevel.Launcher`, with launch failure treated as a normal result. This keeps platform UI services out of the viewmodel and avoids `Process.Start` in app state.
- New authentication controls need explicit labels, stable automation IDs/names, predictable focus order, and credential-free state tests. Headless tests must not assume a functioning platform launcher.
- The consuming app and test project target `net10.0-windows`, which matches LLMTornado's `net10.0` target. The only LLMTornado dependency is `LlmTornado` 3.8.64 in `OpenSourceToolkit.NET.csproj`.
- `Directory.Build.props` already contains a local-project toggle pattern for Flowery. Its current runtime and Flowery entries have pre-existing edits; a LLMTornado toggle can follow that structure without changing those edits.
- No `IServiceCollection`, `AddSingleton`, `AddScoped`, or `AddTransient` usage exists under the desktop app. Current composition is manual, so the requested app-owned abstraction must integrate with the actual lifetime/composition path rather than introducing an unrelated DI container.
- LLMTornado is instantiated directly in `SettingsViewModel` for connection testing/model discovery and held directly by `AiAssistantViewModel` for chat/image calls. Those two areas are the primary separation points.
- The desktop lifetime creates `MainWindow` directly; `SettingsWindow` directly creates `SettingsViewModel`, and `ImageConverterToolViewModel` directly creates `AiAssistantViewModel`. Constructor injection with production defaults and test seams fits the existing manual composition model.
- `SettingsWindow` already owns platform/UI callbacks for dialogs. It is the correct layer to supply the OAuth URI launcher callback from its active `TopLevel`, while the session and authentication state stay in services/viewmodels.
- Application shutdown currently has no app-owned AI session disposer. Subscription session ownership must therefore be tied either to the relevant viewmodel/window lifecycle or an explicit application-scoped coordinator registered and disposed from the desktop lifetime.
- LLMTornado exposes parallel but distinct session types: `CodexSession` for app-server and `CodexOAuthSession` for direct OAuth. Both support account read, browser login, logout, dynamic `CodexModel` listing, thread creation, streamed deltas, follow-up turns, and async disposal, but their thread/options/result types differ.
- App-server login credentials remain owned by the Codex installation; direct OAuth logout revokes best-effort and clears its credential store. The app abstraction must preserve that difference instead of pretending both use one credential mechanism.
- App-server sessions own a spawned `codex app-server` process and stop it on disposal. Direct OAuth sessions own refresh/access state and must also be disposed even though their thread object is client-side.
- The direct OAuth session loads credentials on connect, refreshes and persists rotating tokens, lists only backend-advertised supported text models, and retains client-side conversation history in `CodexOAuthThread`. Its default file store writes plain JSON marked as secret.
- This app already has an `ISecretStorage` abstraction backed by platform-secure storage (DPAPI on Windows in the current target). Therefore the direct OAuth path should provide an `ICodexOAuthCredentialStore` adapter instead of using LLMTornado's default plain JSON file.
- Credential values must remain confined to the secure-store adapter and LLMTornado session; UI state should expose only account identity/status and model metadata.
- Existing persisted `AiConnection` is API-provider-centric: provider type, model ID, optional API key/endpoint, and user-set capability flags. Subscription authentication should not be added as another provider or written into this API-key connection record.
- Settings currently edits/tests API connections with one free-form provider model picker and refreshes that provider's persisted catalog after a successful API test. Subscription account/model state needs a separate object and separate dynamic collection so these behaviors remain unchanged.
- The existing secure storage singleton can host namespaced OAuth credential fields without adding secrets to `settings.json`. An adapter must serialize only within the secure store and clear only its own keys on logout.
- Existing Settings tests parse XAML and exercise viewmodel state without real credentials, providing the correct pattern for the new authentication choice, separate model list, and API-only image capability assertions.
- The AI Assistant currently routes every message by the selected API connection's `SupportsImageGeneration` flag. Text requests create a fresh LLMTornado conversation per send; image requests use provider-specific existing paths. Those API behaviors must remain intact.
- Codex follow-up state should be held as a subscription thread owned by the active subscription session and reset when access mode/model changes or chat is cleared. It should not reuse or modify the API `ChatModel`/conversation catalog.
- The send gate currently requires a selected API connection and `TornadoApi`. It must become capability-aware: API mode requires the existing connection/client, while Codex mode requires an authenticated subscription session, selected dynamic model, and text capability.
- Streaming callbacks currently mutate `ChatMessageItem` directly. Codex delta callbacks may arrive off the UI thread, so the new path must marshal only the message-content delta application through the Avalonia dispatcher.
- `AiAssistantPanel` already uses typed bindings, Daisy controls, static PathIcon resources, explicit message actions, and view-owned TopLevel wiring. The new access/model controls should extend this header pattern and supply the OAuth launcher callback from code-behind.
- The panel currently exposes only an API connection selector and conditionally shows image settings from that connection. A separate access-mode selector and Codex model selector can make capabilities visible without changing the API connection list.
- Panel unload currently only detaches collection events; the long-lived `AiAssistantViewModel` is owned by `ImageConverterToolViewModel`, so session disposal should be implemented on that owner/viewmodel lifecycle rather than every temporary panel unload.
- `ImageConverterToolViewModel` and its view currently expose no disposal lifecycle. Introducing an app-scoped subscription coordinator avoids adding fragile view unload semantics and allows `App` to dispose active sessions once on desktop exit.
- Settings UI/viewmodel files contain substantial pre-existing redesign, model-filter, validation, and DaisyButton changes. New authentication UI must be inserted as a separate card/section with narrow hunks and must not reformat those files.
- The existing API connection editor capability checkboxes remain user-controlled and serve non-subscription providers. Codex image suppression must be driven by authentication-mode capabilities in the assistant, not by mutating or disabling those persisted API fields.
- `TornadoApi.Codex` is present at the requested commit. The app has no existing desktop exit handler, so `App` must subscribe to `desktop.Exit` and dispose the app-scoped access manager there.

### Implementation decision

- Add mutually exclusive LLMTornado package/project conditions controlled by `UseLocalLlmTornado` and overridable `LlmTornadoProject`.
- Add an app-scoped `AiAccessManager` with explicit `OpenAiApi`, `CodexAppServer`, and `CodexOAuth` modes, capability metadata, separate subscription account/model state, session replacement/disposal, model-bound threads, streamed turns, login, and logout.
- Wrap both LLMTornado subscription APIs behind app-owned session/thread interfaces for focused credential-free tests.
- Back direct OAuth credentials with the existing secure store through `ICodexOAuthCredentialStore`.
- Extend the AI Assistant header with mode, subscription status/actions, and dynamic subscription model selection. Keep the existing API connection selector and all image-generation controls visible only in API mode.
- The application solution resolves `LlmTornado/3.8.64` as an MSBuild project with `projectPath` `D:\github\LLMTornado\src\LlmTornado\LlmTornado.csproj`; the identical version-shaped assets key does not indicate a NuGet package when its schema says `type: project`.
- Because the local project is outside `OpenSourceToolkit.Net.sln`, solution builds set `ShouldUnsetParentConfigurationAndPlatform=true` and therefore compiled it with its default Debug configuration. ProjectReference metadata is overwritten by `AssignProjectConfiguration`; the local-reference app project must retain its parent Release configuration instead.
- The test project also receives the local project through the transitive graph and otherwise invokes it in Debug. The conditional configuration-retention property therefore belongs in `Directory.Build.props`, scoped to `UseLocalLlmTornado=true`, so all participating solution projects agree on Release.
- Critical review found that the existing toolbar gate depended only on configured API connections, which would make subscription access unreachable on a fresh setup. The toolbar now uses a broader access-availability property while API connection selection remains unchanged.
- Logout must dispose and clear local subscription state even if the upstream logout/revocation call fails; the manager now guarantees that cleanup and propagates only the failure for safe UI reporting.

## Current Follow-up: Editable searchable connection model picker and required name

- The connection editor already persisted the model as a free-form string; binding a selected model object would have broken arbitrary model IDs.
- The model editor now uses that free-form text together with the provider's categorized `AiModelOption` collection and a case-insensitive incremental contains filter.
- Focus or the adjacent dropdown action opens the known-model suggestions without replacing manually entered text.
- A 600 DIP popup limit with 30 DIP item rows caps the list at 20 visible rows.
- The connection-name field applies the Flowery error variant and `DaisyErrorBrush` whenever the editor is open and the name is blank.
- The first build identified the Avalonia 12 focus-event signature mismatch; the handler now accepts `RoutedEventArgs`.
- Final build completed with 0 warnings/errors; all 209 tests passed.

## Current Follow-up: Use DaisyButton throughout Settings

- The configured local Flowery source confirms `DaisyButton : Button` and the explicit `Variant`, `Size`, `Shape`, and `IconData` component API.
- The actual theme defines `Default`, `Primary`, `Secondary`, `Accent`, `Ghost`, `Link`, `Info`, `Success`, `Warning`, and `Error` variants.
- The final requirement explicitly forbids `Ghost` in Settings and requires every DaisyButton to declare a suitable variant.
- Settings XAML now contains 16 DaisyButtons, zero standard Buttons, zero missing variants, and zero Ghost variants.
- Navigation keeps the existing ListBox selection/content visibility model; each navigation item now exposes an actual DaisyButton while retaining stable automation IDs and page order.
- Neutral actions use `Default`, confirm/save/close actions use `Primary`, destructive actions use `Error`, and the code-created discard action uses `Warning`.
- The local Flowery checkout contains a newer `IconData` API, but the actually referenced NuGet package Flowery.NET 2.2.0 does not; the XAML compiler rejected that property.
- The image-toolbar settings button therefore uses supported `PathIcon` content with `Shape=Square`, `Size=Small`, explicit interaction state, and remains outside the Settings-wide conversion scope.
- Final build: 0 warnings and 0 errors. Final tests: 205 passed.
- The rebuilt win-x64 application is running with a targetable `OpenSourceToolkit.NET` window.

## Current Follow-up: Fix unresponsive AI Settings toolbar button

- The existing toolbar entry is a `DaisyButton` with no explicit `IsEnabled`, `IsHitTestVisible`, or `Focusable` values.
- The routed Click handler and `MainWindow.OpenSettings(SettingsSection.AiConnections)` wiring exist and compile, but the custom control is the only control-specific difference from the adjacent native toolbar buttons.
- The initial Settings page is selected indirectly by setting a child `ListBoxItem.IsSelected`; the owning ListBox itself is unnamed.
- The smallest robust correction is a native Avalonia `Button` with explicit enabled/hit-test/focus state plus a named Settings navigation ListBox whose `SelectedIndex` is set deterministically.
- The running user application must not receive CUA input and must not be closed for this iteration.
- The corrected XAML parses successfully after normalization, and scoped `git diff --check` reports no errors.
- Build/runtime tests were not started because they would target the output currently used by the user's running application.

## Current Follow-up: Always-available AI Settings toolbar button

- The image-editor toolbar places the provider-dependent AI Gen toggle immediately before the Sessions button.
- `SettingsIcon` already exists and is used by the AI Assistant settings button.
- `MainWindow.OpenSettings()` is the existing modal Settings route; it currently always opens the General page.
- The redesigned Settings window has a named `ConnectionsSettingsNavigationItem`, so an explicit initial section can select AI Connections after XAML loading.
- The new toolbar button will use a direct click handler, no `IsEnabled` binding, a stable automation ID, and localized tooltip/accessible name.
- No running Toolkit process or targetable window remained when build validation began, so no application needed to be closed.
- The documented `dotnet build` completed with 0 warnings and 0 errors; all 204 tests passed.
- The rebuilt win-x64 executable started successfully and visibly shows the new cog between AI Gen and Sessions.
- A live click-through was not forced after Computer Use detected concurrent user input in the application window; the direct AI Connections route is covered by focused source tests.

## Current Follow-up: Settings persistence regression protection

- `AppSettings.Save()` wrote directly to `settings.json` with `File.WriteAllText`; another instance could observe a truncated/partial file during shutdown/startup overlap.
- `AppSettings.Load()` caught every failure, replaced the state with a blank `SettingsData`, and startup locale detection immediately called `Save()`. On this German system that produced the observed default file with `Locale=de-DE`, empty favorites, and zero AI connections.
- The same broad catch also allowed an AI synchronization exception after a successful load to discard the already-loaded settings.
- The fix uses atomic replacement, a named cross-process mutex, SHA-256 stale-writer detection, three fixed backup slots containing UTC timestamps, automatic recovery from the newest valid backup, and timestamped corrupt/conflict sidecars.
- Stable backup slots avoid accumulating files while preserving exactly the three preceding valid settings versions.
- Nine focused persistence tests now reproduce the destructive paths, including the exact recovery-then-locale-save sequence with an AI connection and favorite tool retained.
- Final build: 16 projects, 0 warnings/errors. Final test run: 198 passed.

## Current Follow-up: Correct chat bubble layout and actions

- Flowery's `DaisyChatBubble` template fixes `PART_Bubble.MaxWidth` at 400 and measures its content before the border's final width constraint, so limiting only the outer control or border still clips wrapped content and actions.
- The content presenter must be constrained to the responsive width minus horizontal padding: 32 px for normal bubbles and 16 px for the requested 8 px user-bubble padding.
- Flowery's right-aligned footer is positioned outside the visible end-bubble column; user timestamps are therefore rendered in the app's right-aligned wrapper below the bubble.
- The final CUA render shows complete wrapping, both actions within every bubble, full timestamps, 8 px user padding, equal Ghost backgrounds, and a red Delete glyph.
- Final build: 16 projects, 0 warnings/errors. Final test run: 189 passed.

## Current Follow-up: Exclude Settings from remembered sidebar selection

### Findings
- `FloweryComponentSidebar.SelectItem()` assigns `SelectedItem`, persists `last:<id>`, and only then raises the app's `ItemSelected` event.
- Consequently, the current `ToolkitSettingsItem` handler opens the dialog after Settings has already become both the highlighted and persisted selection.
- Startup calls `GetLastViewedItem()` after Flowery loads its state, so a persisted `settings` item is treated like a normal navigation target.
- Settings is a modal administrative action, not application content. It must not replace the last normal sidebar selection.
- The app must track the last navigable item, restore its highlight after Settings is clicked, and rewrite the Flowery sidebar state while preserving collapsed categories.
- Existing stale `last:settings` state cannot reveal the prior tool; startup should replace it with the Home item as a deterministic fallback.

### Constraints
- The app consumes Flowery.NET 2.2.0 as a NuGet package, so changing the separate local Flowery checkout would not affect this build.
- Preserve all existing uncommitted work and normalize `.cs`, `.csproj`, and `.axaml` only once at the end.

### Additional connection-editor requirements
- Add the existing `TestConnectionCommand` to the per-connection editor, not only provider settings.
- The Save command currently has no `CanExecute`; it remains enabled after saving even though the dirty baseline was reset.
- Save must use connection dirty state as its `CanExecute` condition, and every edit field participating in `HasUnsavedConnectionChanges` must notify the command when changed.
- The test result should remain visible in the editor through the existing `ConnectionTestStatus` property.

## Current Follow-up: AI settings navigation and connection selection

### Reported behavior
- After editing and saving the only connection, its card remains in the left list but the right editor changes to the empty-state prompt.
- Clicking the visible card no longer reopens it; restarting the app restores usability.
- The screenshot therefore points to stale selection/reference state after save, not a deleted connection.
- The previously requested AI Assistant cog is implemented against the existing `MainWindow.OpenSettings` dialog path but still awaits final build/test because the user-running app locked the output.

### Constraints
- Preserve all existing uncommitted work and the user-running process.
- Use native Git Bash and RTK.
- Run the iteration's source normalization only once, after both fixes are final.

### Code findings
- `SettingsWindow.axaml` binds the list selection to `SelectedConnection`, shows the editor only while `IsEditingConnection` is true, and shows the empty prompt otherwise.
- `SaveConnection()` leaves `SelectedConnection` pointing at the saved item but unconditionally sets `IsEditingConnection = false`.
- Clicking that still-selected card again does not trigger the setter because `SelectedConnection` rejects the same object reference, so `StartEditConnection()` is never called again.
- This exactly explains the screenshot and why a restart works: settings reload reconstructs the collection and selection state.
- Saving should leave the saved item selected and reopen/reset the edit form; a newly added connection must likewise become the selected item.
- `AiConnectionViewModel.DisplayText` is computed from `Name`, `ProviderType`, and `ModelId`, but those setters do not notify `DisplayText`; this should be corrected while fixing post-save state so the list card always reflects edits.
- No existing test covers `SettingsViewModel` connection selection/save behavior, and constructing it would load and potentially persist the user's real settings. Regression coverage must avoid instantiating it against `AppSettings`.
- The existing edit fields already contain the successfully saved values. The post-save fix can retain those fields, set the saved item as the backing selection, keep `IsEditingConnection` true, and reset the dirty baseline without reloading global settings.
- The implemented save completion uses the actual saved `AiConnectionViewModel` for both edits and additions, keeps the editor visible, refreshes selection-dependent commands, and captures the saved values as the new dirty baseline.
- Regression coverage invokes the private state-completion method on an uninitialized `SettingsViewModel`, so it verifies the real transition without loading or saving the user's `AppSettings`.
- The combined implementation compiles with 0 warnings/errors and all 177 tests pass before final encoding normalization.
- After the single final normalization of all 45 changed source/project/XAML files, the solution still builds with 0 warnings/errors and all 177 tests pass.

## Current Follow-up: Accessible AI error messages

### Reported behavior
- The application has one active AI connection (`Nano Banana`); that is sufficient for sending and is not itself the failure cause.
- The provider returned a long single-line JSON error.
- The error bubble clips the message horizontally, while the chat viewport disables horizontal scrolling.
- There is no visible per-message copy affordance; the existing double-click-to-copy behavior is undiscoverable.
- The fix must preserve the complete provider error while making it readable, vertically scrollable when long, selectable, and explicitly copyable.

### Constraints
- Preserve all existing uncommitted work.
- Use native Git Bash and RTK for shell commands.
- Apply the iteration's BOM/CRLF normalization only once at the end and only to `.cs`, `.csproj`, and `.axaml` files.

### Code findings
- `AiAssistantPanel.axaml` places the message list in a vertical `ScrollViewer` with horizontal scrolling disabled.
- Every message currently passes a plain string into `DaisyChatBubble.Content`; the control template therefore owns wrapping, and the screenshot proves that it does not wrap this long JSON payload within the available width.
- All bubbles support double-click copy through `OnMessageDoubleTapped`, and the viewmodel already exposes `CopyMessageCommand`, but neither interaction is visible in the UI.
- The existing `CopyableTextBox` is not suitable unchanged: its XAML does not forward its declared `AcceptsReturn` or `TextWrapping` properties, and it always includes a clear button that would mutate an error message.
- A focused error-message template should use a read-only multiline `TextBox` with wrapping and its own bounded vertical scrolling, plus a visible copy button that delegates to the existing viewmodel command.
- The app uses the Flowery.NET 2.2.0 NuGet package from the package root recorded in `project.assets.json`; a local Flowery.NET checkout is also present at the configured development path, so the `DaisyChatBubble` template can be inspected without inferring package internals.
- The Flowery.NET template caps each bubble at 400 px but its `ContentPresenter` does not enable text wrapping for string content. Flowery's own documentation shows nested `TextBlock TextWrapping="Wrap"` as the intended pattern.
- Use wrapped nested `TextBlock` content for ordinary chat messages as well, because the screenshot shows the user's long prompt clipped by the same template behavior.
- For errors, keep the semantic `DaisyChatBubble Variant="Error"` but supply a read-only wrapped `TextBox` capped in height plus a visible copy button inside the content.
- Implementation retains the existing viewmodel `CopyMessageCommand`; the new code-behind click handler only forwards the selected `ChatMessageItem`, matching the repository's clipboard separation rule.
- A source-level regression test asserts the exact error TextBox and copy-button invariants without constructing Avalonia controls on MSTest's non-owner dispatcher.
- The implementation compiles through Avalonia's XAML compiler with 0 warnings and 0 errors.
- The full suite now contains 174 passing tests, including the new accessible-error invariant.
- After the required one-time BOM/CRLF normalization of all 43 changed source/project/XAML files, the final solution build still completes with 0 warnings/errors and all 174 tests still pass.

## Current Task: .NET 10 Windows x64 upgrade

### Requirements
- Increase the application version from its current authoritative value.
- Upgrade every project from .NET 8 to .NET 10.
- Treat the entire repository as Windows-only.
- Restrict runtime assets and application execution to `win-x64` to avoid the current oversized multi-platform `runtimes` output.
- Run restore, build, and tests.
- Keep the AI chat enabled without a loaded image when the selected connection can generate an image from text; retain image requirements only for operations that actually consume a source image.

### Initial Constraints
- Use native Git Bash and RTK for all shell commands.
- Preserve the existing uncommitted NuGet, AI-provider, and warning-fix work.
- Apply BOM/CRLF normalization only once at the end and only to `.cs`, `.csproj`, and `.axaml` files.

### Inventory Findings
- The authoritative app version is in `OpenSourceToolkit.NET/OpenSourceToolkit.NET.csproj`: package version `1.0.1`, assembly/file version `1.0.1.0`.
- There are 16 projects: 12 target `net8.0` and 4 target `net8.0-windows`.
- No project currently declares `RuntimeIdentifier`, `RuntimeIdentifiers`, or `PlatformTarget`.
- Every Debug project writes to the shared `bin/debug` root; MSBuild then appends the target framework.
- The current `bin/debug` occupies 1.1 GB. Its `net8.0-windows/runtimes` directory alone occupies 982 MB and contains browser, Linux, macOS, Windows x86/x64/arm64, and other native assets.
- For a routine version increase with no release scheme specified, use the next patch version: `1.0.2` / `1.0.2.0`.
- `Directory.Build.props` currently contains only the Flowery.NET development switch; it is the correct shared location for repository-wide `RuntimeIdentifier=win-x64`, `PlatformTarget=x64`, and `Prefer32Bit=false` settings.
- The oversized runtime tree comes primarily from SkiaSharp/HarfBuzz, Magick.NET, and OpenCvSharp native assets. The current three Windows architecture folders alone use about 615 MB; non-Windows folders account for most of the remaining runtime tree.
- Setting only an output-path property would hide collisions but would not restrict NuGet runtime asset selection. A concrete `RuntimeIdentifier` is required before restore, while `PlatformTarget=x64` expresses the managed executable architecture.
- The machine has stable .NET 10 SDKs installed through `10.0.302`, but without `global.json` the repository currently selects the installed .NET 11 preview SDK. Add `global.json` so the repository actually builds with the .NET 10 SDK family.
- README currently documents a mixed `net8.0`/`net8.0-windows` design and both output directories. It must be revised to describe one Windows x64 `net10.0-windows` target and its RID-appended output path.
- The changelog already has an Unreleased/Changed section suitable for recording the app-version, framework, and runtime restriction changes.
- The app and one test initializer use Avalonia `UsePlatformDetect()`. For an explicitly Windows-only product, switch both to `UseWin32().UseSkia()` so bootstrap selection matches the declared platform rather than retaining runtime OS detection.
- `Avalonia.Desktop` is a convenience package that depends on Win32, X11, Avalonia.Native, Skia, and HarfBuzz. It also supplies the classic desktop lifetime extension, so retain it for lifetime support; the concrete `win-x64` RID will restrict selection of native runtime assets.
- The current NuGet assets file confirms package roots are `D:\nuget\packages` and the Visual Studio shared package cache; no package path was inferred.
- The first .NET 10 restores succeeded but raised `NU1510` for explicit references now provided by the target framework: `System.Text.Json` in the app, Converters, and AI projects, plus `System.Net.Http` in AI. Removing these four references is the direct .NET 10 migration fix.
- The first .NET 10 build succeeded but exposed `SYSLIB0060` in `SecureStorage`: the old stateful PBKDF2 constructor is obsolete. One static 48-byte derivation split at byte 32 preserves the previous two consecutive `GetBytes` results.
- Explicit `UseWin32().UseSkia()` does not configure text shaping. The first test run therefore failed at assembly initialization with Avalonia's direct instruction to add `UseHarfBuzz()`; both app and test bootstrap require it.
- After adding HarfBuzz, the .NET 10 solution build completes with 0 warnings and 0 errors and all 172 tests pass.
- The new RID-specific output is `bin/debug/net10.0-windows/win-x64`, uses 356 MB, and contains no `runtimes` directory. NuGet/MSBuild place only selected win-x64 native assets directly in the output.
- The old `bin/debug/net8.0-windows` tree remains as a pre-existing artifact; it was not recreated by the .NET 10 build and is outside the new output path.
- `project.assets.json` still records every runtime asset published by transitive packages as NuGet metadata, but the physical output contains no foreign-platform filenames. RID selection controls copied assets, not the catalog retained in the restore graph.
- `file` identifies the app host and representative Magick.NET, OpenCvSharp, SkiaSharp, and HarfBuzz native DLLs as PE32+ x86-64 binaries.
- A post-normalization build attempt was blocked because `D:\github\OpensourceToolkit.NET\bin\debug\net10.0-windows\win-x64\OpenSourceToolkit.NET.exe` is currently running as PID 32488 and locks shared output DLLs. The assistant did not start this process, so it must not be terminated without user permission.
- Process inspection attributes PID 32488 to user `tobias`, launched from Total Commander, confirming it is user-owned rather than an assistant-started process.
- The generated deps metadata identifies `.NETCoreApp,Version=v10.0/win-x64`, and the standalone AI project still builds after normalization with 0 warnings and 0 errors.
- The AI chat uses `SendAiMessageCommand = new RelayCommand(..., CanSendAiMessage)` in `AiAssistantViewModel`; the panel component itself only disables the input area while processing, so the higher-level image-converter XAML also had to be inspected.
- The viewmodel already exposes `IsImageGenerationConnection` from the selected connection's `SupportsImageGeneration`, which is the appropriate distinction for allowing text-only prompts without a loaded image.
- `CanSendAiMessage` itself does not test workspace-image availability; it only requires input text, a selected configured connection, and an initialized client. The reported disabled state is therefore higher in the image-converter view hierarchy.
- `ImageConverterToolView.axaml` contains multiple `Workspace.HasWorkspaceImage` gates, including one near the AI panel region; inspect that parent container before changing the command predicate.
- The exact defect is `RightPanelBorder IsEnabled="{Binding Workspace.CanEditSingleImage}"`. This disables the entire AI assistant whenever no workspace image exists, even though the selected AI connection may be text-to-image.
- The AI toolbar button is already enabled based only on `Ai.HasAiConnections`, so removing the unrelated editor-state gate from the AI panel preserves the intended connection requirement.
- There is no existing AI-panel enablement test. A focused Avalonia view regression test should assert that `RightPanelBorder` remains enabled when `Workspace.HasWorkspaceImage` is false.
- The AI panel must remain enabled unconditionally so the user can change the connection even when the initially selected connection is analysis-only. Gating the entire border with an OR condition would still trap the connection selector when both conditions are initially false.
- The correct separation is to remove the parent `IsEnabled` gate and leave the existing send predicate unchanged because both text chat and image generation intentionally support requests without source images.
- Child-viewmodel integration is centralized in `ImageConverterToolViewModel.Wiring.cs`, where AI image delegates are already assigned. Workspace-availability notification should be added there rather than introducing view code-behind coupling.
- Image-generation requests already tolerate zero source images: thumbnails are optional, and the workspace image is appended only when available and requested. The text prompt is always included first.
- Existing AI tests exercise configuration but do not initialize `AppSettings.Current`; regression coverage should avoid depending on persisted user AI connections.
- `AnalyzeImageWithAiAsync` intentionally supports zero images and changes its context to a normal text response. Therefore the AI assistant is designed as text chat, image analysis, and image generation; no send-command image requirement should be added.
- Final fix: remove only the parent `Workspace.CanEditSingleImage` enablement binding and add a view regression test. Image-editing controls retain their existing workspace-image gates.
- Constructing the full Avalonia view from a normal MSTest worker thread is invalid because the assembly initializer owns the Avalonia dispatcher. The regression can be deterministic and narrower by parsing the XAML and asserting that `RightPanelBorder` has no `IsEnabled` attribute.
- The source-level XAML regression passes, the solution builds with 0 warnings/errors, and the full suite now contains 173 passing tests.

---

## Requirements
- Fix the Avalonia `Bitmap.Save(Stream, int?)` obsolete warning.
- Fix the SkiaSharp `SKCanvas.DrawText` obsolete warning.
- Add Hugging Face as a selectable, persisted AI provider.
- Keep prior NuGet and image-model work intact, including exclusion of all Gemini 2.x models.
- Do not search Chinese websites.

## Research Findings
- The current solution builds with exactly two CS0618 warnings: `HistogramControl.axaml.cs:79` and `FontsViewerToolViewModel.cs:1124`.
- The existing provider set is duplicated between `OpenSourceToolkit.AI` and the desktop `OpenSourceToolkit.NET/Services/Ai` layer, so HF must be wired consistently in both.
- Prior live NuGet work established current packages and a successful solution build before this follow-up.
- Avalonia skill guidance recommends preserving direct drawing semantics and using current backend APIs rather than introducing new rendering architecture for a signature-only migration.
- Existing tests are ordinary MSTest tests; a headless Avalonia harness is unnecessary unless the warning fixes change rendered UI behavior.
- Hugging Face Inference Providers exposes an OpenAI-compatible base URL at `https://router.huggingface.co/v1`; its `/chat/completions` and `/models` endpoints are explicitly chat-only.
- Hugging Face text-to-image uses the native task contract: bearer-token authentication, JSON `{ "inputs": prompt, "parameters": ... }`, and raw image bytes in the response.
- The Hub model API supports `inference_provider=all&pipeline_tag=text-to-image` for discovering image models served by at least one inference provider.
- HF's own provider uses `https://router.huggingface.co/hf-inference/models/{modelId}` for native task requests, including raw-byte text-to-image responses.
- Non-chat task APIs are provider-specific; automatic cross-provider routing is implemented by HF client SDKs rather than one documented generic REST route.
- The desktop currently creates LLM Tornado clients in `SettingsViewModel` and maps provider enums in both settings and image-assistant viewmodels; HF needs explicit routing because LLM Tornado has no native HF enum.
- Provider model storage is centralized in duplicated `AiSettingsManager` implementations, so HF defaults and discovered models must be added to both shared and desktop configuration types.
- Live schema inspection returned 124 chat models from `https://router.huggingface.co/v1/models`; entries expose `id`, `architecture`, and `providers`.
- Live HF-Inference image discovery returned one currently served text-to-image model: `stabilityai/stable-diffusion-3-medium-diffusers`.
- LLM Tornado's custom-provider path can reuse HF's OpenAI-compatible chat endpoint, but the image assistant must call the native HF-Inference endpoint directly.
- Existing custom-provider initialization drops API keys because Ollama/LM Studio do not need them; HF requires the custom endpoint constructor that also receives the token.
- Avalonia 12.1 provides `Bitmap.Save(Stream, BitmapEncoderOptions)` and `PngBitmapEncoderOptions.Default`, which preserves the current memory-stream PNG behavior.
- SkiaSharp 4.150.1 provides `SKCanvas.DrawText(string, float, float, SKTextAlign, SKFont, SKPaint)`; passing `SKTextAlign.Left` preserves the existing x-coordinate semantics.
- Settings provider dropdowns are populated from `AiSettingsManager.SupportedProviders`; adding `HuggingFace` there wires it into existing API-key and connection editors without XAML changes.
- Image-generation mode is an explicit per-connection flag, so HF image models do not need to force the UI mode automatically; model classification remains useful for defaults and tests.
- The intermediate solution build succeeds with 0 warnings and 0 errors after the new overloads and HF wiring.
- HF documentation changes are confined to `OpenSourceToolkit.AI/ai.md` and the unreleased changelog; README does not enumerate AI providers.
- `OpenSourceToolkit.AI` is not included in `OpenSourceToolkit.Net.sln`; it requires its own restore/build validation.
- Post-normalization builds remain clean: the solution and standalone AI project both report 0 warnings and 0 errors.
- The final solution test run passes all 172 tests with none failed or skipped.
- An authenticated live HF generation request was not run because no user token is available; the public model catalogs and request contracts were verified live.

## Technical Decisions
| Decision | Rationale |
|----------|-----------|
| Pending API inspection | Avoid assuming HF endpoint formats or available tasks |
| Keep warning fixes signature-only | Both warnings identify direct replacement overloads and do not require UI architecture changes |
| Use HF OpenAI compatibility for chat | Reuses the existing chat pipeline with the official router base URL |
| Implement HF text-to-image with HttpClient | HF documents image generation as a native task, not through the chat-compatible endpoint |
| Discover HF chat and HF-Inference image catalogs separately, then merge | `/v1/models` is chat-only; restricting Hub discovery to `inference_provider=hf-inference` ensures every listed image model matches the implemented native endpoint |
| Validate HF tokens via the official whoami endpoint during connection tests | Public model catalogs alone would incorrectly accept an invalid token |
| Treat HF as LLM Tornado `Custom` for chat | LLM Tornado has no first-class HF provider enum, while HF exposes an OpenAI-compatible endpoint |

## Issues Encountered
| Issue | Resolution |
|-------|------------|
| `SupportedProviders` was initially patched in `AiProviderSettings` | The array is owned by each `AiSettingsManager`; split the patch using the verified locations |
| Standalone AI build found unresolved `StringComparison` | Qualified the references with `System.StringComparison`; the solution build could not reveal this because the AI project is omitted from the solution |
| BOM prevented a first-line import patch | Patched verified method-body context instead of modifying the BOM-prefixed header |

## Resources
- https://huggingface.co/docs/inference-providers/en/index
- https://huggingface.co/docs/inference-providers/tasks/text-to-image
- https://huggingface.co/docs/inference-providers/hub-api
- https://huggingface.co/docs/inference-providers/main/providers/hf-inference
- `D:/github/OpensourceToolkit.NET/AGENTS.md`

## Visual/Browser Findings
- None.
# AI error clipboard follow-up

- The visible error button correctly reaches `AiAssistantViewModel.CopyMessageCommand` with the bound `ChatMessageItem`.
- `CopyMessageToClipboard` only invokes the optional `CopyToClipboardAction`; `AiAssistantPanel` currently does not wire that action to `TopLevel.GetTopLevel(this)?.Clipboard?.SetTextAsync(text)`.
- The Avalonia skill requires clipboard access to remain in the view layer, with the view model exposing an action delegate.
- `ImageConverterToolView.OnDataContextChanged` wires the other image-converter UI actions but omits `vm.CopyToClipboardAction`, so both the per-message copy button and the toolbar's whole-chat copy command currently invoke a null delegate.
- Other tool views use the required pattern successfully: an async `Action<string>` resolved from the view's active `TopLevel`, followed by `await topLevel.Clipboard.SetTextAsync(text)`.
- The image converter already wires image clipboard handling in `OnDataContextChanged`; text clipboard wiring belongs beside it and needs the `Avalonia.Input.Platform` extension namespace.
- The existing test only verified that the copy button was present and tagged with its message. It did not verify that `CopyToClipboardAction` was assigned, which allowed the null delegate regression.
- The focused regression should cover both halves: the view wires the delegate to `SetTextAsync`, and the view-model message-copy method passes the complete error payload unchanged.

# OpenRouter Gemini INVALID_ARGUMENT follow-up

- The desktop AI Assistant does not use `OpenSourceToolkit.AI.OpenAiCompatibleProvider` for this request. It builds an LLM Tornado `ChatRequest` directly in `AiAssistantViewModel.GenerateImageAsync`.
- The failing request includes the connection's `MaxTokens` (32000 in the screenshot) and `Temperature`, plus text/image modalities. The UI size and quality selections are not included in the OpenRouter chat request at all.
- The separate `OpenSourceToolkit.AI` OpenRouter implementation already sends a minimal chat-completions payload and only adds `image_config.aspect_ratio` when explicitly available.
- Current official OpenRouter documentation now defines a dedicated `POST /api/v1/images` endpoint with a minimal `{ model, prompt }` request and base64 images in `data[].b64_json`.
- The dedicated endpoint accepts normalized `size`, `aspect_ratio`, and `quality` fields; unsupported parameters are discoverable per model/endpoint. This is a different contract from the app's current LLM Tornado `/chat/completions` request.
- Google's official Gemini 3.1 Flash Image documentation confirms the model itself supports image output, so the reported `INVALID_ARGUMENT` points to the request contract/parameters rather than authentication or model absence.
- Live OpenRouter image-model metadata confirms `google/gemini-3.1-flash-image` exists and supports `resolution` values `512`, `1K`, `2K`, `4K`, plus explicit aspect ratios including `1:1`, `3:2`, and `2:3`.
- Both live Google endpoints list no `quality` parameter for this model. Sending the UI's `quality: auto` through the dedicated endpoint would therefore be incorrect; the client must omit unsupported quality and map the three UI dimensions to `resolution: 1K` with ratios `1:1`, `3:2`, or `2:3`.
- The app currently uses LLM Tornado 3.8.64. Its documented OpenRouter image example uses the older chat-completions modality flow; OpenRouter's unified `/images` API was announced later (June 23, 2026), so the installed SDK does not provide the required current request surface.
- The fix therefore needs a small native OpenRouter image client, analogous to the existing Hugging Face client, instead of routing this image request through LLM Tornado chat completions.
- `AiConnectionConfig` supplies the correct OpenRouter base endpoint (`https://openrouter.ai/api/v1`) and the AI Assistant already has the resolved per-connection API key, model ID, requested size, and any input images needed by a native client.
- Existing desktop tests cover model classification but not serialized image requests. No standalone AI test directory exists at the inferred path, so focused request-building tests should be added to the verified `OpenSourceToolkit.Tests` project.
- `SendAiMessageAsync` already catches request exceptions and exposes their messages as copyable system errors, so the new client can surface the complete OpenRouter HTTP response without adding another UI error path.
- The OpenRouter branch should be selected before the generic LLM Tornado chat request, while direct OpenAI, Hugging Face, and other providers keep their current paths.
- The implemented client fetches the current image-model capability map from OpenRouter before generation, builds a dedicated `/images` payload from only advertised parameters, and parses `data[].b64_json` plus optional `media_type`.
- Input images are copied into a new list before adding the workspace image, avoiding mutation of the view model's source collection.

# AI chat clipboard lifecycle follow-up

- `AiAssistantPanel.DataContext` is assigned through `{Binding Ai}` in the parent view, but clipboard wiring only runs from `OnLoaded`; a late or replaced binding leaves `CopyToClipboardAction` unset.
- Both whole-chat and per-message methods create non-empty text and invoke the same delegate, so the remaining fault is the delegate lifecycle/async boundary rather than text generation.
- The corrective path is to wire on `OnDataContextChanged`, use a task-returning delegate with async commands, and flush the platform clipboard after `SetTextAsync`.
- Final implementation assigns the delegate from `ImageConverterToolView.OnDataContextChanged`, so it does not depend on the nested panel being loaded after its bound DataContext becomes available.
- Final verification completed with a clean 16-project build and 185 passing tests.

# Image-strip editor loading overlay follow-up

- The image-strip click is functional; the perceived failure is a one-to-two-second image load with no visual state change.
- The requested change is visual feedback over the main editor only while the newly selected strip image is loading.
- The strip's load event is raised by `ThumbnailStripViewModel.LoadThumbnailToWorkspaceCommand` and handled asynchronously in `ImageConverterToolViewModel.Wiring.cs` by `Workspace.LoadFromThumbnailAsync(...)`.
- The main editor and strip are both composed in `ImageConverterToolView.axaml`; the overlay can remain declarative if the workspace exposes a loading property covering that awaited operation.
- `WorkspaceEditorViewModel.LoadFromThumbnailAsync` performs preview conversion and metadata inspection before replacing `WorkspaceImage`; it currently exposes no loading state for this path.
- The main image is already inside a layered `Grid` within a clipped preview `Border`, so a final theme-aware `Grid` child can cover only the editor image, block interaction during replacement, and leave the strip usable.
- The existing `Workspace.IsProcessing` state belongs to other processing/save operations and affects editing command availability; a separate thumbnail-load state avoids showing the overlay for unrelated work.
- `WorkspaceEditorViewModel` uses ordinary `SetProperty` notification properties; a dedicated `IsLoadingWorkspaceImage` property fits the existing binding contract without changing `CanEditSingleImage`.
- Existing thumbnail tests live in `OpenSourceToolkit.Tests/ThumbnailStripViewModelTests.cs`; there is no current workspace-load or main-editor overlay test.
- The main view already uses compiled bindings with `x:DataType="vm:ImageConverterToolViewModel"`, so the overlay should bind through `Workspace.IsLoadingWorkspaceImage` rather than use code-behind.
- No `.resx` files exist under the app project; localization uses a different repository mechanism that must be resolved before adding visible loading text. A text-free indeterminate indicator remains an option if no suitable key exists.
- The ordinary file-open path already performs decode/conversion inside `Task.Run`, while thumbnail loading performs the same work on the UI thread. Because the user requested a visual-only change, keep the loading semantics and explicitly yield at background dispatcher priority after setting the overlay state so render work can run before conversion begins.
- `ToolkitLocalization.cs` has no existing generic loading/processing key; the overlay can avoid localization churn by using only a centered indeterminate progress indicator with an accessible automation name.
- Localization strings are JSON-backed across 12 locale files. Because the requested overlay can communicate loading without visible text, use a shaded layer plus indeterminate progress bar and avoid adding a new user-facing string to every locale.
- The repository has no existing `Panel.ZIndex` usage. Placing the loading layer last in the main preview `Grid` is sufficient and avoids introducing an unverified attached-property dependency.
- A focused regression can parse `ImageConverterToolView.axaml` as existing UI tests already do, while a viewmodel test verifies property notification independently of Avalonia rendering.
- The Avalonia skill's overlay guidance influenced the implementation: the shade is a lightweight final child of the existing clipped preview grid, remains declarative, and does not introduce an adorner or code-behind dependency.
- Final verification completed after normalization with a clean 16-project build and 187 passing tests.

# AI chat message usability follow-up

- The screenshot shows user bubbles extending beyond the visible chat viewport; text is clipped on the right even though it contains normal spaces that should allow wrapping.
- Only the toolbar offers an obvious copy action. The previously added visible per-message action is limited to error bubbles, so ordinary user, AI, system, and success messages need consistent actions.
- Individual deletion must remove exactly the selected `ChatMessageItem` and invoke the same chat-change path used for persistence and command-state updates.
- All five role/state templates already use typed `DataTemplate` bindings and `TextWrapping="Wrap"`, but only the error template has a hardcoded content width and a visible copy button.
- The ordinary templates place a bare `TextBlock` inside `DaisyChatBubble`; the screenshot proves the bubble is being measured wider than the chat viewport, so wrapping alone cannot work without constraining the bubble/content width.
- `CopyMessageCommand` and the awaited TopLevel clipboard delegate already exist and accept any `ChatMessageItem`; the missing functionality is the visible command surface for non-error messages.
- `NotifyChatChanged()` centralizes persistence dirtiness, `HasMessages`, and toolbar command state. A new `DeleteMessageCommand` should remove the exact collection item and then call this method.
- Current assets resolve Flowery.NET 2.2.0 from `D:\nuget\packages\flowery.net\2.2.0`; the package contains only the compiled assembly and README, so its current documentation or authoritative source must be used to confirm bubble layout behavior.
- The current local Flowery.NET source checkout is `D:\github\Flowery.NET`; its `DaisyChatBubble` theme sets the bubble border to `MaxWidth="400"` without adapting that width to a narrower host.
- The chat viewport in the screenshot is narrower than 400 px, explaining why the bubble is clipped even though its inner `TextBlock` has wrapping enabled.
- Flowery.NET provides `FloweryResponsive` specifically for this case: enabling it on the `ScrollViewer` exposes a `ResponsiveMaxWidth` equal to the smaller of the configured base width and the available bounds minus 48 px.
- Binding each `DaisyChatBubble.MaxWidth` to that ancestor value preserves left/right alignment while constraining the internal 400 px bubble border to the actual chat viewport.
- `AiAssistantPanel.axaml` has no local resource section yet, while the global icon set already supplies `CopyIcon` and the trash-shaped `ClearIcon`.
- A typed reusable `ChatMessageActionsTemplate` can provide the same compact copy/delete buttons to all five bubble variants without duplicating action markup; each bubble content remains a single child by wrapping text and actions in a `Grid`.
- The Avalonia skill's typed-template and command guidance led to one `x:DataType="models:ChatMessageItem"` action template; routed UI handlers only resolve the tagged item and dispatch existing/new viewmodel commands.
- The intermediate Avalonia XAML compilation succeeds with the responsive attached-property binding, resource DataTemplate event handlers, and all five updated bubble templates.
- Final verification completed after normalization with a clean 16-project build and 189 passing tests.

# AI chat internal bubble width correction

- The follow-up screenshot confirms the previous outer `DaisyChatBubble.MaxWidth` binding is insufficient: user-message text and both action buttons still extend past the right boundary.
- Short success messages happen to fit, which is why their action buttons remain visible; long user messages expose the internal overflow.
- Flowery's template places `MaxWidth="400"` on `Border#PART_Bubble`. The responsive binding must target that internal border directly through a global descendant style, which the repository theme rules explicitly permit outside a `ControlTheme`.
- The corrected view-local descendant style compiles successfully and overrides the internal border width without replacing or copying Flowery's full control theme.

## 2026-07-25 - Provider model list splitter and row actions

- The two search inputs already bind to distinct properties and distinct projected collections.
- `RefreshProviderModelFilters()` currently recomputes both collections from either query setter; the predicates are correct, but splitting the refresh methods will make the filter sources strictly independent.
- The text and image areas are consecutive Borders inside a vertical StackPanel, so no real height-resizing boundary exists between them.
- Each list currently has one shared delete button outside the list. It is visually ambiguous and cannot represent the requested per-model hover action.
- The required row action belongs immediately beside the model name, not at the far right.
- Accessibility approach: hide the row delete action visually by default, reveal it for pointer hover and keyboard focus-within, retain a localized automation name, and keep the model row selectable.
- The actual restored UI uses Avalonia 12.1.0 from `D:\nuget\packages`; its reference metadata confirms `GridSplitter.ResizeDirection`, `ResizeBehavior`, `KeyboardIncrement`, and the enum values `Rows` plus `PreviousAndNext`.
- Because the provider detail content is inside a vertically measuring ScrollViewer/StackPanel, the split model area needs its own bounded height; otherwise star rows receive no finite height for the splitter to redistribute.
- Critical review found the first splitter draft was technically draggable but visually transparent and did not explicitly opt into keyboard focus. It now uses the themed divider brush, is focusable, supports 16-DIP keyboard increments, and keeps 1-DIP drag precision.
- The per-row delete action uses opacity rather than layout removal, so model text does not shift on hover. Hit testing and focusability are disabled while visually hidden, then enabled for row hover or focus-within.

## 2026-07-25 - Disabled KI-Gen tooltip

- The KI-Gen button is disabled exclusively through `Ai.HasAiConnections`.
- `AiButtonTooltip` already distinguishes that state and explains that a connection must be configured.
- Avalonia 12.1.0 exposes the attached `ToolTip.ShowOnDisabled` property; enabling it is the minimal change required to display the existing reason while the button is disabled.
- The screenshot exposed a global theme mismatch: Fluent's tooltip surface and the app-wide TextBlock foreground could combine into black-on-black text.
- Tooltips now consistently use `DaisyNeutralBrush` with `DaisyNeutralContentBrush`, including nested TextBlocks.
- Per-row delete buttons now set `DaisyErrorBrush` and white foreground as local values so pointer-over styles cannot replace the semantic error color.

## 2026-07-25 - Connection model autocomplete and required name

- The current connection editor already uses `AutoCompleteBox`, `EditAvailableModelOptions`, `ValueMemberBinding=ModelId`, and a two-way free-text binding.
- Avalonia 12.1 documentation confirms `ValueMemberBinding` supplies the displayed text and built-in filtering source, so the current `Contains` filter is conceptually correct.
- The incomplete behavior is popup UX: it does not reliably open for focus/manual entry, and `MaxDropDownHeight=280` is far below the requested 20 visible rows.
- A fixed 30-DIP item row plus a 600-DIP maximum popup height provides an explicit 20-row cap while the popup still remains constrained by screen space.
- The connection name currently has no required-field visual state.

## 2026-07-26 - Codex connection type and connection-test state

- Codex subscription access is a separate connection provider; it must not be represented as an OpenAI API connection.
- Codex connections use the authenticated subscription model catalog and do not expose API key, token, temperature, or capability fields.
- API connection testing uses the connection override first, then an existing connection key, then the provider key.
- Ollama and LMStudio remain testable without a key because their local endpoints do not require one.
- For Codex, the test action is hidden while signed out and visible/enabled while authenticated.
- Real UI validation confirmed API testing is disabled without a key, becomes enabled after entering an unsaved override, and authenticated Codex shows the action with `gpt-5.6-sol`.
## 2026-07-25 - Settings redesign

- The approved prototype uses a persistent left navigation rail and one content surface instead of a top tab strip.
- The KI-Anbieter page must expose genuinely separate Textmodelle and Bildmodelle sections; badges inside one combined popup are explicitly insufficient.
- Each section needs independent query state and a typed item presentation.
- Preserve theme switching through dynamic Daisy resources, typed templates through `x:DataType`, predictable keyboard order, and stable automation names/IDs for navigation and searches.
- Existing uncommitted changes already touch `SettingsWindow.axaml` and `SettingsViewModel.cs`; their exact ownership must be identified before editing.
- The current Settings window is an 800x800 `DaisyTabs` dialog with Allgemein, KI-Verbindungen, KI-Anbieter, and Über as top tabs.
- The connection editor already has an uncommitted `AutoCompleteBox` experiment backed by `AiModelOption`; this remains a single mixed model result list with badges and does not satisfy the requirement.
- Other uncommitted Settings changes implement connection dirty tracking, connection testing, selection retention, provider discovery, and Hugging Face support. They must remain functional during the redesign.
- The design should stay XAML-first, use typed item templates, retain dynamic Daisy brushes, and keep visual/tab/automation order aligned.
- KI-Anbieter currently has a 250 px provider list and one details card containing API key/endpoint, one unfiltered `SelectedProviderModels` list, a single add field, and shared remove/reset actions.
- `SettingsViewModel` exposes only one provider-model collection, one selected model, and one new-model field. Separate sections therefore require separate classified collections, query properties, selected-item properties, and add commands or typed command parameters.
- `AiModelOption` already centralizes the image-generation classification flag and can serve as the typed row model instead of duplicating name heuristics in XAML.
- No Flowery source junction is present in this workspace, and the package assets do not provide an in-repo `DaisyTabs` example with left placement. The redesign should avoid depending on an unverified custom-tab placement API; an explicit navigation selector plus page visibility state is safer.
- The existing untracked `SettingsConnectionTests.cs` contains a regression test for the rejected combined `AutoCompleteBox`; that test should be replaced with assertions for two independently bound provider model sections.
- All 12 localization files carry the same Settings provider key block. New section/search/empty-state labels must be added consistently rather than only to German and English.
- Persistence still stores one combined provider model list. The focused design will keep the existing shared add/reset behavior and classify the stored list into two UI collections; this preserves the data contract and allows known-name heuristics to place custom entries.
- Removal can operate on the typed `AiModelOption` passed from either section, avoiding ambiguous shared selection state and avoiding a settings-schema change.
- The implemented navigation uses explicit named `ListBoxItem` state and stable automation IDs; the reset button remains tied only to the Allgemein navigation item.
- Model search is a viewmodel projection, so it does not mutate or truncate the provider’s persisted catalog.
