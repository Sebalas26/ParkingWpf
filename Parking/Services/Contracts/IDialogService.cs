using System.Threading.Tasks;
using Parking.Core.Enums;
using Parking.Entities;

namespace Parking.Services.Contracts;

public interface IDialogService
{
    Task ShowReceiptPreviewAsync(ParkingTicket ticket);
    Task ShowAlertAsync(string title, string message, DialogNotificationType type = DialogNotificationType.Information);
    Task<bool> ShowConfirmationAsync(string title, string message, DialogNotificationType type = DialogNotificationType.Question, string confirmText = "Confirmar", string cancelText = "Cancelar");
    Task<bool> ShowSyncProgressModalAsync(ISyncEngineService syncEngine);
    Task<bool> ShowSyncRequiredModalAsync(Models.ApiModels.ConfigNotificationDto notification, ISyncEngineService syncEngine);
    Task<bool> ShowCheckOutDialogAsync(object viewModel);
}

