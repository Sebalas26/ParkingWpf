using System.Threading.Tasks;
using Parking.Entities;

namespace Parking.Services.Contracts;

public interface IDialogService
{
    Task ShowReceiptPreviewAsync(ParkingTicket ticket);
    Task ShowAlertAsync(string title, string message);
    Task<bool> ShowConfirmationAsync(string title, string message);
}
