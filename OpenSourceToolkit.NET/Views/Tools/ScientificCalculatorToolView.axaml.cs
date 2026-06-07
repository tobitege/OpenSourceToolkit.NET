#nullable enable
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Markup.Xaml;
using System.Collections.Specialized;
using OpenSourceToolkit.NET.ViewModels.Tools;

namespace OpenSourceToolkit.NET.Views.Tools
{
    public partial class ScientificCalculatorToolView : UserControl
    {
        private ScientificCalculatorToolViewModel? _vm;

        public ScientificCalculatorToolView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            Focusable = true;
            // Use tunneling to capture Enter before any focused button can handle it
            AddHandler(KeyDownEvent, OnKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            Focus();
        }

        private void OnDataContextChanged(object? sender, System.EventArgs e)
        {
            if (DataContext is ScientificCalculatorToolViewModel vm)
            {
                _vm = vm;
                vm.CopyToClipboardAction = async text =>
                {
                    var topLevel = TopLevel.GetTopLevel(this);
                    if (topLevel?.Clipboard != null)
                    {
                        await topLevel.Clipboard.SetTextAsync(text);
                    }
                };
                vm.History.CollectionChanged += OnHistoryChanged;
            }
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (_vm == null) return;

            bool handled = true;
            switch (e.Key)
            {
                // Digits
                case Key.D0 or Key.NumPad0:
                    _vm.DigitCommand.Execute("0");
                    break;
                case Key.D1 or Key.NumPad1:
                    _vm.DigitCommand.Execute("1");
                    break;
                case Key.D2 or Key.NumPad2:
                    _vm.DigitCommand.Execute("2");
                    break;
                case Key.D3 or Key.NumPad3:
                    _vm.DigitCommand.Execute("3");
                    break;
                case Key.D4 or Key.NumPad4:
                    _vm.DigitCommand.Execute("4");
                    break;
                case Key.D5 or Key.NumPad5:
                    _vm.DigitCommand.Execute("5");
                    break;
                case Key.D6 or Key.NumPad6:
                    _vm.DigitCommand.Execute("6");
                    break;
                case Key.D7 or Key.NumPad7:
                    _vm.DigitCommand.Execute("7");
                    break;
                case Key.D8 or Key.NumPad8:
                    if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                        _vm.OperatorCommand.Execute("*"); // Shift+8 = *
                    else
                        _vm.DigitCommand.Execute("8");
                    break;
                case Key.D9 or Key.NumPad9:
                    _vm.DigitCommand.Execute("9");
                    break;

                // Operators
                case Key.Add:
                    _vm.OperatorCommand.Execute("+");
                    break;
                case Key.Subtract or Key.OemMinus:
                    _vm.OperatorCommand.Execute("-");
                    break;
                case Key.Multiply:
                    _vm.OperatorCommand.Execute("*");
                    break;
                case Key.Divide or Key.OemQuestion:
                    _vm.OperatorCommand.Execute("/");
                    break;

                // Decimal point
                case Key.Decimal or Key.OemPeriod:
                    _vm.DigitCommand.Execute(".");
                    break;

                // Equals / Enter
                case Key.Enter or Key.Return:
                    _vm.EqualsCommand.Execute(null);
                    break;

                // Backspace
                case Key.Back:
                    _vm.BackspaceCommand.Execute(null);
                    break;

                // Delete / Clear
                case Key.Delete:
                    _vm.ClearCommand.Execute(null);
                    break;

                // Escape = Clear
                case Key.Escape:
                    _vm.ClearCommand.Execute(null);
                    break;

                default:
                    handled = false;
                    break;
            }

            if (handled)
            {
                e.Handled = true;
                Focus(); // Refocus the UserControl to prevent buttons from keeping focus
            }
        }

        private void OnHistoryChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                // Use FindControl since source generation doesn't work with manual InitializeComponent
                var scrollViewer = this.FindControl<ScrollViewer>("RollScrollViewer");
                scrollViewer?.ScrollToEnd();
            }
        }
    }
}
