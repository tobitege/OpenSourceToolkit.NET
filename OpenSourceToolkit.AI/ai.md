# OpenSourceToolkit.AI

A .NET Standard 2.0 library providing a unified abstraction layer for multiple AI providers, designed for integration with the OpenSourceToolkit.NET Avalonia application.

## Features

- **Multi-provider support**: OpenAI, OpenRouter, Hugging Face, Anthropic (Claude), Google (Gemini), Ollama, LM Studio
- **Unified API**: Single interface for all providers
- **Multi-modal support**: Text and image inputs
- **Image generation**: OpenAI, OpenRouter, Hugging Face Inference, and Google
- **Streaming**: Callback-based streaming for real-time responses (C# 7.3 compatible)
- **Secure error handling**: API keys are automatically redacted from error messages
- **Connection management**: Named connections with per-connection or per-provider API keys

## Project Structure

```txt
OpenSourceToolkit.AI/
├── AiProviderType.cs          # Enum: OpenAI, OpenRouter, HuggingFace, Anthropic, Google, Ollama, LMStudio
├── IAiProvider.cs             # Provider interface
├── IAiService.cs              # High-level service interface
├── AiService.cs               # Service implementation with secure error handling
├── AiProviderFactory.cs       # Creates providers from settings
├── Models/
│   ├── AiProviderSettings.cs  # Provider config, default models, image model detection
│   ├── AiSettingsManager.cs   # Settings management with secure API key storage
│   ├── ChatMessage.cs         # Message with role, content, optional images
│   ├── ChatRequest.cs         # Request with messages, tokens, temperature
│   ├── ChatResponse.cs        # Response with content, tokens, success/error
│   ├── ImageGenerationRequest.cs  # Image generation parameters
│   └── ImageGenerationResponse.cs # Generated images with metadata
└── Providers/
    ├── BaseProvider.cs            # Abstract base with HttpClient, JSON helpers
    ├── OpenAiCompatibleProvider.cs # OpenAI, OpenRouter, LM Studio
    ├── HuggingFaceProvider.cs     # HF chat router and HF-Inference image generation
    ├── AnthropicProvider.cs       # Anthropic Claude API
    ├── GoogleProvider.cs          # Google Gemini API
    └── OllamaProvider.cs          # Local Ollama API
```

## Target Framework

- **netstandard2.0** (compatible with .NET Framework 4.7.2+, .NET Core 2.0+, .NET 5+)
- **C# 7.3** (no async streams, uses callback-based streaming instead)

## Dependencies

- `System.Text.Json` 8.0.5
- `System.Net.Http` 4.3.4

## Usage

### Basic Completion

```csharp
var service = new AiService();
service.Configure(new AiProviderSettings
{
    ProviderType = AiProviderType.OpenAI,
    ApiKey = "sk-...",
    Endpoint = "https://api.openai.com/v1",
    ModelId = "gpt-4o",
    MaxTokens = 4096,
    Temperature = 0.7
});

string response = await service.CompleteAsync("Hello, world!");
```

### Multi-modal (with image)

```csharp
byte[] imageData = File.ReadAllBytes("image.png");
string response = await service.CompleteAsync(
    "Describe this image",
    imageData,
    "image/png"
);
```

### Streaming (callback-based)

```csharp
await service.StreamAsync("Tell me a story", chunk =>
{
    Console.Write(chunk); // Called for each token
});
```

### Full Request Control

```csharp
var request = new ChatRequest();
request.Messages.Add(ChatMessage.System("You are a helpful assistant."));
request.Messages.Add(ChatMessage.User("What is 2+2?"));
request.MaxTokens = 100;

ChatResponse response = await service.CompleteAsync(request);
if (response.IsSuccess)
{
    Console.WriteLine(response.Content);
    Console.WriteLine($"Tokens: {response.PromptTokens} + {response.CompletionTokens}");
}
```

### Image Generation

```csharp
// Simple generation
var response = await service.GenerateImageAsync("A sunset over mountains");
if (response.IsSuccess && response.Images.Count > 0)
{
    byte[] imageData = response.Images[0].Data;
    File.WriteAllBytes("generated.png", imageData);
}

// OpenAI gpt-image-2 with full control
var request = new ImageGenerationRequest("A futuristic city")
{
    Model = "gpt-image-2",
    Size = "1536x1024",           // Landscape
    Quality = "high",             // high/medium/low/auto
    Background = "transparent",   // transparent/opaque/auto
    OutputFormat = "webp",        // png/jpeg/webp
    OutputCompression = 90        // 0-100% for webp/jpeg
};
var response = await service.GenerateImageAsync(request);

// Check token usage
if (response.TotalTokens.HasValue)
{
    Console.WriteLine($"Tokens: {response.InputTokens} in, {response.OutputTokens} out");
}

// OpenRouter with Gemini image model
var request = new ImageGenerationRequest("A mountain landscape")
{
    Model = "google/gemini-3.1-flash-image",
    AspectRatio = "16:9"          // OpenRouter/Gemini aspect ratio
};
var response = await service.GenerateImageAsync(request);
```

### Image Editing (OpenAI)

When an input image is provided, OpenAI uses the `/images/edits` endpoint:

```csharp
// Edit an existing image
byte[] sourceImage = File.ReadAllBytes("photo.png");
var request = new ImageGenerationRequest("Add a rainbow to the sky")
{
    Model = "gpt-image-2",
    InputImage = sourceImage,
    InputImageMimeType = "image/png",
    Size = "1024x1024",
    Quality = "high"
};
var response = await service.GenerateImageAsync(request);
```

**Note**: OpenAI automatically routes to the correct endpoint:

- No `InputImage` → `/images/generations` (create from scratch)
- With `InputImage` → `/images/edits` (edit existing image, multipart/form-data)

## Secure Error Handling

The `AiException` class automatically sanitizes error messages to prevent API key leakage:

```csharp
try
{
    await service.CompleteAsync("test");
}
catch (Exception ex)
{
    // Safe for display to end users - API keys are redacted
    string safeMessage = AiException.GetUserFriendlyMessage(ex);
}
```

## Supported Providers

| Provider | Multi-modal | Streaming | Image Gen | Image Edit | Endpoint |
|----------|-------------|-----------|-----------|------------|----------|
| OpenAI | ✓ | ✓ | ✓ | ✓ | api.openai.com/v1 |
| OpenRouter | ✓ | ✓ | ✓ | ✓ | openrouter.ai/api/v1 |
| Hugging Face | ✓ | ✓ | ✓ | ✗ | router.huggingface.co/v1 |
| Anthropic | ✓ | ✓ | ✗ | ✗ | api.anthropic.com/v1 |
| Google | ✓ | ✓ | ✓ | ✗ | generativelanguage.googleapis.com/v1beta |
| Ollama | ✓ | ✓ | ✗ | ✗ | localhost:11434 |
| LM Studio | ✓ | ✓ | ✗ | ✗ | localhost:1234/v1 |

## Default Models

### Chat/Completion Models (as of Nov 2025)

- **OpenAI**: gpt-5.1, gpt-4.1, gpt-4.1-mini, gpt-4o, o3, o1
- **OpenRouter**: anthropic/claude-sonnet-4.5, anthropic/claude-opus-4.5, openai/gpt-5.1, google/gemini-3-pro-preview
- **Hugging Face**: openai/gpt-oss-120b; the desktop app refreshes the full chat catalog from the HF router
- **Anthropic**: claude-opus-4-5-20251101, claude-sonnet-4-5-20251022
- **Google**: gemini-3-pro-preview
- **Ollama**: llama3.2, gemma2, mistral, llava
- **LM Studio**: local-model

### Image Generation Models (as of July 2026)

- **OpenAI**: gpt-image-2
- **OpenRouter**: refreshed from OpenRouter's image-input/image-output models API, with current built-in fallbacks
- **Hugging Face**: refreshed from the Hub API for models currently served by the `hf-inference` provider
- **Google**: gemini-3.1-flash-image, gemini-3.1-flash-lite-image, gemini-3-pro-image

### Auto-Detection

`AiProviderSettings.IsImageGenerationModel()` detects image models by name pattern:

- OpenAI: contains "gpt-image"
- OpenRouter: known image-model families plus the capability-filtered API catalog
- Hugging Face: live HF-Inference image catalog plus Stable Diffusion, FLUX, and Qwen-Image naming patterns
- Google: contains "imagen" or "-image"

## OpenAI Image API Parameters

### ImageGenerationRequest Properties

| Property | Type | Description | GPT Image |
|----------|------|-------------|-------------|
| `Prompt` | string | Text description (max 32000 chars) | ✓ |
| `Model` | string | Model ID (e.g., "gpt-image-2") | ✓ |
| `Size` | string | "1024x1024", "1536x1024", "1024x1536", "auto" | ✓ |
| `Quality` | string | "high", "medium", "low", "auto" | ✓ |
| `Count` | int | Number of images (1-10) | ✓ |
| `Background` | string | "transparent", "opaque", "auto" | ✓ |
| `OutputFormat` | string | "png", "jpeg", "webp" | ✓ |
| `OutputCompression` | int? | 0-100% (webp/jpeg only) | ✓ |
| `Moderation` | string | "low", "auto" | ✓ |
| `InputImage` | byte[] | Source image for editing | ✓ |
| `InputImageMimeType` | string | MIME type of input image | ✓ |
| `AspectRatio` | string | "16:9", "4:3", etc. (OpenRouter only) | ✗ |
| `Style` | string | "vivid", "natural" (legacy models) | ✗ |

### ImageGenerationResponse Properties

| Property | Type | Description |
|----------|------|-------------|
| `IsSuccess` | bool | Whether generation succeeded |
| `ErrorMessage` | string | Error details if failed |
| `Images` | List&lt;GeneratedImage&gt; | Generated images |
| `RevisedPrompt` | string | AI-modified prompt (if any) |
| `TotalTokens` | int? | Total tokens used |
| `InputTokens` | int? | Input/prompt tokens |
| `OutputTokens` | int? | Output/image tokens |

### OpenAI Endpoints Used

- **`/images/generations`** - Create new images from prompt only
- **`/images/edits`** - Edit existing images (when `InputImage` is provided, uses multipart/form-data)

## Secure Settings Management

The `AiSettingsManager` class provides centralized management of AI settings with secure API key storage:

```csharp
// Create manager with secure storage implementation
var manager = new AiSettingsManager(mySecretStorage);

// Store/retrieve API keys securely
manager.SetProviderApiKey("OpenAI", "sk-...");
string apiKey = manager.GetProviderApiKey("OpenAI");

// Manage connections
var conn = manager.AddConnection("My GPT", "OpenAI", "gpt-4o", customApiKey);
var settings = manager.CreateSettingsFromConnection(conn.Id);

// Migrate existing plain-text keys to secure storage
if (manager.MigrateToSecureStorage())
    SaveSettings();
```

### ISecretStorage Interface

Implement this interface for platform-specific secure storage:

```csharp
public interface ISecretStorage
{
    void Store(string key, string value);
    string Retrieve(string key);
    void Remove(string key);
    bool Contains(string key);
    void Clear();
}
```

## Integration with OpenSourceToolkit.NET

The Avalonia app provides a Settings UI with:

1. **AI Connections tab**: Named connections with provider/model selection, capability flags
2. **AI Providers tab**: Per-provider API key management, custom endpoints, model list editing

### Secure Storage

API keys are stored securely using platform-specific encryption:

| Platform | Method |
|----------|--------|
| Windows | DPAPI (`ProtectedData`) - user-bound encryption |
| macOS | Keychain + AES encryption |
| Linux | AES encryption with machine-derived key |

Settings structure:

- **Non-sensitive data**: `%LocalAppData%/OpenSourceToolkit/settings.json`
- **API keys**: `%LocalAppData%/OpenSourceToolkit/.secrets` (encrypted)

The JSON file stores `"secure:provider.OpenAI.apikey"` references instead of actual keys.

## Architecture

┌─────────────────────────────────────────────────────────────┐
│                    OpenSourceToolkit.AI                     │
│  ┌─────────────────────────────────────────────────────┐    │
│  │              AiSettingsManager                      │    │
│  │  - Provider/Connection management                   │    │
│  │  - Secure key storage via ISecretStorage            │    │
│  │  - Model list management                            │    │
│  │  - Migration support                                │    │
│  └─────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
                            ▲
                            │ implements ISecretStorage
┌─────────────────────────────────────────────────────────────┐
│                  OpenSourceToolkit.Security                 │
│  ┌─────────────────────────────────────────────────────┐    │
│  │              SecureStorage                          │    │
│  │  - DPAPI (Windows)                                  │    │
│  │  - Keychain (macOS)                                 │    │
│  │  - Encrypted file (Linux)                           │    │
│  └─────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
                            ▲
                            │ wraps
┌─────────────────────────────────────────────────────────────┐
│                  OpenSourceToolkit.NET                      │
│  ┌────────────────┐  ┌────────────────┐                     │
│  │ SecureStorage  │  │  AppSettings   │                     │
│  │ (static wrap)  │  │  + AiManager   │                     │
│  └────────────────┘  └────────────────┘                     │
└─────────────────────────────────────────────────────────────┘
