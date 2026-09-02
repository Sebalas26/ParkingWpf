using System;
using System.Globalization;
using System.Windows.Data;
using Parking.Core.Enums;

namespace Parking.Core.Converters;

public class VehicleTypeToStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null) return string.Empty;

        var type = value is VehicleType vt ? vt : Parking.Core.Helpers.VehicleTypeHelper.Parse(value);
        return type switch
        {
            VehicleType.Motorcycle => "Motocicleta",
            VehicleType.Car => "Automóvil / Sedán",
            VehicleType.Suv => "Camioneta / SUV",
            VehicleType.Van => "Furgón / Minibús",
            VehicleType.Bicycle => "Bicicleta",
            VehicleType.HeavyTruck => "Vehículo Pesado / Camión",
            _ => value.ToString() ?? string.Empty
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
