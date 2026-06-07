using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input.Platform;
using Avalonia.Media;
using System.Windows.Input;

namespace OpenSourceToolkit.NET.Controls
{
    public partial class CopyableTextBox : UserControl
    {
        public static readonly StyledProperty<string> TextProperty =
            AvaloniaProperty.Register<CopyableTextBox, string>(nameof(Text), defaultBindingMode: BindingMode.TwoWay);

        public string Text
        {
            get => GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static readonly StyledProperty<string> WatermarkProperty =
            AvaloniaProperty.Register<CopyableTextBox, string>(nameof(Watermark));

        public string Watermark
        {
            get => GetValue(WatermarkProperty);
            set => SetValue(WatermarkProperty, value);
        }

        public static readonly StyledProperty<bool> IsReadOnlyProperty =
            AvaloniaProperty.Register<CopyableTextBox, bool>(nameof(IsReadOnly), defaultValue: true);

        public bool IsReadOnly
        {
            get => GetValue(IsReadOnlyProperty);
            set => SetValue(IsReadOnlyProperty, value);
        }

        public static readonly StyledProperty<bool> AcceptsReturnProperty =
            AvaloniaProperty.Register<CopyableTextBox, bool>(nameof(AcceptsReturn), defaultValue: false);

        public bool AcceptsReturn
        {
            get => GetValue(AcceptsReturnProperty);
            set => SetValue(AcceptsReturnProperty, value);
        }

        public static readonly StyledProperty<TextWrapping> TextWrappingProperty =
            AvaloniaProperty.Register<CopyableTextBox, TextWrapping>(nameof(TextWrapping), defaultValue: TextWrapping.NoWrap);

        public TextWrapping TextWrapping
        {
            get => GetValue(TextWrappingProperty);
            set => SetValue(TextWrappingProperty, value);
        }

        public CopyableTextBox()
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
