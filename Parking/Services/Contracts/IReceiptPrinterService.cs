using System.Threading.Tasks;
using Parking.Entities;
using Parking.Models.ApiModels;

namespace Parking.Services.Contracts;

public interface IReceiptPrinterService
{
    Task<bool> PrintEntryTicketAsync(ParkingTicket ticket);
    Task<bool> PrintExitReceiptAsync(ParkingTicket ticket);
    Task<bool> PrintShiftCloseReceiptAsync(WorkShift shift, ShiftSummaryModel? summary = null);
}
