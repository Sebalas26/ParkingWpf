using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Parking.Core.Converters;

public class OccupancyToColorConverter : IValueConverter
{
    private static readonly SolidColorBrush LowBrush = new(Color.FromRgb(16, 185, 129));
    private static readonly SolidColorBrush MediumBrush = new(Color.FromRgb(245, 158, 11));
    private static readonly SolidColorBrush HighBrush = new(Color.FromRgb(239, 68, 68));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double percentage)
        {
            if (percentage < 70)
            {
                return LowBrush;
            }
            if (percentage < 90)
            {
                return MediumBrush;
            }
            return HighBrush;
        }
        return LowBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
