# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- Updated all direct NuGet package references to their current stable versions; retained the newer `Material.Icons.Avalonia` prerelease.
- Refreshed the image-generation model catalogs for OpenAI, OpenRouter, and Google, with OpenRouter capability discovery from its public models API.
- Routed direct OpenAI image generation through LLM Tornado's image-generation API.
- Added Hugging Face as a provider with OpenAI-compatible chat, authenticated live model discovery, and HF-Inference text-to-image generation.
- Replaced obsolete Avalonia bitmap encoding and SkiaSharp text drawing overloads.
- Increased the application version to 1.0.2.
- Upgraded all projects to .NET 10 Windows and restricted builds and runtime assets to Windows x64.
- Removed redundant `System.Text.Json` and `System.Net.Http` package references now supplied by .NET 10.
- Kept the AI assistant enabled without a loaded workspace image so text chat and text-to-image generation remain available.

## [1.0.2] - 2026-07-29

### Security

- Updated `Magick.NET-Q8-AnyCPU` to `14.15.0`.

### Fixed

- Prevented parallel localization tests from racing over shared culture state.

## [1.0.1] - 2026-06-17

### Changed

- Updated stable NuGet dependencies, including Flowery.NET, Magick.NET, OpenCvSharp4, System.Text.Json, System.Drawing.Common, System.IdentityModel.Tokens.Jwt, Microsoft.SqlServer.TransactSql.ScriptDom, and LlmTornado.

## [1.0.0] - 2026-06-07

### Changed

- Upgraded the desktop app to Avalonia 12 and Flowery.NET 2.0.6.
- Updated eligible direct NuGet dependencies that were older than seven days, including OpenCvSharp, SkiaSharp, System.Text.Json, System.Drawing.Common, YamlDotNet, LlmTornado, Microsoft.SqlServer.TransactSql.ScriptDom, and MSTest packages.
- Replaced deprecated Avalonia placeholder APIs across the UI with `PlaceholderText`.
- Updated fullscreen image viewer window chrome settings for Avalonia 12.
- Updated MSTest exception assertions for the MSTest 4 assertion API.

### Fixed

- Restored text clipboard copy behavior after Avalonia 12 moved clipboard text helpers to extension methods.
- Preserved duplicate validation-plugin removal behavior after Avalonia 12 made the binding plugin container non-public.

## [0.2.3] - 2026-05-18

### Security

- Updated `Magick.NET-Q8-AnyCPU` from `14.13.0` to `14.13.1` in the app and converters projects to address current Dependabot advisories.

## [0.2.2] - 2026-05-01

### Changed

- Updated stable NuGet dependencies for System.Text.Json, System.Drawing.Common, Magick.NET, OpenCvSharp4, System.IdentityModel.Tokens.Jwt, and LlmTornado.

## [0.2.1] - 2026-04-16

### Fixed

- Updated the OpenCvSharp API usage in `ImageProcessor` from `CmpType` to `CmpTypes` so the solution builds again with the current minor package line.

### Changed

- Applied non-major NuGet updates across the solution, including `CommunityToolkit.Mvvm`, `LlmTornado`, `Microsoft.SqlServer.TransactSql.ScriptDom`, `NAudio`, `OpenCvSharp4`, `OpenCvSharp4.runtime.win`, `PDFsharp`, `QRCoder`, `SkiaSharp`, `System.Drawing.Common`, `System.IdentityModel.Tokens.Jwt`, and `System.Text.Json`.

### Security

- Kept `Magick.NET-Q8-AnyCPU` on `14.12.0` because the Dependabot advisory metadata points to `14.20.0`, but that version is not currently published on NuGet and does not restore or build.
- Verified that `14.20.0` is not consumable from the public NuGet feed, so `14.12.0` is the current buildable fallback until a newer package is actually released.

## [0.2.0] - 2026-04-07

### Changed

- Updated Magick.NET-Q8-AnyCPU from 14.10.0 to 14.11.1 in the app and converters projects to address known security advisories.
- Updated Flowery.NET from 1.8.0 to 1.9.2 and aligned Avalonia package references to 11.3.12.
- Updated System.Text.Json package references to 10.0.5 across affected projects.
- Switched the default Flowery.NET dependency back to the NuGet package instead of a local project reference.

### Fixed

- Resolved NuGet restore and build warnings caused by Avalonia and System.Text.Json package downgrades.

## [0.1.1] - 2025-12-23

### Changed

- Updated Avalonia packages from 11.3.9 to 11.3.10 (required by Flowery.NET).
- Updated System.Text.Json from 10.0.0 to 10.0.1.
- Updated App_Title in all localization files to include ".NET" suffix.

### Fixed

- Fixed deprecation warning in `AsciiArtGenerator.cs` by replacing `SKFilterQuality` with `SKSamplingOptions`.
- Fixed CA1416 platform compatibility warnings in `ImageConverter.cs` by adding `[SupportedOSPlatform("windows")]` attribute.
- Fixed Scientific Calculator: Enter key now always triggers calculation instead of re-activating the last clicked button.
- Fixed Scientific Calculator: Unary minus was being ignored, causing negative results to become positive in subsequent calculations (e.g., `-1522 + 2000` incorrectly returned `3517` instead of `478`).
- App title in all localizations now state ".NET" at the end

## [0.1.0] - 2025-12-20

### Added

- Initial public release.
