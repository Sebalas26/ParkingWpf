using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Parking.Core.Enums;

namespace Parking.Core.Converters;

public class VehicleTypeToIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var app = Application.Current;
        if (app == null) return Geometry.Empty;

        if (value is VehicleType vType)
        {
            return vType switch
            {
                VehicleType.Motorcycle => app.TryFindResource("IconMotorcycle") as Geometry ?? (Geometry)app.Resources["IconCar"],
                VehicleType.Suv => app.TryFindResource("IconSuv") as Geometry ?? (Geometry)app.Resources["IconCar"],
                VehicleType.Van => app.TryFindResource("IconVan") as Geometry ?? (Geometry)app.Resources["IconCar"],
                VehicleType.HeavyTruck => app.TryFindResource("IconTruck") as Geometry ?? (Geometry)app.Resources["IconCar"],
                _ => app.TryFindResource("IconCar") as Geometry ?? Geometry.Empty
            };
        }

        return app.TryFindResource("IconCar") as Geometry ?? Geometry.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
