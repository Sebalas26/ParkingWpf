using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Printing;
using System.Windows.Controls;
using System.Windows.Media;
using Parking.Services.Contracts;

namespace Parking.Services.Implementations;

public class PrinterDiscoveryService : IPrinterDiscoveryService
{
    private static readonly string[] ThermalKeywords = new[]
    {
        "pos", "thermal", "receipt", "ticket", "tm-", "xp-", "zj-", 
        "epson", "bixolon", "star", "58", "80", "generic"
    };

    public IReadOnlyList<PrinterQueueInfo> GetInstalledPrinters()
    {
        var list = new List<PrinterQueueInfo>();

        try
        {
            using var printServer = new LocalPrintServer();
            var defaultQueueName = string.Empty;
            try
            {
                defaultQueueName = LocalPrintServer.GetDefaultPrintQueue()?.Name ?? string.Empty;
            }
            catch
            {
                // Fallback defensivo si no hay impresora predeterminada
            }

            var queues = printServer.GetPrintQueues(new[]
            {
                EnumeratedPrintQueueTypes.Local,
                EnumeratedPrintQueueTypes.Connections
            });

            foreach (var q in queues)
            {
                try
                {
                    bool isOnline = !q.IsOffline;
                    bool isDefault = !string.IsNullOrWhiteSpace(defaultQueueName) && 
                                     string.Equals(q.Name, defaultQueueName, StringComparison.OrdinalIgnoreCase);
                    bool isThermal = ThermalKeywords.Any(k => q.Name.Contains(k, StringComparison.OrdinalIgnoreCase));

                    list.Add(new PrinterQueueInfo
                    {
                        Name = q.Name,
                        IsDefault = isDefault,
                        IsThermal = isThermal,
                        IsOnline = isOnline,
                        StatusDescription = isOnline ? "En línea" : "Desconectada / Offline"
                    });
                }
                catch
                {
                    // Omitir colas inaccesibles
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PrinterDiscoveryService] Error al enumerar impresoras: {ex.Message}");
        }

        return list;
    }

    public PrintQueue? ResolveConnectedPrinter(string? preferredPrinterName = null)
    {
        try
        {
            using var printServer = new LocalPrintServer();
            var queues = printServer.GetPrintQueues(new[]
            {
                EnumeratedPrintQueueTypes.Local,
                EnumeratedPrintQueueTypes.Connections
            }).ToList();

            if (queues.Count == 0)
            {
                return null;
            }

            // 1. Si se especificó una impresora preferida (ej: configurada en sede)
            if (!string.IsNullOrWhiteSpace(preferredPrinterName))
            {
                var preferred = queues.FirstOrDefault(q => 
                    string.Equals(q.Name, preferredPrinterName.Trim(), StringComparison.OrdinalIgnoreCase) && !q.IsOffline);
                if (preferred != null) return preferred;
            }

            // 2. Buscar impresora térmica en línea
            var thermalOnline = queues.FirstOrDefault(q => 
                ThermalKeywords.Any(k => q.Name.Contains(k, StringComparison.OrdinalIgnoreCase)) && !q.IsOffline);
            if (thermalOnline != null) return thermalOnline;

            // 3. Impresora predeterminada de Windows si está en línea
            try
            {
                var defaultQueue = LocalPrintServer.GetDefaultPrintQueue();
                if (defaultQueue != null && !defaultQueue.IsOffline)
                {
                    return defaultQueue;
                }
            }
            catch { }

            // 4. Cualquier impresora que no esté desconectada
            var anyOnline = queues.FirstOrDefault(q => !q.IsOffline);
            if (anyOnline != null) return anyOnline;

            // 5. Último fallback: la impresora predeterminada o la primera disponible
            try
            {
                return LocalPrintServer.GetDefaultPrintQueue() ?? queues.FirstOrDefault();
            }
            catch
            {
                return queues.FirstOrDefault();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PrinterDiscoveryService] Error al resolver impresora conectada: {ex.Message}");
            return null;
        }
    }

    public (bool Success, string? PrinterName, string? ErrorMessage) PrintVisualDirect(
        Visual visual, 
        string jobTitle = "ParkingFlow POS - Comprobante", 
        string? preferredPrinterName = null)
    {
        if (visual == null)
        {
            return (false, null, "El contenido visual a imprimir no es válido o está vacío.");
        }

        try
        {
            var targetQueue = ResolveConnectedPrinter(preferredPrinterName);
            if (targetQueue == null)
            {
                return (false, null, "No se detectó ninguna impresora conectada o instalada en este equipo. Por favor verifique la conexión física del dispositivo.");
            }

            var printDialog = new PrintDialog();
            printDialog.PrintQueue = targetQueue;

            // Red de seguridad: si el visual excede el área imprimible del driver,
            // aplicar escala proporcional automática para que nada se corte jamás.
            Visual printTarget = visual;
            if (visual is System.Windows.FrameworkElement fe && fe.ActualWidth > 0)
            {
                var printableWidth = printDialog.PrintableAreaWidth;
                if (printableWidth > 0 && fe.ActualWidth > printableWidth)
                {
                    var scale = printableWidth / fe.ActualWidth;
                    var drawingVisual = new System.Windows.Media.DrawingVisual();
                    using (var dc = drawingVisual.RenderOpen())
                    {
                        dc.PushTransform(new ScaleTransform(scale, scale));
                        var brush = new System.Windows.Media.VisualBrush(fe)
                        {
                            Stretch = System.Windows.Media.Stretch.None,
                            AlignmentX = System.Windows.Media.AlignmentX.Left,
                            AlignmentY = System.Windows.Media.AlignmentY.Top
                        };
                        dc.DrawRectangle(brush, null, new System.Windows.Rect(0, 0, fe.ActualWidth, fe.ActualHeight));
                        dc.Pop();
                    }
                    printTarget = drawingVisual;
                }
            }

            // Enviar a imprimir directamente a la cola sin invocar ShowDialog()
            printDialog.PrintVisual(printTarget, jobTitle);

            return (true, targetQueue.Name, null);
        }
        catch (Exception ex)
        {
            var errorMsg = $"Error al enviar trabajo a la impresora: {ex.Message}";
            Debug.WriteLine($"[PrinterDiscoveryService] {errorMsg}");
            return (false, null, errorMsg);
        }
    }
}
