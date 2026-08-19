using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Parking.Core.Constants;
using Parking.Core.Enums;
using Parking.Data.Factories;
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
    private readonly IPermissionService _permissionService;
    private readonly ISessionHeartbeatService _heartbeatService;
    private readonly IDbConnectionManager _connectionManager;
    private readonly IThemeService _themeService;
    private readonly IDialogService _dialogService;
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
    private string _databaseStatusText = string.Empty;

    [ObservableProperty]
    private AppTheme _currentAppTheme = AppTheme.MidnightDark;

    [ObservableProperty]
    private ThemeInfo? _selectedTheme;

    [ObservableProperty]
    private bool _canViewCheckIn;

    [ObservableProperty]
    private bool _canViewCheckOut;

    [ObservableProperty]
    private bool _canViewAnalytics;

    [ObservableProperty]
    private bool _canViewStores;

    [ObservableProperty]
    private bool _canViewAgreements;

    [ObservableProperty]
    private bool _canViewRates;

    [ObservableProperty]
    private bool _canViewSecurity;

    public IReadOnlyList<ThemeInfo> AvailableThemes => _themeService.GetAvailableThemes();

    public event Action? LogoutRequested;

    public MainShellViewModel(
        IAuthService authService,
        IParkingTicketService ticketService,
        INavigationService navigationService,
        IPermissionService permissionService,
        ISessionHeartbeatService heartbeatService,
        IDbConnectionManager connectionManager,
        IThemeService themeService,
        IDialogService dialogService)
    {
        _authService = authService;
        _ticketService = ticketService;
        _navigationService = navigationService;
        _permissionService = permissionService;
        _heartbeatService = heartbeatService;
        _connectionManager = connectionManager;
        _themeService = themeService;
        _dialogService = dialogService;

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

        _connectionManager.ConnectionStateChanged += (s, isOnline) =>
        {
            IsOnlineMode = isOnline;
            DatabaseStatusText = _connectionManager.StatusDescription;
        };

        _themeService.ThemeChanged += (s, theme) =>
        {
            CurrentAppTheme = theme;
            SelectedTheme = AvailableThemes.FirstOrDefault(t => t.Theme == theme);
        };

        _heartbeatService.SessionRevoked += async (s, message) =>
        {
            await _dialogService.ShowAlertAsync("Sesión Finalizada", message);
            LogoutRequested?.Invoke();
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
        IsOnlineMode = _connectionManager.IsOnlineMode;
        DatabaseStatusText = _connectionManager.StatusDescription;

        Occupancy = await _ticketService.GetOccupancyStatsAsync();

        if (CurrentUser != null)
        {
            var accessibleModules = await _permissionService.GetAccessibleModulesAsync(CurrentUser.UserId);
            var moduleKeys = accessibleModules.Select(m => m.ModuleKey).ToHashSet(StringComparer.OrdinalIgnoreCase);

            CanViewCheckIn = CurrentUser.IsAdmin || moduleKeys.Contains(ModuleKeys.CheckIn);
            CanViewCheckOut = CurrentUser.IsAdmin || moduleKeys.Contains(ModuleKeys.CheckOut);
            CanViewAnalytics = CurrentUser.IsAdmin || moduleKeys.Contains(ModuleKeys.Analytics);
            CanViewStores = CurrentUser.IsAdmin || moduleKeys.Contains(ModuleKeys.Stores);
            CanViewAgreements = CurrentUser.IsAdmin || moduleKeys.Contains(ModuleKeys.Agreements);
            CanViewRates = CurrentUser.IsAdmin || moduleKeys.Contains(ModuleKeys.Rates);
            CanViewSecurity = CurrentUser.IsAdmin || moduleKeys.Contains(ModuleKeys.Security);
        }
        else
        {
            CanViewCheckIn = true;
            CanViewCheckOut = true;
            CanViewAnalytics = true;
            CanViewStores = true;
            CanViewAgreements = true;
            CanViewRates = true;
            CanViewSecurity = true;
        }

        _heartbeatService.StartMonitoring();

        if (CanViewCheckIn)
        {
            NavigateToCheckIn();
        }
        else if (CanViewCheckOut)
        {
            NavigateToCheckOut();
        }
        else
        {
            NavigateToAnalytics();
        }
    }

    private void UpdateClock()
    {
        CurrentTimeString = DateTime.Now.ToString("dddd, dd 'de' MMMM 'de' yyyy • HH:mm:ss", SpanishCulture);
    }

    private void UpdateSelectedNavSection(ViewModelBase vm)
    {
        SelectedNavSection = vm switch
        {
            CheckInViewModel => "CheckIn",
            CheckOutViewModel => "CheckOut",
            AnalyticsViewModel => "Analytics",
            StoreSettingsViewModel => "Stores",
            AgreementSettingsViewModel => "Agreements",
            SettingsViewModel => "Rates",
            SecuritySettingsViewModel => "Security",
            _ => "CheckIn"
        };
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
    private void NavigateToAnalytics()
    {
        _navigationService.NavigateTo<AnalyticsViewModel>();
    }

    [RelayCommand]
    private void NavigateToStores()
    {
        _navigationService.NavigateTo<StoreSettingsViewModel>();
    }

    [RelayCommand]
    private void NavigateToAgreements()
    {
        _navigationService.NavigateTo<AgreementSettingsViewModel>();
    }

    [RelayCommand]
    private void NavigateToSettings()
    {
        _navigationService.NavigateTo<SettingsViewModel>();
    }

    [RelayCommand]
    private void NavigateToSecurity()
    {
        _navigationService.NavigateTo<SecuritySettingsViewModel>();
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        _heartbeatService.StopMonitoring();
        await _authService.LogoutAsync();
        LogoutRequested?.Invoke();
    }
}
