using System;
using System.Globalization;
using System.Windows.Data;
using Parking.Core.Enums;

namespace Parking.Core.Converters;

public class VehicleTypeToStringConverter : IValueConverter
{
    public static Func<VehicleType, string?>? CustomNameResolver { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null) return string.Empty;

        var type = value is VehicleType vt ? vt : Parking.Core.Helpers.VehicleTypeHelper.Parse(value);

        var customName = CustomNameResolver?.Invoke(type);
        if (!string.IsNullOrWhiteSpace(customName))
        {
            return customName;
        }

        return type switch
        {
            VehicleType.Motorcycle => "Motocicleta",
            VehicleType.Car => "Automóvil",
            VehicleType.Suv => "Camioneta",
            VehicleType.Van => "Furgón",
            VehicleType.Bicycle => "Bicicleta",
            VehicleType.HeavyTruck => "Vehículo Pesado",
            _ => value.ToString() ?? string.Empty
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
