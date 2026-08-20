using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Parking.Core.Enums;
using Parking.Entities;
using Parking.Models;
using Parking.Services.Contracts;

namespace Parking.ViewModels;

public partial class MainShellViewModel : ViewModelBase
{
    private static readonly CultureInfo SpanishCulture = new("es-ES");
    private readonly IAuthService _authService;
    private readonly IParkingTicketService _ticketService;
    private readonly INavigationService _navigationService;
    private readonly ISyncEngineService _syncEngine;
    private readonly IBackgroundSyncScheduler _backgroundSync;
    private readonly IThemeService _themeService;
    private readonly IDialogService _dialogService;
    private readonly IShiftService _shiftService;
    private readonly DispatcherTimer _clockTimer;

    [ObservableProperty]
    private ViewModelBase? _activeView;

    [ObservableProperty]
    private UserSessionModel? _currentUser;

    [ObservableProperty]
    private string _currentTimeString = string.Empty;

    [ObservableProperty]
    private OccupancyStats _occupancy = new();

    [ObservableProperty]
    private string _selectedNavSection = "CheckIn";

    [ObservableProperty]
    private bool _isOnlineMode;

    [ObservableProperty]
    private string _syncStatusText = "Conectando al API Central...";

    [ObservableProperty]
    private bool _isSyncing;

    [ObservableProperty]
    private AppTheme _currentAppTheme = AppTheme.FigmaTeal;

    [ObservableProperty]
    private ThemeInfo? _selectedTheme;

    public IReadOnlyList<ThemeInfo> AvailableThemes => _themeService.GetAvailableThemes();

    public event Action? LogoutRequested;

    public MainShellViewModel(
        IAuthService authService,
        IParkingTicketService ticketService,
        INavigationService navigationService,
        ISyncEngineService syncEngine,
        IBackgroundSyncScheduler backgroundSync,
        IThemeService themeService,
        IDialogService dialogService,
        IShiftService shiftService)
    {
        _authService = authService;
        _ticketService = ticketService;
        _navigationService = navigationService;
        _syncEngine = syncEngine;
        _backgroundSync = backgroundSync;
        _themeService = themeService;
        _dialogService = dialogService;
        _shiftService = shiftService;

        _currentAppTheme = _themeService.CurrentTheme;
        _selectedTheme = AvailableThemes.FirstOrDefault(t => t.Theme == _themeService.CurrentTheme) ?? AvailableThemes.First();

        _navigationService.CurrentViewModelChanged += (s, vm) =>
        {
            ActiveView = vm;
            UpdateSelectedNavSection(vm);
        };

        _ticketService.OccupancyChanged += (s, stats) =>
        {
            Occupancy = stats;
        };

        _syncEngine.SyncStatusChanged += (s, status) =>
        {
            IsOnlineMode = _syncEngine.IsOnline;
            SyncStatusText = status;
        };

        _themeService.ThemeChanged += (s, theme) =>
        {
            CurrentAppTheme = theme;
            SelectedTheme = AvailableThemes.FirstOrDefault(t => t.Theme == theme);
        };

        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (s, e) => UpdateClock();
        _clockTimer.Start();

        UpdateClock();
    }

    partial void OnSelectedThemeChanged(ThemeInfo? value)
    {
        if (value != null && value.Theme != _themeService.CurrentTheme)
        {
            _themeService.SetTheme(value.Theme);
        }
    }

    public override async Task InitializeAsync()
    {
        CurrentUser = _authService.CurrentUser;
        Occupancy = await _ticketService.GetOccupancyStatsAsync();

        // 1. Verificar si hay turno activo; si no, abrir pantalla de apertura de turno
        var activeShift = await _shiftService.GetActiveShiftAsync();
        if (activeShift == null)
        {
            NavigateToShiftClosure();
        }
        else
        {
            NavigateToCheckIn();
        }

        // 2. Iniciar programador de sincronización en segundo plano (cada 1 hora)
        _backgroundSync.Start();

        // 3. Ejecutar primera sincronización con el API en segundo plano sin bloquear la interfaz
        _ = Task.Run(async () =>
        {
            try
            {
                await ManualSyncAsync();
            }
            catch { }
        });
    }

    private void UpdateClock()
    {
        CurrentTimeString = DateTime.Now.ToString("ddd, dd MMM yyyy • HH:mm:ss", SpanishCulture);
    }

    private void UpdateSelectedNavSection(ViewModelBase vm)
    {
        SelectedNavSection = vm switch
        {
            CheckInViewModel => "CheckIn",
            CheckOutViewModel => "CheckOut",
            RecentEntriesViewModel => "RecentEntries",
            AnalyticsViewModel => "Analytics",
            ShiftClosureViewModel => "ShiftClosure",
            MonthlySubscriptionsViewModel => "MonthlySubscriptions",
            _ => "CheckIn"
        };
    }

    [RelayCommand]
    public async Task ManualSyncAsync()
    {
        if (IsSyncing) return;
        IsSyncing = true;
        SyncStatusText = "Sincronizando con API Central...";

        try
        {
            await _backgroundSync.TriggerManualSyncAsync();
            IsOnlineMode = _syncEngine.IsOnline;
            SyncStatusText = _syncEngine.SyncStatusDescription;
            Occupancy = await _ticketService.GetOccupancyStatsAsync();
        }
        finally
        {
            IsSyncing = false;
        }
    }

    [RelayCommand]
    public async Task UserManualSyncAsync()
    {
        if (IsSyncing) return;
        IsSyncing = true;
        SyncStatusText = "Sincronizando con API Central...";

        try
        {
            await _backgroundSync.TriggerManualSyncAsync();
            IsOnlineMode = _syncEngine.IsOnline;
            SyncStatusText = _syncEngine.SyncStatusDescription;
            Occupancy = await _ticketService.GetOccupancyStatsAsync();

            if (IsOnlineMode)
            {
                await _dialogService.ShowAlertAsync(
                    "Sincronización Exitosa",
                    "El sistema y la base de datos local se han actualizado y sincronizado correctamente con el servidor central.");
            }
            else
            {
                await _dialogService.ShowAlertAsync(
                    "Modo Sin Conexión",
                    "No se pudo conectar con el servidor central. Se mantendrán los datos locales hasta restablecer la conexión.");
            }
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync(
                "Error de Sincronización",
                $"Ocurrió un error al sincronizar con el servidor: {ex.Message}");
        }
        finally
        {
            IsSyncing = false;
        }
    }

    [RelayCommand]
    private void SelectTheme(AppTheme theme)
    {
        _themeService.SetTheme(theme);
    }

    [RelayCommand]
    private void NavigateToCheckIn()
    {
        _navigationService.NavigateTo<CheckInViewModel>();
    }

    [RelayCommand]
    private void NavigateToCheckOut()
    {
        _navigationService.NavigateTo<CheckOutViewModel>();
    }

    [RelayCommand]
    private void NavigateToRecentEntries()
    {
        _navigationService.NavigateTo<RecentEntriesViewModel>();
    }

    [RelayCommand]
    private void NavigateToAnalytics()
    {
        _navigationService.NavigateTo<AnalyticsViewModel>();
    }

    [RelayCommand]
    private void NavigateToShiftClosure()
    {
        _navigationService.NavigateTo<ShiftClosureViewModel>();
    }

    [RelayCommand]
    private void NavigateToMonthlySubscriptions()
    {
        _navigationService.NavigateTo<MonthlySubscriptionsViewModel>();
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        _backgroundSync.Stop();
        await _authService.LogoutAsync();
        LogoutRequested?.Invoke();
    }
}
