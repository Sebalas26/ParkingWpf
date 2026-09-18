using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Parking.Core.Enums;
using Parking.Entities;
using Parking.Services.Contracts;
using Parking.ViewModels;
using Parking.Views;

namespace Parking.Services.Implementations;

public class DialogService : IDialogService
{
    private readonly IServiceProvider _serviceProvider;

    public DialogService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task ShowReceiptPreviewAsync(ParkingTicket ticket, BillingResolution? resolution = null)
    {
        return Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var dialog = new ReceiptPreviewDialog();
            var viewModel = _serviceProvider.GetRequiredService<ReceiptPreviewViewModel>();
            viewModel.LoadTicket(ticket, resolution);

            void OnRequestClose()
            {
                viewModel.RequestClose -= OnRequestClose;
                dialog.Close();
            }

            viewModel.RequestClose += OnRequestClose;
            dialog.DataContext = viewModel;
            dialog.Owner = Application.Current.MainWindow;
            dialog.ShowDialog();
        }).Task;
    }

    public Task ShowAlertAsync(string title, string message, DialogNotificationType type = DialogNotificationType.Information)
    {
        return Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var resolvedType = type;
            if (resolvedType == DialogNotificationType.Information)
            {
                var lowerTitle = title.ToLowerInvariant();
                if (lowerTitle.Contains("exit") || lowerTitle.Contains("correct") || lowerTitle.Contains("guardad"))
                {
                    resolvedType = DialogNotificationType.Success;
                }
                else if (lowerTitle.Contains("error") || lowerTitle.Contains("fall") || lowerTitle.Contains("denegad"))
                {
                    resolvedType = DialogNotificationType.Error;
                }
                else if (lowerTitle.Contains("advert") || lowerTitle.Contains("sin conexión") || lowerTitle.Contains("cuidado"))
                {
                    resolvedType = DialogNotificationType.Warning;
                }
            }

            ModernMessageDialog.ShowAlert(Application.Current.MainWindow, title, message, resolvedType);
        }).Task;
    }

    public Task<bool> ShowConfirmationAsync(string title, string message, DialogNotificationType type = DialogNotificationType.Question, string confirmText = "Confirmar", string cancelText = "Cancelar")
    {
        return Application.Current.Dispatcher.InvokeAsync(() =>
        {
            return ModernMessageDialog.ShowConfirmation(Application.Current.MainWindow, title, message, type, confirmText, cancelText);
        }).Task;
    }

    public Task<bool> ShowSyncProgressModalAsync(ISyncEngineService syncEngine)
    {
        return Application.Current.Dispatcher.InvokeAsync(() =>
        {
            return SyncProgressDialog.ShowSyncAsync(Application.Current.MainWindow, syncEngine);
        }).Task.Unwrap();
    }

    public Task<bool> ShowSyncRequiredModalAsync(Models.ApiModels.ConfigNotificationDto notification, ISyncEngineService syncEngine)
    {
        return Application.Current.Dispatcher.InvokeAsync(() =>
        {
            return SyncRequiredDialog.ShowDialogAsync(Application.Current.MainWindow, notification, syncEngine);
        }).Task.Unwrap();
    }

    public Task<bool> ShowCheckOutDialogAsync(object viewModel)
    {
        return Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var dialog = new CheckOutDialog();
            dialog.DataContext = viewModel;
            dialog.Owner = Application.Current.MainWindow;
            
            Action requestCloseHandler = () =>
            {
                dialog.DialogResult = true; // We can set result to true to signify it closed intentionally
                dialog.Close();
            };

            if (viewModel is CheckOutViewModel checkoutVm)
            {
                checkoutVm.RequestCloseDialog += requestCloseHandler;
            }

            var result = dialog.ShowDialog();

            if (viewModel is CheckOutViewModel checkoutVmUnsub)
            {
                checkoutVmUnsub.RequestCloseDialog -= requestCloseHandler;
            }

            return result ?? false;
        }).Task;
    }

    public Task<Customer?> ShowCustomerSelectionDialogAsync(string? defaultPlate = null)
    {
        return Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var connMgr = _serviceProvider.GetRequiredService<Parking.Data.Factories.IDbConnectionManager>();
            var apiClient = _serviceProvider.GetRequiredService<IApiClientService>();
            var session = _serviceProvider.GetRequiredService<ISessionService>();

            var dialog = new CustomerSelectionDialog(connMgr, apiClient, session, defaultPlate);
            dialog.Owner = Application.Current.MainWindow;

            var result = dialog.ShowDialog();
            return result == true ? dialog.SelectedCustomer : null;
        }).Task;
    }

    public Task<bool> ShowAppUpdateDialogAsync(Models.AppReleaseInfoDto release)
    {
        return Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var updateVm = _serviceProvider.GetRequiredService<AppUpdateViewModel>();
            updateVm.Initialize(release);

            var dialog = new AppUpdateDialog(updateVm);
            if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
            {
                dialog.Owner = Application.Current.MainWindow;
            }

            var result = dialog.ShowDialog();
            return result ?? false;
        }).Task;
    }
}

