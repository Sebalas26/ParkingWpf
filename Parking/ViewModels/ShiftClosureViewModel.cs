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

[RequirePermission("shift.view", "Control de Turno y Arqueo")]
public partial class ShiftClosureViewModel : ViewModelBase
{
    private readonly IShiftService _shiftService;
    private readonly IAuthService _authService;
    private readonly IDialogService _dialogService;
    private readonly IReceiptPrinterService _receiptPrinter;
    private readonly IDbConnectionManager _connectionManager;
    private readonly INavigationService _navigationService;
    private readonly IApiClientService _apiClient;
    private readonly ISessionService _sessionService;

    [ObservableProperty]
    private ShiftSummaryModel _summary = new();

    [ObservableProperty]
    private decimal _actualCashCounted;

    [ObservableProperty]
    private decimal _cashDifference;

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private bool _hasActiveShift;

    [ObservableProperty]
    private decimal _newShiftBaseAmount = 50000m;

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
        ISessionService sessionService)
    {
        _shiftService = shiftService;
        _authService = authService;
        _dialogService = dialogService;
        _receiptPrinter = receiptPrinter;
        _connectionManager = connectionManager;
        _navigationService = navigationService;
        _apiClient = apiClient;
        _sessionService = sessionService;
        _operatorName = _authService.CurrentUser?.FullName ?? "Operador General";

        syncEngine.DataSynchronized += async () =>
        {
            await LoadShiftDataAsync();
        };

        _sessionService.ActiveBranchChanged += async _ =>
        {
            await LoadShiftDataAsync();
        };
    }

    public override async Task InitializeAsync()
    {
        OperatorName = _authService.CurrentUser?.FullName ?? "Operador General";
        await LoadShiftDataAsync();
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

        var receiverRole = authenticatedReceiver.RoleName?.ToLowerInvariant() ?? string.Empty;
        var hasValidRole = receiverRole.Contains("operador") ||
                           receiverRole.Contains("administrador") ||
                           receiverRole.Contains("admin") ||
                           receiverRole.Contains("cajero") ||
                           receiverRole.Contains("operator");

        if (!hasValidRole)
        {
            await _dialogService.ShowAlertAsync(
                "Usuario Sin Permisos Operativos",
                $"El usuario '{SelectedHandoverUser.FullName}' no cuenta con un rol con permisos para operar la terminal de parqueadero.\n\n" +
                $"Por favor asigne un rol de Operador o Administrador en la gestión de usuarios antes de transferirle la caja.",
                DialogNotificationType.Warning);
            return;
        }

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

    private async Task LoadShiftDataAsync()
    {
        IsBusy = true;
        BusyMessage = "Consultando balance y arqueo de caja...";

        try
        {
            var active = await _shiftService.GetActiveShiftAsync();
            HasActiveShift = active != null;

            if (HasActiveShift)
            {
                Summary = await _shiftService.GetCurrentShiftSummaryAsync();
                RecalculateDifference();
                CurrentShiftWithdrawals = await _shiftService.GetShiftCashWithdrawalsAsync(active!.ShiftId);
            }
            else
            {
                CurrentShiftWithdrawals = new List<CashWithdrawal>();
                LastClosedShift = await _shiftService.GetLastClosedShiftAsync();
                HasLastClosedShift = LastClosedShift != null;
                if (HasLastClosedShift)
                {
                    NewShiftBaseAmount = LastClosedShift!.ActualCashCounted;
                }
            }

            // Cargar usuarios reales asignados a la sede activa con rol operativo para entrega de turno
            using var db = _connectionManager.CreateDbContext();
            var currentUserId = _authService.CurrentUser?.UserId;
            var currentUsername = _authService.CurrentUser?.Username?.ToLower();
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
            foreach (var u in branchUsers)
            {
                // Excluir estrictamente al usuario actual en sesión
                if (u.UserId == currentUserId || (currentUsername != null && u.Username.ToLower() == currentUsername))
                {
                    continue;
                }

                // Filtrar únicamente usuarios que tengan un rol operativo válido o de administración
                var roleName = u.Role?.Name?.ToLowerInvariant() ?? string.Empty;
                var isAllowedRole = roleName.Contains("operador") ||
                                    roleName.Contains("administrador") ||
                                    roleName.Contains("admin") ||
                                    roleName.Contains("cajero") ||
                                    roleName.Contains("operator");

                if (isAllowedRole)
                {
                    AvailableUsers.Add(u);
                }
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
            IsBusy = false;
            BusyMessage = null;
        }
    }
}
