using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Parking.Core.Converters;

public class ConnectionStatusToColorConverter : IValueConverter
{
    private static readonly SolidColorBrush OnlineBrush = new(Color.FromRgb(16, 185, 129));
    private static readonly SolidColorBrush OfflineBrush = new(Color.FromRgb(245, 158, 11));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isOnline && isOnline)
        {
            return OnlineBrush;
        }
        return OfflineBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
