using System;
using System.IO;
using System.Windows.Media.Imaging;
using QRCoder;

namespace Parking.Services.Implementations;

/// <summary>
/// Servicio de generación de códigos QR de alta definición usando QRCoder (Licencia MIT Open Source).
/// </summary>
public static class QrCodeGeneratorService
{
    public static BitmapImage? GenerateQrCode(string content, int pixelsPerModule = 15)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        try
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
            using var qrCode = new PngByteQRCode(data);
            byte[] bytes = qrCode.GetGraphic(pixelsPerModule);

            var bitmap = new BitmapImage();
            using var stream = new MemoryStream(bytes);
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();

            return bitmap;
        }
        catch
        {
            return null;
        }
    }
}
