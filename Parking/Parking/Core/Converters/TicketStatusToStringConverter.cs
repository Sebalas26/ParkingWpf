using System;
using System.Globalization;
using System.Windows.Data;
using Parking.Core.Enums;

namespace Parking.Core.Converters;

public class TicketStatusToStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TicketStatus status)
        {
            return status switch
            {
                TicketStatus.Active => "Activo",
                TicketStatus.Completed => "Completado",
                TicketStatus.Cancelled => "Cancelado",
                _ => value.ToString() ?? string.Empty
            };
        }
        return string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
