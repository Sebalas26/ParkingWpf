using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Parking.Core.Enums;
using Parking.Entities;
using Parking.Models;
using Parking.Services.Contracts;

namespace Parking.ViewModels;

public partial class CheckInViewModel : ViewModelBase
{
    private readonly IParkingTicketService _ticketService;
    private readonly IPricingCalculatorService _pricingCalculator;
    private readonly IMonthlySubscriptionService _monthlySubscriptionService;
    private readonly IAuthService _authService;
    private readonly IDialogService _dialogService;
    private readonly DispatcherTimer _feedbackTimer;

    [ObservableProperty]
    private string _plateNumber = string.Empty;

    [ObservableProperty]
    private VehicleType _selectedVehicleType = VehicleType.Car;

    [ObservableProperty]
    private string? _phoneNumber;

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private VehicleRate _currentRate = new();

    [ObservableProperty]
    private IReadOnlyList<VehicleRate> _availableRates = new List<VehicleRate>();

    [ObservableProperty]
    private MonthlySubscription? _activeSubscription;

    [ObservableProperty]
    private bool _isMonthlySubscriber;

    [ObservableProperty]
    private string? _feedbackMessage;

    [ObservableProperty]
    private bool _hasFeedback;

    [ObservableProperty]
    private bool _isSuccessFeedback;

    [ObservableProperty]
    private bool _isVirtualKeyboardVisible;

    public CheckInViewModel(
        IParkingTicketService ticketService,
        IPricingCalculatorService pricingCalculator,
        IMonthlySubscriptionService monthlySubscriptionService,
        IAuthService authService,
        IDialogService dialogService)
    {
        _ticketService = ticketService;
        _pricingCalculator = pricingCalculator;
        _monthlySubscriptionService = monthlySubscriptionService;
        _authService = authService;
        _dialogService = dialogService;

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
        UpdateCurrentRate();
    }

    async partial void OnPlateNumberChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length < 3)
        {
            ActiveSubscription = null;
            IsMonthlySubscriber = false;
            return;
        }

        try
        {
            var sub = await _monthlySubscriptionService.GetActiveSubscriptionByPlateAsync(value.Trim());
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

    [RelayCommand]
    private async Task RegisterAndPrintAsync()
    {
        if (string.IsNullOrWhiteSpace(PlateNumber))
        {
            ShowFeedback("Por favor ingrese un número de placa válido.", false);
            return;
        }

        var normalizedPlate = PlateNumber.Trim().ToUpperInvariant();
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

            var successMsg = IsMonthlySubscriber
                ? $"Vehículo abonado {ticket.PlateNumber} registrado correctamente (Mensualidad Activa - Tarifa $0.00)."
                : $"Vehículo {ticket.PlateNumber} registrado exitosamente (Tiquete #{ticket.TicketNumber}).";

            ShowFeedback(successMsg, true);

            await _dialogService.ShowReceiptPreviewAsync(ticket);
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
