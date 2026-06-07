using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input.Platform;

namespace OpenSourceToolkit.NET.Controls
{
    public partial class CopyableInput : UserControl
    {
        public static readonly StyledProperty<string> TextProperty =
            AvaloniaProperty.Register<CopyableInput, string>(nameof(Text), defaultBindingMode: BindingMode.TwoWay);

        public string Text
        {
            get => GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static readonly StyledProperty<string> WatermarkProperty =
            AvaloniaProperty.Register<CopyableInput, string>(nameof(Watermark));

        public string Watermark
        {
            get => GetValue(WatermarkProperty);
            set => SetValue(WatermarkProperty, value);
        }

        public static readonly StyledProperty<bool> IsReadOnlyProperty =
            AvaloniaProperty.Register<CopyableInput, bool>(nameof(IsReadOnly), defaultValue: true);

        public bool IsReadOnly
        {
            get => GetValue(IsReadOnlyProperty);
            set => SetValue(IsReadOnlyProperty, value);
        }

        public CopyableInput()
        {
            InitializeComponent();
        }

        private void Clear_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            Text = string.Empty;
        }

        private async void Copy_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.Clipboard != null && !string.IsNullOrEmpty(Text))
            {
                await topLevel.Clipboard.SetTextAsync(Text);
            }
        }
    }
}
