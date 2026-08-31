using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Parking.Core.Enums;
using Parking.Models;
using Parking.Services.Contracts;
using Parking.Views;

namespace Parking.ViewModels;

public partial class MainShellViewModel : ViewModelBase
{
    private static readonly CultureInfo SpanishCulture = new("es-ES");
    private readonly IAuthService _authService;
    private readonly ISessionService _sessionService;
    private readonly IPermissionService _permissionService;
    private readonly IParkingTicketService _ticketService;
    private readonly INavigationService _navigationService;
    private readonly IApiClientService _apiClient;
    private readonly ISyncEngineService _syncEngine;
    private readonly IBackgroundSyncScheduler _backgroundSync;
    private readonly IDialogService _dialogService;
    private readonly IShiftService _shiftService;
    private readonly ISignalRClientService _signalRClient;
    private readonly DispatcherTimer _clockTimer;
    private bool _isSyncPromptOpen;

    [ObservableProperty]
    private ViewModelBase? _activeView;

    [ObservableProperty]
    private UserSessionModel? _currentUser;

    [ObservableProperty]
    private BranchModel? _currentBranch;

    [ObservableProperty]
    private bool _hasMultipleBranches;

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

    public event Action? LogoutRequested;

    public MainShellViewModel(
        IAuthService authService,
        ISessionService sessionService,
        IPermissionService permissionService,
        IParkingTicketService ticketService,
        INavigationService navigationService,
        IApiClientService apiClient,
        ISyncEngineService syncEngine,
        IBackgroundSyncScheduler backgroundSync,
        IDialogService dialogService,
        IShiftService shiftService,
        ISignalRClientService signalRClient)
    {
        _authService = authService;
        _sessionService = sessionService;
        _permissionService = permissionService;
        _ticketService = ticketService;
        _navigationService = navigationService;
        _apiClient = apiClient;
        _syncEngine = syncEngine;
        _backgroundSync = backgroundSync;
        _dialogService = dialogService;
        _shiftService = shiftService;
        _signalRClient = signalRClient;

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

        _sessionService.UserSessionChanged += user =>
        {
            CurrentUser = user;
        };

        _sessionService.ActiveBranchChanged += branch =>
        {
            CurrentBranch = branch;
            HasMultipleBranches = _sessionService.HasMultipleBranches;
            if (branch != null)
            {
                _ = _signalRClient.SetCurrentBranchAsync(branch.Id);
            }
        };

        _shiftService.ShiftStateChanged += () =>
        {
            _ = RefreshOccupancyAsync();
        };

        _signalRClient.ConfigUpdateRequired += notification =>
        {
            Application.Current?.Dispatcher.InvokeAsync(async () =>
            {
                await HandleRealtimeNotificationAsync(notification);
            });
        };

        _apiClient.SessionTerminated += reason =>
        {
            Application.Current?.Dispatcher.InvokeAsync(async () =>
            {
                await HandleConcurrentSessionTerminatedAsync(reason);
            });
        };

        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (s, e) => UpdateClock();
        _clockTimer.Start();

        UpdateClock();
    }

    private bool _isTerminatingSession;

    private async Task HandleConcurrentSessionTerminatedAsync(string? message)
    {
        if (_isTerminatingSession) return;
        _isTerminatingSession = true;

        try
        {
            _clockTimer.Stop();
            await _dialogService.ShowAlertAsync(
                "Sesión Cerrada en Otro Dispositivo",
                string.IsNullOrWhiteSpace(message)
                    ? "Se ha detectado un nuevo inicio de sesión con este usuario desde otra estación de trabajo o dispositivo. Por seguridad, esta sesión se cerrará automáticamente."
                    : message,
                DialogNotificationType.Warning);

            _sessionService.Clear();
            _apiClient.ClearAuthToken();
            LogoutRequested?.Invoke();
        }
        catch
        {
            _sessionService.Clear();
            _apiClient.ClearAuthToken();
            LogoutRequested?.Invoke();
        }
    }

    private async Task HandleRealtimeNotificationAsync(Models.ApiModels.ConfigNotificationDto notification)
    {
        // 0. Manejo reactivo de terminación forzada por inicio de sesión concurrente (estrictamente para el mismo usuario)
        if (notification.EventType == "UserSessionTerminated")
        {
            var currentUser = _sessionService.CurrentUser;
            if (currentUser != null && notification.UserId.HasValue && currentUser.ServerUserId == notification.UserId.Value)
            {
                await HandleConcurrentSessionTerminatedAsync(notification.Message);
            }
            return;
        }

        // 1. Manejo reactivo de cambios de permisos y roles en tiempo real
        if (notification.EventType == "PermissionsChanged" || notification.EventType == "RolesChanged")
        {
            var user = _sessionService.CurrentUser;
            if (user != null)
            {
                var roleId = user.ServerRoleId ?? (user.IsAdmin ? 1 : 2);
                try
                {
                    var updatedPermissions = await _apiClient.GetRolePermissionsAsync(roleId);
                    if (updatedPermissions != null && updatedPermissions.Count > 0)
                    {
                        user.GrantedPermissions = new HashSet<string>(updatedPermissions, StringComparer.OrdinalIgnoreCase);
                        _permissionService.LoadPermissions(updatedPermissions, user.IsAdmin);
                        SyncStatusText = $"Permisos actualizados en tiempo real ({DateTime.Now:HH:mm})";
                    }
                }
                catch { }
            }
            return;
        }

        if (_isSyncPromptOpen) return;

        // Validar si aplica a la sede activa o es global
        var currentBranchId = _sessionService.CurrentBranch?.Id;
        if (notification.BranchId.HasValue && currentBranchId.HasValue && notification.BranchId.Value != currentBranchId.Value)
        {
            return;
        }

        try
        {
            _isSyncPromptOpen = true;
            await _dialogService.ShowSyncRequiredModalAsync(notification, _syncEngine);
            await RefreshOccupancyAsync();
        }
        catch (Exception)
        {
        }
        finally
        {
            _isSyncPromptOpen = false;
        }
    }

    public override async Task InitializeAsync()
    {
        CurrentUser = _sessionService.CurrentUser;
        CurrentBranch = _sessionService.CurrentBranch;
        HasMultipleBranches = _sessionService.HasMultipleBranches;

        _ = _signalRClient.StartAsync();
        if (CurrentBranch != null)
        {
            _ = _signalRClient.SetCurrentBranchAsync(CurrentBranch.Id);
        }

        IsOnlineMode = _syncEngine.IsOnline;
        SyncStatusText = _syncEngine.SyncStatusDescription;

        await RefreshOccupancyAsync();

        var activeShift = await _shiftService.GetActiveShiftAsync();
        if (activeShift == null)
        {
            NavigateToShiftClosure();
            _ = _dialogService.ShowAlertAsync(
                "Apertura de Turno Requerida",
                "No hay un turno operativo abierto. Debe ingresar la base inicial de caja y abrir el turno antes de operar en la terminal.",
                DialogNotificationType.Warning);
        }
        else
        {
            var isCurrentShiftOwner = CurrentUser != null && (
                string.Equals(activeShift.OperatorName, CurrentUser.FullName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(activeShift.OperatorName, CurrentUser.Username, StringComparison.OrdinalIgnoreCase));

            var isAdmin = CurrentUser != null && CurrentUser.IsAdmin;

            if (!isCurrentShiftOwner && !isAdmin)
            {
                NavigateToShiftClosure();
                _ = _dialogService.ShowAlertAsync(
                    "Turno Activo a Nombre de Otro Operador",
                    $"Existe un turno operativo abierto a nombre de '{activeShift.OperatorName}'.\n\n" +
                    $"Para operar la terminal con su usuario ('{CurrentUser?.FullName}'), debe solicitar la Entrega / Relevo de Turno o el Cierre de Caja anterior.",
                    DialogNotificationType.Warning);
            }
            else
            {
                NavigateToCheckIn();
            }
        }
    }

    [RelayCommand]
    private async Task RefreshOccupancyAsync()
    {
        Occupancy = await _ticketService.GetOccupancyStatsAsync();
    }

    [RelayCommand]
    private void SwitchBranch()
    {
        var branches = _sessionService.UserBranches;
        if (branches.Count <= 1) return;

        var dialog = new BranchSelectionDialog(branches)
        {
            Owner = Application.Current?.MainWindow
        };

        var result = dialog.ShowDialog();
        if (result == true && dialog.SelectedBranch != null)
        {
            _sessionService.SetActiveBranch(dialog.SelectedBranch);
            _ = RefreshOccupancyAsync();
            _ = _dialogService.ShowAlertAsync("Sede Actualizada", $"Sede activa cambiada a '{dialog.SelectedBranch.Name}'", DialogNotificationType.Information);
        }
    }

    [RelayCommand]
    private async Task ForceSyncAsync()
    {
        if (IsSyncing) return;

        IsSyncing = true;
        SyncStatusText = "Sincronizando con API Central...";

        try
        {
            var success = await _dialogService.ShowSyncProgressModalAsync(_syncEngine);
            await RefreshOccupancyAsync();
            SyncStatusText = success ? "Sincronización completada" : "Sincronización finalizada con advertencias";
        }
        catch (Exception ex)
        {
            SyncStatusText = $"Error de sincronización: {ex.Message}";
        }
        finally
        {
            IsSyncing = false;
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        var confirmed = await _dialogService.ShowConfirmationAsync(
            "Cerrar Sesión",
            "¿Está seguro de que desea cerrar la sesión actual de la terminal?");

        if (confirmed)
        {
            _clockTimer.Stop();
            _sessionService.Clear();
            LogoutRequested?.Invoke();
        }
    }

    private bool ValidateShiftAccess(out string? errorMessage)
    {
        errorMessage = null;
        if (!_shiftService.HasActiveShift)
        {
            errorMessage = "Debes abrir un turno operativo e indicar la base inicial de caja antes de registrar movimientos.";
            return false;
        }

        var activeShift = _shiftService.CurrentShift;
        if (activeShift == null) return true;

        var isCurrentShiftOwner = CurrentUser != null && (
            string.Equals(activeShift.OperatorName, CurrentUser.FullName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(activeShift.OperatorName, CurrentUser.Username, StringComparison.OrdinalIgnoreCase));

        var isAdmin = CurrentUser != null && CurrentUser.IsAdmin;

        if (!isCurrentShiftOwner && !isAdmin)
        {
            errorMessage = $"La caja actual está a nombre de '{activeShift.OperatorName}'. Debe realizarse la Entrega / Relevo de Turno para operar con su cuenta.";
            return false;
        }

        return true;
    }

    [RelayCommand]
    private void NavigateToCheckIn()
    {
        if (!_permissionService.HasPermission("checkin.create_ticket"))
        {
            _ = _dialogService.ShowAlertAsync("Acceso Denegado", "No tienes permisos para acceder al módulo de Ingreso Vehicular.", DialogNotificationType.Warning);
            return;
        }
        if (!ValidateShiftAccess(out var error))
        {
            _ = _dialogService.ShowAlertAsync("Apertura de Turno Requerida", error ?? "Turno no disponible", DialogNotificationType.Warning);
            NavigateToShiftClosure();
            return;
        }
        _navigationService.NavigateTo<CheckInViewModel>();
    }

    [RelayCommand]
    private void NavigateToCheckOut()
    {
        if (!_permissionService.HasPermission("checkout.process_payment"))
        {
            _ = _dialogService.ShowAlertAsync("Acceso Denegado", "No tienes permisos para acceder al módulo de Salida y Cobro.", DialogNotificationType.Warning);
            return;
        }
        if (!ValidateShiftAccess(out var error))
        {
            _ = _dialogService.ShowAlertAsync("Apertura de Turno Requerida", error ?? "Turno no disponible", DialogNotificationType.Warning);
            NavigateToShiftClosure();
            return;
        }
        _navigationService.NavigateTo<CheckOutViewModel>();
    }

    [RelayCommand]
    private void NavigateToMonthlySubscriptions()
    {
        if (!_permissionService.HasPermission("subscriptions.view_list"))
        {
            _ = _dialogService.ShowAlertAsync("Acceso Denegado", "No tienes permisos para acceder al módulo de Mensualidades.", DialogNotificationType.Warning);
            return;
        }
        if (!ValidateShiftAccess(out var error))
        {
            _ = _dialogService.ShowAlertAsync("Apertura de Turno Requerida", error ?? "Turno no disponible", DialogNotificationType.Warning);
            NavigateToShiftClosure();
            return;
        }
        _navigationService.NavigateTo<MonthlySubscriptionsViewModel>();
    }

    [RelayCommand]
    private void NavigateToRecentEntries()
    {
        if (!_permissionService.HasPermission("monitoring.view_occupancy"))
        {
            _ = _dialogService.ShowAlertAsync("Acceso Denegado", "No tienes permisos para acceder al módulo de Patio / Vehículos Recientes.", DialogNotificationType.Warning);
            return;
        }
        _navigationService.NavigateTo<RecentEntriesViewModel>();
    }

    [RelayCommand]
    private void NavigateToAnalytics()
    {
        if (!_permissionService.HasPermission("analytics.view_dashboard"))
        {
            _ = _dialogService.ShowAlertAsync("Acceso Denegado", "No tienes permisos para acceder al módulo de Analítica y Finanzas.", DialogNotificationType.Warning);
            return;
        }
        _navigationService.NavigateTo<AnalyticsViewModel>();
    }

    [RelayCommand]
    private void NavigateToShiftClosure()
    {
        if (!_permissionService.HasPermission("shifts.view_current"))
        {
            _ = _dialogService.ShowAlertAsync("Acceso Denegado", "No tienes permisos para acceder al módulo de Control de Turnos y Caja.", DialogNotificationType.Warning);
            return;
        }
        _navigationService.NavigateTo<ShiftClosureViewModel>();
    }

    private void UpdateSelectedNavSection(ViewModelBase viewModel)
    {
        SelectedNavSection = viewModel switch
        {
            CheckInViewModel => "CheckIn",
            CheckOutViewModel => "CheckOut",
            MonthlySubscriptionsViewModel => "Subscriptions",
            RecentEntriesViewModel => "RecentEntries",
            AnalyticsViewModel => "Analytics",
            ShiftClosureViewModel => "ShiftClosure",
            _ => string.Empty
        };
    }

    private void UpdateClock()
    {
        CurrentTimeString = DateTime.Now.ToString("dddd, dd MMMM yyyy  •  HH:mm:ss", SpanishCulture);
    }
}
