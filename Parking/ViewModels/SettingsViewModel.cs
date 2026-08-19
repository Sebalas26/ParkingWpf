using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Parking.Entities;
using Parking.Services.Contracts;

namespace Parking.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IPricingCalculatorService _pricingCalculator;
    private readonly IParkingTicketService _ticketService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private int _totalCapacity = 120;

    [ObservableProperty]
    private string? _feedbackMessage;

    [ObservableProperty]
    private bool _hasFeedback;

    [ObservableProperty]
    private bool _isSuccessFeedback;

    public ObservableCollection<VehicleRate> Rates { get; } = new();

    public SettingsViewModel(
        IPricingCalculatorService pricingCalculator,
        IParkingTicketService ticketService,
        IDialogService dialogService)
    {
        _pricingCalculator = pricingCalculator;
        _ticketService = ticketService;
        _dialogService = dialogService;
    }

    public override async Task InitializeAsync()
    {
        var stats = await _ticketService.GetOccupancyStatsAsync();
        TotalCapacity = stats.TotalCapacity;
        await LoadRatesAsync();
    }

    private async Task LoadRatesAsync()
    {
        var currentRates = await _pricingCalculator.GetAllRatesAsync();
        Rates.Clear();
        foreach (var r in currentRates)
        {
            Rates.Add(r);
        }
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        HasFeedback = false;
        FeedbackMessage = null;

        if (TotalCapacity <= 0)
        {
            HasFeedback = true;
            IsSuccessFeedback = false;
            FeedbackMessage = "La capacidad total debe ser mayor a cero.";
            return;
        }

        IsBusy = true;
        BusyMessage = "Guardando configuración de tarifas y capacidad...";

        try
        {
            _ticketService.UpdateTotalCapacity(TotalCapacity);

            foreach (var rate in Rates)
            {
                await _pricingCalculator.UpdateRateAsync(
                    rate.VehicleType,
                    rate.HourRate,
                    rate.MinuteRate,
                    rate.FullDayRate,
                    rate.GracePeriodMinutes);
            }

            await _pricingCalculator.ReloadRatesAsync();

            HasFeedback = true;
            IsSuccessFeedback = true;
            FeedbackMessage = "Configuración del sistema y estructura de tarifas guardada exitosamente.";

            await _dialogService.ShowAlertAsync("Configuración Guardada", "Todos los parámetros tarifarios y de capacidad han sido actualizados en la base de datos.");
        }
        catch (Exception ex)
        {
            HasFeedback = true;
            IsSuccessFeedback = false;
            FeedbackMessage = $"Error al guardar configuración: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            BusyMessage = null;
        }
    }

    [RelayCommand]
    private async Task ResetDefaultsAsync()
    {
        var confirmed = await _dialogService.ShowConfirmationAsync(
            "Restablecer Valores",
            "¿Está seguro de que desea recargar la estructura de tarifas desde la base de datos?");

        if (confirmed)
        {
            await LoadRatesAsync();
            HasFeedback = true;
            IsSuccessFeedback = true;
            FeedbackMessage = "Valores recargados desde la base de datos.";
        }
    }
}
