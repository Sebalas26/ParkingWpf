using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Parking.Core.Enums;
using Parking.Data.Factories;
using Parking.Entities;
using Parking.Models.ApiModels;
using Parking.Services.Contracts;
using Parking.Views;

namespace Parking.ViewModels;

public partial class ShiftClosureViewModel : ViewModelBase
{
    private readonly IShiftService _shiftService;
    private readonly IAuthService _authService;
    private readonly IDialogService _dialogService;
    private readonly IReceiptPrinterService _receiptPrinter;
    private readonly IDbConnectionManager _connectionManager;
    private readonly INavigationService _navigationService;

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
    private IReadOnlyList<CashWithdrawal> _currentShiftWithdrawals = new List<CashWithdrawal>();

    public ShiftClosureViewModel(
        IShiftService shiftService,
        IAuthService authService,
        IDialogService dialogService,
        IReceiptPrinterService receiptPrinter,
        IDbConnectionManager connectionManager,
        INavigationService navigationService)
    {
        _shiftService = shiftService;
        _authService = authService;
        _dialogService = dialogService;
        _receiptPrinter = receiptPrinter;
        _connectionManager = connectionManager;
        _navigationService = navigationService;
        _operatorName = _authService.CurrentUser?.FullName ?? "Operador General";
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

    [RelayCommand]
    private async Task CloseShiftAsync()
    {
        HasFeedback = false;
        if (!_shiftService.HasActiveShift && !HasActiveShift)
        {
            HasFeedback = true;
            IsSuccessFeedback = false;
            FeedbackMessage = "No hay ningún turno activo para cerrar.";
            return;
        }

        if (SelectedHandoverUser == null)
        {
            HasFeedback = true;
            IsSuccessFeedback = false;
            FeedbackMessage = "Debe seleccionar el operador receptor al que se le realiza la entrega de turno.";
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
            }

            // Cargar usuarios para entrega de turno
            using var db = _connectionManager.CreateDbContext();
            var currentUserId = _authService.CurrentUser?.UserId;
            var currentUsername = _authService.CurrentUser?.Username?.ToLower();

            var users = await db.Users
                .Where(u => u.IsActive && u.FullName != "Alexander Wright" && u.FullName != "Elena Vance")
                .OrderBy(u => u.FullName)
                .ToListAsync();

            if (users.Count == 0)
            {
                await _connectionManager.InitializeDatabaseAsync();
                users = await db.Users
                    .Where(u => u.IsActive && u.FullName != "Alexander Wright" && u.FullName != "Elena Vance")
                    .OrderBy(u => u.FullName)
                    .ToListAsync();
            }

            AvailableUsers.Clear();
            foreach (var u in users)
            {
                // Excluir estrictamente al usuario actual en sesión
                if (u.UserId == currentUserId || (currentUsername != null && u.Username.ToLower() == currentUsername))
                {
                    continue;
                }
                AvailableUsers.Add(u);
            }

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
