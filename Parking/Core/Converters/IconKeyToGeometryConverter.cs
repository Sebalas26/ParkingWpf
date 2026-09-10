using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Parking.Core.Converters;

public class IconKeyToGeometryConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var app = Application.Current;
        if (app == null) return Geometry.Empty;

        var key = value?.ToString();
        if (!string.IsNullOrWhiteSpace(key))
        {
            var res = app.TryFindResource(key) as Geometry;
            if (res != null) return res;
        }

        return app.TryFindResource("IconCash") as Geometry ?? Geometry.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
