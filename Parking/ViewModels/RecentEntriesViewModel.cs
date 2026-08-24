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

[RequirePermission("recent_entries.view", "Entradas del Turno / Patio")]
public partial class RecentEntriesViewModel : ViewModelBase
{
    private readonly IParkingTicketService _ticketService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private ParkingTicket? _selectedTicket;

    [ObservableProperty]
    private int _totalEntriesCount;

    public ObservableCollection<ParkingTicket> Entries { get; } = new();

    public RecentEntriesViewModel(
        IParkingTicketService ticketService,
        IDialogService dialogService)
    {
        _ticketService = ticketService;
        _dialogService = dialogService;

        _ticketService.TicketRegistered += (s, e) => _ = LoadEntriesAsync();
        _ticketService.TicketCompleted += (s, e) => _ = LoadEntriesAsync();
    }

    public override async Task InitializeAsync()
    {
        await LoadEntriesAsync();
    }

    partial void OnSearchQueryChanged(string value) => _ = LoadEntriesAsync();

    [RelayCommand]
    public async Task LoadEntriesAsync()
    {
        IsBusy = true;
        BusyMessage = "Cargando entradas registradas en el turno...";

        try
        {
            var activeTickets = await _ticketService.GetActiveTicketsAsync();

            var query = SearchQuery?.Trim() ?? string.Empty;
            var filtered = string.IsNullOrWhiteSpace(query)
                ? activeTickets
                : activeTickets.Where(t => t.PlateNumber.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                           t.TicketNumber.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

            Entries.Clear();
            foreach (var ticket in filtered)
            {
                Entries.Add(ticket);
            }

            TotalEntriesCount = Entries.Count;
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
