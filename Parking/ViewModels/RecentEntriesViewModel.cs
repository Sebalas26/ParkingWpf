using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    private int _selectedTab = 0; // 0 = Activos en Patio, 1 = Salidas del Turno

    public bool IsActiveTab => SelectedTab == 0;
    public bool IsCompletedTab => SelectedTab == 1;
    public bool HasSearchQuery => !string.IsNullOrWhiteSpace(SearchQuery);

    [ObservableProperty]
    private ParkingTicket? _selectedTicket;

    [ObservableProperty]
    private int _totalEntriesCount;

    [ObservableProperty]
    private int _activeTicketsCount;

    [ObservableProperty]
    private int _completedTicketsCount;

    public ObservableCollection<ParkingTicket> Entries { get; } = new();
    public ObservableCollection<ParkingTicket> ActiveEntries { get; } = new();
    public ObservableCollection<ParkingTicket> CompletedEntries { get; } = new();

    public RecentEntriesViewModel(
        IParkingTicketService ticketService,
        IDialogService dialogService,
        IShiftService shiftService)
    {
        _ticketService = ticketService;
        _dialogService = dialogService;
        _shiftService = shiftService;

        _ticketService.TicketRegistered += (s, e) => _ = LoadEntriesAsync();
        _ticketService.TicketCompleted += (s, e) => _ = LoadEntriesAsync();
    }

    public override async Task InitializeAsync()
    {
        await LoadEntriesAsync();
    }

    partial void OnSearchQueryChanged(string value)
    {
        OnPropertyChanged(nameof(HasSearchQuery));
        _ = LoadEntriesAsync();
    }

    partial void OnSelectedTabChanged(int value)
    {
        OnPropertyChanged(nameof(IsActiveTab));
        OnPropertyChanged(nameof(IsCompletedTab));
        Entries.Clear();
        var currentList = value == 0 ? ActiveEntries : CompletedEntries;
        foreach (var t in currentList)
        {
            Entries.Add(t);
        }
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchQuery = string.Empty;
    }

    [RelayCommand]
    private void SetTab(int tabIndex)
    {
        SelectedTab = tabIndex;
    }

    [RelayCommand]
    public async Task LoadEntriesAsync()
    {
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

            // Sincronizar colección Entries con la pestaña activa
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
    private async Task ReprintTicketAsync(ParkingTicket ticket)
    {
        if (ticket == null) return;
        await _dialogService.ShowReceiptPreviewAsync(ticket);
    }
}
