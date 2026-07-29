# Task Plan: Upgrade the repository to .NET 10 Windows x64

## Current Follow-up: Add OpenAI-Compatible connections

### Phase 1: Contract and persistence
- [x] Add a distinct OpenAI-Compatible provider type and defaults
- [x] Persist a Base URL per connection without changing official OpenAI or Codex behavior
- **Status:** complete

### Phase 2: UI and routing
- [x] Expose Base URL, optional Bearer key, and free model ID in the connection editor
- [x] Route tests and Assistant requests through LLMTornado `Custom`
- [x] Keep the test action aligned with the model column
- **Status:** complete

### Phase 3: Verification
- [x] Add focused provider, persistence, routing, and XAML regressions
- [x] Normalize changed C#/AXAML once and perform all permitted static checks
- [ ] Build/test only after explicit user authorization required by repository rules
- **Status:** implementation complete; build/test awaiting explicit authorization

## Completed Follow-up: Discard and connection-editor cancel behavior

### Phase 1: Diagnose and correct
- [x] Trace the unsaved-changes dialog result through `CanCloseAsync` and `OnClosing`
- [x] Confirm Discard leaves the editor dirty and therefore reopens the dialog on the recursive close
- [x] Clear connection-edit state when Discard is chosen
- [x] Add the incident-specific regression, normalize changed C# files once, then build and test
- [x] Verify in the real UI that Discard keeps Settings open and exits the connection editor
- [x] Add a red Abbrechen button to the right of Speichern, bind it to `CancelConnectionCommand`, and verify it in the real UI
- [x] Prompt on Abbrechen only after actual user changes; leave a pristine new connection prompt-free
- **Status:** complete

## Completed Follow-up: Add Codex as a connection type

- [x] Add a distinct Codex subscription connection option without conflating it with OpenAI API access
- [x] Route Codex connections through the shared subscription catalog and selected connection in the Assistant
- [x] Hide API-only connection fields for Codex and retain them for API providers
- [x] Enable API connection testing only with an effective key, while preserving keyless local providers
- [x] Show Codex connection testing only while the subscription is authenticated
- [x] Add focused regressions, build against the local LLMTornado fork, and validate the states in the real UI
- **Status:** complete

## Current Follow-up: Align with the LLMTornado PR branch

### Phase 1: Verify and adapt
- [x] Verify the clean local LLMTornado PR branch and compare its public Codex API with the prior integration commit
- [x] Identify Toolkit overrides that are now owned by LLMTornado
- [x] Remove the obsolete Toolkit protocol-version override and update focused regression coverage
- [x] Normalize changed C# files once, then build and test against the local LLMTornado project
- **Status:** complete

## Current Follow-up: Compact provider title and Codex model row

### Phase 1: Implement and verify
- [x] Locate the provider-list title localization and Codex model item template
- [x] Rename the provider-list title to the localized equivalent of "Providers"
- [x] Render Codex model choices as one display-name line
- [x] Add focused regression coverage, normalize changed C#/AXAML once, then build and validate the UI
- **Status:** complete

## Current Follow-up: Persist OpenAI access mode and keep speed selection visible

### Phase 1: Diagnose restart and selection state
- [x] Verify how the saved OAuth credentials, active access mode, and Settings selectors are restored
- [x] Trace the blank Fast selection to synchronous collection rebuilding during ComboBox selection
- **Status:** complete

### Phase 2: Implement and verify
- [x] Persist and restore the selected OpenAI access mode, including silent subscription reconnection
- [x] Defer access-state projection so reasoning/speed selections are not rebuilt reentrantly
- [x] Add restart/selection regressions, normalize changed source files without Perl, then build and test
- **Status:** complete

## Current Follow-up: Subscription connect state, effort, and speed

### Phase 1: Inspect available metadata and turn options
- [x] Verify the current LLMTornado Codex model metadata and turn-option surfaces for reasoning effort and speed
- [x] Inspect the live model-catalog schema narrowly for optional speed/service-tier metadata
- **Status:** complete

### Phase 2: Implement and verify
- [x] Make Connect a Primary action that is disabled while already connected
- [x] Add model-dependent effort and speed settings where supported and route selections into subscription turns
- [x] Restore the configured AI connection selector in the Assistant without restoring authentication controls
- [x] Add incident-specific regressions, verify encoding without Perl, then build and test
- **Status:** complete

## Current Follow-up: OAuth subscription model discovery failure

### Phase 1: Diagnose the model endpoint
- [x] Trace the exact LLMTornado OAuth model request, response handling, and app-state mutation
- [x] Reproduce the failure without exposing stored credentials and identify the HTTP/API mismatch
- **Status:** complete

### Phase 2: Correct and verify
- [x] Represent authentication versus catalog errors correctly and provide a working catalog retry
- [x] Restore dynamic subscription model discovery for direct OAuth
- [x] Add incident-specific regressions, normalize once, then build and test
- **Status:** complete

## Current Follow-up: OAuth completion must update OpenAI Settings

### Phase 1: Diagnose the browser return path
- [x] Reproduce the state transition in credential-free tests and trace LLMTornado OAuth completion
- [x] Determine whether completion is lost in the browser callback, session adapter, shared manager, or Settings projection
- **Status:** complete; callback and shared state succeeded, presentation is misleading

### Phase 2: Correct and verify
- [x] Implement the smallest reliable completion/state-update fix
- [x] Make authenticated state unmistakable and update command enablement
- [x] Remove authentication, subscription model selection, and login/logout controls from the AI Assistant UI
- [x] Add incident-specific regressions, normalize changed source/XAML once, then build and test
- **Status:** complete

## Current Follow-up: ChatGPT subscription access through local LLMTornado

### Correction: expose subscription authentication in OpenAI Settings
- [x] Inspect the current OpenAI provider page, Settings viewmodel composition, launcher wiring, and existing Settings tests
- [x] Add an explicit API/Codex authentication section to the OpenAI provider page without changing API-key behavior
- [x] Share the existing app-scoped subscription state, dynamic models, login/logout, and mode switching with Settings
- [x] Add focused Settings regressions, normalize changed source/XAML once, then build and test
- **Status:** complete

### Phase 1: Inspect and establish local dependency
- [x] Verify the LLMTornado checkout, commit, repository rules, solution/project paths, and supported target frameworks
- [x] Attempt the `D:\github\LLMTornado\src\LlmTornado.slnx` Release build before application changes (blocked by the solution's Docs project because `npm run build` has no matching package script)
- [x] Trace the consuming app's target framework, LLMTornado package reference, DI, authentication UI, model state, chat/image flows, and tests
- **Status:** complete with documented upstream Docs build failure

### Phase 2: Design and implement
- [x] Add mutually exclusive conditional PackageReference/ProjectReference selection using `UseLocalLlmTornado` and `LlmTornadoProject`
- [x] Encapsulate API-key, Codex app-server, and direct OAuth modes behind app-owned capability-aware abstractions
- [x] Keep API-key text/image behavior intact while adding subscription authentication, dynamic models, streaming threads, follow-ups, logout, and correct session disposal
- [x] Update the Avalonia UI so image generation is available only for OpenAI API access and the app-server requirement is clear
- **Status:** complete

### Phase 3: Verify
- [x] Add focused tests for newly introduced app state and routing without real credentials
- [x] Normalize changed `.cs`, `.csproj`, and `.axaml` files once at iteration end
- [x] Build the full app with the local LLMTornado reference and prove the resolved local project is used
- [x] Run all credential-free unit tests sequentially and record any interactive checks still pending
- **Status:** complete with documented pre-existing localization test-order failure

### Constraints
- Do not start a real browser OAuth login without explicit user approval.
- Never log access, ID, or refresh tokens.
- Preserve all pre-existing worktree changes and avoid broad rewrites.
- Use native Git Bash and RTK; invoke `dotnet` only through the requested documented build/test paths.

### Errors Encountered
| Error | Attempt | Resolution |
|-------|---------|------------|
| LLMTornado Release solution build failed in `LlmTornado.Docs.csproj` because `npm run build` reported a missing script | 1 | Retained the full log and did not run an unauthorized alternate build; inspect the documented Docs target and continue read-only architecture analysis |
| Combined ClientApp/Codex path check did not identify which expected path was absent | 1 | Stop the combined shape; verify each path independently and enumerate the Docs package files narrowly |
| RTK-proxied `sed` failed with `Bad locale: LC_ALL=C.UTF-8` while resuming plan files | 1 | Do not retry the same command shape; use RTK-proxied `head` for the required short plan refresh |
| RTK could not resolve Git Bash `awk` while reading a source range | 1 | Do not retry with `awk`; compose the bounded range from RTK-proxied `head` and `tail` |
| Git Bash path conversion stripped the leading slash from MSBuild `/p:` arguments, causing MSB1008 before build execution | 1 | Rebuild the argument array with MSYS path conversion disabled for the native dotnet invocation |
| The corrected MSBuild invocation combined native properties with a POSIX solution path, causing MSB1001 before compilation | 1 | Use native Windows paths for the solution, local project, and log while path conversion is disabled |
| A broad `type: project` assets search produced excessive unrelated output | 1 | Query the exact `LlmTornado.csproj` entry and its surrounding schema only |
| A multi-target configuration search included an unverified, absent LLMTornado `Directory.Build.props` path | 1 | Verify each path first; no such LLMTornado props file exists, so inspect the verified project and repository props separately |
| Piping the full LLMTornado log search into `head` caused an expected RTK broken-pipe diagnostic | 1 | Use exact output-path patterns without truncating an active RTK pipeline |
| `AdditionalProperties=Configuration=Release` evaluated correctly but the external solution reference still reused Debug output | 1 | Also set the ProjectReference's explicit `SetConfiguration` metadata used by solution builds |
| Querying a solution with MSBuild `-getProperty` failed with MSB1063 | 1 | Inspect the verified SDK targets and project-level evaluated items; solution files do not support that query |
| Explicit `SetConfiguration` metadata was overwritten and LLMTornado still resolved to Debug | 1 | The SDK target shows solution builds unset parent configuration for unassigned external references; disable that behavior only in the local-reference app project |
| The app project built LLMTornado in Release, but the test project's transitive graph invoked the external project again in Debug | 1 | Apply the conditional parent-configuration retention repository-wide through `Directory.Build.props` so every local-reference graph node uses Release |
| The first full test invocation could not write its `C:\tmp` log inside the sandbox | 1 | Rerun the same authorized test entry point outside the sandbox |
| Full test runs each failed one existing localization test because global culture state leaked between tests | 2 | Both exact failing tests passed in isolation; all 17 focused AI tests passed, so leave unrelated localization behavior unchanged |
| The first correction full-suite run timed out in two DNS tests under restricted networking and hit the known order-dependent localization assertion | 1 | The localization test passed alone, the DNS tests passed with network access, and the repeated full network-enabled suite passed all 220 tests |
| Read-only LLMTornado Git verification hit sandbox dubious-ownership protection | 1 | Repeat the same status and HEAD checks under the repository owner's approved context |
| Disabling MSYS conversion also left the POSIX solution path unchanged, causing MSB1001 before compilation | 2 | Use only verified native Windows paths in the conversion-disabled argument array |
| Combined English/German localization patch assumed the wrong capitalization for the English test-connection value | 1 | No localization file was changed; use the exact verified key line as the only patch context |
| Combined OAuth UI correction patch included BOM-sensitive first-line context in `AiAssistantPanel.axaml.cs` | 1 | No file was changed; split the correction into smaller file-scoped patches and start code-behind edits after verified non-first-line context |
| Combined findings/progress note patch used progress lines as findings context | 1 | No file was changed; update each planning file with its own verified section context |
| Final combined planning update again used progress lines as findings context | 1 | No file was changed; apply final findings and progress updates as separate file-scoped patches |

## Current Follow-up: Editable searchable connection model picker and required name

### Phase 1: Inspect
- [x] Trace the existing model field, provider model source, free-text persistence, and save/test paths
- [x] Verify Avalonia 12.1 AutoCompleteBox filtering and popup APIs
- [x] Trace connection-name validation and dirty/save state
- **Status:** complete

### Phase 2: Implement
- [x] Keep arbitrary model IDs editable while opening/filtering the known model list incrementally
- [x] Limit the suggestion popup to 20 visible model rows
- [x] Preserve model badges, keyboard selection, provider changes, testing, and persistence
- [x] Give an empty required connection name a red error border
- **Status:** complete

### Phase 3: Verify
- [x] Add focused regression coverage
- [x] Perform a critical source review
- [x] Normalize changed `.cs` and `.axaml` files once
- [x] Run permitted build/tests and restart only when the running app no longer locks output
- **Status:** complete

### Validation note
- The first build exposed an Avalonia 12 event-argument mismatch; the focus handler now uses the routed event type.
- Final build: 16 projects, 0 warnings, 0 errors.
- Final tests: 209 passed.

## Current Follow-up: Disabled KI-Gen tooltip and tooltip/delete contrast

### Implementation
- [x] Show the existing connection-required KI-Gen tooltip while the button is disabled
- [x] Keep per-row delete buttons on the semantic error color during hover
- [x] Give application tooltips contrasting Daisy neutral surface/content colors
- [x] Preserve dynamic theme switching and accessibility metadata
- **Status:** complete

### Verification
- [x] Normalize changed `.cs` and `.axaml` files once for this iteration
- [x] Build with 0 warnings and 0 errors
- [x] Run focused AI Assistant and Settings regression tests
- [x] Start the current win-x64 output without monitoring or CUA
- **Status:** complete

### Validation note
- The first build attempt was blocked by the previously launched app holding output DLLs. After the user closed it, the rebuild succeeded.
- The full suite passed 207 of 208 tests; the same unrelated localization event test remains order-dependent. All 23 focused tests passed.

## Current Follow-up: Resizable and independently filtered provider model lists

### Phase 1: Inspect
- [x] Trace the two search bindings, projected collections, row templates, and current layout rows
- [x] Verify existing selection and persistence paths remain shared only where intended
- **Status:** complete

### Phase 2: Implement
- [x] Add a real GridSplitter between the text and image model areas
- [x] Ensure each search property filters only its matching collection
- [x] Show each row's delete action only on row hover or keyboard focus
- [x] Preserve row selection, clickability, automation metadata, and persisted model data
- **Status:** complete

### Phase 3: Verify
- [x] Add or adjust focused source/viewmodel regression coverage
- [x] Normalize changed `.cs` and `.axaml` files once at iteration end
- [x] Perform static validation and a critical self-review without CUA
- [x] Build successfully and run focused regression tests
- **Status:** complete

### Validation note
- The full 207-test run had one unrelated failure in `ToolkitLocalization_CultureChanged_FiresEvent`; the same test passed in isolation, and all 13 focused Settings tests passed.

## Current Follow-up: Use DaisyButton throughout Settings

### Phase 1: Inspect
- [x] Verify the actual Flowery `DaisyButton` API and theme variants
- [x] Inventory XAML, navigation, footer, page, and code-created dialog buttons
- [x] Confirm the final requirement: explicit suitable non-Ghost variant on every button
- **Status:** complete

### Phase 2: Implement
- [x] Use `DaisyButton` for all Settings navigation actions
- [x] Retain all existing page/footer DaisyButtons and make every variant explicit
- [x] Replace code-created save/discard/cancel buttons with DaisyButton
- [x] Preserve commands, enabled states, accessibility IDs, selection, and layout
- [x] Add focused regression coverage
- **Status:** complete

### Phase 3: Verify
- [x] Normalize changed `.cs` and `.axaml` files
- [x] Run documented build and tests sequentially
- [x] Start the updated win-x64 application and confirm its window appears
- **Status:** complete

## Current Follow-up: Fix unresponsive AI Settings toolbar button

### Phase 1: Diagnose
- [x] Inspect the actual toolbar enablement and hit-testing state
- [x] Trace the routed Click handler through MainWindow to SettingsWindow
- [x] Inspect the initial Settings navigation selection
- **Status:** complete

### Phase 2: Correct
- [x] Replace the custom themed toolbar control with a normal Avalonia Button
- [x] Make enablement, hit-testing, focus, and automation semantics explicit
- [x] Select AI Connections through the Settings ListBox itself
- [x] Strengthen focused regression coverage
- **Status:** complete

### Phase 3: Verify
- [x] Normalize changed `.cs` and `.axaml` files once at iteration end
- [x] Validate XAML parsing, scoped diffs, and source invariants
- [ ] Run build and tests after the user permits closing/rebuilding the running app
- **Status:** complete within the running-app constraint

## Current Follow-up: Always-available AI Settings toolbar button

### Phase 1: Inspect
- [x] Locate the image-editor toolbar and existing AI Settings dialog path
- [x] Confirm the Settings navigation target for AI connections
- [x] Identify unrelated worktree changes that must remain untouched
- **Status:** complete

### Phase 2: Implement
- [x] Add an icon-only AI Settings button between AI Gen and Sessions
- [x] Keep the button enabled independently of images, providers, and connections
- [x] Open Settings directly on AI Connections
- [x] Add localized tooltip/accessibility text and focused source coverage
- **Status:** complete

### Phase 3: Verify
- [x] Normalize changed `.cs` and `.axaml` files once at iteration end
- [x] Build the solution and run all tests sequentially
- [x] Start the updated application and verify the new toolbar icon is visible
- **Status:** complete

## Current Follow-up: Modernize Settings and separate provider model catalogs

### Phase 1: Analyze current UI and prototype
- [x] Inspect the prototype and record its navigation/model-catalog structure
- [x] Trace the current Settings XAML, viewmodel state, localization, and tests
- [x] Identify unrelated worktree changes that must be preserved
- **Status:** complete

### Phase 2: Implement focused Settings redesign
- [x] Replace the top tab strip with clear Settings page navigation
- [x] Present text models and image-generation models as separate sections on KI-Anbieter
- [x] Give each model section independent search/filter state
- [x] Preserve provider, connection, and model-selection behavior
- [x] Update localization and focused regression coverage where supported
- **Status:** complete

### Phase 3: Verify
- [x] Normalize changed `.cs` and `.axaml` files once at iteration end
- [x] Run user-permitted build and tests sequentially
- [x] Visually verify every Settings page and the two model searches
- **Status:** complete

### Current design findings
- The prototype uses persistent left navigation with a single page content surface.
- KI-Anbieter contains distinct Textmodelle and Bildmodelle cards, each with its own search.
- Current on-disk work already modifies Settings XAML and viewmodel files; those edits must be separated from this task before patching.

## Goal
Increase the application version, move every project from .NET 8 to .NET 10 Windows, constrain runtime assets and execution to win-x64, and keep text-to-image chat usable without a loaded source image, with restore/build/test closure.

## Current Phase
Settings persistence regression protection: complete

## Current Follow-up: Protect settings from destructive overwrite

### Phase 1: Diagnose
- [x] Confirm the overwritten file contains defaults and zero AI connections
- [x] Trace direct non-atomic writes, fallback-to-default behavior, and startup locale saves
- [x] Identify stale-process overwrite risk
- **Status:** complete

### Phase 2: Implement
- [x] Replace direct writes with atomic file replacement
- [x] Add three rotating backup slots with UTC timestamps
- [x] Restore the newest valid backup after invalid JSON
- [x] Preserve corrupt/conflicting snapshots and reject stale-process writes
- [x] Prevent AI synchronization errors from discarding loaded settings
- **Status:** complete

### Phase 3: Verify
- [x] Add backup rotation, truncated-file recovery, older-backup fallback, first-start, locale-save, stale-writer, concurrent-writer, and abandoned-mutex regression tests
- [x] Normalize changed C# files once
- [x] Build all projects and run all tests sequentially
- **Status:** complete

## Current Follow-up: Correct chat bubble layout and actions

### Phase 1: Correct diagnosis
- [x] Compare the rendered screenshot with the outer responsive-width implementation
- [x] Confirm the outer control is constrained but `PART_Bubble` still overflows internally
- **Status:** complete

### Phase 2: Implement
- [x] Apply the responsive width directly to Flowery's internal `PART_Bubble` through a permitted global descendant style
- [x] Strengthen regression coverage so it checks the internal template border, not only the outer control
- [x] Constrain the internal content width by the bubble's horizontal padding
- [x] Use 8 px padding for user bubbles and render their timestamp outside Flowery's broken end-footer layout
- [x] Give Copy and Delete the same background while coloring only the Delete glyph red
- **Status:** complete

### Phase 3: Verify
- [x] Compile the corrected XAML successfully
- [x] Run all tests sequentially
- [x] Normalize changed `.cs`, `.csproj`, and `.axaml` files once per correction iteration
- [x] Launch and visually validate the real Windows application with CUA
- **Status:** complete

## Current Follow-up: Wrap, copy, and delete individual chat messages

### Phase 1: Inspect
- [x] Inspect every chat-message template and its effective width constraints
- [x] Trace existing per-message copy handling and clipboard command wiring
- [x] Trace message persistence/change notifications needed for individual deletion
- **Status:** complete

### Phase 2: Implement
- [x] Make every message wrap within the available chat-panel width
- [x] Add a visible copy action to every individual message
- [x] Add a visible delete action to every individual message
- [x] Add focused regression coverage
- **Status:** complete

### Phase 3: Verify
- [x] Normalize changed `.cs`, `.csproj`, and `.axaml` files once
- [x] Build and run all tests sequentially
- **Status:** complete

## Current Follow-up: Show loading shade while switching strip images

### Phase 1: Inspect
- [x] Trace the image-strip selection command through workspace image loading
- [x] Identify that no existing state narrowly spans the thumbnail decode/load operation
- [x] Locate the main-editor image container and current overlay composition
- **Status:** complete

### Phase 2: Implement
- [x] Expose a narrowly scoped image-switch loading state
- [x] Add a theme-aware, input-blocking shade over the main image
- [x] Add focused regression coverage
- **Status:** complete

### Phase 3: Verify
- [x] Normalize changed `.cs`, `.csproj`, and `.axaml` files once
- [x] Build and run all tests sequentially
- **Status:** complete

## Current Follow-up: Fix late-bound AI chat clipboard

### Phase 1: Inspect
- [x] Verify toolbar and per-message commands produce non-empty text
- [x] Verify panel DataContext is supplied by a binding
- [x] Identify clipboard wiring is limited to `OnLoaded`
- **Status:** complete

### Phase 2: Implement and verify
- [x] Wire from the parent view's DataContext lifecycle
- [x] Await and flush clipboard writes through async commands
- [x] Add wiring, complete-message, and whole-chat regression coverage
- [x] Normalize, build, and run all tests
- **Status:** complete

## Current Follow-up: Fix OpenRouter Gemini image request

### Phase 1: Inspect
- [x] Trace the exact request path and serialized payload
- [x] Compare it with current official OpenRouter and Google image-generation contracts
- [x] Check current LLM Tornado support for the required fields
- **Status:** complete

### Phase 2: Implement
- [x] Correct the OpenRouter image-generation request path and parameter mapping
- [x] Add focused regression coverage for Gemini/OpenRouter payloads
- **Status:** complete

### Phase 3: Verify
- [x] Run the iteration's single source normalization
- [x] Build and run all tests sequentially
- **Status:** complete

## Current Follow-up: Fix AI error clipboard action

### Phase 1: Inspect
- [x] Trace the error bubble click handler and message command
- [x] Find where `CopyToClipboardAction` is wired to the active TopLevel clipboard
- **Status:** complete

### Phase 2: Implement
- [x] Wire clipboard access from the view layer using the active TopLevel
- [x] Add regression coverage for the actual copy path
- **Status:** complete

### Phase 3: Verify
- [x] Run the iteration's single source normalization
- [x] Build and run all tests sequentially
- **Status:** complete

## Current Follow-up: Exclude Settings from remembered sidebar selection

### Phase 1: Inspect
- [x] Trace Flowery sidebar selection and persistence order
- [x] Identify startup restore and in-session highlight behavior
- **Status:** complete

### Phase 2: Implement
- [x] Restore the last navigable item after Settings is invoked
- [x] Prevent persisted `settings` state from being restored at startup
- [x] Add Test Connection to the connection editor
- [x] Disable Save after a successful save and re-enable it for every edit-field change
- [x] Add regression coverage
- **Status:** complete

### Phase 3: Verify
- [x] Run the iteration's single source normalization
- [x] Build and run all tests sequentially
- **Status:** complete

## Current Follow-up: AI settings navigation and connection selection

### Phase 1: Inspect
- [x] Trace connection selection, edit, save, and collection replacement
- [x] Identify why the saved connection remains visible but the editor becomes empty
- **Status:** complete

### Phase 2: Implement
- [x] Add the AI Assistant settings cog and route it through the existing Settings dialog
- [x] Preserve or restore the edited connection selection after saving
- [x] Add regression coverage
- **Status:** complete

### Phase 3: Verify
- [x] Run the final one-time BOM/CRLF normalization for this iteration
- [x] Build the solution and run all tests sequentially
- **Status:** complete

## Current Follow-up: Accessible AI error messages

### Follow-up Phase 1: Inspect and design
- [x] Inspect chat bubble templates, message model, and existing clipboard handlers
- [x] Identify the exact layout cause of clipped error text
- **Status:** complete

### Follow-up Phase 2: Implementation
- [x] Make provider errors wrap, scroll, and remain selectable
- [x] Add a visible per-error copy action
- **Status:** complete

### Follow-up Phase 3: Verification
- [x] Add focused regression coverage
- [x] Run build and tests sequentially
- [x] Normalize changed `.cs`, `.csproj`, and `.axaml` files once
- **Status:** complete

## Current Phases

### Phase 1: Inventory and design
- [x] Find the authoritative application-version source
- [x] Inventory every target framework and runtime-related MSBuild setting
- [x] Attribute the large `runtimes` directory to exact packages/assets
- **Status:** complete

### Phase 2: Implementation
- [x] Bump the application version
- [x] Change all projects and documentation from .NET 8 to .NET 10 Windows
- [x] Enforce win-x64 runtime/platform selection
- **Status:** complete

### Phase 3: Verification
- [x] Fix AI-chat enablement for text-to-image without an input image
- [x] Add regression coverage that the AI panel remains enabled without an image
- [x] Restore, build, and test sequentially
- [x] Verify output layout and runtime contents
- [x] Normalize changed source/project files once and verify the diff
- **Status:** complete

### Phase 4: Delivery
- [x] Review scope and results
- [x] Report version, framework, RID, output size, and validation results
- **Status:** complete

## Previous Completed Task: Hugging Face provider and warning cleanup

### Phase 1: Requirements and discovery
- [x] Capture user requirements and repository constraints
- [x] Inspect provider/configuration architecture and warning call sites
- [x] Verify current Hugging Face API contracts from official English documentation
- **Status:** complete

### Phase 2: Technical design
- [x] Choose supported Hugging Face tasks and endpoint/authentication mapping
- [x] Identify UI, persistence, provider-routing, and test changes
- [x] Record decisions in findings.md
- **Status:** complete

### Phase 3: Implementation
- [x] Replace obsolete Avalonia and SkiaSharp calls
- [x] Add Hugging Face provider across shared and desktop AI layers
- [x] Add/update tests and documentation
- **Status:** complete

### Phase 4: Verification
- [x] Run restore/build/tests sequentially
- [x] Fix failures without workaround builds
- [x] Run one final BOM/CRLF normalization for changed .cs/.csproj/.axaml files
- [x] Verify diff and provider/model exclusions
- **Status:** complete

### Phase 5: Delivery
- [x] Review changed scope and validation results
- [x] Report supported HF behavior and remaining limitations
- **Status:** complete

## Key Questions
1. Which Hugging Face API surface supports both chat and image generation with a stable OpenAI-compatible contract?
2. How should models be discovered and classified for the existing combined model dropdown?
3. What exact replacement signatures remove the Avalonia and SkiaSharp warnings without behavior changes?

## Decisions Made
| Decision | Rationale |
|----------|-----------|
| Use official English vendor documentation only | User explicitly prohibited Chinese websites |
| Preserve all existing uncommitted task changes | They belong to the current requested update set |
| Use `https://router.huggingface.co/v1` for HF chat | Official OpenAI-compatible endpoint minimizes duplicated chat code |
| Use native HF text-to-image HTTP contract | Official docs state the OpenAI-compatible endpoint is chat-only |

## Errors Encountered
| Combined localization patch used unstable translated-line context | 1 | Patch was atomic and wrote nothing; reapplied using only the stable localization key line |
| RTK-filtered `rg --files` output added line numbers to JSON paths | 1 | Replaced the pipeline with direct `find -print0` enumeration |
| Provider-section regression looked for `AutomationId` instead of XAML's dotted attached-property name | 1 | Corrected the test to match `AutomationProperties.AutomationId` |
| Post-normalization test patch introduced mixed CRLF/LF in the test file | 1 | Restored that file to UTF-8 BOM with CRLF and verified all three iteration source files |
| Error | Attempt | Resolution |
|-------|---------|------------|
| First AI-panel regression test constructed Avalonia UI on a non-owner MSTest thread | 1 | Parse the XAML source and assert the named AI panel has no `IsEnabled` binding, avoiding dispatcher and persisted-session side effects |
| Test search for `AppSettings.Current` returned no matches with unhandled `rg` exit 1 | 1 | Treat the valid no-match result explicitly and avoid relying on AppSettings-based test setup |
| WMIC status query with `/value` produced an invalid GET expression under Git Bash | 1 | Drop the formatting switch and use the previously verified plain WMIC column query |
| Post-normalization build could not overwrite DLLs locked by running app PID 32488 | 1 | Inspect process attribution first; do not terminate because the assistant did not start it |
| `tasklist.exe` filter switch was path-converted by Git Bash/MSYS | 1 | Do not retry that command shape; use WMIC read-only process and owner queries instead |
| First test run aborted all 172 tests because explicit Win32/Skia startup had no text shaping system | 1 | Add `UseHarfBuzz()` to both explicit Avalonia bootstrap chains before rerunning build/tests |
| First .NET 10 build reported SYSLIB0060 for the PBKDF2 constructor | 1 | Replace the constructor/stateful reads with one static 48-byte PBKDF2 derivation and split it into the same 32-byte key and 16-byte IV |
| .NET 10 restore reported NU1510 for explicit framework package references | 1 | Remove only the four package references identified as redundant: `System.Text.Json` in three projects and `System.Net.Http` in the AI project |
| Combined HF patch targeted `SupportedProviders` in the wrong file | 1 | No changes were written; split the patch by verified file location |
| Standalone AI build failed with three missing `StringComparison` references | 1 | Qualified the references with `System.StringComparison`; solution builds had not compiled this omitted project |
| First-line import patch did not match the BOM-prefixed file | 1 | Avoided first-line context and patched the verified method body instead |
| Final cog-button build was blocked by a user-running app instance (PID 45580) | 1 | Preserved the process and deferred build/test until the app is closed |
| Scoped `rg` call placed `--glob` options after paths, so Windows treated them as file paths | 1 | Do not repeat that shape; use options before the assigned target path in any follow-up search |
| Flowery interface search repeated the same invalid post-path `--glob` placement | 2 | Stop using that command shape; inspect the already identified concrete interface file directly |
| Local Flowery source and test-project paths were inferred incorrectly | 1 | Resolve exact paths from `project.assets.json` and `rg --files` before access |
| First follow-up build used a non-public Flowery `SelectItem` method | 1 | Use the public `SelectedItem` property and `StateStorageProvider.SaveLines`, preserving collapsed categories |
| Clipboard wiring patch included BOM-prefixed first-line context | 1 | Patch imports from the verified second line, then patch method bodies separately |
| Combined web-open call failed to parse before execution | 1 | Split official OpenRouter API and documentation opens into separate calls |
| Endpoint-capability projection assumed a `.data[]` array | 1 | Inspect the exact endpoint response schema before projecting provider capabilities |
| Assumed a standalone `OpenSourceToolkit.AI.Tests` directory existed | 1 | Use `rg --files` to resolve actual test projects before further access |
| OpenRouter test patch included BOM-prefixed first-line context | 1 | Patch imports from the verified second line and add test methods separately |
| Clipboard ownership patch included BOM-prefixed first-line context | 1 | Remove imports from verified non-first-line context and patch parent/panel separately |
| Repository-wide helper search expanded `rg --files` into a second `rg` argument list | 1 | Stop that command shape; inspect the already identified concrete scripts path directly |
| Assumed the default localization file was under `OpenSourceToolkit.NET/Localization` | 1 | Do not retry the inferred path; resolve the exact `.resx` location with `rg --files` first |
| Assumed the previously documented `flowery` junction still existed | 1 | Stop using that path; derive the installed Flowery.NET package root from the current `project.assets.json` package folders |
| `xmllint` is not installed in native Git Bash | 1 | Do not retry that tool; rely on the authorized Avalonia XAML compilation during the normal solution build |

## Notes
- Repository shell policy requires native Git Bash and RTK.
- User explicitly authorized test execution during verification.
- BOM normalization applies only to .cs, .csproj, and .axaml and runs once at the end of the iteration.
