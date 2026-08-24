using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Parking.Core.Converters;

public class BooleanToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var boolValue = value is bool b && b;
        if (Invert || (parameter is string paramStr && paramStr.Equals("Invert", StringComparison.OrdinalIgnoreCase)))
        {
            boolValue = !boolValue;
        }
        return boolValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Visibility vis)
        {
            var isVisible = vis == Visibility.Visible;
            return Invert ? !isVisible : isVisible;
        }
        return false;
    }
}
