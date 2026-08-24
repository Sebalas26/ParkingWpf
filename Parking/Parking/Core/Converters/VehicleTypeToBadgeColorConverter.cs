using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Parking.Core.Enums;

namespace Parking.Core.Converters;

public class VehicleTypeToBadgeColorConverter : IValueConverter
{
    private static readonly SolidColorBrush MotorcycleBrush = new(Color.FromRgb(6, 182, 212));
    private static readonly SolidColorBrush CarBrush = new(Color.FromRgb(99, 102, 241));
    private static readonly SolidColorBrush SuvBrush = new(Color.FromRgb(16, 185, 129));
    private static readonly SolidColorBrush VanBrush = new(Color.FromRgb(245, 158, 11));
    private static readonly SolidColorBrush HeavyTruckBrush = new(Color.FromRgb(239, 68, 68));
    private static readonly SolidColorBrush DefaultBrush = new(Color.FromRgb(148, 163, 184));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is VehicleType type)
        {
            return type switch
            {
                VehicleType.Motorcycle => MotorcycleBrush,
                VehicleType.Car => CarBrush,
                VehicleType.Suv => SuvBrush,
                VehicleType.Van => VanBrush,
                VehicleType.HeavyTruck => HeavyTruckBrush,
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
