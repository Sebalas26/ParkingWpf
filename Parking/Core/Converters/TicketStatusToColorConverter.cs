using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Parking.Core.Enums;

namespace Parking.Core.Converters;

public class TicketStatusToColorConverter : IValueConverter
{
    private static readonly SolidColorBrush ActiveBrush = new(Color.FromRgb(16, 185, 129));
    private static readonly SolidColorBrush CompletedBrush = new(Color.FromRgb(148, 163, 184));
    private static readonly SolidColorBrush CancelledBrush = new(Color.FromRgb(239, 68, 68));
    private static readonly SolidColorBrush DefaultBrush = new(Color.FromRgb(100, 116, 139));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TicketStatus status)
        {
            return status switch
            {
                TicketStatus.Active => ActiveBrush,
                TicketStatus.Completed => CompletedBrush,
                TicketStatus.Cancelled => CancelledBrush,
                _ => DefaultBrush
            };
        }
        return DefaultBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
