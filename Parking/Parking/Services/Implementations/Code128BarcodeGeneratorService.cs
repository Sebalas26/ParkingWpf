using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Parking.Services.Contracts;
using ZXing;
using ZXing.Common;

namespace Parking.Services.Implementations;

public class Code128BarcodeGeneratorService : IBarcodeGeneratorService
{
    public BitmapSource GenerateCode128Barcode(string content, int height = 60, int moduleWidth = 2)
    {
        var text = string.IsNullOrWhiteSpace(content) ? "000000" : content.Trim();

        try
        {
            var writer = new BarcodeWriterPixelData
            {
                Format = BarcodeFormat.CODE_128,
                Options = new EncodingOptions
                {
                    Height = Math.Max(50, height),
                    Width = Math.Max(240, text.Length * 18),
                    Margin = 6,
                    PureBarcode = true
                }
            };

            var pixelData = writer.Write(text);
            var bitmap = BitmapSource.Create(
                pixelData.Width,
                pixelData.Height,
                96.0,
                96.0,
                PixelFormats.Bgra32,
                null,
                pixelData.Pixels,
                pixelData.Width * 4);

            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return CreateEmptyFallback();
        }
    }

    private static BitmapSource CreateEmptyFallback()
    {
        int width = 200;
        int height = 50;
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];
        for (int i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = 255;
            pixels[i + 1] = 255;
            pixels[i + 2] = 255;
            pixels[i + 3] = 255;
        }

        var bmp = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        bmp.Freeze();
        return bmp;
    }
}
