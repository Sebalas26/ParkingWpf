using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Parking.Core.Enums;
using Parking.Core.Security;
using Parking.Entities;
using Parking.Models;
using Parking.Services.Contracts;

namespace Parking.ViewModels;

[RequirePermission("checkin.view", "Ingreso de Vehículos")]
public partial class CheckInViewModel : ViewModelBase
{
    private readonly IParkingTicketService _ticketService;
    private readonly IPricingCalculatorService _pricingCalculator;
    private readonly IMonthlySubscriptionService _monthlySubscriptionService;
    private readonly IAuthService _authService;
    private readonly IDialogService _dialogService;
    private readonly IShiftService _shiftService;
    private readonly DispatcherTimer _feedbackTimer;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterAndPrintCommand))]
    private string _plateNumber = string.Empty;

    [ObservableProperty]
    private VehicleType _selectedVehicleType = VehicleType.Car;

    [ObservableProperty]
    private string? _phoneNumber;

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private VehicleRate? _currentRate;

    [ObservableProperty]
    private IReadOnlyList<VehicleRate> _availableRates = new List<VehicleRate>();

    [ObservableProperty]
    private MonthlySubscription? _activeSubscription;

    [ObservableProperty]
    private bool _isMonthlySubscriber;

    [ObservableProperty]
    private bool _isPlateBlocked;

    [ObservableProperty]
    private string? _blockedReason;

    [ObservableProperty]
    private string? _blockedIncidentType;

    [ObservableProperty]
    private string? _blockedDescription;

    [ObservableProperty]
    private OccupancyStats _occupancy = new();

    [ObservableProperty]
    private IReadOnlyList<ParkingTicket> _recentEntries = new List<ParkingTicket>();

    [ObservableProperty]
    private string? _feedbackMessage;

    [ObservableProperty]
    private bool _hasFeedback;

    [ObservableProperty]
    private bool _isSuccessFeedback;

    [ObservableProperty]
    private bool _isVirtualKeyboardVisible;

    [ObservableProperty]
    private bool _hasConfiguredRates;

    public CheckInViewModel(
        IParkingTicketService ticketService,
        IPricingCalculatorService pricingCalculator,
        IMonthlySubscriptionService monthlySubscriptionService,
        IAuthService authService,
        IDialogService dialogService,
        IShiftService shiftService,
        ISyncEngineService syncEngine)
    {
        _ticketService = ticketService;
        _pricingCalculator = pricingCalculator;
        _monthlySubscriptionService = monthlySubscriptionService;
        _authService = authService;
        _dialogService = dialogService;
        _shiftService = shiftService;

        syncEngine.DataSynchronized += async () =>
        {
            await InitializeAsync();
        };

        _feedbackTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(10)
        };
        _feedbackTimer.Tick += (s, e) =>
        {
            _feedbackTimer.Stop();
            HasFeedback = false;
            FeedbackMessage = null;
        };
    }

    public override async Task InitializeAsync()
    {
        AvailableRates = await _pricingCalculator.GetAllRatesAsync();
        HasConfiguredRates = AvailableRates.Count > 0;
        if (HasConfiguredRates)
        {
            SelectedVehicleType = AvailableRates[0].VehicleType;
            UpdateCurrentRate();
        }
        else
        {
            CurrentRate = null;
        }
        await RefreshRecentEntriesAndOccupancyAsync();
    }

    public async Task RefreshRecentEntriesAndOccupancyAsync()
    {
        try
        {
            Occupancy = await _ticketService.GetOccupancyStatsAsync();
            var active = await _ticketService.GetActiveTicketsAsync();
            RecentEntries = active.Take(6).ToList();
        }
        catch { }
    }


    async partial void OnPlateNumberChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length < 3)
        {
            ActiveSubscription = null;
            IsMonthlySubscriber = false;
            IsPlateBlocked = false;
            BlockedReason = null;
            BlockedIncidentType = null;
            BlockedDescription = null;
            return;
        }

        var normalizedPlate = value.Trim().ToUpperInvariant();

        try
        {
            var localBlock = await _ticketService.GetActiveBlockAsync(normalizedPlate);
            if (localBlock != null)
            {
                IsPlateBlocked = true;
                BlockedIncidentType = localBlock.IncidentType;
                BlockedDescription = localBlock.Description;
                BlockedReason = $"VEHÍCULO BLOQUEADO: {localBlock.IncidentType} - {localBlock.Description}";
            }
            else
            {
                IsPlateBlocked = false;
                BlockedReason = null;
                BlockedIncidentType = null;
                BlockedDescription = null;
            }
        }
        catch
        {
            IsPlateBlocked = false;
            BlockedReason = null;
        }

        try
        {
            var sub = await _monthlySubscriptionService.GetActiveSubscriptionByPlateAsync(normalizedPlate);
            if (sub != null)
            {
                ActiveSubscription = sub;
                IsMonthlySubscriber = true;
                SelectedVehicleType = sub.VehicleType;
                if (!string.IsNullOrWhiteSpace(sub.CustomerPhone))
                {
                    PhoneNumber = sub.CustomerPhone;
                }
            }
            else
            {
                ActiveSubscription = null;
                IsMonthlySubscriber = false;
            }
        }
        catch
        {
            ActiveSubscription = null;
            IsMonthlySubscriber = false;
        }
    }

    partial void OnSelectedVehicleTypeChanged(VehicleType value)
    {
        UpdateCurrentRate();
    }

    private void UpdateCurrentRate()
    {
        CurrentRate = _pricingCalculator.GetRate(SelectedVehicleType);
    }

    [RelayCommand]
    private void SelectVehicleType(VehicleType type)
    {
        SelectedVehicleType = type;
    }

    [RelayCommand]
    private void ToggleVirtualKeyboard()
    {
        IsVirtualKeyboardVisible = !IsVirtualKeyboardVisible;
    }

    [RelayCommand]
    private void AppendVirtualKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return;

        if (key.Equals("BACKSPACE", StringComparison.OrdinalIgnoreCase) || key.Equals("DEL", StringComparison.OrdinalIgnoreCase))
        {
            if (PlateNumber.Length > 0)
            {
                PlateNumber = PlateNumber[..^1];
            }
        }
        else if (key.Equals("CLEAR", StringComparison.OrdinalIgnoreCase))
        {
            PlateNumber = string.Empty;
        }
        else if (key.Equals("SPACE", StringComparison.OrdinalIgnoreCase))
        {
            PlateNumber += " ";
        }
        else
        {
            PlateNumber = (PlateNumber + key).ToUpperInvariant();
        }
    }

    private void ShowFeedback(string message, bool isSuccess)
    {
        FeedbackMessage = message;
        IsSuccessFeedback = isSuccess;
        HasFeedback = true;
        _feedbackTimer.Stop();
        _feedbackTimer.Start();
    }

    private bool CanRegisterAndPrint()
    {
        return !string.IsNullOrWhiteSpace(PlateNumber) && PlateNumber.Trim().Length > 0;
    }

    [RelayCommand(CanExecute = nameof(CanRegisterAndPrint))]
    private async Task RegisterAndPrintAsync()
    {
        var activeShift = await _shiftService.GetActiveShiftAsync();
        if (activeShift == null || activeShift.Status != 0)
        {
            ShowFeedback("No hay un turno operativo abierto. Debe abrir turno antes de ingresar vehículos.", false);
            await _dialogService.ShowAlertAsync(
                "Apertura de Turno Requerida",
                "Debes abrir un turno operativo e indicar la base inicial de caja antes de registrar ingresos.",
                DialogNotificationType.Warning);
            return;
        }

        if (!HasConfiguredRates)
        {
            ShowFeedback("No existen tarifas vehiculares configuradas en el sistema. Configure las tarifas antes de continuar.", false);
            await _dialogService.ShowAlertAsync(
                "Configuración de Tarifas Requerida",
                "No existen tarifas vehiculares configuradas ni activas en el sistema. Por favor, crea y sincroniza las tarifas vehiculares antes de emitir tiquetes de ingreso.",
                DialogNotificationType.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(PlateNumber))
        {
            ShowFeedback("Por favor ingrese un número de placa válido.", false);
            return;
        }

        var normalizedPlate = PlateNumber.Trim().ToUpperInvariant();

        if (IsPlateBlocked)
        {
            await _dialogService.ShowAlertAsync(
                "⛔ Ingreso Restringido por Novedad",
                $"La placa '{normalizedPlate}' presenta un BLOQUEO ACTIVO / LISTA NEGRA en el sistema.\n\n" +
                $"• Tipo de Novedad: {BlockedIncidentType ?? "Restricción de Ingreso"}\n" +
                $"• Motivo / Detalle: {BlockedDescription ?? "Vehículo con novedad administrativa."}\n\n" +
                "⚠️ Este vehículo no tiene autorización para ingresar. Para permitir su acceso, un administrador debe resolver la novedad desde la plataforma web (PWA).",
                DialogNotificationType.Error);
            return;
        }

        if (await _ticketService.IsPlateCurrentlyParkedAsync(normalizedPlate))
        {
            ShowFeedback($"El vehículo con placa '{normalizedPlate}' ya se encuentra registrado y activo adentro.", false);
            return;
        }

        IsBusy = true;
        BusyMessage = "Registrando ingreso de vehículo y emitiendo tiquete...";

        try
        {
            var operatorName = _authService.CurrentUser?.FullName ?? "Operador General";
            decimal? customRate = IsMonthlySubscriber ? 0m : null;
            var ticketNotes = IsMonthlySubscriber && ActiveSubscription != null
                ? $"Mensualidad Activa: {ActiveSubscription.CustomerName} (Vence: {ActiveSubscription.EndDate:yyyy-MM-dd})"
                : Notes;

            var ticket = await _ticketService.RegisterEntryAsync(
                normalizedPlate,
                SelectedVehicleType,
                PhoneNumber,
                ticketNotes,
                operatorName,
                customRate);

            ClearInputs();
            await RefreshRecentEntriesAndOccupancyAsync();

            var successMsg = IsMonthlySubscriber
                ? $"Vehículo abonado {ticket.PlateNumber} registrado correctamente (Mensualidad Activa - Tarifa $0.00)."
                : $"Vehículo {ticket.PlateNumber} registrado exitosamente (Tiquete #{ticket.TicketNumber}).";

            ShowFeedback(successMsg, true);

            await _dialogService.ShowReceiptPreviewAsync(ticket);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("BLOQUEO ACTIVO") || ex.Message.Contains("LISTA NEGRA") || ex.Message.Contains("novedad", StringComparison.OrdinalIgnoreCase))
        {
            await _dialogService.ShowAlertAsync(
                "⛔ Ingreso Restringido por Novedad",
                $"{ex.Message}\n\n⚠️ Para autorizar el ingreso, un administrador debe resolver la novedad desde la plataforma web (PWA).",
                DialogNotificationType.Error);
        }
        catch (Exception ex)
        {
            ShowFeedback($"Error al registrar ingreso: {ex.Message}", false);
        }
        finally
        {
            IsBusy = false;
            BusyMessage = null;
        }
    }

    private void ClearInputs()
    {
        PlateNumber = string.Empty;
        PhoneNumber = null;
        Notes = null;
        ActiveSubscription = null;
        IsMonthlySubscriber = false;
        IsPlateBlocked = false;
        BlockedReason = null;
        BlockedIncidentType = null;
        BlockedDescription = null;
        SelectedVehicleType = VehicleType.Car;
    }

    [RelayCommand]
    private void ClearForm()
    {
        ClearInputs();
        _feedbackTimer.Stop();
        HasFeedback = false;
        FeedbackMessage = null;
    }
}
