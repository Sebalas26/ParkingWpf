using System;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Parking.Core.Enums;
using Parking.Core.Security;
using Parking.Services.Contracts;
using Parking.ViewModels;

namespace Parking.Services.Implementations;

public class NavigationService : INavigationService
{
    private readonly IServiceProvider _serviceProvider;
    private ViewModelBase? _currentViewModel;

    public ViewModelBase? CurrentViewModel
    {
        get => _currentViewModel;
        private set
        {
            if (_currentViewModel != value)
            {
                _currentViewModel = value;
                CurrentViewModelChanged?.Invoke(this, _currentViewModel!);
            }
        }
    }

    public event EventHandler<ViewModelBase>? CurrentViewModelChanged;

    public NavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void NavigateTo<TViewModel>() where TViewModel : ViewModelBase
    {
        NavigateTo(typeof(TViewModel));
    }

    public void NavigateTo(Type viewModelType)
    {
        // 1. Validar permiso requerido mediante [RequirePermission]
        var reqPermAttr = viewModelType.GetCustomAttribute<RequirePermissionAttribute>();
        if (reqPermAttr != null)
        {
            var permissionService = _serviceProvider.GetService<IPermissionService>() ?? PermissionService.Current;
            if (!permissionService.HasPermission(reqPermAttr.PermissionKey))
            {
                var dialogService = _serviceProvider.GetService<IDialogService>();
                var moduleName = !string.IsNullOrWhiteSpace(reqPermAttr.DisplayModuleName) 
                    ? reqPermAttr.DisplayModuleName 
                    : "solicitado";
                
                _ = dialogService?.ShowAlertAsync(
                    "Acceso Restringido",
                    $"No tienes los permisos necesarios para acceder al módulo de '{moduleName}'. Consulta con el administrador del sistema.",
                    DialogNotificationType.Warning);
                return;
            }
        }

        if (_serviceProvider.GetRequiredService(viewModelType) is ViewModelBase viewModel)
        {
            CurrentViewModel = viewModel;
            _ = viewModel.InitializeAsync();
        }
    }
}
