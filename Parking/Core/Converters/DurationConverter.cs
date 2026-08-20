using System;
using System.Globalization;
using System.Windows.Data;

namespace Parking.Core.Converters;

public class DurationConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TimeSpan span)
        {
            if (span.TotalDays >= 1)
            {
                return $"{(int)span.TotalDays}d {span.Hours}h {span.Minutes}min";
            }
            if (span.TotalHours >= 1)
            {
                return $"{(int)span.TotalHours}h {span.Minutes}min {span.Seconds}seg";
            }
            return $"{span.Minutes}min {span.Seconds}seg";
        }
        if (value is double minutes)
        {
            var ts = TimeSpan.FromMinutes(minutes);
            if (ts.TotalHours >= 1)
            {
                return $"{(int)ts.TotalHours}h {ts.Minutes}min";
            }
            return $"{ts.Minutes}min";
        }
        return "0min";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
