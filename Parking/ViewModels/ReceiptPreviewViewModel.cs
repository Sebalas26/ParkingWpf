using System;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Parking.Entities;
using Parking.Services.Contracts;

namespace Parking.ViewModels;

public partial class ReceiptPreviewViewModel : ViewModelBase
{
    private readonly IReceiptPrinterService _printerService;
    private readonly IBarcodeGeneratorService _barcodeGenerator;

    [ObservableProperty]
    private ParkingTicket _ticket = new();

    [ObservableProperty]
    private bool _isPrinting;

    [ObservableProperty]
    private bool _printSuccess;

    [ObservableProperty]
    private BitmapImage? _qrCodeImage;

    [ObservableProperty]
    private BitmapSource? _barcodeImage;

    public string PublicConsultationUrl => $"https://localhost:7023/api/public/tickets/status?plate={Ticket.PlateNumber}";

    public event Action? RequestClose;

    public ReceiptPreviewViewModel(
        IReceiptPrinterService printerService,
        IBarcodeGeneratorService barcodeGenerator)
    {
        _printerService = printerService;
        _barcodeGenerator = barcodeGenerator;
    }

    public void LoadTicket(ParkingTicket ticket)
    {
        Ticket = ticket;
        PrintSuccess = false;
        QrCodeImage = Services.Implementations.QrCodeGeneratorService.GenerateQrCode(PublicConsultationUrl);
        BarcodeImage = _barcodeGenerator.GenerateCode128Barcode(ticket.TicketNumber, height: 50, moduleWidth: 2);
    }

    [RelayCommand]
    private async Task PrintTicketAsync()
    {
        if (IsPrinting)
        {
            return;
        }

        IsPrinting = true;
        try
        {
            if (Ticket.Status == Core.Enums.TicketStatus.Completed)
            {
                PrintSuccess = await _printerService.PrintExitReceiptAsync(Ticket);
            }
            else
            {
                PrintSuccess = await _printerService.PrintEntryTicketAsync(Ticket);
            }
        }
        finally
        {
            IsPrinting = false;
        }
    }

    [RelayCommand]
    private void Close()
    {
        RequestClose?.Invoke();
    }
}
