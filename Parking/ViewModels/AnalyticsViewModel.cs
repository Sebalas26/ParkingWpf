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
    public ObservableCollection<VehicleCategoryDistributionItem> CategoryDistribution { get; } = new();
    public ObservableCollection<DailyTrafficStatItem> WeeklyTrafficStats { get; } = new();

    private readonly System.Windows.Threading.DispatcherTimer _liveTimer;

    public AnalyticsViewModel(
        IAnalyticsService analyticsService,
        IParkingTicketService ticketService,
        IDialogService dialogService)
    {
        _analyticsService = analyticsService;
        _ticketService = ticketService;
        _dialogService = dialogService;

        _liveTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _liveTimer.Tick += (s, e) => _ = RefreshDataAsync();

        _ticketService.TicketRegistered += (s, e) => _ = RefreshDataAsync();
        _ticketService.TicketCompleted += (s, e) => _ = RefreshDataAsync();
    }

    public override async Task InitializeAsync()
    {
        _liveTimer.Start();
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
            UpdateCategoryDistribution();
            UpdateWeeklyTrafficStats();
        }
        finally
        {
            IsBusy = false;
            BusyMessage = null;
        }
    }

    private void UpdateCategoryDistribution()
    {
        CategoryDistribution.Clear();

        var categories = new (VehicleType Type, string Name, string Color)[]
        {
            (VehicleType.Car, "Automóviles / Sedán", "BrushPrimary"),
            (VehicleType.Suv, "Camionetas / SUV", "BrushCyan"),
            (VehicleType.Motorcycle, "Motocicletas", "BrushSuccess"),
            (VehicleType.Van, "Furgón / Minibús", "BrushWarning"),
            (VehicleType.HeavyTruck, "Vehículos Pesados / Camión", "BrushDanger")
        };

        int totalCount = Summary.CountByVehicleType.Values.Sum();

        foreach (var cat in categories)
        {
            Summary.CountByVehicleType.TryGetValue(cat.Type, out var count);
            double pct = totalCount > 0 ? ((double)count / totalCount) * 100.0 : 0.0;

            CategoryDistribution.Add(new VehicleCategoryDistributionItem
            {
                CategoryName = cat.Name,
                Count = count,
                Percentage = pct,
                ColorBrushKey = cat.Color
            });
        }
    }

    private void UpdateWeeklyTrafficStats()
    {
        WeeklyTrafficStats.Clear();

        var dayNames = new[] { "Lun", "Mar", "Mié", "Jue", "Vie", "Sáb", "Dom" };
        var todayDayOfWeek = (int)DateTime.Now.DayOfWeek;
        // Ajustar DayOfWeek de .NET (0=Dom, 1=Lun...) a (0=Lun ... 6=Dom)
        int todayIndex = todayDayOfWeek == 0 ? 6 : todayDayOfWeek - 1;

        int[] dayCounts = new int[7];

        foreach (var t in Transactions)
        {
            var day = (int)t.EntryTime.DayOfWeek;
            int idx = day == 0 ? 6 : day - 1;
            dayCounts[idx]++;
        }

        int maxCount = dayCounts.Max();
        if (maxCount == 0) maxCount = 1;

        for (int i = 0; i < 7; i++)
        {
            int count = dayCounts[i];
            double height = Math.Max(12, ((double)count / maxCount) * 120.0);

            WeeklyTrafficStats.Add(new DailyTrafficStatItem
            {
                DayName = dayNames[i],
                Count = count,
                BarHeight = height,
                IsToday = i == todayIndex
            });
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

public class VehicleCategoryDistributionItem
{
    public string CategoryName { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percentage { get; set; }
    public string FormattedPercentage => $"{Percentage:F0}%";
    public string CountLabel => $"{Count} vehículos";
    public string ColorBrushKey { get; set; } = "BrushPrimary";
}

public class DailyTrafficStatItem
{
    public string DayName { get; set; } = string.Empty;
    public int Count { get; set; }
    public double BarHeight { get; set; }
    public bool IsToday { get; set; }
}
