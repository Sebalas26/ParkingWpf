using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Parking.Core.Enums;
using Parking.Core.Security;
using Parking.Entities;
using Parking.Services.Contracts;

namespace Parking.ViewModels;

[RequirePermission("monitoring.view_occupancy", "Entradas del Turno / Patio")]
public partial class RecentEntriesViewModel : ViewModelBase
{
    private readonly IParkingTicketService _ticketService;
    private readonly IDialogService _dialogService;
    private readonly IShiftService _shiftService;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private int _selectedTab = 0;

    public bool IsActiveTab => SelectedTab == 0;
    public bool IsCompletedTab => SelectedTab == 1;
    public bool IsHistoricalTab => SelectedTab == 2;
    public bool HasSearchQuery => !string.IsNullOrWhiteSpace(SearchQuery);

    [ObservableProperty]
    private ParkingTicket? _selectedTicket;

    [ObservableProperty]
    private int _totalEntriesCount;

    [ObservableProperty]
    private int _activeTicketsCount;

    [ObservableProperty]
    private int _completedTicketsCount;

    [ObservableProperty]
    private int _historicalTicketsCount;

    [ObservableProperty]
    private DateTime _historicalDateFrom = DateTime.Today;

    [ObservableProperty]
    private DateTime _historicalDateTo = DateTime.Today.AddDays(1).AddSeconds(-1);

    public ObservableCollection<ParkingTicket> Entries { get; } = new();
    public ObservableCollection<ParkingTicket> ActiveEntries { get; } = new();
    public ObservableCollection<ParkingTicket> CompletedEntries { get; } = new();
    public ObservableCollection<ParkingTicket> HistoricalEntries { get; } = new();

    public RecentEntriesViewModel(
        IParkingTicketService ticketService,
        IDialogService dialogService,
        IShiftService shiftService)
    {
        _ticketService = ticketService;
        _dialogService = dialogService;
        _shiftService = shiftService;

        _ticketService.TicketRegistered += (s, e) => _ = LoadEntriesAsync();
        _ticketService.TicketCompleted += (s, e) =>
        {
            _ = LoadEntriesAsync();
            if (SelectedTab == 2)
            {
                _ = LoadHistoricalEntriesAsync();
            }
        };
    }

    public override async Task InitializeAsync()
    {
        await LoadEntriesAsync();
    }

    partial void OnSearchQueryChanged(string value)
    {
        OnPropertyChanged(nameof(HasSearchQuery));
        if (SelectedTab == 2)
        {
            _ = LoadHistoricalEntriesAsync();
        }
        else
        {
            _ = LoadEntriesAsync();
        }
    }

    partial void OnHistoricalDateFromChanged(DateTime value)
    {
        if (SelectedTab == 2)
        {
            _ = LoadHistoricalEntriesAsync();
        }
    }

    partial void OnHistoricalDateToChanged(DateTime value)
    {
        if (SelectedTab == 2)
        {
            _ = LoadHistoricalEntriesAsync();
        }
    }

    partial void OnSelectedTabChanged(int value)
    {
        OnPropertyChanged(nameof(IsActiveTab));
        OnPropertyChanged(nameof(IsCompletedTab));
        OnPropertyChanged(nameof(IsHistoricalTab));
        Entries.Clear();
        if (value == 0)
        {
            foreach (var t in ActiveEntries) Entries.Add(t);
        }
        else if (value == 1)
        {
            foreach (var t in CompletedEntries) Entries.Add(t);
        }
        else if (value == 2)
        {
            _ = LoadHistoricalEntriesAsync();
        }
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchQuery = string.Empty;
    }

    [RelayCommand]
    private void SetTab(object? parameter)
    {
        if (parameter is int intVal)
        {
            SelectedTab = intVal;
        }
        else if (parameter != null && int.TryParse(parameter.ToString(), out var parsedVal))
        {
            SelectedTab = parsedVal;
        }
    }

    [RelayCommand]
    public async Task LoadEntriesAsync()
    {
        if (SelectedTab == 2)
        {
            await LoadHistoricalEntriesAsync();
            return;
        }

        IsBusy = true;
        BusyMessage = "Cargando vehículos del turno...";

        try
        {
            var activeTickets = await _ticketService.GetActiveTicketsAsync();

            var shiftStart = _shiftService.CurrentShift?.StartTimeUtc ?? DateTime.UtcNow.Date;
            var completedTickets = await _ticketService.GetCompletedTicketsByShiftAsync(shiftStart);

            var query = SearchQuery?.Trim() ?? string.Empty;

            var filteredActive = string.IsNullOrWhiteSpace(query)
                ? activeTickets
                : activeTickets.Where(t => t.PlateNumber.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                           t.TicketNumber.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

            var filteredCompleted = string.IsNullOrWhiteSpace(query)
                ? completedTickets
                : completedTickets.Where(t => t.PlateNumber.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                              t.TicketNumber.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                              (!string.IsNullOrWhiteSpace(t.InvoiceNumber) && t.InvoiceNumber.Contains(query, StringComparison.OrdinalIgnoreCase))).ToList();

            ActiveEntries.Clear();
            foreach (var ticket in filteredActive)
            {
                ActiveEntries.Add(ticket);
            }

            CompletedEntries.Clear();
            foreach (var ticket in filteredCompleted)
            {
                CompletedEntries.Add(ticket);
            }

            Entries.Clear();
            var currentList = SelectedTab == 0 ? ActiveEntries : CompletedEntries;
            foreach (var t in currentList)
            {
                Entries.Add(t);
            }

            ActiveTicketsCount = ActiveEntries.Count;
            CompletedTicketsCount = CompletedEntries.Count;
            TotalEntriesCount = ActiveTicketsCount;
        }
        finally
        {
            IsBusy = false;
            BusyMessage = null;
        }
    }

    [RelayCommand]
    public async Task LoadHistoricalEntriesAsync()
    {
        IsBusy = true;
        BusyMessage = "Consultando histórico de facturación...";

        try
        {
            var fromUtc = HistoricalDateFrom.Date.ToUniversalTime();
            var toUtc = HistoricalDateTo.Date.AddDays(1).AddTicks(-1).ToUniversalTime();

            var tickets = await _ticketService.GetHistoricalTicketsAsync(fromUtc, toUtc, SearchQuery);

            HistoricalEntries.Clear();
            foreach (var ticket in tickets)
            {
                HistoricalEntries.Add(ticket);
            }

            HistoricalTicketsCount = HistoricalEntries.Count;
        }
        finally
        {
            IsBusy = false;
            BusyMessage = null;
        }
    }

    [RelayCommand]
    private async Task ConvertTicketToInvoiceAsync(ParkingTicket? ticket)
    {
        if (ticket == null) return;

        if (ticket.IsElectronicInvoice && !string.IsNullOrWhiteSpace(ticket.InvoiceNumber))
        {
            await _dialogService.ShowAlertAsync("Tiquete Ya Facturado", $"Este tiquete ya cuenta con la factura electrónica {ticket.InvoiceNumber}.", DialogNotificationType.Information);
            return;
        }

        var customer = await _dialogService.ShowCustomerSelectionDialogAsync(ticket.PlateNumber);
        if (customer == null) return;

        IsBusy = true;
        BusyMessage = "Convirtiendo comprobante a Factura Electrónica...";

        try
        {
            var updatedTicket = await _ticketService.ConvertTicketToInvoiceAsync(ticket.TicketId, customer.CustomerId);
            if (updatedTicket != null)
            {
                await LoadHistoricalEntriesAsync();

                var invNumber = !string.IsNullOrWhiteSpace(updatedTicket.InvoiceNumber)
                    ? updatedTicket.InvoiceNumber
                    : "FE-Pendiente";

                await _dialogService.ShowAlertAsync(
                    "Facturación Exitosa",
                    $"El tiquete con placa {ticket.PlateNumber} ha sido emitido como Factura Electrónica DIAN ({invNumber}).",
                    DialogNotificationType.Success);

                var wantPrint = await _dialogService.ShowConfirmationAsync(
                    "Imprimir Factura Electrónica",
                    "¿Desea visualizar e imprimir el comprobante fiscal ahora?",
                    DialogNotificationType.Question,
                    "Imprimir",
                    "No imprimir");

                if (wantPrint)
                {
                    await _dialogService.ShowReceiptPreviewAsync(updatedTicket);
                }
            }
            else
            {
                await _dialogService.ShowAlertAsync("Aviso de Emisión", "El comprobante fue encolado para sincronización con la DIAN.", DialogNotificationType.Warning);
            }
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync("Error de Emisión", $"No se pudo emitir la factura electrónica: {ex.Message}", DialogNotificationType.Error);
        }
        finally
        {
            IsBusy = false;
            BusyMessage = null;
        }
    }

    [RelayCommand]
    private async Task ReprintTicketAsync(ParkingTicket ticket)
    {
        if (ticket == null) return;
        await _dialogService.ShowReceiptPreviewAsync(ticket);
    }
}
