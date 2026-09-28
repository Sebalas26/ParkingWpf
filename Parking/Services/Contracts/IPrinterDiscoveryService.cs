using System.Collections.Generic;
using System.Printing;
using System.Windows.Media;

namespace Parking.Services.Contracts;

public class PrinterQueueInfo
{
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsThermal { get; set; }
    public bool IsOnline { get; set; }
    public string StatusDescription { get; set; } = string.Empty;
}

public interface IPrinterDiscoveryService
{
    /// <summary>
    /// Obtiene la lista de impresoras instaladas en el sistema operativo y su estado.
    /// </summary>
    IReadOnlyList<PrinterQueueInfo> GetInstalledPrinters();

    /// <summary>
    /// Resuelve la cola de impresión óptima disponible (prioriza térmica, luego predeterminada, luego primera online).
    /// </summary>
    /// <param name="preferredPrinterName">Nombre de impresora preferida si existe en la configuración.</param>
    PrintQueue? ResolveConnectedPrinter(string? preferredPrinterName = null);

    /// <summary>
    /// Envía a imprimir directamente un elemento visual a la impresora conectada sin abrir el cuadro de diálogo de Windows.
    /// </summary>
    /// <param name="visual">Elemento visual a imprimir.</param>
    /// <param name="jobTitle">Título del trabajo de impresión.</param>
    /// <param name="preferredPrinterName">Impresora preferida opcional.</param>
    /// <returns>Tupla indicando éxito, nombre de la impresora utilizada y mensaje de error en caso de fallo.</returns>
    (bool Success, string? PrinterName, string? ErrorMessage) PrintVisualDirect(
        Visual visual, 
        string jobTitle = "ParkingFlow POS - Comprobante", 
        string? preferredPrinterName = null);
}
