using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Parking.Data.Factories;
using Parking.Entities;
using Parking.Models.ApiModels;
using Parking.Services.Contracts;

namespace Parking.ViewModels;

public partial class ShiftClosureViewModel : ViewModelBase
{
    private readonly IShiftService _shiftService;
    private readonly IAuthService _authService;
    private readonly IDialogService _dialogService;
    private readonly IReceiptPrinterService _receiptPrinter;
    private readonly IDbConnectionManager _connectionManager;

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

    public ShiftClosureViewModel(
        IShiftService shiftService,
        IAuthService authService,
        IDialogService dialogService,
        IReceiptPrinterService receiptPrinter,
        IDbConnectionManager connectionManager)
    {
        _shiftService = shiftService;
        _authService = authService;
        _dialogService = dialogService;
        _receiptPrinter = receiptPrinter;
        _connectionManager = connectionManager;
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
            FeedbackMessage = "Debe seleccionar el usuario al que se le realiza la entrega de turno.";
            return;
        }

        var confirmed = await _dialogService.ShowConfirmationAsync(
            "Confirmar Cierre de Turno y Arqueo",
            $"¿Está seguro de cerrar el turno de {OperatorName} y entregarlo a {SelectedHandoverUser.FullName}?\n\n" +
            $"• Base de Caja: ${Summary.BaseAmount:N0}\n" +
            $"• Efectivo Esperado: ${Summary.ExpectedCash:N0}\n" +
            $"• Efectivo Contado: ${ActualCashCounted:N0}\n" +
            $"• Diferencia: ${CashDifference:N0}");

        if (!confirmed) return;

        IsBusy = true;
        BusyMessage = "Liquidando turno, cuadrando caja y generando comprobante...";

        try
        {
            var closedShift = await _shiftService.CloseShiftAsync(
                ActualCashCounted,
                Notes,
                SelectedHandoverUser.UserId,
                SelectedHandoverUser.FullName);

            if (closedShift != null)
            {
                HasFeedback = true;
                IsSuccessFeedback = true;
                FeedbackMessage = $"Turno cerrado exitosamente y entregado a {SelectedHandoverUser.FullName}. Diferencia: ${closedShift.CashDifference:N0}.";
                ActualCashCounted = 0m;
                Notes = null;
                await LoadShiftDataAsync();
            }
            else
            {
                HasFeedback = true;
                IsSuccessFeedback = false;
                FeedbackMessage = "No se pudo cerrar el turno. Intente nuevamente.";
            }
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
            }

            // Cargar usuarios para entrega de turno
            using var db = _connectionManager.CreateDbContext();
            var users = await db.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
            AvailableUsers.Clear();
            foreach (var u in users)
            {
                AvailableUsers.Add(u);
            }

            var currentUserId = _authService.CurrentUser?.UserId;
            SelectedHandoverUser = AvailableUsers.FirstOrDefault(u => u.UserId != currentUserId) ?? AvailableUsers.FirstOrDefault();

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
