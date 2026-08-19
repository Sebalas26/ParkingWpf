using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
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

    public Task ShowReceiptPreviewAsync(ParkingTicket ticket)
    {
        return Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var dialog = new ReceiptPreviewDialog();
            var viewModel = _serviceProvider.GetRequiredService<ReceiptPreviewViewModel>();
            viewModel.LoadTicket(ticket);

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

    public Task ShowAlertAsync(string title, string message)
    {
        return Application.Current.Dispatcher.InvokeAsync(() =>
        {
            MessageBox.Show(Application.Current.MainWindow, message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }).Task;
    }

    public Task<bool> ShowConfirmationAsync(string title, string message)
    {
        return Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var result = MessageBox.Show(Application.Current.MainWindow, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
            return result == MessageBoxResult.Yes;
        }).Task;
    }
}
