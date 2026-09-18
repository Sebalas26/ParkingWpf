using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Parking.Models;
using Parking.Services.Contracts;

namespace Parking.ViewModels;

public partial class AppUpdateViewModel : ObservableObject
{
    private readonly IAppUpdateService _updateService;

    [ObservableProperty]
    private AppReleaseInfoDto _releaseInfo = new();

    [ObservableProperty]
    private bool _isUpdating;

    [ObservableProperty]
    private int _progressPercentage;

    [ObservableProperty]
    private string _statusMessage = "Listo para actualizar";

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _canCancel;

    public event Action? UpdateCompleted;
    public event Action? CancelRequested;

    public AppUpdateViewModel(IAppUpdateService updateService)
    {
        _updateService = updateService;
    }

    public void Initialize(AppReleaseInfoDto release)
    {
        ReleaseInfo = release;
        CanCancel = !release.IsMandatory;
        StatusMessage = release.IsMandatory
            ? "Esta actualización es obligatoria para garantizar la estabilidad y compatibilidad con el servidor central."
            : "Una nueva versión se encuentra disponible con mejoras y optimizaciones.";
    }

    [RelayCommand]
    private async Task StartUpdateAsync()
    {
        IsUpdating = true;
        ErrorMessage = null;
        ProgressPercentage = 0;

        var progress = new Progress<UpdateProgressReport>(report =>
        {
            ProgressPercentage = report.Percentage;
            StatusMessage = report.StepDescription;

            if (report.IsError)
            {
                ErrorMessage = report.ErrorMessage;
            }
        });

        var success = await _updateService.PrepareAndApplyUpdateAsync(ReleaseInfo, progress);
        if (success)
        {
            UpdateCompleted?.Invoke();
        }
        else
        {
            IsUpdating = false;
            if (string.IsNullOrEmpty(ErrorMessage))
            {
                ErrorMessage = "No se pudo aplicar la actualización en este momento.";
            }
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        if (CanCancel)
        {
            CancelRequested?.Invoke();
        }
    }
}
