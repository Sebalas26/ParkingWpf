using System.Threading.Tasks;
using Parking.Entities;
using Parking.Services.Contracts;

namespace Parking.Services.Implementations;

public class MockReceiptPrinterService : IReceiptPrinterService
{
    public async Task<bool> PrintEntryTicketAsync(ParkingTicket ticket)
    {
        await Task.Delay(300);
        return true;
    }

    public async Task<bool> PrintExitReceiptAsync(ParkingTicket ticket)
    {
        await Task.Delay(300);
        return true;
    }
}
