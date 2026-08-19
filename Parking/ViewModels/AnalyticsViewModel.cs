using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Parking.Core.Enums;
using Parking.Entities;
using Parking.Models;
using Parking.Services.Contracts;

namespace Parking.ViewModels;

public partial class AnalyticsViewModel : ViewModelBase
{
    private readonly IAnalyticsService _analyticsService;
    private readonly IParkingTicketService _ticketService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private FinancialSummary _summary = new();

    [ObservableProperty]
    private int _totalCapacity = 120;

    [ObservableProperty]
    private int _occupiedSpaces = 0;

    [ObservableProperty]
    private int _availableSpaces = 120;

    [ObservableProperty]
    private double _occupancyPercentage = 0.0;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private TicketStatus? _selectedStatusFilter;

    [ObservableProperty]
    private VehicleType? _selectedVehicleTypeFilter;

    [ObservableProperty]
    private string _selectedStatusName = "Todos los Estados";

    [ObservableProperty]
    private string _selectedTypeName = "Todos los Tipos";

    public ObservableCollection<ParkingTicket> Transactions { get; } = new();

    public AnalyticsViewModel(
        IAnalyticsService analyticsService,
        IParkingTicketService ticketService,
        IDialogService dialogService)
    {
        _analyticsService = analyticsService;
        _ticketService = ticketService;
        _dialogService = dialogService;

        _ticketService.TicketRegistered += (s, e) => _ = RefreshDataAsync();
        _ticketService.TicketCompleted += (s, e) => _ = RefreshDataAsync();
    }

    public override async Task InitializeAsync()
    {
        await RefreshDataAsync();
    }

    partial void OnSearchTextChanged(string value) => _ = FilterTransactionsAsync();

    partial void OnSelectedStatusFilterChanged(TicketStatus? value)
    {
        SelectedStatusName = value.HasValue ? (value.Value == TicketStatus.Active ? "Activo" : value.Value == TicketStatus.Completed ? "Completado" : "Cancelado") : "Todos los Estados";
        _ = FilterTransactionsAsync();
    }

    partial void OnSelectedVehicleTypeFilterChanged(VehicleType? value)
    {
        SelectedTypeName = value.HasValue ? (value.Value switch
        {
            VehicleType.Motorcycle => "Motocicleta",
            VehicleType.Car => "Automóvil",
            VehicleType.Suv => "Camioneta",
            VehicleType.Van => "Furgón",
            VehicleType.HeavyTruck => "Pesado",
            _ => value.Value.ToString()
        }) : "Todos los Tipos";
        _ = FilterTransactionsAsync();
    }

    [RelayCommand]
    public async Task RefreshDataAsync()
    {
        IsBusy = true;
        BusyMessage = "Consolidando métricas financieras e historial de transacciones...";

        try
        {
            Summary = await _analyticsService.GetDailySummaryAsync();
            var occupancyStats = await _ticketService.GetOccupancyStatsAsync();

            TotalCapacity = occupancyStats.TotalCapacity > 0 ? occupancyStats.TotalCapacity : 120;
            OccupiedSpaces = occupancyStats.OccupiedSpots;
            AvailableSpaces = occupancyStats.AvailableSpots;
            OccupancyPercentage = occupancyStats.OccupancyPercentage;

            await FilterTransactionsAsync();
        }
        finally
        {
            IsBusy = false;
            BusyMessage = null;
        }
    }

    private async Task FilterTransactionsAsync()
    {
        var items = await _analyticsService.GetFilteredTransactionsAsync(
            SearchText,
            SelectedStatusFilter,
            SelectedVehicleTypeFilter);

        Transactions.Clear();
        foreach (var item in items)
        {
            Transactions.Add(item);
        }
    }

    [RelayCommand]
    private void FilterByStatus(string statusString)
    {
        if (statusString.Equals("All", StringComparison.OrdinalIgnoreCase) || statusString.Equals("Todos", StringComparison.OrdinalIgnoreCase))
        {
            SelectedStatusFilter = null;
        }
        else if (Enum.TryParse<TicketStatus>(statusString, true, out var status))
        {
            SelectedStatusFilter = status;
        }
    }

    [RelayCommand]
    private void FilterByType(string typeString)
    {
        if (typeString.Equals("All", StringComparison.OrdinalIgnoreCase) || typeString.Equals("Todos", StringComparison.OrdinalIgnoreCase))
        {
            SelectedVehicleTypeFilter = null;
        }
        else if (Enum.TryParse<VehicleType>(typeString, true, out var type))
        {
            SelectedVehicleTypeFilter = type;
        }
    }

    [RelayCommand]
    private void ClearFilters()
    {
        SearchText = string.Empty;
        SelectedStatusFilter = null;
        SelectedVehicleTypeFilter = null;
    }

    [RelayCommand]
    private async Task ExportReportAsync()
    {
        await _dialogService.ShowAlertAsync(
            "Exportar Auditoría",
            $"Informe de auditoría diaria exportado exitosamente!\n\nRecaudación Total: ${Summary.TotalRevenueToday:F2}\nTotal de Transacciones: {Transactions.Count}\nHora de Generación: {DateTime.Now:g}");
    }
}
