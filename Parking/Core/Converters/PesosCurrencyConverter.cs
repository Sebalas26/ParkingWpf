using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace Parking.Core.Converters;

/// <summary>
/// Convertidor de valores numéricos a cadena con formato de pesos colombianos ($ X.XXX)
/// y viceversa para enlace bidireccional (TwoWay) en controles TextBox.
/// </summary>
public class PesosCurrencyConverter : IValueConverter
{
    private static readonly CultureInfo ColombianCulture = new("es-CO")
    {
        NumberFormat =
        {
            NumberGroupSeparator = ".",
            NumberDecimalSeparator = ",",
            CurrencySymbol = "$"
        }
    };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is decimal dec)
        {
            return $"$ {dec.ToString("N0", ColombianCulture)}";
        }
        if (value is double dbl)
        {
            return $"$ {dbl.ToString("N0", ColombianCulture)}";
        }
        if (value is int num)
        {
            return $"$ {num.ToString("N0", ColombianCulture)}";
        }
        if (value is long lng)
        {
            return $"$ {lng.ToString("N0", ColombianCulture)}";
        }
        return "$ 0";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string str)
        {
            var digitsOnly = new string(str.Where(char.IsDigit).ToArray());
            if (decimal.TryParse(digitsOnly, NumberStyles.None, CultureInfo.InvariantCulture, out var result))
            {
                if (targetType == typeof(double)) return (double)result;
                if (targetType == typeof(int)) return (int)result;
                if (targetType == typeof(long)) return (long)result;
                return result;
            }
        }
        return 0m;
    }
}
