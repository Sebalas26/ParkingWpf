using System.Diagnostics;
using System.Threading.Tasks;
using Parking.Entities;
using Parking.Services.Contracts;

namespace Parking.Services.Implementations;

public class MockReceiptPrinterService : IReceiptPrinterService
{
    private readonly ISessionService _sessionService;

    public MockReceiptPrinterService(ISessionService sessionService)
    {
        _sessionService = sessionService;
    }

    public async Task<bool> PrintEntryTicketAsync(ParkingTicket ticket)
    {
        var paperWidth = _sessionService.CurrentBranch?.PaperWidth > 0 ? _sessionService.CurrentBranch.PaperWidth : 80;
        Debug.WriteLine($"[PrintService] Imprimiendo Tiquete de Entrada {ticket.TicketNumber} en formato térmico {paperWidth}mm.");
        await Task.Delay(300);
        return true;
    }

    public async Task<bool> PrintExitReceiptAsync(ParkingTicket ticket)
    {
        var paperWidth = _sessionService.CurrentBranch?.PaperWidth > 0 ? _sessionService.CurrentBranch.PaperWidth : 80;
        Debug.WriteLine($"[PrintService] Imprimiendo Recibo de Salida {ticket.InvoiceNumber ?? ticket.TicketNumber} en formato térmico {paperWidth}mm.");
        await Task.Delay(300);
        return true;
    }
}
