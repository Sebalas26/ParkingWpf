using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Parking.Core.Converters;

public class NullToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isNullOrEmpty = value == null || (value is string str && string.IsNullOrWhiteSpace(str));
        var isVisible = Invert ? isNullOrEmpty : !isNullOrEmpty;
        return isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
