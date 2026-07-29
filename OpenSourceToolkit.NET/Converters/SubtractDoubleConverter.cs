using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace OpenSourceToolkit.NET.Converters
{
    public sealed class SubtractDoubleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not double source ||
                !double.TryParse(parameter?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var amount))
            {
                return AvaloniaProperty.UnsetValue;
            }

            return Math.Max(0, source - amount);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
