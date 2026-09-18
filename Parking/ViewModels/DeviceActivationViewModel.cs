using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Parking.Services.Contracts;

namespace Parking.ViewModels;

public partial class DeviceActivationViewModel : ObservableObject
{
    private readonly IDeviceLicenseService _licenseService;
    private readonly IHardwareFingerprintService _fingerprintService;

    [ObservableProperty]
    private string _licenseKey = string.Empty;

    [ObservableProperty]
    private string _machineFingerprint = string.Empty;

    [ObservableProperty]
    private string _machineName = string.Empty;

    [ObservableProperty]
    private string _windowsUser = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _successMessage;

    public event Action? ActivationCompleted;
    public event Action? CancelRequested;

    public DeviceActivationViewModel(IDeviceLicenseService licenseService, IHardwareFingerprintService fingerprintService)
    {
        _licenseService = licenseService;
        _fingerprintService = fingerprintService;

        MachineFingerprint = _fingerprintService.GetMachineFingerprint();
        MachineName = _fingerprintService.GetMachineName();
        WindowsUser = _fingerprintService.GetWindowsUser();
    }

    [RelayCommand]
    private async Task ActivateAsync()
    {
        if (string.IsNullOrWhiteSpace(LicenseKey))
        {
            ErrorMessage = "Debe ingresar la clave de licencia asignada a esta sede.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        SuccessMessage = null;

        try
        {
            var result = await _licenseService.ActivateLicenseAsync(LicenseKey.Trim());
            if (result.Success)
            {
                SuccessMessage = "¡Terminal autorizada exitosamente!";
                await Task.Delay(800);
                ActivationCompleted?.Invoke();
            }
            else
            {
                ErrorMessage = result.Message;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error al comunicar con el servidor: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        CancelRequested?.Invoke();
    }
}
