using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Parking.Entities;
using Parking.Services.Contracts;

namespace Parking.ViewModels;

public partial class ReceiptPreviewViewModel : ViewModelBase
{
    private readonly IReceiptPrinterService _printerService;

    [ObservableProperty]
    private ParkingTicket _ticket = new();

    [ObservableProperty]
    private bool _isPrinting;

    [ObservableProperty]
    private bool _printSuccess;

    public string PublicConsultationUrl => $"https://localhost:7023/api/public/tickets/status?plate={Ticket.PlateNumber}";

    public event Action? RequestClose;

    public ReceiptPreviewViewModel(IReceiptPrinterService printerService)
    {
        _printerService = printerService;
    }

    public void LoadTicket(ParkingTicket ticket)
    {
        Ticket = ticket;
        PrintSuccess = false;
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
