using System.Windows.Media.Imaging;

namespace Parking.Services.Contracts;

public interface IBarcodeGeneratorService
{
    BitmapSource GenerateCode128Barcode(string content, int height = 60, int moduleWidth = 2);
}
