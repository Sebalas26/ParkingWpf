using System.Threading.Tasks;
using Parking.Entities;

namespace Parking.Services.Contracts;

public interface IReceiptPrinterService
{
    Task<bool> PrintEntryTicketAsync(ParkingTicket ticket);
    Task<bool> PrintExitReceiptAsync(ParkingTicket ticket);
}
