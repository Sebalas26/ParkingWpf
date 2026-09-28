using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Parking.Services.Contracts;
using Parking.Services.Implementations;
using Parking.ViewModels;

namespace Parking.Views;

public partial class ReceiptPreviewDialog : Window
{
    private readonly IPrinterDiscoveryService _printerDiscovery;

    public ReceiptPreviewDialog()
    {
        InitializeComponent();
        _printerDiscovery = App.CurrentServices?.GetService<IPrinterDiscoveryService>() 
            ?? new PrinterDiscoveryService();

        Loaded += ReceiptPreviewDialog_Loaded;
        PreviewKeyDown += ReceiptPreviewDialog_PreviewKeyDown;
    }

    private async void ReceiptPreviewDialog_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Return)
        {
            e.Handled = true;
            if (DataContext is ReceiptPreviewViewModel vm)
            {
                if (vm.PrintTicketCommand.CanExecute(null))
                {
                    await vm.PrintTicketCommand.ExecuteAsync(null);
                    if (vm.PrintSuccess)
                    {
                        await Task.Delay(350);
                        Close();
                    }
                }
            }
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }

    private void ReceiptPreviewDialog_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ReceiptPreviewViewModel vm)
        {
            vm.DirectPrintHandler = () =>
            {
                var result = _printerDiscovery.PrintVisualDirect(TicketPrintableContent, "ParkingFlow POS - Comprobante");
                return Task.FromResult(result);
            };
            vm.RefreshDetectedPrinter();
        }

        if (Owner != null)
        {
            if (Owner.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Maximized;
            }
            else
            {
                this.WindowState = WindowState.Normal;
                this.Left = Owner.Left;
                this.Top = Owner.Top;
                this.Width = Owner.ActualWidth;
                this.Height = Owner.ActualHeight;
            }
        }
        else if (Application.Current?.MainWindow != null && Application.Current.MainWindow.IsVisible)
        {
            var main = Application.Current.MainWindow;
            if (main.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Maximized;
            }
            else
            {
                this.WindowState = WindowState.Normal;
                this.Left = main.Left;
                this.Top = main.Top;
                this.Width = main.ActualWidth;
                this.Height = main.ActualHeight;
            }
        }
        else
        {
            this.WindowState = WindowState.Maximized;
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
