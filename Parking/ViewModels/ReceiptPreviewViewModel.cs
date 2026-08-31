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
    private readonly ISessionService _sessionService;

    [ObservableProperty]
    private ParkingTicket _ticket = new();

    [ObservableProperty]
    private bool _isPrinting;

    [ObservableProperty]
    private bool _printSuccess;

    [ObservableProperty]
    private System.Windows.Media.ImageSource? _barcodeImage;

    [ObservableProperty]
    private string _branchName = "PARKING FLOW";

    [ObservableProperty]
    private string _branchNit = "NIT: 900.914.246-2";

    [ObservableProperty]
    private string _branchAddress = "CALLE 26 #57-83";

    [ObservableProperty]
    private string _formattedRateText = string.Empty;

    [ObservableProperty]
    private System.Windows.Media.ImageSource? _consultationQrCodeImage;

    public string PublicConsultationUrl => "https://www.parking-flow.com/mockup-consulta";

    public event Action? RequestClose;

    public ReceiptPreviewViewModel(IReceiptPrinterService printerService, ISessionService sessionService)
    {
        _printerService = printerService;
        _sessionService = sessionService;
    }

    public void LoadTicket(ParkingTicket ticket)
    {
        Ticket = ticket;
        PrintSuccess = false;
        BarcodeImage = Services.Implementations.BarcodeGeneratorService.GenerateCode128(ticket.PlateNumber);

        var currentBranch = _sessionService.CurrentBranch;
        BranchName = !string.IsNullOrWhiteSpace(currentBranch?.Name) ? currentBranch.Name.ToUpperInvariant() : "PARKING FLOW";
        BranchAddress = !string.IsNullOrWhiteSpace(currentBranch?.Address) ? currentBranch.Address.ToUpperInvariant() : "CALLE 26 #57-83";
        BranchNit = !string.IsNullOrWhiteSpace(currentBranch?.Notes) && currentBranch.Notes.StartsWith("NIT", StringComparison.OrdinalIgnoreCase)
            ? currentBranch.Notes
            : "NIT: 900.914.246-2";

        FormattedRateText = $"TARIFA: {ticket.HourlyRate:C0} / HORA";
        ConsultationQrCodeImage = Services.Implementations.QrCodeGeneratorService.GenerateQrCode("https://www.parking-flow.com/mockup-consulta", 8);
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
