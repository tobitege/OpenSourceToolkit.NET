using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using OpenSourceToolkit.NET.Localization;
using OpenSourceToolkit.NET.Services;
using OpenSourceToolkit.Converters;
using OpenSourceToolkit.NET.ViewModels.Tools.ImageConverter;

namespace OpenSourceToolkit.NET.ViewModels.Tools
{
    /// <summary>
    /// Orchestrator ViewModel for Image Editor tool.
    /// Owns child VMs for workspace editing, thumbnails, batch conversion, AI assistant, and sessions.
    /// XAML binds directly to child VMs (e.g. Workspace.*, Batch.*, Ai.*, Thumbnails.*, Sessions.*).
    /// </summary>
    public partial class ImageConverterToolViewModel : ToolViewModel
    {
        public override int Id => 32;
        public override string Name => ToolkitLocalization.GetString("Tool_ImageEditor_Name");
        public override string Description => ToolkitLocalization.GetString("Tool_ImageEditor_Description");
        public override string IconKey => "ImageConverterIcon";

        // ═══════════════════════════════════════════════════════════════════════════
        // Child ViewModels
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>Single-image workspace editing (zoom, adjustments, crop, etc.)</summary>
        public WorkspaceEditorViewModel Workspace { get; }

        /// <summary>Thumbnail strip with full-resolution images</summary>
        public ThumbnailStripViewModel Thumbnails { get; }

        /// <summary>Batch conversion, GIF/PDF creation</summary>
        public BatchConversionViewModel Batch { get; }

        /// <summary>AI chat/generation assistant</summary>
        public AiAssistantViewModel Ai { get; }

        /// <summary>Session persistence controller</summary>
        public SessionController Sessions { get; }

        private readonly ImageProcessor _imageProcessor;

        // ═══════════════════════════════════════════════════════════════════════════
        // Combined Properties (depend on multiple child VMs)
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>True when any processing is in progress (workspace or batch)</summary>
        public bool IsProcessing => Workspace.IsProcessing || Batch.IsProcessing;

        // ═══════════════════════════════════════════════════════════════════════════
        // External Actions (wired by View code-behind)
        // ═══════════════════════════════════════════════════════════════════════════

        public Action OpenSingleImageAction { get => Workspace.OpenSingleImageAction; set => Workspace.OpenSingleImageAction = value; }
        public Func<Task<string>> SaveWorkspaceImageAction { get => Workspace.SaveWorkspaceImageAction; set => Workspace.SaveWorkspaceImageAction = value; }
        public Action<byte[]> CopyImageToClipboardAction { get => Workspace.CopyImageToClipboardAction; set => Workspace.CopyImageToClipboardAction = value; }
        public Action OpenFullscreenViewerAction { get => Workspace.OpenFullscreenViewerAction; set => Workspace.OpenFullscreenViewerAction = value; }
        public Action LoadWatermarkImageAction { get => Workspace.LoadWatermarkImageAction; set => Workspace.LoadWatermarkImageAction = value; }
        public Action<string> ShowErrorAction { get => Workspace.ShowErrorAction; set { Workspace.ShowErrorAction = value; Batch.ShowErrorAction = value; Ai.ShowErrorAction = value; } }

        public Func<string, Task<string>> SaveFullImageAction { get => Thumbnails.SaveFullImageAction; set => Thumbnails.SaveFullImageAction = value; }
        public Func<string, Task<bool>> ConfirmDeleteThumbnailAction { get => Thumbnails.ConfirmDeleteThumbnailAction; set => Thumbnails.ConfirmDeleteThumbnailAction = value; }

        private Func<List<string>, string, Task<bool>> _confirmDestructiveActionAsync;
        /// <summary>Action to confirm destructive action with unsaved images. Returns true if user confirms.</summary>
        public Func<List<string>, string, Task<bool>> ConfirmDestructiveActionAsync
        {
            get => _confirmDestructiveActionAsync;
            set
            {
                _confirmDestructiveActionAsync = value;
                Sessions.ConfirmDestructiveActionAsync = value;
                Thumbnails.ConfirmDestructiveActionAsync = value;
            }
        }

        public Action SelectFilesAction { get => Batch.SelectFilesAction; set => Batch.SelectFilesAction = value; }
        public Func<Task<string>> SelectOutputFolderAction { get => Batch.SelectOutputFolderAction; set => Batch.SelectOutputFolderAction = value; }
        public Func<Task<string>> SaveGifAction { get => Batch.SaveGifAction; set => Batch.SaveGifAction = value; }
        public Func<Task<string>> SavePdfAction { get => Batch.SavePdfAction; set => Batch.SavePdfAction = value; }
        public Action OpenPdfAction { get => Batch.OpenPdfAction; set => Batch.OpenPdfAction = value; }

        public Func<string, Task> CopyToClipboardAction { get => Ai.CopyToClipboardAction; set => Ai.CopyToClipboardAction = value; }

        public Func<string, Task<string>> ShowRenameSessionDialogAction { get => Sessions.ShowRenameSessionDialogAction; set => Sessions.ShowRenameSessionDialogAction = value; }
        public Func<string, Task<bool?>> PromptSaveChangesAction { get; set; }

        // ═══════════════════════════════════════════════════════════════════════════
        // Constructor
        // ═══════════════════════════════════════════════════════════════════════════

        public ImageConverterToolViewModel()
        {
            _imageProcessor = new ImageProcessor();
            var sessionStorage = SessionStorageService.Default;

            // Create child VMs
            Workspace = new WorkspaceEditorViewModel(_imageProcessor);
            Thumbnails = new ThumbnailStripViewModel(_imageProcessor);
            Batch = new BatchConversionViewModel(_imageProcessor);
            Ai = new AiAssistantViewModel();
            Sessions = new SessionController(sessionStorage);

            // Initialize commands and wire up child VMs
            InitializeSidebarCommands();
            WireChildViewModels();

            // Load persisted settings
            LoadWatermarkSettings();
            LoadThumbnailStripCollapseState();
            LoadSidebarWidthPercent();

            // Subscribe to settings changes
            ToolViewModel.SettingsClosed += RefreshAiConnections;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Public Methods
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Initializes session management on tool load.
        /// </summary>
        public Task InitializeSessionAsync() => Sessions.InitializeAsync();

        /// <summary>
        /// Sets the workspace image from a file path.
        /// </summary>
        public Task SetWorkspaceImageAsync(string filePath) => Workspace.SetWorkspaceImageAsync(filePath);

        /// <summary>
        /// Sets the workspace image (legacy sync wrapper).
        /// </summary>
        public void SetWorkspaceImage(string filePath) => _ = SetWorkspaceImageAsync(filePath);

        /// <summary>
        /// Adds files to batch conversion list.
        /// </summary>
        public void AddFiles(string[] filePaths) => Batch.AddFiles(filePaths);

        /// <summary>
        /// Adds an image to the thumbnail strip.
        /// </summary>
        public void AddToThumbnailStrip(byte[] imageBytes, string label, string mimeType = "image/png", bool selectForAi = false, string filePath = null)
            => Thumbnails.Add(imageBytes, label, mimeType, selectForAi, filePath);

        /// <summary>
        /// Clears all thumbnails from the strip.
        /// </summary>
        public void ClearThumbnailStrip() => Thumbnails.Clear();

        /// <summary>
        /// Extracts pages from a PDF file.
        /// </summary>
        public Task ExtractPdfPages(string pdfPath) => Batch.ExtractPdfPages(pdfPath);

        /// <summary>
        /// Sets the watermark image bytes.
        /// </summary>
        public void SetWatermarkImage(byte[] imageBytes) => Workspace.WatermarkImageBytes = imageBytes;

        /// <summary>
        /// Refreshes AI connections from settings.
        /// </summary>
        public void RefreshAiConnections() => Ai.RefreshAiConnections();

        /// <summary>
        /// Validates a session name for Windows filename compatibility.
        /// </summary>
        public static string ValidateSessionName(string name) => SessionController.ValidateSessionName(name);

        /// <summary>
        /// Maximum session name length.
        /// </summary>
        public const int MaxSessionNameLength = SessionController.MaxSessionNameLength;

        // ═══════════════════════════════════════════════════════════════════════════
        // Private Methods
        // ═══════════════════════════════════════════════════════════════════════════

        private async Task<bool> CheckUnsavedChangesAsync()
        {
            if (!Workspace.HasUnsavedChanges || Workspace.WorkspaceFile == null)
                return true;

            if (PromptSaveChangesAction == null)
                return true;

            var result = await PromptSaveChangesAction(Workspace.WorkspaceFile.FileName ?? "current image");

            if (result == null)
                return false; // Cancel

            if (result == true)
            {
                // Save
                if (Workspace.SaveWorkspaceImageAction != null)
                {
                    var outputPath = await Workspace.SaveWorkspaceImageAction();
                    if (!string.IsNullOrEmpty(outputPath))
                    {
                        await Task.Run(() =>
                        {
                            var options = Workspace.BuildOptions();
                            var resultBytes = _imageProcessor.ProcessImage(Workspace.WorkspaceFile.RawBytes, options);
                            File.WriteAllBytes(outputPath, resultBytes);
                        });
                    }
                }
            }

            Workspace.ClearUndoHistory();
            return true;
        }

        private void LoadWatermarkSettings()
        {
            var settings = AppSettings.Current;
            Workspace.WatermarkText = settings.WatermarkText ?? "";
            Workspace.WatermarkPosition = settings.WatermarkPosition ?? "BottomRight";
            Workspace.WatermarkOpacity = settings.WatermarkOpacity;
            Workspace.WatermarkFontSize = settings.WatermarkFontSize;
            Workspace.WatermarkColor = settings.WatermarkColor ?? "#FFFFFF";
            Workspace.WatermarkPadding = settings.WatermarkPadding;
        }

        private void LoadThumbnailStripCollapseState()
        {
            Thumbnails.IsCollapsed = AppSettings.Current.ImageEditorSessions?.ThumbnailStripCollapsed ?? false;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Cleanup
        // ═══════════════════════════════════════════════════════════════════════════

        public override void Cleanup()
        {
            // Save session before cleanup
            Sessions.SaveOnCleanup();

            // Unsubscribe from settings changes
            ToolViewModel.SettingsClosed -= RefreshAiConnections;
        }
    }
}
