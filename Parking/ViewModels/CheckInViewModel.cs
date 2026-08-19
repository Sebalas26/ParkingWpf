using System;
using System.Collections.Generic;
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

public partial class CheckInViewModel : ViewModelBase
{
    private readonly IParkingTicketService _ticketService;
    private readonly IPricingCalculatorService _pricingCalculator;
    private readonly IAuthService _authService;
    private readonly IDialogService _dialogService;

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
    private string? _feedbackMessage;

    [ObservableProperty]
    private bool _hasFeedback;

    [ObservableProperty]
    private bool _isSuccessFeedback;

    public ObservableCollection<ParkingTicket> RecentEntries { get; } = new();

    public CheckInViewModel(
        IParkingTicketService ticketService,
        IPricingCalculatorService pricingCalculator,
        IAuthService authService,
        IDialogService dialogService)
    {
        _ticketService = ticketService;
        _pricingCalculator = pricingCalculator;
        _authService = authService;
        _dialogService = dialogService;

        _ticketService.TicketRegistered += OnTicketRegistered;
    }

    public override async Task InitializeAsync()
    {
        AvailableRates = await _pricingCalculator.GetAllRatesAsync();
        UpdateCurrentRate();
        await LoadRecentEntriesAsync();
    }

    partial void OnSelectedVehicleTypeChanged(VehicleType value)
    {
        UpdateCurrentRate();
    }

    private void UpdateCurrentRate()
    {
        CurrentRate = _pricingCalculator.GetRate(SelectedVehicleType);
    }

    private async Task LoadRecentEntriesAsync()
    {
        var active = await _ticketService.GetActiveTicketsAsync();
        RecentEntries.Clear();
        foreach (var ticket in active.Take(6))
        {
            RecentEntries.Add(ticket);
        }
    }

    private void OnTicketRegistered(object? sender, ParkingTicket ticket)
    {
        RecentEntries.Insert(0, ticket);
        if (RecentEntries.Count > 6)
        {
            RecentEntries.RemoveAt(RecentEntries.Count - 1);
        }
    }

    [RelayCommand]
    private void SelectVehicleType(VehicleType type)
    {
        SelectedVehicleType = type;
    }

    [RelayCommand]
    private async Task RegisterAndPrintAsync()
    {
        HasFeedback = false;
        FeedbackMessage = null;

        if (string.IsNullOrWhiteSpace(PlateNumber))
        {
            HasFeedback = true;
            IsSuccessFeedback = false;
            FeedbackMessage = "Por favor ingrese un número de placa válido.";
            return;
        }

        var normalizedPlate = PlateNumber.Trim().ToUpperInvariant();
        if (await _ticketService.IsPlateCurrentlyParkedAsync(normalizedPlate))
        {
            HasFeedback = true;
            IsSuccessFeedback = false;
            FeedbackMessage = $"El vehículo con placa '{normalizedPlate}' ya se encuentra registrado y activo adentro.";
            return;
        }

        IsBusy = true;
        BusyMessage = "Registrando ingreso de vehículo y generando tiquete...";

        try
        {
            var operatorName = _authService.CurrentUser?.FullName ?? "Operador General";
            var ticket = await _ticketService.RegisterEntryAsync(
                normalizedPlate,
                SelectedVehicleType,
                PhoneNumber,
                Notes,
                operatorName);

            ClearForm();

            HasFeedback = true;
            IsSuccessFeedback = true;
            FeedbackMessage = $"Vehículo {ticket.PlateNumber} registrado exitosamente en la Bahía {ticket.BayNumber} (Tiquete #{ticket.TicketNumber}).";

            await _dialogService.ShowReceiptPreviewAsync(ticket);
        }
        catch (Exception ex)
        {
            HasFeedback = true;
            IsSuccessFeedback = false;
            FeedbackMessage = $"Error al registrar ingreso: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            BusyMessage = null;
        }
    }

    [RelayCommand]
    private void ClearForm()
    {
        PlateNumber = string.Empty;
        PhoneNumber = null;
        Notes = null;
        SelectedVehicleType = VehicleType.Car;
        HasFeedback = false;
        FeedbackMessage = null;
    }
}
