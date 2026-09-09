using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace Parking.Core.Converters;

/// <summary>
/// Convierte una cadena Base64 (con o sin encabezado data URI) a un objeto BitmapImage utilizable en controles Image de XAML.
/// </summary>
public class Base64ToImageConverter : IValueConverter
{
    private static BitmapImage? _fallbackImage;

    private static BitmapImage? GetFallbackImage()
    {
        if (_fallbackImage == null)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri("pack://application:,,,/Parking;component/Resources/logo.jpeg", UriKind.Absolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                _fallbackImage = bmp;
            }
            catch
            {
                // Fallback silencioso si no se encuentra el recurso
            }
        }
        return _fallbackImage;
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string base64Str || string.IsNullOrWhiteSpace(base64Str))
            return GetFallbackImage();

        try
        {
            var trimmed = base64Str.Trim();
            if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("pack://", StringComparison.OrdinalIgnoreCase))
            {
                var uriSource = new Uri(trimmed, UriKind.Absolute);
                var uriBitmap = new BitmapImage();
                uriBitmap.BeginInit();
                uriBitmap.UriSource = uriSource;
                uriBitmap.CacheOption = BitmapCacheOption.OnLoad;
                uriBitmap.EndInit();
                uriBitmap.Freeze();
                return uriBitmap;
            }

            if (trimmed.StartsWith("/", StringComparison.OrdinalIgnoreCase))
            {
                return GetFallbackImage();
            }

            // Remover prefijo data:image/...;base64, si viene incluido
            var base64Clean = trimmed;
            var commaIndex = base64Clean.IndexOf(',');
            if (commaIndex >= 0 && base64Clean.Substring(0, commaIndex).Contains("base64", StringComparison.OrdinalIgnoreCase))
            {
                base64Clean = base64Clean.Substring(commaIndex + 1);
            }

            var imageBytes = System.Convert.FromBase64String(base64Clean.Trim());
            using var ms = new MemoryStream(imageBytes);

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = ms;
            bitmap.EndInit();
            bitmap.Freeze();

            return bitmap;
        }
        catch
        {
            return GetFallbackImage();
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw creativeNotSupported();
    }

    private static NotSupportedException creativeNotSupported() =>
        new("Base64ToImageConverter solo admite conversión unidireccional (Convert).");
}
