using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenSourceToolkit.Converters;
using OpenSourceToolkit.NET.ViewModels.Tools.ImageConverter.Models;
using OpenSourceToolkit.NET.ViewModels.Tools; // For UndoHistoryItem

namespace OpenSourceToolkit.NET.ViewModels.Tools.ImageConverter
{
    /// <summary>
    /// ViewModel for single-image workspace editing: load/save, preview, zoom/compare,
    /// adjustments/filters/transform, crop, background removal, watermark, histogram.
    /// Also manages the in-memory undo/redo stack.
    /// </summary>
    public sealed class WorkspaceEditorViewModel : ObservableObject
    {
        private readonly ImageProcessor _imageProcessor;

        // ═══════════════════════════════════════════════════════════════════════════
        // Workspace Image State
        // ═══════════════════════════════════════════════════════════════════════════

        private byte[] _previewSourceBytes;
        private CancellationTokenSource _previewCts;

        private ImageFileModel _workspaceFile;
        public ImageFileModel WorkspaceFile
        {
            get => _workspaceFile;
            set
            {
                if (SetProperty(ref _workspaceFile, value))
                {
                    _workspaceLoadedAt = value != null ? DateTime.Now : default;
                    OnPropertyChanged(nameof(HasWorkspaceImage));
                    OnPropertyChanged(nameof(WorkspaceImage));
                    OnPropertyChanged(nameof(ImageWidth));
                    OnPropertyChanged(nameof(ImageHeight));
                    OnPropertyChanged(nameof(WorkspaceFileName));
                    OnPropertyChanged(nameof(WorkspaceDimensions));
                    OnPropertyChanged(nameof(WorkspaceFileSize));
                    OnPropertyChanged(nameof(WorkspaceFileSizeBytes));
                    OnPropertyChanged(nameof(WorkspaceTimestamp));
                    OnPropertyChanged(nameof(WorkspaceFormat));
                    OnPropertyChanged(nameof(CanEditSingleImage));
                    UpdateResizeDefaultsFromWorkspace();
                    CopyWorkspaceImageCommand?.NotifyCanExecuteChanged();
                    OpenFullscreenViewerCommand?.NotifyCanExecuteChanged();
                }
            }
        }

        public bool HasWorkspaceImage => WorkspaceFile != null;
        public Bitmap WorkspaceImage => WorkspaceFile?.Preview;
        public int ImageWidth => WorkspaceFile?.OriginalWidth ?? 0;
        public int ImageHeight => WorkspaceFile?.OriginalHeight ?? 0;

        public string WorkspaceFileName => WorkspaceFile?.FileName ?? "";
        public string WorkspaceDimensions => WorkspaceFile != null ? $"{WorkspaceFile.OriginalWidth} × {WorkspaceFile.OriginalHeight}" : "";
        public string WorkspaceFileSize => WorkspaceFile?.SizeDisplay ?? "";
        public string WorkspaceFormat => WorkspaceFile?.OriginalFormat ?? "";
        public string WorkspaceFileSizeBytes => WorkspaceFile != null ? string.Format("{0:N0} bytes", WorkspaceFile.OriginalSize) : "";

        private DateTime _workspaceLoadedAt;
        public string WorkspaceTimestamp => _workspaceLoadedAt != default ? _workspaceLoadedAt.ToString("g") : "";

        public bool CanEditSingleImage => HasWorkspaceImage && !IsProcessing;

        private bool _isProcessing;
        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                if (SetProperty(ref _isProcessing, value))
                {
                    OnPropertyChanged(nameof(CanEditSingleImage));
                }
            }
        }

        private bool _isLoadingWorkspaceImage;
        public bool IsLoadingWorkspaceImage
        {
            get => _isLoadingWorkspaceImage;
            set => SetProperty(ref _isLoadingWorkspaceImage, value);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Before/After Comparison
        // ═══════════════════════════════════════════════════════════════════════════

        private Bitmap _originalImage;
        public Bitmap OriginalImage
        {
            get => _originalImage;
            set => SetProperty(ref _originalImage, value);
        }

        public bool IsZoomViewVisible => EnableZoom || ShowComparison;

        private bool _showComparison;
        public bool ShowComparison
        {
            get => _showComparison;
            set
            {
                if (SetProperty(ref _showComparison, value))
                {
                    if (value && _cropEnabled)
                    {
                        _cropEnabled = false;
                        OnPropertyChanged(nameof(CropEnabled));
                    }
                    OnPropertyChanged(nameof(IsZoomViewVisible));
                }
            }
        }

        private double _comparisonSliderPosition = 0.5;
        public double ComparisonSliderPosition
        {
            get => _comparisonSliderPosition;
            set => SetProperty(ref _comparisonSliderPosition, Math.Max(0, Math.Min(1, value)));
        }

        private string _comparisonMode = "Off";
        public string ComparisonMode
        {
            get => _comparisonMode;
            set
            {
                if (SetProperty(ref _comparisonMode, value))
                {
                    ShowComparison = value != "Off";
                    OnPropertyChanged(nameof(ComparisonModeDisplay));
                    OnPropertyChanged(nameof(IsHorizontalComparison));
                    OnPropertyChanged(nameof(IsVerticalComparison));
                }
            }
        }

        public string ComparisonModeDisplay => ComparisonMode == "Off" 
            ? Localization.ToolkitLocalization.GetString("Image_Compare_Button") 
            : $"{Localization.ToolkitLocalization.GetString("Image_Compare_Button")}: {Localization.ToolkitLocalization.GetString($"Image_Compare_{ComparisonMode}_Short")}";

        public bool IsHorizontalComparison => ComparisonMode == "Horizontal";
        public bool IsVerticalComparison => ComparisonMode == "Vertical";
        public bool CanShowComparison => HasWorkspaceImage && OriginalImage != null;

        // ═══════════════════════════════════════════════════════════════════════════
        // Histogram & Zoom
        // ═══════════════════════════════════════════════════════════════════════════

        private bool _showHistogram;
        public bool ShowHistogram
        {
            get => _showHistogram;
            set => SetProperty(ref _showHistogram, value);
        }

        private bool _enableZoom = true;
        public bool EnableZoom
        {
            get => _enableZoom;
            set
            {
                if (SetProperty(ref _enableZoom, value))
                {
                    if (value && _cropEnabled)
                    {
                        _cropEnabled = false;
                        OnPropertyChanged(nameof(CropEnabled));
                    }
                    OnPropertyChanged(nameof(IsZoomViewVisible));
                }
            }
        }

        private double _zoomLevel = 1.0;
        public double ZoomLevel
        {
            get => _zoomLevel;
            set => SetProperty(ref _zoomLevel, Math.Max(0.1, Math.Min(10.0, value)));
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Output Format & Quality
        // ═══════════════════════════════════════════════════════════════════════════

        private string _outputFormat = "png";
        public string OutputFormat
        {
            get => _outputFormat;
            set
            {
                if (SetProperty(ref _outputFormat, value))
                {
                    OnPropertyChanged(nameof(IsQualityVisible));
                    OnPropertyChanged(nameof(IsIcoFormat));
                }
            }
        }

        public bool IsQualityVisible =>
            OutputFormat?.ToLower() == "jpeg" ||
            OutputFormat?.ToLower() == "jpg" ||
            OutputFormat?.ToLower() == "webp";

        public bool IsIcoFormat => OutputFormat?.ToLower() == "ico";

        public ObservableCollection<string> AvailableFormats { get; } = new ObservableCollection<string>(ImageProcessor.SupportedFormats);

        private int _quality = 90;
        public int Quality
        {
            get => _quality;
            set => SetProperty(ref _quality, value);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Adjustments
        // ═══════════════════════════════════════════════════════════════════════════

        private int _brightness;
        public int Brightness
        {
            get => _brightness;
            set { if (SetProperty(ref _brightness, value)) UpdatePreview(); }
        }

        private int _contrast;
        public int Contrast
        {
            get => _contrast;
            set { if (SetProperty(ref _contrast, value)) UpdatePreview(); }
        }

        private int _saturation;
        public int Saturation
        {
            get => _saturation;
            set { if (SetProperty(ref _saturation, value)) UpdatePreview(); }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Filters
        // ═══════════════════════════════════════════════════════════════════════════

        private bool _grayscale;
        public bool Grayscale
        {
            get => _grayscale;
            set { if (SetProperty(ref _grayscale, value)) UpdatePreview(); }
        }

        private bool _sepia;
        public bool Sepia
        {
            get => _sepia;
            set { if (SetProperty(ref _sepia, value)) UpdatePreview(); }
        }

        private bool _invert;
        public bool Invert
        {
            get => _invert;
            set { if (SetProperty(ref _invert, value)) UpdatePreview(); }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Blur / Sharpen
        // ═══════════════════════════════════════════════════════════════════════════

        private int _blurRadius;
        public int BlurRadius
        {
            get => _blurRadius;
            set { if (SetProperty(ref _blurRadius, value)) UpdatePreview(); }
        }

        private int _sharpenAmount;
        public int SharpenAmount
        {
            get => _sharpenAmount;
            set { if (SetProperty(ref _sharpenAmount, value)) UpdatePreview(); }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Transformations
        // ═══════════════════════════════════════════════════════════════════════════

        private int _rotationAngle;
        public int RotationAngle
        {
            get => _rotationAngle;
            set { if (SetProperty(ref _rotationAngle, value)) UpdatePreview(); }
        }

        private bool _flipHorizontal;
        public bool FlipHorizontal
        {
            get => _flipHorizontal;
            set { if (SetProperty(ref _flipHorizontal, value)) UpdatePreview(); }
        }

        private bool _flipVertical;
        public bool FlipVertical
        {
            get => _flipVertical;
            set { if (SetProperty(ref _flipVertical, value)) UpdatePreview(); }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Crop
        // ═══════════════════════════════════════════════════════════════════════════

        private (int X, int Y, int Width, int Height)? _previousCropState;
        public bool CanUndoCrop => _previousCropState.HasValue && CropEnabled;

        private bool _cropEnabled;
        public bool CropEnabled
        {
            get => _cropEnabled;
            set
            {
                if (SetProperty(ref _cropEnabled, value))
                {
                    if (value)
                    {
                        if (_showComparison)
                        {
                            _showComparison = false;
                            _comparisonMode = "Off";
                            OnPropertyChanged(nameof(ShowComparison));
                            OnPropertyChanged(nameof(ComparisonMode));
                            OnPropertyChanged(nameof(ComparisonModeDisplay));
                            OnPropertyChanged(nameof(IsHorizontalComparison));
                            OnPropertyChanged(nameof(IsVerticalComparison));
                        }
                        if (_enableZoom)
                        {
                            _enableZoom = false;
                            OnPropertyChanged(nameof(EnableZoom));
                        }
                        OnPropertyChanged(nameof(IsZoomViewVisible));

                        if (WorkspaceFile != null)
                        {
                            // Only reset if crop doesn't already match image dimensions
                            // (avoids resetting after crop operation when re-enabling)
                            if (_cropWidth != WorkspaceFile.OriginalWidth || _cropHeight != WorkspaceFile.OriginalHeight)
                            {
                                SaveCropState();
                                CropX = 0;
                                CropY = 0;
                                CropWidth = WorkspaceFile.OriginalWidth;
                                CropHeight = WorkspaceFile.OriginalHeight;
                            }
                        }
                    }
                    else if (!_showComparison && !_cropEnabled)
                    {
                        EnableZoom = true;
                    }
                    OnPropertyChanged(nameof(CanUndoCrop));
                    UpdatePreview();
                }
            }
        }

        private int _cropX;
        public int CropX
        {
            get => _cropX;
            set { if (SetProperty(ref _cropX, value)) { OnPropertyChanged(nameof(CanApplyCrop)); UpdatePreview(); } }
        }

        private int _cropY;
        public int CropY
        {
            get => _cropY;
            set { if (SetProperty(ref _cropY, value)) { OnPropertyChanged(nameof(CanApplyCrop)); UpdatePreview(); } }
        }

        private int _cropWidth;
        public int CropWidth
        {
            get => _cropWidth;
            set { if (SetProperty(ref _cropWidth, value)) { OnPropertyChanged(nameof(CanApplyCrop)); UpdatePreview(); } }
        }

        private int _cropHeight;
        public int CropHeight
        {
            get => _cropHeight;
            set { if (SetProperty(ref _cropHeight, value)) { OnPropertyChanged(nameof(CanApplyCrop)); UpdatePreview(); } }
        }

        private string _cropAspectRatio = "Free";
        public string CropAspectRatio
        {
            get => _cropAspectRatio;
            set
            {
                if (SetProperty(ref _cropAspectRatio, value))
                {
                    if (value != "Free") SaveCropState();
                    ApplyCropAspectRatio();
                }
            }
        }

        public ObservableCollection<string> CropAspectRatios { get; } = new ObservableCollection<string>
        {
            "Free", "1:1", "4:3", "3:2", "16:9", "16:10", "21:9", "3:4", "2:3", "9:16"
        };

        public bool CanApplyCrop => CropEnabled && WorkspaceFile != null &&
            (CropX > 0 || CropY > 0 || CropWidth < WorkspaceFile.OriginalWidth || CropHeight < WorkspaceFile.OriginalHeight);

        // ═══════════════════════════════════════════════════════════════════════════
        // Watermark
        // ═══════════════════════════════════════════════════════════════════════════

        private bool _watermarkEnabled;
        public bool WatermarkEnabled
        {
            get => _watermarkEnabled;
            set { if (SetProperty(ref _watermarkEnabled, value)) UpdatePreview(); }
        }

        private string _watermarkText = "";
        public string WatermarkText
        {
            get => _watermarkText;
            set { if (SetProperty(ref _watermarkText, value)) UpdatePreview(); }
        }

        private byte[] _watermarkImageBytes;
        public byte[] WatermarkImageBytes
        {
            get => _watermarkImageBytes;
            set { if (SetProperty(ref _watermarkImageBytes, value)) { OnPropertyChanged(nameof(HasWatermarkImage)); UpdatePreview(); } }
        }

        public bool HasWatermarkImage => WatermarkImageBytes != null && WatermarkImageBytes.Length > 0;

        private string _watermarkPosition = "BottomRight";
        public string WatermarkPosition
        {
            get => _watermarkPosition;
            set { if (SetProperty(ref _watermarkPosition, value)) UpdatePreview(); }
        }

        public ObservableCollection<string> WatermarkPositions { get; } = new ObservableCollection<string>
        {
            "TopLeft", "TopCenter", "TopRight",
            "MiddleLeft", "MiddleCenter", "MiddleRight",
            "BottomLeft", "BottomCenter", "BottomRight",
            "Tile"
        };

        private int _watermarkOpacity = 50;
        public int WatermarkOpacity
        {
            get => _watermarkOpacity;
            set { if (SetProperty(ref _watermarkOpacity, value)) UpdatePreview(); }
        }

        private int _watermarkFontSize = 24;
        public int WatermarkFontSize
        {
            get => _watermarkFontSize;
            set { if (SetProperty(ref _watermarkFontSize, value)) UpdatePreview(); }
        }

        private string _watermarkColor = "#FFFFFF";
        public string WatermarkColor
        {
            get => _watermarkColor;
            set { if (SetProperty(ref _watermarkColor, value)) UpdatePreview(); }
        }

        private int _watermarkPadding = 10;
        public int WatermarkPadding
        {
            get => _watermarkPadding;
            set { if (SetProperty(ref _watermarkPadding, value)) UpdatePreview(); }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Phase 3 Filters / Effects
        // ═══════════════════════════════════════════════════════════════════════════

        private bool _vignette;
        public bool Vignette
        {
            get => _vignette;
            set { if (SetProperty(ref _vignette, value)) UpdatePreview(); }
        }

        private int _vignetteRadius = 50;
        public int VignetteRadius
        {
            get => _vignetteRadius;
            set { if (SetProperty(ref _vignetteRadius, value)) UpdatePreview(); }
        }

        private int _vignetteSoftness = 50;
        public int VignetteSoftness
        {
            get => _vignetteSoftness;
            set { if (SetProperty(ref _vignetteSoftness, value)) UpdatePreview(); }
        }

        private bool _autoEnhance;
        public bool AutoEnhance
        {
            get => _autoEnhance;
            set { if (SetProperty(ref _autoEnhance, value)) UpdatePreview(); }
        }

        private bool _posterize;
        public bool Posterize
        {
            get => _posterize;
            set { if (SetProperty(ref _posterize, value)) UpdatePreview(); }
        }

        private int _posterizeLevels = 4;
        public int PosterizeLevels
        {
            get => _posterizeLevels;
            set { if (SetProperty(ref _posterizeLevels, value)) UpdatePreview(); }
        }

        private bool _edgeDetect;
        public bool EdgeDetect
        {
            get => _edgeDetect;
            set { if (SetProperty(ref _edgeDetect, value)) UpdatePreview(); }
        }

        private int _edgeDetectRadius = 1;
        public int EdgeDetectRadius
        {
            get => _edgeDetectRadius;
            set { if (SetProperty(ref _edgeDetectRadius, value)) UpdatePreview(); }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Background Removal
        // ═══════════════════════════════════════════════════════════════════════════

        private string _backgroundColor = "transparent";
        public string BackgroundColor
        {
            get => _backgroundColor;
            set { if (SetProperty(ref _backgroundColor, value)) UpdatePreview(); }
        }

        public ObservableCollection<string> BackgroundColors { get; } = new ObservableCollection<string>
        {
            "transparent", "#FFFFFF", "#000000", "#FF0000", "#00FF00", "#0000FF"
        };

        private int _backgroundTolerance = 10;
        public int BackgroundTolerance
        {
            get => _backgroundTolerance;
            set { if (SetProperty(ref _backgroundTolerance, value)) UpdatePreview(); }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Metadata & ICO
        // ═══════════════════════════════════════════════════════════════════════════

        private bool _stripMetadata;
        public bool StripMetadata
        {
            get => _stripMetadata;
            set => SetProperty(ref _stripMetadata, value);
        }

        private bool _generateMultiSizeIco;
        public bool GenerateMultiSizeIco
        {
            get => _generateMultiSizeIco;
            set => SetProperty(ref _generateMultiSizeIco, value);
        }

        private IcoSizePreset _selectedIcoPreset;
        public IcoSizePreset SelectedIcoPreset
        {
            get => _selectedIcoPreset;
            set { if (SetProperty(ref _selectedIcoPreset, value) && value != null) OnPropertyChanged(nameof(SelectedIcoSizesDisplay)); }
        }

        public string SelectedIcoSizesDisplay => SelectedIcoPreset != null
            ? string.Join(", ", System.Linq.Enumerable.Select(SelectedIcoPreset.Sizes, s => $"{s}px"))
            : "";

        public ObservableCollection<IcoSizePreset> IcoSizePresets { get; } = new ObservableCollection<IcoSizePreset>
        {
            new IcoSizePreset("Favicon (16×16)", new[] { 16 }),
            new IcoSizePreset("Small Icon (32×32)", new[] { 32 }),
            new IcoSizePreset("Medium Icon (48×48)", new[] { 48 }),
            new IcoSizePreset("Large Icon (64×64)", new[] { 64 }),
            new IcoSizePreset("Extra Large (128×128)", new[] { 128 }),
            new IcoSizePreset("Jumbo (256×256)", new[] { 256 }),
            new IcoSizePreset("Favicon Set (16, 32, 48)", new[] { 16, 32, 48 }),
            new IcoSizePreset("Windows Standard (16, 32, 48, 256)", new[] { 16, 32, 48, 256 }),
            new IcoSizePreset("All Sizes (16-256)", new[] { 16, 32, 48, 64, 128, 256 })
        };

        // ═══════════════════════════════════════════════════════════════════════════
        // Resize
        // ═══════════════════════════════════════════════════════════════════════════

        private bool _resizeEnabled;
        public bool ResizeEnabled
        {
            get => _resizeEnabled;
            set
            {
                if (SetProperty(ref _resizeEnabled, value))
                {
                    if (value) UpdateResizeDefaultsFromWorkspace();
                    else { ResizeWidth = null; ResizeHeight = null; }
                }
            }
        }

        private int? _resizeWidth;
        public int? ResizeWidth
        {
            get => _resizeWidth;
            set
            {
                if (SetProperty(ref _resizeWidth, value))
                {
                    if (MaintainAspectRatio && ResizeEnabled && value.HasValue && WorkspaceFile != null && WorkspaceFile.OriginalWidth > 0)
                    {
                        double ratio = (double)WorkspaceFile.OriginalHeight / WorkspaceFile.OriginalWidth;
                        _resizeHeight = (int)Math.Round(value.Value * ratio);
                        OnPropertyChanged(nameof(ResizeHeight));
                    }
                }
            }
        }

        private int? _resizeHeight;
        public int? ResizeHeight
        {
            get => _resizeHeight;
            set
            {
                if (SetProperty(ref _resizeHeight, value))
                {
                    if (MaintainAspectRatio && ResizeEnabled && value.HasValue && WorkspaceFile != null && WorkspaceFile.OriginalHeight > 0)
                    {
                        double ratio = (double)WorkspaceFile.OriginalWidth / WorkspaceFile.OriginalHeight;
                        _resizeWidth = (int)Math.Round(value.Value * ratio);
                        OnPropertyChanged(nameof(ResizeWidth));
                    }
                }
            }
        }

        private bool _maintainAspectRatio = true;
        public bool MaintainAspectRatio
        {
            get => _maintainAspectRatio;
            set => SetProperty(ref _maintainAspectRatio, value);
        }

        public List<string> ResizePresets { get; } = new List<string>
        {
            "Custom",
            "640×480 (VGA)",
            "800×600 (SVGA)",
            "1024×768 (XGA)",
            "1280×720 (HD)",
            "1920×1080 (Full HD)",
            "2560×1440 (2K)",
            "3840×2160 (4K)",
            "256×256 (Icon)",
            "512×512 (App Icon)",
            "1200×630 (Social)",
            "50%",
            "75%",
            "150%",
            "200%"
        };

        private string _selectedResizePreset = "Custom";
        public string SelectedResizePreset
        {
            get => _selectedResizePreset;
            set
            {
                if (SetProperty(ref _selectedResizePreset, value) && value != "Custom")
                    ApplyResizePreset(value);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Undo/Redo (in-memory only)
        // ═══════════════════════════════════════════════════════════════════════════

        public int MaxUndoHistory { get; set; } = 10;
        private List<UndoHistoryItem> _undoHistory = new List<UndoHistoryItem>();
        private int _undoHistoryIndex = -1;

        public bool CanUndo => _undoHistory.Count > 0 && _undoHistoryIndex < _undoHistory.Count - 1;
        public bool CanRedo => _undoHistoryIndex > 0;
        public int UndoHistoryCount => _undoHistory.Count;
        public string UndoTooltip => CanUndo ? $"Undo image edit ({_undoHistory.Count - _undoHistoryIndex - 1} states)" : "Undo image edit (no history)";
        public string RedoTooltip => CanRedo ? $"Redo image edit ({_undoHistoryIndex} states)" : "Redo image edit (none)";
        public bool HasUnsavedChanges => _undoHistory.Count > 0;

        // Used to prevent marking dirty during load
        internal bool IsLoadingSession { get; set; }

        // ═══════════════════════════════════════════════════════════════════════════
        // Commands
        // ═══════════════════════════════════════════════════════════════════════════

        public RelayCommand OpenSingleImageCommand { get; }
        public RelayCommand ClearWorkspaceCommand { get; }
        public AsyncRelayCommand SaveWorkspaceImageCommand { get; }
        public RelayCommand CopyWorkspaceImageCommand { get; }
        public RelayCommand OpenFullscreenViewerCommand { get; }
        public RelayCommand ToggleComparisonCommand { get; }
        public RelayCommand<string> SetComparisonModeCommand { get; }
        public RelayCommand RotateLeftCommand { get; }
        public RelayCommand RotateRightCommand { get; }
        public RelayCommand ResetAllCommand { get; }
        public RelayCommand ResetAdjustmentsCommand { get; }
        public RelayCommand ResetFiltersCommand { get; }
        public RelayCommand ResetBlurSharpenCommand { get; }
        public RelayCommand ResetEffectsCommand { get; }
        public RelayCommand ResetResizeCommand { get; }
        public RelayCommand ResetCropCommand { get; }
        public RelayCommand UndoCropCommand { get; }
        public AsyncRelayCommand ApplyCropCommand { get; }
        public AsyncRelayCommand ApplyBackgroundRemovalCommand { get; }
        public RelayCommand LoadWatermarkImageCommand { get; }
        public RelayCommand ClearWatermarkImageCommand { get; }
        public RelayCommand UndoCommand { get; }
        public RelayCommand RedoCommand { get; }

        // ═══════════════════════════════════════════════════════════════════════════
        // External Actions (wired by root/view)
        // ═══════════════════════════════════════════════════════════════════════════

        public Action OpenSingleImageAction { get; set; }
        public Func<Task<string>> SaveWorkspaceImageAction { get; set; }
        public Action<byte[]> CopyImageToClipboardAction { get; set; }
        public Action OpenFullscreenViewerAction { get; set; }
        public Action LoadWatermarkImageAction { get; set; }
        public Action<string> ShowErrorAction { get; set; }
        public Action OnWorkspaceChanged { get; set; }
        /// <summary>Action to request immediate session save when workspace is cleared.</summary>
        public Action OnWorkspaceClearRequested { get; set; }
        public Action OnAdjustmentsReset { get; set; }

        /// <summary>Event raised when a thumbnail should be added (bytes, label, mime, selectForAi, filePath)</summary>
        public event Action<byte[], string, string, bool, string> ThumbnailAddRequested;

        /// <summary>Delegate for checking unsaved changes (returns true if should proceed)</summary>
        public Func<Task<bool>> CheckUnsavedChangesAsync { get; set; }

        // ═══════════════════════════════════════════════════════════════════════════
        // Constructor
        // ═══════════════════════════════════════════════════════════════════════════

        public WorkspaceEditorViewModel(ImageProcessor imageProcessor)
        {
            _imageProcessor = imageProcessor ?? throw new ArgumentNullException(nameof(imageProcessor));
            _selectedIcoPreset = IcoSizePresets[IcoSizePresets.Count - 1]; // All Sizes default

            OpenSingleImageCommand = new RelayCommand(() => OpenSingleImageAction?.Invoke());
            ClearWorkspaceCommand = new RelayCommand(ClearWorkspace);
            SaveWorkspaceImageCommand = new AsyncRelayCommand(SaveWorkspaceImageAsync);
            CopyWorkspaceImageCommand = new RelayCommand(CopyWorkspaceImage, () => HasWorkspaceImage);
            OpenFullscreenViewerCommand = new RelayCommand(() => OpenFullscreenViewerAction?.Invoke(), () => HasWorkspaceImage);
            ToggleComparisonCommand = new RelayCommand(() => ShowComparison = !ShowComparison);
            SetComparisonModeCommand = new RelayCommand<string>(mode => ComparisonMode = mode ?? "Off");
            RotateLeftCommand = new RelayCommand(() => RotationAngle = (RotationAngle - 90 + 360) % 360);
            RotateRightCommand = new RelayCommand(() => RotationAngle = (RotationAngle + 90) % 360);
            ResetAllCommand = new RelayCommand(ResetAllOptions);
            ResetAdjustmentsCommand = new RelayCommand(ResetAdjustmentsOnly);
            ResetFiltersCommand = new RelayCommand(ResetFilters);
            ResetBlurSharpenCommand = new RelayCommand(ResetBlurSharpen);
            ResetEffectsCommand = new RelayCommand(ResetEffects);
            ResetResizeCommand = new RelayCommand(ResetResize);
            ResetCropCommand = new RelayCommand(ResetCrop);
            UndoCropCommand = new RelayCommand(UndoCrop);
            ApplyCropCommand = new AsyncRelayCommand(ApplyCropAsync);
            ApplyBackgroundRemovalCommand = new AsyncRelayCommand(ApplyBackgroundRemovalAsync);
            LoadWatermarkImageCommand = new RelayCommand(() => LoadWatermarkImageAction?.Invoke());
            ClearWatermarkImageCommand = new RelayCommand(() => WatermarkImageBytes = null);
            UndoCommand = new RelayCommand(ExecuteUndo, () => CanUndo);
            RedoCommand = new RelayCommand(ExecuteRedo, () => CanRedo);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Public Methods
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Sets the workspace image from a file path. Checks for unsaved changes first.
        /// </summary>
        public async Task SetWorkspaceImageAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

            if (CheckUnsavedChangesAsync != null && !await CheckUnsavedChangesAsync())
                return;

            _previewSourceBytes = null;

            await Task.Run(async () =>
            {
                try
                {
                    var info = new FileInfo(filePath);
                    byte[] fileBytes = File.ReadAllBytes(filePath);

                    int width = 0;
                    int height = 0;
                    byte[] previewBytes = null;

                    try
                    {
                        var imageInfo = new ImageMagick.MagickImageInfo(filePath);
                        width = (int)imageInfo.Width;
                        height = (int)imageInfo.Height;
                        previewBytes = _imageProcessor.ConvertToPreviewPng(fileBytes);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to load workspace image {filePath}: {ex.Message}");
                        return;
                    }

                    if (previewBytes != null)
                    {
                        await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            _previewSourceBytes = previewBytes;
                            using (var stream = new MemoryStream(previewBytes))
                            {
                                var preview = new Bitmap(stream);
                                WorkspaceFile = new ImageFileModel
                                {
                                    Id = Guid.NewGuid().ToString(),
                                    FileName = info.Name,
                                    FilePath = filePath,
                                    OriginalSize = info.Length,
                                    OriginalFormat = info.Extension.TrimStart('.').ToUpper(),
                                    Preview = preview,
                                    OriginalWidth = width > 0 ? width : (int)preview.Size.Width,
                                    OriginalHeight = height > 0 ? height : (int)preview.Size.Height,
                                    RawBytes = fileBytes
                                };
                            }

                            using (var originalStream = new MemoryStream(previewBytes))
                            {
                                OriginalImage = new Bitmap(originalStream);
                            }
                            ShowComparison = false;
                            OnPropertyChanged(nameof(CanShowComparison));

                            string mimeType = GetMimeTypeFromExtension(info.Extension);
                            ThumbnailAddRequested?.Invoke(fileBytes, "Original", mimeType, false, filePath);
                            ClearUndoHistory();
                            OnWorkspaceChanged?.Invoke();
                        });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading workspace image: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Loads a generated image into workspace (e.g., from AI).
        /// </summary>
        public void LoadGeneratedImage(byte[] imageBytes, string label, string mimeType)
        {
            if (imageBytes == null || imageBytes.Length == 0) return;

            PushUndoState("Before AI generation");

            try
            {
                using (var ms = new MemoryStream(imageBytes))
                {
                    var bitmap = new Bitmap(ms);
                    WorkspaceFile = new ImageFileModel
                    {
                        Id = Guid.NewGuid().ToString(),
                        FileName = $"{label}.png",
                        FilePath = "",
                        OriginalFormat = "png",
                        OriginalWidth = bitmap.PixelSize.Width,
                        OriginalHeight = bitmap.PixelSize.Height,
                        OriginalSize = imageBytes.Length,
                        RawBytes = imageBytes,
                        Preview = bitmap
                    };

                    OriginalImage?.Dispose();
                    using (var ms2 = new MemoryStream(imageBytes))
                    {
                        OriginalImage = new Bitmap(ms2);
                    }
                    _previewSourceBytes = imageBytes;
                    ResetAllOptions();
                }
                OnWorkspaceChanged?.Invoke();
            }
            catch (Exception ex)
            {
                ShowErrorAction?.Invoke($"Error loading generated image: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads a thumbnail's raw bytes into the workspace.
        /// </summary>
        public async Task LoadFromThumbnailAsync(byte[] rawBytes, string label, string mimeType, string filePath)
        {
            if (rawBytes == null) return;

            if (CheckUnsavedChangesAsync != null && !await CheckUnsavedChangesAsync())
                return;

            IsLoadingWorkspaceImage = true;
            try
            {
                await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(
                    () => { },
                    global::Avalonia.Threading.DispatcherPriority.Background);

                byte[] previewBytes = _imageProcessor.ConvertToPreviewPng(rawBytes);
                int width = 0, height = 0;
                try
                {
                    using (var ms = new MemoryStream(rawBytes))
                    {
                        var imageInfo = new ImageMagick.MagickImageInfo(ms);
                        width = (int)imageInfo.Width;
                        height = (int)imageInfo.Height;
                    }
                }
                catch { }

                await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _previewSourceBytes = previewBytes;
                    using (var stream = new MemoryStream(previewBytes))
                    {
                        var preview = new Bitmap(stream);
                        WorkspaceFile = new ImageFileModel
                        {
                            Id = Guid.NewGuid().ToString(),
                            FileName = (label ?? "image") + ".png",
                            FilePath = filePath ?? "",
                            OriginalSize = rawBytes.Length,
                            OriginalFormat = mimeType?.Split('/').LastOrDefault()?.ToUpper() ?? "PNG",
                            Preview = preview,
                            OriginalWidth = width > 0 ? width : (int)preview.Size.Width,
                            OriginalHeight = height > 0 ? height : (int)preview.Size.Height,
                            RawBytes = rawBytes
                        };
                    }

                    using (var originalStream = new MemoryStream(previewBytes))
                    {
                        OriginalImage = new Bitmap(originalStream);
                    }
                    ShowComparison = false;
                    OnPropertyChanged(nameof(CanShowComparison));
                    ClearUndoHistory();
                    OnWorkspaceChanged?.Invoke();
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading thumbnail to workspace: {ex.Message}");
            }
            finally
            {
                IsLoadingWorkspaceImage = false;
            }
        }

        /// <summary>
        /// Restores workspace from raw bytes (used during session loading).
        /// </summary>
        public void RestoreFromBytes(byte[] imageBytes, int width, int height, string fileName, string format, string sourcePath)
        {
            if (imageBytes == null) return;
            try
            {
                IsLoadingSession = true;
                byte[] previewBytes = _imageProcessor.ConvertToPreviewPng(imageBytes);
                _previewSourceBytes = previewBytes;

                using (var stream = new MemoryStream(previewBytes))
                {
                    var preview = new Bitmap(stream);
                    WorkspaceFile = new ImageFileModel
                    {
                        Id = Guid.NewGuid().ToString(),
                        FileName = Path.GetFileName(sourcePath ?? fileName),
                        FilePath = sourcePath ?? "",
                        OriginalSize = imageBytes.Length,
                        OriginalFormat = format ?? "PNG",
                        Preview = preview,
                        OriginalWidth = width > 0 ? width : (int)preview.Size.Width,
                        OriginalHeight = height > 0 ? height : (int)preview.Size.Height,
                        RawBytes = imageBytes
                    };
                }

                using (var originalStream = new MemoryStream(previewBytes))
                {
                    OriginalImage = new Bitmap(originalStream);
                }
                OnPropertyChanged(nameof(CanShowComparison));
            }
            finally
            {
                IsLoadingSession = false;
            }
        }

        /// <summary>
        /// Clears the workspace without prompt (internal use after check).
        /// </summary>
        public void ClearWorkspaceInternal(bool clearThumbnails = false)
        {
            WorkspaceFile = null;
            _previewSourceBytes = null;
            OriginalImage = null;
            ShowComparison = false;
            OnPropertyChanged(nameof(CanShowComparison));
            EnableZoom = true;
            ResetAllOptions();
            ClearUndoHistory();
            
            // Request immediate session save (not debounced auto-save) to persist the cleared state
            OnWorkspaceClearRequested?.Invoke();
        }

        /// <summary>
        /// Pushes the current workspace state to undo history before making changes.
        /// </summary>
        public void PushUndoState(string description)
        {
            if (WorkspaceFile?.RawBytes == null || IsLoadingSession) return;

            if (_undoHistoryIndex >= 0)
            {
                for (int i = 0; i < _undoHistoryIndex && _undoHistory.Count > 0; i++)
                    _undoHistory.RemoveAt(0);
                _undoHistoryIndex = -1;
            }

            var historyItem = new UndoHistoryItem
            {
                ImageBytes = (byte[])WorkspaceFile.RawBytes.Clone(),
                Description = description,
                Timestamp = DateTime.Now,
                Width = WorkspaceFile.OriginalWidth,
                Height = WorkspaceFile.OriginalHeight
            };
            _undoHistory.Insert(0, historyItem);

            while (_undoHistory.Count > MaxUndoHistory)
                _undoHistory.RemoveAt(_undoHistory.Count - 1);

            UpdateUndoRedoState();
        }

        /// <summary>
        /// Clears undo history (call when switching images after save/discard).
        /// </summary>
        public void ClearUndoHistory()
        {
            _undoHistory.Clear();
            _undoHistoryIndex = -1;
            UpdateUndoRedoState();
        }

        /// <summary>
        /// Builds ImageProcessingOptions from current settings.
        /// </summary>
        public ImageProcessingOptions BuildOptions(string format = null, bool includeResizeAndOutput = true, bool includeCrop = true)
        {
            return ImageProcessingOptionsBuilder.BuildSingleImageOptions(
                format: format ?? OutputFormat,
                quality: Quality,
                includeResizeAndOutput: includeResizeAndOutput,
                resizeEnabled: ResizeEnabled,
                resizeWidth: ResizeWidth,
                resizeHeight: ResizeHeight,
                maintainAspectRatio: MaintainAspectRatio,
                brightness: Brightness,
                contrast: Contrast,
                saturation: Saturation,
                grayscale: Grayscale,
                sepia: Sepia,
                invert: Invert,
                blurRadius: BlurRadius,
                sharpenAmount: SharpenAmount,
                rotationAngle: RotationAngle,
                flipHorizontal: FlipHorizontal,
                flipVertical: FlipVertical,
                cropEnabled: includeCrop && CropEnabled,
                cropX: CropX,
                cropY: CropY,
                cropWidth: CropWidth,
                cropHeight: CropHeight,
                watermarkEnabled: WatermarkEnabled,
                watermarkText: WatermarkText,
                watermarkImageBytes: WatermarkImageBytes,
                watermarkPosition: ParseWatermarkPosition(WatermarkPosition),
                watermarkOpacity: WatermarkOpacity,
                watermarkFontSize: WatermarkFontSize,
                watermarkColor: WatermarkColor,
                watermarkPadding: WatermarkPadding,
                autoEnhance: AutoEnhance,
                vignette: Vignette,
                vignetteRadius: VignetteRadius,
                vignetteSoftness: VignetteSoftness,
                posterize: Posterize,
                posterizeLevels: PosterizeLevels,
                edgeDetect: EdgeDetect,
                edgeDetectRadius: EdgeDetectRadius,
                removeBackground: false,
                backgroundColor: BackgroundColor,
                backgroundTolerance: BackgroundTolerance,
                stripMetadata: StripMetadata,
                generateMultiSizeIco: GenerateMultiSizeIco,
                icoSizes: SelectedIcoPreset?.Sizes);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Private Methods
        // ═══════════════════════════════════════════════════════════════════════════

        private async void ClearWorkspace()
        {
            if (CheckUnsavedChangesAsync != null && !await CheckUnsavedChangesAsync())
                return;
            ClearWorkspaceInternal(true);
        }

        private void CopyWorkspaceImage()
        {
            if (WorkspaceFile?.RawBytes == null || CopyImageToClipboardAction == null) return;
            CopyImageToClipboardAction(WorkspaceFile.RawBytes);
        }

        private async Task SaveWorkspaceImageAsync()
        {
            if (WorkspaceFile == null || WorkspaceFile.RawBytes == null) return;
            if (SaveWorkspaceImageAction == null) return;

            IsProcessing = true;
            try
            {
                string outputPath = await SaveWorkspaceImageAction();
                if (string.IsNullOrEmpty(outputPath)) { IsProcessing = false; return; }

                await Task.Run(() =>
                {
                    var ext = Path.GetExtension(outputPath)?.TrimStart('.').ToLowerInvariant();
                    var actualFormat = !string.IsNullOrEmpty(ext) ? ext : OutputFormat;
                    if (actualFormat == "jpeg") actualFormat = "jpg";

                    var options = BuildOptions(actualFormat);
                    byte[] resultBytes = _imageProcessor.ProcessImage(WorkspaceFile.RawBytes, options);
                    File.WriteAllBytes(outputPath, resultBytes);
                });
            }
            catch (Exception ex)
            {
                ShowErrorAction?.Invoke($"Save failed: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private void UpdateResizeDefaultsFromWorkspace()
        {
            if (WorkspaceFile != null && ResizeEnabled)
            {
                if (ResizeWidth == null || ResizeWidth == 0) ResizeWidth = WorkspaceFile.OriginalWidth;
                if (ResizeHeight == null || ResizeHeight == 0) ResizeHeight = WorkspaceFile.OriginalHeight;
            }
        }

        private void SaveCropState()
        {
            _previousCropState = (_cropX, _cropY, _cropWidth, _cropHeight);
            OnPropertyChanged(nameof(CanUndoCrop));
        }

        private void UndoCrop()
        {
            if (_previousCropState.HasValue)
            {
                var state = _previousCropState.Value;
                _cropX = state.X;
                _cropY = state.Y;
                _cropWidth = state.Width;
                _cropHeight = state.Height;
                _previousCropState = null;

                OnPropertyChanged(nameof(CropX));
                OnPropertyChanged(nameof(CropY));
                OnPropertyChanged(nameof(CropWidth));
                OnPropertyChanged(nameof(CropHeight));
                OnPropertyChanged(nameof(CanUndoCrop));
                UpdatePreview();
            }
        }

        private void ResetCrop()
        {
            if (WorkspaceFile != null)
            {
                _cropX = 0;
                _cropY = 0;
                _cropWidth = WorkspaceFile.OriginalWidth;
                _cropHeight = WorkspaceFile.OriginalHeight;
                _cropAspectRatio = "Free";
                _previousCropState = null;

                OnPropertyChanged(nameof(CropX));
                OnPropertyChanged(nameof(CropY));
                OnPropertyChanged(nameof(CropWidth));
                OnPropertyChanged(nameof(CropHeight));
                OnPropertyChanged(nameof(CropAspectRatio));
                OnPropertyChanged(nameof(CanUndoCrop));
                UpdatePreview();
            }
        }

        private void ApplyCropAspectRatio()
        {
            if (WorkspaceFile == null || !CropEnabled) return;

            switch (CropAspectRatio)
            {
                case "1:1": ApplyCropRatio(1, 1); break;
                case "4:3": ApplyCropRatio(4, 3); break;
                case "3:2": ApplyCropRatio(3, 2); break;
                case "16:9": ApplyCropRatio(16, 9); break;
                case "16:10": ApplyCropRatio(16, 10); break;
                case "21:9": ApplyCropRatio(21, 9); break;
                case "3:4": ApplyCropRatio(3, 4); break;
                case "2:3": ApplyCropRatio(2, 3); break;
                case "9:16": ApplyCropRatio(9, 16); break;
            }
        }

        private void ApplyCropRatio(int ratioW, int ratioH)
        {
            if (WorkspaceFile == null) return;

            int maxW = WorkspaceFile.OriginalWidth;
            int maxH = WorkspaceFile.OriginalHeight;

            double targetRatio = (double)ratioW / ratioH;
            double imageRatio = (double)maxW / maxH;

            int newW, newH;
            if (targetRatio > imageRatio)
            {
                newW = maxW;
                newH = (int)(maxW / targetRatio);
            }
            else
            {
                newH = maxH;
                newW = (int)(maxH * targetRatio);
            }

            _cropX = (maxW - newW) / 2;
            _cropY = (maxH - newH) / 2;
            _cropWidth = newW;
            _cropHeight = newH;

            OnPropertyChanged(nameof(CropX));
            OnPropertyChanged(nameof(CropY));
            OnPropertyChanged(nameof(CropWidth));
            OnPropertyChanged(nameof(CropHeight));
            UpdatePreview();
        }

        private async Task ApplyCropAsync()
        {
            if (WorkspaceFile?.RawBytes == null || !CropEnabled) return;

            PushUndoState("Before crop");

            IsProcessing = true;
            try
            {
                await Task.Run(() =>
                {
                    var options = new ImageProcessingOptions
                    {
                        CropEnabled = true,
                        CropX = CropX,
                        CropY = CropY,
                        CropWidth = CropWidth,
                        CropHeight = CropHeight
                    };

                    byte[] croppedBytes = _imageProcessor.ProcessImage(WorkspaceFile.RawBytes, options);

                    global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        int newWidth = CropWidth;
                        int newHeight = CropHeight;

                        _previewSourceBytes = _imageProcessor.ConvertToPreviewPng(croppedBytes);
                        Bitmap newPreview;
                        using (var stream = new MemoryStream(_previewSourceBytes))
                        {
                            newPreview = new Bitmap(stream);
                        }

                        using (var originalStream = new MemoryStream(_previewSourceBytes))
                        {
                            OriginalImage = new Bitmap(originalStream);
                        }

                        // Create new WorkspaceFile to trigger binding updates for nested properties
                        WorkspaceFile = new ImageFileModel
                        {
                            Id = WorkspaceFile.Id,
                            FileName = WorkspaceFile.FileName,
                            FilePath = WorkspaceFile.FilePath,
                            OriginalSize = croppedBytes.Length,
                            OriginalFormat = WorkspaceFile.OriginalFormat,
                            OriginalWidth = newWidth,
                            OriginalHeight = newHeight,
                            RawBytes = croppedBytes,
                            Preview = newPreview
                        };

                        // Reset crop state for next use
                        _cropEnabled = false;
                        _cropX = 0;
                        _cropY = 0;
                        _cropWidth = newWidth;
                        _cropHeight = newHeight;
                        _previousCropState = null;

                        OnPropertyChanged(nameof(CropEnabled));
                        OnPropertyChanged(nameof(CropX));
                        OnPropertyChanged(nameof(CropY));
                        OnPropertyChanged(nameof(CropWidth));
                        OnPropertyChanged(nameof(CropHeight));
                        OnPropertyChanged(nameof(CanUndoCrop));
                        OnPropertyChanged(nameof(CanApplyCrop));
                        OnPropertyChanged(nameof(WorkspaceImage));
                        OnPropertyChanged(nameof(WorkspaceDimensions));
                        OnPropertyChanged(nameof(CanShowComparison));
                        // Ensure image dimensions are notified for crop overlay
                        OnPropertyChanged(nameof(ImageWidth));
                        OnPropertyChanged(nameof(ImageHeight));

                        OnWorkspaceChanged?.Invoke();
                        OnAdjustmentsReset?.Invoke();
                    });
                });
            }
            catch (Exception ex)
            {
                ShowErrorAction?.Invoke($"Crop failed: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async Task ApplyBackgroundRemovalAsync()
        {
            if (WorkspaceFile?.RawBytes == null) return;

            PushUndoState("Before background removal");

            IsProcessing = true;
            try
            {
                await Task.Run(() =>
                {
                    var options = new ImageProcessingOptions
                    {
                        RemoveBackground = true,
                        BackgroundColor = BackgroundColor,
                        BackgroundTolerance = BackgroundTolerance
                    };

                    byte[] processedBytes = _imageProcessor.ProcessImage(WorkspaceFile.RawBytes, options);

                    global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        _previewSourceBytes = _imageProcessor.ConvertToPreviewPng(processedBytes);
                        Bitmap newPreview;
                        using (var stream = new MemoryStream(_previewSourceBytes))
                        {
                            newPreview = new Bitmap(stream);
                        }

                        using (var originalStream = new MemoryStream(_previewSourceBytes))
                        {
                            OriginalImage = new Bitmap(originalStream);
                        }

                        WorkspaceFile = new ImageFileModel
                        {
                            Id = WorkspaceFile.Id,
                            FileName = WorkspaceFile.FileName,
                            FilePath = WorkspaceFile.FilePath,
                            OriginalSize = processedBytes.Length,
                            OriginalFormat = WorkspaceFile.OriginalFormat,
                            OriginalWidth = newPreview.PixelSize.Width,
                            OriginalHeight = newPreview.PixelSize.Height,
                            RawBytes = processedBytes,
                            Preview = newPreview
                        };

                        OnPropertyChanged(nameof(WorkspaceImage));
                        OnPropertyChanged(nameof(WorkspaceDimensions));
                        OnPropertyChanged(nameof(ImageWidth));
                        OnPropertyChanged(nameof(ImageHeight));

                        OnWorkspaceChanged?.Invoke();
                    });
                });
            }
            catch (Exception ex)
            {
                ShowErrorAction?.Invoke($"Background removal failed: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private void ResetAdjustmentsOnly()
        {
            _brightness = 0;
            _contrast = 0;
            _saturation = 0;
            OnPropertyChanged(nameof(Brightness));
            OnPropertyChanged(nameof(Contrast));
            OnPropertyChanged(nameof(Saturation));
            UpdatePreview();
        }

        private void ResetFilters()
        {
            _grayscale = false;
            _sepia = false;
            _invert = false;
            OnPropertyChanged(nameof(Grayscale));
            OnPropertyChanged(nameof(Sepia));
            OnPropertyChanged(nameof(Invert));
            UpdatePreview();
        }

        private void ResetBlurSharpen()
        {
            _blurRadius = 0;
            _sharpenAmount = 0;
            OnPropertyChanged(nameof(BlurRadius));
            OnPropertyChanged(nameof(SharpenAmount));
            UpdatePreview();
        }

        private void ResetEffects()
        {
            _autoEnhance = false;
            _vignette = false;
            _vignetteRadius = 50;
            _vignetteSoftness = 50;
            _posterize = false;
            _posterizeLevels = 4;
            _edgeDetect = false;
            _edgeDetectRadius = 1;
            OnPropertyChanged(nameof(AutoEnhance));
            OnPropertyChanged(nameof(Vignette));
            OnPropertyChanged(nameof(VignetteRadius));
            OnPropertyChanged(nameof(VignetteSoftness));
            OnPropertyChanged(nameof(Posterize));
            OnPropertyChanged(nameof(PosterizeLevels));
            OnPropertyChanged(nameof(EdgeDetect));
            OnPropertyChanged(nameof(EdgeDetectRadius));
            UpdatePreview();
        }

        private void ResetResize()
        {
            _selectedResizePreset = "Custom";
            OnPropertyChanged(nameof(SelectedResizePreset));
            UpdateResizeDefaultsFromWorkspace();
        }

        private void ApplyResizePreset(string preset)
        {
            if (WorkspaceFile == null) return;

            int? newWidth = null;
            int? newHeight = null;

            if (preset.EndsWith("%"))
            {
                var percentStr = preset.TrimEnd('%');
                if (int.TryParse(percentStr, out int percent))
                {
                    newWidth = (int)Math.Round(WorkspaceFile.OriginalWidth * percent / 100.0);
                    newHeight = (int)Math.Round(WorkspaceFile.OriginalHeight * percent / 100.0);
                }
            }
            else if (preset.Contains("×"))
            {
                var parts = preset.Split('×');
                if (parts.Length == 2)
                {
                    var widthPart = parts[0].Trim();
                    var heightPart = parts[1].Split(' ')[0].Trim();
                    if (int.TryParse(widthPart, out int w) && int.TryParse(heightPart, out int h))
                    {
                        newWidth = w;
                        newHeight = h;
                    }
                }
            }

            if (newWidth.HasValue && newHeight.HasValue)
            {
                _maintainAspectRatio = false;
                OnPropertyChanged(nameof(MaintainAspectRatio));
                _resizeWidth = newWidth;
                _resizeHeight = newHeight;
                OnPropertyChanged(nameof(ResizeWidth));
                OnPropertyChanged(nameof(ResizeHeight));
                UpdatePreview();
            }
        }

        private void ResetAllOptions()
        {
            _brightness = 0;
            _contrast = 0;
            _saturation = 0;
            _grayscale = false;
            _sepia = false;
            _invert = false;
            _blurRadius = 0;
            _sharpenAmount = 0;
            _rotationAngle = 0;
            _flipHorizontal = false;
            _flipVertical = false;
            _cropEnabled = false;
            _cropX = 0;
            _cropY = 0;
            _cropWidth = WorkspaceFile?.OriginalWidth ?? 0;
            _cropHeight = WorkspaceFile?.OriginalHeight ?? 0;
            _cropAspectRatio = "Free";
            _watermarkEnabled = false;
            _watermarkText = "";
            _watermarkImageBytes = null;
            _watermarkPosition = "BottomRight";
            _watermarkOpacity = 50;
            _watermarkFontSize = 24;
            _watermarkColor = "#FFFFFF";
            _watermarkPadding = 10;
            _vignette = false;
            _vignetteRadius = 50;
            _vignetteSoftness = 50;
            _autoEnhance = false;
            _posterize = false;
            _posterizeLevels = 4;
            _edgeDetect = false;
            _edgeDetectRadius = 1;
            _backgroundColor = "transparent";
            _backgroundTolerance = 10;

            // Notify all properties
            OnPropertyChanged(nameof(Brightness));
            OnPropertyChanged(nameof(Contrast));
            OnPropertyChanged(nameof(Saturation));
            OnPropertyChanged(nameof(Grayscale));
            OnPropertyChanged(nameof(Sepia));
            OnPropertyChanged(nameof(Invert));
            OnPropertyChanged(nameof(BlurRadius));
            OnPropertyChanged(nameof(SharpenAmount));
            OnPropertyChanged(nameof(RotationAngle));
            OnPropertyChanged(nameof(FlipHorizontal));
            OnPropertyChanged(nameof(FlipVertical));
            OnPropertyChanged(nameof(CropEnabled));
            OnPropertyChanged(nameof(CropX));
            OnPropertyChanged(nameof(CropY));
            OnPropertyChanged(nameof(CropWidth));
            OnPropertyChanged(nameof(CropHeight));
            OnPropertyChanged(nameof(CropAspectRatio));
            OnPropertyChanged(nameof(WatermarkEnabled));
            OnPropertyChanged(nameof(WatermarkText));
            OnPropertyChanged(nameof(WatermarkImageBytes));
            OnPropertyChanged(nameof(HasWatermarkImage));
            OnPropertyChanged(nameof(WatermarkPosition));
            OnPropertyChanged(nameof(WatermarkOpacity));
            OnPropertyChanged(nameof(WatermarkFontSize));
            OnPropertyChanged(nameof(WatermarkColor));
            OnPropertyChanged(nameof(WatermarkPadding));
            OnPropertyChanged(nameof(Vignette));
            OnPropertyChanged(nameof(VignetteRadius));
            OnPropertyChanged(nameof(VignetteSoftness));
            OnPropertyChanged(nameof(AutoEnhance));
            OnPropertyChanged(nameof(Posterize));
            OnPropertyChanged(nameof(PosterizeLevels));
            OnPropertyChanged(nameof(EdgeDetect));
            OnPropertyChanged(nameof(EdgeDetectRadius));
            OnPropertyChanged(nameof(BackgroundColor));
            OnPropertyChanged(nameof(BackgroundTolerance));

            UpdatePreview();
            OnAdjustmentsReset?.Invoke();
        }

        private async void UpdatePreview()
        {
            if (WorkspaceFile?.RawBytes == null) return;

            _previewCts?.Cancel();
            _previewCts = new CancellationTokenSource();
            var token = _previewCts.Token;

            try { await Task.Delay(100, token); }
            catch (TaskCanceledException) { return; }

            if (token.IsCancellationRequested) return;

            var options = BuildOptions(format: null, includeResizeAndOutput: false, includeCrop: false);
            var inputBytes = _previewSourceBytes ?? WorkspaceFile.RawBytes;

            try
            {
                await Task.Run(() =>
                {
                    if (token.IsCancellationRequested) return;
                    try
                    {
                        byte[] resultBytes = _imageProcessor.ProcessImage(inputBytes, options);
                        if (token.IsCancellationRequested) return;

                        global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            if (token.IsCancellationRequested) return;
                            using (var stream = new MemoryStream(resultBytes))
                            {
                                WorkspaceFile.Preview = new Bitmap(stream);
                                OnPropertyChanged(nameof(WorkspaceImage));
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Preview update failed: {ex.Message}");
                    }
                }, token);
            }
            catch (TaskCanceledException) { }
        }

        private void ExecuteUndo()
        {
            if (!CanUndo) return;

            if (_undoHistoryIndex == -1)
            {
                _undoHistory.Insert(0, new UndoHistoryItem
                {
                    ImageBytes = (byte[])WorkspaceFile.RawBytes.Clone(),
                    Description = "Current state",
                    Timestamp = DateTime.Now,
                    Width = WorkspaceFile.OriginalWidth,
                    Height = WorkspaceFile.OriginalHeight
                });
                _undoHistoryIndex = 0;
            }

            _undoHistoryIndex++;
            RestoreFromHistory(_undoHistory[_undoHistoryIndex]);
        }

        private void ExecuteRedo()
        {
            if (!CanRedo) return;
            _undoHistoryIndex--;
            RestoreFromHistory(_undoHistory[_undoHistoryIndex]);
        }

        private void RestoreFromHistory(UndoHistoryItem item)
        {
            if (item?.ImageBytes == null) return;

            try
            {
                IsLoadingSession = true;
                byte[] previewBytes = _imageProcessor.ConvertToPreviewPng(item.ImageBytes);
                _previewSourceBytes = previewBytes;

                using (var stream = new MemoryStream(previewBytes))
                {
                    var preview = new Bitmap(stream);
                    if (WorkspaceFile != null)
                    {
                        WorkspaceFile.RawBytes = item.ImageBytes;
                        WorkspaceFile.Preview = preview;
                        WorkspaceFile.OriginalWidth = item.Width;
                        WorkspaceFile.OriginalHeight = item.Height;
                        OnPropertyChanged(nameof(WorkspaceImage));
                        OnPropertyChanged(nameof(WorkspaceDimensions));
                        OnPropertyChanged(nameof(WorkspaceFile));
                    }
                }
                ResetAllOptions();
            }
            finally
            {
                IsLoadingSession = false;
            }
            UpdateUndoRedoState();
        }

        private void UpdateUndoRedoState()
        {
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
            OnPropertyChanged(nameof(UndoHistoryCount));
            OnPropertyChanged(nameof(UndoTooltip));
            OnPropertyChanged(nameof(RedoTooltip));
            OnPropertyChanged(nameof(HasUnsavedChanges));
            UndoCommand?.NotifyCanExecuteChanged();
            RedoCommand?.NotifyCanExecuteChanged();
        }

        private static OpenSourceToolkit.Converters.WatermarkPosition ParseWatermarkPosition(string position)
        {
            switch (position)
            {
                case "TopLeft": return OpenSourceToolkit.Converters.WatermarkPosition.TopLeft;
                case "TopCenter": return OpenSourceToolkit.Converters.WatermarkPosition.TopCenter;
                case "TopRight": return OpenSourceToolkit.Converters.WatermarkPosition.TopRight;
                case "MiddleLeft": return OpenSourceToolkit.Converters.WatermarkPosition.MiddleLeft;
                case "MiddleCenter": return OpenSourceToolkit.Converters.WatermarkPosition.MiddleCenter;
                case "MiddleRight": return OpenSourceToolkit.Converters.WatermarkPosition.MiddleRight;
                case "BottomLeft": return OpenSourceToolkit.Converters.WatermarkPosition.BottomLeft;
                case "BottomCenter": return OpenSourceToolkit.Converters.WatermarkPosition.BottomCenter;
                case "BottomRight": return OpenSourceToolkit.Converters.WatermarkPosition.BottomRight;
                case "Tile": return OpenSourceToolkit.Converters.WatermarkPosition.Tile;
                default: return OpenSourceToolkit.Converters.WatermarkPosition.BottomRight;
            }
        }

        private static string GetMimeTypeFromExtension(string extension)
        {
            var ext = extension?.TrimStart('.').ToLowerInvariant();
            switch (ext)
            {
                case "jpg":
                case "jpeg": return "image/jpeg";
                case "gif": return "image/gif";
                case "webp": return "image/webp";
                default: return "image/png";
            }
        }
    }

    // UndoHistoryItem class is defined in ImageEditorSession.cs in the parent namespace
}
