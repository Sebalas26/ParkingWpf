using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZXing;
using ZXing.Common;

namespace Parking.Services.Implementations;

/// <summary>
/// Servicio de generación de códigos de barras Code 128 de alta definición para tiquetes térmicos.
/// </summary>
public static class BarcodeGeneratorService
{
    public static ImageSource? GenerateCode128(string content, int width = 360, int height = 90)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        try
        {
            var writer = new BarcodeWriterPixelData
            {
                Format = BarcodeFormat.CODE_128,
                Options = new EncodingOptions
                {
                    Height = height,
                    Width = width,
                    Margin = 4,
                    PureBarcode = true
                }
            };

            var pixelData = writer.Write(content.Trim().ToUpperInvariant());
            var bitmap = BitmapSource.Create(
                pixelData.Width,
                pixelData.Height,
                96,
                96,
                PixelFormats.Bgra32,
                null,
                pixelData.Pixels,
                pixelData.Width * 4);

            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }
}
