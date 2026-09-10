using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Parking.Core.Enums;
using Parking.Core.Security;
using Parking.Data.Factories;
using Parking.Entities;
using Parking.Models.ApiModels;
using Parking.Services.Contracts;
using Parking.Views;

namespace Parking.ViewModels;

[RequirePermission("shifts.view_current", "Control de Turno y Arqueo")]
public partial class ShiftClosureViewModel : ViewModelBase
{
    private readonly IShiftService _shiftService;
    private readonly IAuthService _authService;
    private readonly IDialogService _dialogService;
    private readonly IReceiptPrinterService _receiptPrinter;
    private readonly IDbConnectionManager _connectionManager;
    private readonly INavigationService _navigationService;
    private readonly IApiClientService _apiClient;
    private readonly ISyncEngineService _syncEngine;
    private readonly ISessionService _sessionService;
    private readonly IPermissionService _permissionService;

    [ObservableProperty]
    private string _branchName = "Sede Principal";

    [ObservableProperty]
    private bool _isOnlineMode = true;

    [ObservableProperty]
    private string _syncStatusText = "Sincronizado";

    [ObservableProperty]
    private ShiftSummaryModel _summary = new();

    [ObservableProperty]
    private ObservableCollection<ShiftPaymentMethodItem> _paymentMethodCards = new();

    [ObservableProperty]
    private decimal _actualCashCounted;

    [ObservableProperty]
    private decimal _cashDifference;

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private bool _hasActiveShift;

    [ObservableProperty]
    private decimal _newShiftBaseAmount = 0m;

    [ObservableProperty]
    private string? _feedbackMessage;

    [ObservableProperty]
    private bool _hasFeedback;

    [ObservableProperty]
    private bool _isSuccessFeedback;

    [ObservableProperty]
    private IReadOnlyList<WorkShift> _shiftHistory = new List<WorkShift>();

    [ObservableProperty]
    private string _operatorName = "Operador General";

    [ObservableProperty]
    private bool _isShiftOwner = true;

    [ObservableProperty]
    private bool _canWithdrawCash;

    [ObservableProperty]
    private bool _canCloseShift;

    [ObservableProperty]
    private bool _canHandoverShift;

    [ObservableProperty]
    private bool _canExportShift;

    [ObservableProperty]
    private bool _canViewShiftHistory;

    [ObservableProperty]
    private bool _canOpenShift;

    [ObservableProperty]
    private string _activeShiftOperatorName = string.Empty;

    [ObservableProperty]
    private DateTime? _activeShiftStartTime;

    [ObservableProperty]
    private ObservableCollection<User> _availableUsers = new();

    [ObservableProperty]
    private User? _selectedHandoverUser;

    [ObservableProperty]
    private bool _hasAvailableHandoverUsers;

    [ObservableProperty]
    private WorkShift? _lastClosedShift;

    [ObservableProperty]
    private bool _hasLastClosedShift;

    [ObservableProperty]
    private IReadOnlyList<CashWithdrawal> _currentShiftWithdrawals = new List<CashWithdrawal>();

    public ShiftClosureViewModel(
        IShiftService shiftService,
        IAuthService authService,
        IDialogService dialogService,
        IReceiptPrinterService receiptPrinter,
        IDbConnectionManager connectionManager,
        INavigationService navigationService,
        ISyncEngineService syncEngine,
        IApiClientService apiClient,
        ISessionService sessionService,
        IPermissionService permissionService)
    {
        _shiftService = shiftService;
        _authService = authService;
        _dialogService = dialogService;
        _receiptPrinter = receiptPrinter;
        _connectionManager = connectionManager;
        _navigationService = navigationService;
        _apiClient = apiClient;
        _syncEngine = syncEngine;
        _sessionService = sessionService;
        _permissionService = permissionService;
        _operatorName = _authService.CurrentUser?.FullName ?? "Operador General";
        _branchName = _sessionService.CurrentBranch?.Name ?? "Sede Principal";
        _isOnlineMode = _syncEngine.IsOnline;
        _syncStatusText = _isOnlineMode ? "Sincronizado" : "Modo Local";

        _permissionService.PermissionsChanged += () =>
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(UpdatePermissions);
        };
        UpdatePermissions();

        _syncEngine.DataSynchronized += () =>
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.InvokeAsync(async () =>
                {
                    try { await LoadShiftDataAsync(isSilent: true); } catch { }
                });
            }
            else
            {
                _ = LoadShiftDataAsync(isSilent: true);
            }
        };

        _shiftService.ShiftStateChanged += () =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(async () =>
            {
                try { await LoadShiftDataAsync(isSilent: true); } catch { }
            });
        };

        _syncEngine.SyncStatusChanged += (s, e) =>
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        IsOnlineMode = _syncEngine.IsOnline;
                        SyncStatusText = IsOnlineMode ? "API Central Online - Sincronizado" : "Modo Local / Desconectado";
                    }
                    catch { }
                });
            }
            else
            {
                IsOnlineMode = _syncEngine.IsOnline;
                SyncStatusText = IsOnlineMode ? "Sincronizado" : "Modo Local";
            }
        };

        _sessionService.ActiveBranchChanged += async branch =>
        {
            BranchName = branch?.Name ?? _sessionService.CurrentBranch?.Name ?? "Sede Principal";
            try { await LoadShiftDataAsync(); } catch { }
        };
    }

    public override async Task InitializeAsync()
    {
        OperatorName = _authService.CurrentUser?.FullName ?? "Operador General";
        BranchName = _sessionService.CurrentBranch?.Name ?? "Sede Principal";
        IsOnlineMode = _syncEngine.IsOnline;
        SyncStatusText = IsOnlineMode ? "Sincronizado" : "Modo Local";
        UpdatePermissions();
        await LoadShiftDataAsync();
    }

    private void UpdatePermissions()
    {
        CanWithdrawCash = _permissionService.HasPermission("shifts.blind_count");
        CanCloseShift = _permissionService.HasPermission("shifts.close");
        CanHandoverShift = _permissionService.HasPermission("shifts.close");
        CanExportShift = _permissionService.HasPermission("shifts.reprint_closure");
        CanViewShiftHistory = _permissionService.HasPermission("shifts.view_history");
        CanOpenShift = _permissionService.HasPermission("shifts.open");
    }

    partial void OnActualCashCountedChanged(decimal value)
    {
        RecalculateDifference();
    }

    private void RecalculateDifference()
    {
        CashDifference = ActualCashCounted - Summary.ExpectedCash;
    }

    [RelayCommand]
    private async Task RefreshSummaryAsync()
    {
        await LoadShiftDataAsync();
    }

    [RelayCommand]
    private async Task OpenCashWithdrawalDialogAsync()
    {
        if (!CanWithdrawCash)
        {
            await _dialogService.ShowAlertAsync(
                "Acceso Denegado",
                "No cuenta con permisos para registrar retiros o sangrías parciales de caja (shift.cash_withdrawal).",
                DialogNotificationType.Warning);
            return;
        }

        if (!HasActiveShift)
        {
            await _dialogService.ShowAlertAsync(
                "Sin Turno Activo",
                "Debe haber un turno operativo abierto para poder registrar retiros o recogidas de efectivo.",
                DialogNotificationType.Warning);
            return;
        }

        var result = await CashWithdrawalDialog.ShowDialogAsync(
            System.Windows.Application.Current.MainWindow,
            _authService,
            _shiftService);

        if (result)
        {
            await LoadShiftDataAsync();
            await _dialogService.ShowAlertAsync(
                "Retiro Registrado con Éxito",
                "Se ha registrado el retiro de efectivo de la gaveta y se ha actualizado el balance esperado de caja.",
                DialogNotificationType.Success);
        }
    }

    [RelayCommand]
    private async Task OpenShiftAsync()
    {
        if (!CanOpenShift)
        {
            await _dialogService.ShowAlertAsync(
                "Acceso Denegado",
                "No cuenta con permisos para abrir nuevos turnos operativos (shift.open).",
                DialogNotificationType.Warning);
            return;
        }

        if (NewShiftBaseAmount <= 0)
        {
            var branchDefault = _sessionService.CurrentBranch?.DefaultInitialCash ?? 0m;
            if (branchDefault > 0)
            {
                NewShiftBaseAmount = branchDefault;
            }
        }

        if (_sessionService.CurrentUser?.RequireInitialCashAmount == true && NewShiftBaseAmount <= 0)
        {
            await _dialogService.ShowAlertAsync(
                "Monto Base Requerido",
                "Para esta empresa es obligatorio ingresar un monto base inicial mayor a cero antes de abrir la caja.",
                DialogNotificationType.Warning);
            return;
        }

        HasFeedback = false;
        IsBusy = true;
        BusyMessage = "Abriendo nuevo turno operativo y registrando base de caja...";

        try
        {
            await _shiftService.OpenShiftAsync(NewShiftBaseAmount, Notes);
            HasFeedback = true;
            IsSuccessFeedback = true;
            FeedbackMessage = $"Turno abierto exitosamente con base de ${NewShiftBaseAmount:N0}.";
            await LoadShiftDataAsync();

            await _dialogService.ShowAlertAsync(
                "Turno Operativo Abierto",
                $"Se ha registrado la apertura del turno con base inicial de ${NewShiftBaseAmount:N0}. Ya puedes iniciar el ingreso de vehículos.",
                DialogNotificationType.Success);

            _navigationService.NavigateTo<CheckInViewModel>();
        }
        catch (Exception ex)
        {
            HasFeedback = true;
            IsSuccessFeedback = false;
            FeedbackMessage = $"Error al abrir turno: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            BusyMessage = null;
        }
    }

    /// <summary>
    /// Modalidad 1: Cierre Definitivo de Turno (Fin de Jornada / Sin Relevo Inmediato)
    /// </summary>
    [RelayCommand]
    private async Task CloseShiftDirectAsync()
    {
        if (!CanCloseShift)
        {
            await _dialogService.ShowAlertAsync(
                "Acceso Denegado",
                "No cuenta con permisos para realizar el cierre definitivo de turnos (shift.close).",
                DialogNotificationType.Warning);
            return;
        }

        HasFeedback = false;
        if (!_shiftService.HasActiveShift && !HasActiveShift)
        {
            HasFeedback = true;
            IsSuccessFeedback = false;
            FeedbackMessage = "No hay ningún turno activo para cerrar.";
            return;
        }

        var confirmed = await _dialogService.ShowConfirmationAsync(
            "Confirmar Cierre de Turno y Fin de Jornada",
            $"¿Deseas cerrar el turno definitivamente y finalizar la jornada?\n\n" +
            $"• Efectivo Contado en Gaveta: ${ActualCashCounted:N0}\n" +
            $"• Efectivo Esperado: ${Summary.ExpectedCash:N0}\n" +
            $"• Diferencia de Arqueo: ${CashDifference:N0}\n\n" +
            $"El sistema quedará en estado cerrado hasta la próxima apertura.",
            DialogNotificationType.Question,
            "Cerrar Turno",
            "Cancelar");

        if (!confirmed) return;

        IsBusy = true;
        BusyMessage = "Cerrando turno operativo y generando comprobante de arqueo...";

        try
        {
            var closedShift = await _shiftService.CloseShiftAsync(ActualCashCounted, Notes, null, null);

            await _dialogService.ShowAlertAsync(
                "Turno Cerrado con Éxito",
                $"El turno ha sido cerrado formalmente.\n\n" +
                $"• Total Arqueo en Gaveta: ${ActualCashCounted:N0}\n" +
                $"• Total Tiquetes Liquidados: {Summary.TotalTicketsProcessed}\n\n" +
                $"La caja ha finalizado su jornada.",
                DialogNotificationType.Success);

            ActualCashCounted = 0m;
            Notes = null;
            await LoadShiftDataAsync();
        }
        catch (Exception ex)
        {
            HasFeedback = true;
            IsSuccessFeedback = false;
            FeedbackMessage = $"Error al cerrar turno: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            BusyMessage = null;
        }
    }

    /// <summary>
    /// Modalidad 2: Entrega de Turno y Relevo en Caliente a Otro Operador
    /// </summary>
    [RelayCommand]
    private async Task HandoverShiftAsync()
    {
        if (!CanHandoverShift)
        {
            await _dialogService.ShowAlertAsync(
                "Acceso Denegado",
                "No cuenta con permisos para realizar la entrega y relevo de turno (shift.handover).",
                DialogNotificationType.Warning);
            return;
        }

        HasFeedback = false;
        if (!_shiftService.HasActiveShift && !HasActiveShift)
        {
            HasFeedback = true;
            IsSuccessFeedback = false;
            FeedbackMessage = "No hay ningún turno activo para cerrar.";
            return;
        }

        if (!HasAvailableHandoverUsers || SelectedHandoverUser == null)
        {
            await _dialogService.ShowAlertAsync(
                "Sin Operadores para Relevo",
                "No existen otros operadores registrados en el sistema para realizar el relevo de turno.\n\n" +
                "Utilice la opción de 'Cerrar Turno (Fin de Jornada)' o registre nuevos usuarios operadores en el módulo de seguridad.",
                DialogNotificationType.Warning);
            return;
        }

        var currentUserId = _authService.CurrentUser?.UserId;
        var currentUsername = _authService.CurrentUser?.Username?.ToLower();
        if (SelectedHandoverUser.UserId == currentUserId || (currentUsername != null && SelectedHandoverUser.Username.ToLower() == currentUsername))
        {
            HasFeedback = true;
            IsSuccessFeedback = false;
            FeedbackMessage = "No puedes entregarte el turno a ti mismo. Selecciona a otro operario receptor.";
            return;
        }

        var cashToHandover = ActualCashCounted > 0 ? ActualCashCounted : Summary.ExpectedCash;

        // Abrir Modal de Recepción y Firma con Contraseña del Operador Receptor
        var authenticatedReceiver = await ShiftHandoverAuthDialog.ShowAuthAsync(
            System.Windows.Application.Current.MainWindow,
            _authService,
            SelectedHandoverUser,
            OperatorName,
            cashToHandover);

        if (authenticatedReceiver == null)
        {
            return; // Cancelado o contraseña inválida
        }

        // Relevo validado con credenciales del operador receptor

        IsBusy = true;
        BusyMessage = $"Entregando caja a {SelectedHandoverUser.FullName} e iniciando nuevo turno...";

        try
        {
            // Cerrar turno saliente y abrir inmediatamente el nuevo turno
            await _shiftService.HandoverAndOpenNextShiftAsync(
                ActualCashCounted,
                Notes,
                SelectedHandoverUser.UserId,
                SelectedHandoverUser.FullName,
                cashToHandover);

            // Cambiar de inmediato la sesión activa al operador entrante
            _authService.SwitchCurrentUser(authenticatedReceiver);

            await _dialogService.ShowAlertAsync(
                "Entrega de Turno Exitosa",
                $"El turno ha sido entregado exitosamente a {SelectedHandoverUser.FullName}.\n" +
                $"El nuevo turno ha quedado abierto con base de ${cashToHandover:N0}.",
                DialogNotificationType.Success);

            ActualCashCounted = 0m;
            Notes = null;
            await LoadShiftDataAsync();

            // Redirigir a la pantalla de entradas con la nueva sesión activa
            _navigationService.NavigateTo<CheckInViewModel>();
        }
        catch (Exception ex)
        {
            HasFeedback = true;
            IsSuccessFeedback = false;
            FeedbackMessage = $"Error al transferir turno: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            BusyMessage = null;
        }
    }

    /// <summary>
    /// Modalidad 3: Toma de Relevo / Asunción de Caja por Operador Entrante
    /// </summary>
    [RelayCommand]
    private async Task TakeOverShiftAsync()
    {
        HasFeedback = false;
        var active = await _shiftService.GetActiveShiftAsync();
        if (active == null)
        {
            HasFeedback = true;
            IsSuccessFeedback = false;
            FeedbackMessage = "No hay ningún turno activo para asumir.";
            return;
        }

        var currentUserId = _authService.CurrentUser?.UserId ?? Guid.NewGuid();
        var currentFullName = _authService.CurrentUser?.FullName ?? "Operador";

        var confirmed = await _dialogService.ShowConfirmationAsync(
            "Confirmar Recepción de Turno y Caja",
            $"¿Deseas asumir el turno y recibir la caja de la terminal?\n\n" +
            $"• Turno Saliente: {ActiveShiftOperatorName}\n" +
            $"• Saldo Esperado en Sistema: ${Summary.ExpectedCash:N0}\n" +
            $"• Efectivo Contado en Gaveta: ${ActualCashCounted:N0}\n" +
            $"• Diferencia de Arqueo: ${CashDifference:N0}\n\n" +
            $"Se cerrará formalmente el turno de '{ActiveShiftOperatorName}' y se abrirá tu nuevo turno a nombre de '{currentFullName}' con base de ${ActualCashCounted:N0}.",
            DialogNotificationType.Question,
            "Recibir Caja e Iniciar",
            "Cancelar");

        if (!confirmed) return;

        IsBusy = true;
        BusyMessage = "Cerrando turno anterior e iniciando tu nuevo turno...";

        try
        {
            var note = string.IsNullOrWhiteSpace(Notes)
                ? $"Relevo asumido por {currentFullName}. Base recibida: ${ActualCashCounted:N0}"
                : $"{Notes} (Relevo asumido por {currentFullName})";

            await _shiftService.HandoverAndOpenNextShiftAsync(
                ActualCashCounted,
                note,
                currentUserId,
                currentFullName,
                ActualCashCounted);

            await _dialogService.ShowAlertAsync(
                "Turno Asumido con Éxito",
                $"Has recibido la caja correctamente.\n\n" +
                $"• Base Inicial de tu Turno: ${ActualCashCounted:N0}\n" +
                $"• Operador a Cargo: {currentFullName}\n\n" +
                $"Ya puedes comenzar a registrar ingresos y cobros en el parqueadero.",
                DialogNotificationType.Success);

            ActualCashCounted = 0m;
            Notes = null;
            await LoadShiftDataAsync();

            _navigationService.NavigateTo<CheckInViewModel>();
        }
        catch (Exception ex)
        {
            HasFeedback = true;
            IsSuccessFeedback = false;
            FeedbackMessage = $"Error al asumir turno: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            BusyMessage = null;
        }
    }

    private async Task LoadShiftDataAsync(bool isSilent = false)
    {
        if (!isSilent)
        {
            IsBusy = true;
            BusyMessage = "Consultando balance y arqueo de caja...";
        }

        try
        {
            var currentUserId = _authService.CurrentUser?.UserId;
            var currentFullName = _authService.CurrentUser?.FullName ?? string.Empty;
            var currentUsername = _authService.CurrentUser?.Username?.ToLower() ?? string.Empty;
            var isAdmin = _authService.CurrentUser != null && _authService.CurrentUser.IsAdmin;

            var active = await _shiftService.GetActiveShiftAsync();
            HasActiveShift = active != null;

            if (HasActiveShift)
            {
                ActiveShiftOperatorName = active!.OperatorName ?? "Operador Anterior";
                ActiveShiftStartTime = active.StartTimeUtc.ToLocalTime();

                IsShiftOwner = isAdmin ||
                               (active.UserId > 0 && _authService.CurrentUser?.ServerUserId.HasValue == true && active.UserId == _authService.CurrentUser.ServerUserId.Value) ||
                               string.Equals(active.OperatorName, currentFullName, StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(active.OperatorName, currentUsername, StringComparison.OrdinalIgnoreCase);

                Summary = await _shiftService.GetCurrentShiftSummaryAsync();
                ActualCashCounted = Summary.ExpectedCash;
                RecalculateDifference();
                CurrentShiftWithdrawals = await _shiftService.GetShiftCashWithdrawalsAsync(active!.ShiftId);

                // Poblar dinámicamente las tarjetas con los medios de pago reales de la sede
                PaymentMethodCards.Clear();
                foreach (var pm in Summary.PaymentMethodsBreakdown)
                {
                    PaymentMethodCards.Add(pm);
                }

                // Agregar la tarjeta de Descuentos por Convenios para completar la cuadrícula oficial
                PaymentMethodCards.Add(new ShiftPaymentMethodItem
                {
                    PaymentMethodId = -1,
                    Name = "Descuentos por Convenios",
                    IconKey = "IconDiscount",
                    IconBg = "#FFF8E1",
                    IconBrushKey = "BrushWarning",
                    AmountBrushKey = "BrushWarningText",
                    TotalCollected = Summary.TotalDiscounts,
                    TransactionCount = 0,
                    Subtitle = "Deducciones por convenios",
                    RequiresCashTender = false
                });
            }
            else
            {
                IsShiftOwner = true;
                ActiveShiftOperatorName = string.Empty;
                ActiveShiftStartTime = null;
                CurrentShiftWithdrawals = new List<CashWithdrawal>();
                LastClosedShift = await _shiftService.GetLastClosedShiftAsync();
                HasLastClosedShift = LastClosedShift != null;
                using var dbCheck = _connectionManager.CreateDbContext();
                var currentBranchId = _sessionService.CurrentBranch?.Id;
                var localBranch = currentBranchId.HasValue
                    ? await dbCheck.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == currentBranchId.Value)
                    : null;

                var configuredBranchBase = (localBranch != null && localBranch.DefaultInitialCash > 0)
                    ? localBranch.DefaultInitialCash
                    : (_sessionService.CurrentBranch?.DefaultInitialCash ?? 0m);

                if (configuredBranchBase > 0)
                {
                    NewShiftBaseAmount = configuredBranchBase;
                    if (_sessionService.CurrentBranch != null && (_sessionService.CurrentBranch.DefaultInitialCash == null || _sessionService.CurrentBranch.DefaultInitialCash == 0))
                    {
                        _sessionService.UpdateCurrentBranch(b => b.DefaultInitialCash = configuredBranchBase);
                    }
                }
                else if (HasLastClosedShift)
                {
                    NewShiftBaseAmount = LastClosedShift!.ActualCashCounted;
                }
                else
                {
                    NewShiftBaseAmount = 0m;
                }

                // Cargar medios de pago reales inactivos para proyectar estructura limpia
                PaymentMethodCards.Clear();
                var branchPmIds = currentBranchId.HasValue
                    ? await dbCheck.BranchPaymentMethods
                        .Where(bpm => bpm.BranchId == currentBranchId.Value && bpm.IsActive)
                        .Select(bpm => bpm.PaymentMethodId)
                        .ToListAsync()
                    : new List<int>();

                var inactiveMethods = await dbCheck.PaymentMethods
                    .Where(pm => pm.State && (branchPmIds.Count == 0 || branchPmIds.Contains(pm.Id)))
                    .ToListAsync();

                if (inactiveMethods.Count == 0)
                {
                    inactiveMethods = await dbCheck.PaymentMethods.Where(pm => pm.State).ToListAsync();
                }

                foreach (var pm in inactiveMethods)
                {
                    var isCash = pm.RequiresCashTender || pm.Name.ToLowerInvariant().Contains("efectivo");
                    var isTransfer = pm.Name.ToLowerInvariant().Contains("nequi") || pm.Name.ToLowerInvariant().Contains("transfer") || pm.Name.ToLowerInvariant().Contains("qr") || pm.Name.ToLowerInvariant().Contains("davi");
                    var isCard = pm.Name.ToLowerInvariant().Contains("tarjeta") || pm.Name.ToLowerInvariant().Contains("card") || pm.Name.ToLowerInvariant().Contains("credito") || pm.Name.ToLowerInvariant().Contains("debito");

                    PaymentMethodCards.Add(new ShiftPaymentMethodItem
                    {
                        PaymentMethodId = pm.Id,
                        Name = pm.Name,
                        IconKey = isCash ? "IconCash" : (isTransfer ? "IconQr" : (isCard ? "IconCard" : "IconCash")),
                        IconBg = isCash ? "#E0F2F1" : (isTransfer ? "#E0F7FA" : (isCard ? "#E0F2F1" : "#F1F5F9")),
                        IconBrushKey = isCash ? "BrushPrimary" : (isTransfer ? "BrushCyan" : (isCard ? "BrushPrimary" : "BrushTextSecondary")),
                        AmountBrushKey = isCash ? "BrushPrimary" : (isTransfer ? "BrushCyan" : (isCard ? "BrushPrimary" : "BrushTextPrimary")),
                        TotalCollected = 0m,
                        TransactionCount = 0,
                        Subtitle = "Sin turno activo",
                        RequiresCashTender = isCash
                    });
                }

                PaymentMethodCards.Add(new ShiftPaymentMethodItem
                {
                    PaymentMethodId = -1,
                    Name = "Descuentos por Convenios",
                    IconKey = "IconDiscount",
                    IconBg = "#FFF8E1",
                    IconBrushKey = "BrushWarning",
                    AmountBrushKey = "BrushWarningText",
                    TotalCollected = 0m,
                    TransactionCount = 0,
                    Subtitle = "Deducciones por convenios",
                    RequiresCashTender = false
                });
            }

            // Cargar usuarios reales asignados a la sede activa con rol operativo para entrega de turno
            using var db = _connectionManager.CreateDbContext();
            var currentBranch = _sessionService.CurrentBranch;

            List<User> branchUsers = new();

            if (currentBranch != null && currentBranch.Id > 0)
            {
                try
                {
                    var apiUsers = await _apiClient.GetBranchUsersAsync(currentBranch.Id);
                    if (apiUsers != null && apiUsers.Count > 0)
                    {
                        var usernames = apiUsers.Select(u => u.Username.ToLower()).ToHashSet();
                        branchUsers = await db.Users
                            .Include(u => u.Role)
                            .AsNoTracking()
                            .Where(u => u.IsActive && (usernames.Contains(u.Username.ToLower()) || (u.Email != null && usernames.Contains(u.Email.ToLower()))))
                            .OrderBy(u => u.FullName)
                            .ToListAsync();

                        // Si hay usuarios asignados a la sede en el API que aún no están en la BD local SQLite, agregarlos a la lista
                        var existingUsernames = branchUsers.Select(u => u.Username.ToLower()).ToHashSet();
                        foreach (var apiUser in apiUsers)
                        {
                            if (!existingUsernames.Contains(apiUser.Username.ToLower()))
                            {
                                branchUsers.Add(new User
                                {
                                    UserId = Guid.NewGuid(),
                                    Username = apiUser.Username,
                                    FullName = !string.IsNullOrWhiteSpace(apiUser.FullName) ? apiUser.FullName : apiUser.Username,
                                    Email = apiUser.Email,
                                    IsActive = true,
                                    Role = new Role { Name = "Operador" }
                                });
                            }
                        }
                    }
                }
                catch { }
            }

            if (branchUsers.Count == 0)
            {
                branchUsers = await db.Users
                    .Include(u => u.Role)
                    .AsNoTracking()
                    .Where(u => u.IsActive)
                    .OrderBy(u => u.FullName)
                    .ToListAsync();
            }

            AvailableUsers.Clear();
            foreach (var u in branchUsers.OrderBy(u => u.FullName))
            {
                // Excluir estrictamente al usuario actual en sesión
                if (u.UserId == currentUserId || (currentUsername != null && u.Username.Equals(currentUsername, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                AvailableUsers.Add(u);
            }

            HasAvailableHandoverUsers = AvailableUsers.Count > 0;
            SelectedHandoverUser = AvailableUsers.FirstOrDefault();

            ShiftHistory = await _shiftService.GetShiftHistoryAsync(DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            HasFeedback = true;
            IsSuccessFeedback = false;
            FeedbackMessage = $"Error al cargar balance: {ex.Message}";
        }
        finally
        {
            if (!isSilent)
            {
                IsBusy = false;
                BusyMessage = null;
            }
        }
    }
}
