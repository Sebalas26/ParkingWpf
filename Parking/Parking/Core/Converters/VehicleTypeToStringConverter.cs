using System;
using System.Globalization;
using System.Windows.Data;
using Parking.Core.Enums;

namespace Parking.Core.Converters;

public class VehicleTypeToStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is VehicleType type)
        {
            return type switch
            {
                VehicleType.Motorcycle => "Motocicleta",
                VehicleType.Car => "Automóvil / Sedán",
                VehicleType.Suv => "Camioneta / SUV",
                VehicleType.Van => "Furgón / Minibús",
                VehicleType.HeavyTruck => "Vehículo Pesado / Camión",
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
