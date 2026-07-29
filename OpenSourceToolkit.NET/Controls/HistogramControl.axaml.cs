using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace OpenSourceToolkit.NET.Controls
{
    public partial class HistogramControl : UserControl
    {
        private int[] _redHistogram = new int[256];
        private int[] _greenHistogram = new int[256];
        private int[] _blueHistogram = new int[256];
        private int[] _luminanceHistogram = new int[256];

        private int _minValue;
        private int _maxValue;
        private double _meanValue;

        public static readonly StyledProperty<Bitmap> SourceImageProperty =
            AvaloniaProperty.Register<HistogramControl, Bitmap>(nameof(SourceImage));

        public Bitmap SourceImage
        {
            get => GetValue(SourceImageProperty);
            set => SetValue(SourceImageProperty, value);
        }

        public HistogramControl()
        {
            InitializeComponent();
        }


        protected override void OnLoaded(global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            base.OnLoaded(e);
            SetupEventHandlers();
        }

        private void SetupEventHandlers()
        {
            // Named controls are generated automatically by Avalonia source generators
            ShowRedCheckBox.IsCheckedChanged += (s, ev) => DrawHistogram();
            ShowGreenCheckBox.IsCheckedChanged += (s, ev) => DrawHistogram();
            ShowBlueCheckBox.IsCheckedChanged += (s, ev) => DrawHistogram();
            ShowLuminanceCheckBox.IsCheckedChanged += (s, ev) => DrawHistogram();
            HistogramCanvas.SizeChanged += (s, ev) => DrawHistogram();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == SourceImageProperty)
            {
                CalculateHistogram();
                DrawHistogram();
            }
        }

        private void CalculateHistogram()
        {
            Array.Clear(_redHistogram, 0, 256);
            Array.Clear(_greenHistogram, 0, 256);
            Array.Clear(_blueHistogram, 0, 256);
            Array.Clear(_luminanceHistogram, 0, 256);

            var bitmap = SourceImage;
            if (bitmap == null) return;

            try
            {
                using (var ms = new MemoryStream())
                {
                    bitmap.Save(ms, PngBitmapEncoderOptions.Default);
                    ms.Position = 0;

                    using (var magickImage = new ImageMagick.MagickImage(ms))
                    {
                        var pixels = magickImage.GetPixels();
                        long totalLuminance = 0;
                        int pixelCount = 0;
                        _minValue = 255;
                        _maxValue = 0;

                        foreach (var pixel in pixels)
                        {
                            var color = pixel.ToColor();
                            if (color == null) continue;

                            byte r = color.R;
                            byte g = color.G;
                            byte b = color.B;

                            _redHistogram[r]++;
                            _greenHistogram[g]++;
                            _blueHistogram[b]++;

                            int luminance = (int)(0.299 * r + 0.587 * g + 0.114 * b);
                            luminance = Math.Max(0, Math.Min(255, luminance));
                            _luminanceHistogram[luminance]++;

                            totalLuminance += luminance;
                            pixelCount++;

                            if (luminance < _minValue) _minValue = luminance;
                            if (luminance > _maxValue) _maxValue = luminance;
                        }

                        _meanValue = pixelCount > 0 ? (double)totalLuminance / pixelCount : 0;
                    }
                }

                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    MinLabel.Text = $"Min: {_minValue}";
                    MeanLabel.Text = $"Mean: {_meanValue:F0}";
                    MaxLabel.Text = $"Max: {_maxValue}";
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Histogram calculation error: {ex.Message}");
            }
        }

        private void DrawHistogram()
        {
            HistogramCanvas.Children.Clear();

            // Use actual canvas dimensions (scales to fit container)
            double width = HistogramCanvas.Bounds.Width;
            double height = HistogramCanvas.Bounds.Height;

            if (width <= 0 || height <= 0) return;

            bool showRed = ShowRedCheckBox.IsChecked == true;
            bool showGreen = ShowGreenCheckBox.IsChecked == true;
            bool showBlue = ShowBlueCheckBox.IsChecked == true;
            bool showLuminance = ShowLuminanceCheckBox.IsChecked == true;

            int maxCount = 1;
            for (int i = 0; i < 256; i++)
            {
                if (showRed && _redHistogram[i] > maxCount) maxCount = _redHistogram[i];
                if (showGreen && _greenHistogram[i] > maxCount) maxCount = _greenHistogram[i];
                if (showBlue && _blueHistogram[i] > maxCount) maxCount = _blueHistogram[i];
                if (showLuminance && _luminanceHistogram[i] > maxCount) maxCount = _luminanceHistogram[i];
            }

            // Scale bar width to fit available width
            double barWidth = width / 256.0;

            if (showLuminance)
            {
                DrawChannel(_luminanceHistogram, maxCount, width, height, barWidth, Color.FromArgb(180, 170, 170, 170));
            }
            if (showBlue)
            {
                DrawChannel(_blueHistogram, maxCount, width, height, barWidth, Color.FromArgb(120, 68, 68, 255));
            }
            if (showGreen)
            {
                DrawChannel(_greenHistogram, maxCount, width, height, barWidth, Color.FromArgb(120, 68, 255, 68));
            }
            if (showRed)
            {
                DrawChannel(_redHistogram, maxCount, width, height, barWidth, Color.FromArgb(120, 255, 68, 68));
            }
        }

        private void DrawChannel(int[] histogram, int maxCount, double width, double height, double barWidth, Color color)
        {
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(new Point(0, height), true);

                for (int i = 0; i < 256; i++)
                {
                    double barHeight = (histogram[i] / (double)maxCount) * height;
                    double x = i * barWidth;
                    ctx.LineTo(new Point(x, height - barHeight));
                }

                ctx.LineTo(new Point(width, height));
                ctx.EndFigure(true);
            }

            var pathShape = new global::Avalonia.Controls.Shapes.Path
            {
                Data = geometry,
                Fill = new SolidColorBrush(color),
                Stroke = new SolidColorBrush(Color.FromArgb((byte)Math.Min(255, color.A + 50), color.R, color.G, color.B)),
                StrokeThickness = 0.5
            };

            HistogramCanvas.Children.Add(pathShape);
        }
    }
}
