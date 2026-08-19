using System;
using System.Globalization;
using System.Windows.Data;

namespace Parking.Core.Converters;

public class CurrencyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is decimal dec)
        {
            return dec.ToString("C2", CultureInfo.GetCultureInfo("en-US"));
        }
        if (value is double dbl)
        {
            return dbl.ToString("C2", CultureInfo.GetCultureInfo("en-US"));
        }
        if (value is int num)
        {
            return num.ToString("C2", CultureInfo.GetCultureInfo("en-US"));
        }
        return "$0.00";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string str)
        {
            var cleaned = str.Replace("$", "").Trim();
            if (decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.GetCultureInfo("en-US"), out var result))
            {
                return result;
            }
        }
        return 0m;
    }
}
