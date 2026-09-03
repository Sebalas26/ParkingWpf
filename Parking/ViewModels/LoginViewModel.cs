using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Parking.Models;
using Parking.Services.Contracts;
using Parking.Views;
using Parking.Core.Enums;

namespace Parking.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private readonly IAuthService _authService;
    private readonly ISessionService _sessionService;
    private readonly IApiClientService _apiClient;
    private readonly ISyncEngineService _syncEngine;
    private readonly IPermissionService _permissionService;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private bool _isOnline = true;

    [ObservableProperty]
    private string _networkStatusText = "Comprobando conexión...";

    [ObservableProperty]
    private int _syncProgressPercentage;

    [ObservableProperty]
    private string _syncStepDescription = string.Empty;

    [ObservableProperty]
    private bool _isSyncing;

    public event Action? LoginSuccessful;

    public LoginViewModel(
        IAuthService authService,
        ISessionService sessionService,
        IApiClientService apiClient,
        ISyncEngineService syncEngine,
        IPermissionService permissionService)
    {
        _authService = authService;
        _sessionService = sessionService;
        _apiClient = apiClient;
        _syncEngine = syncEngine;
        _permissionService = permissionService;

        _ = CheckInitialConnectionAsync();
    }

    private async Task CheckInitialConnectionAsync()
    {
        try
        {
            var isAvailable = await _apiClient.PingAsync();
            IsOnline = isAvailable;
            NetworkStatusText = isAvailable ? "API Central Online" : "Modo Offline (Sin Conexión)";
        }
        catch
        {
            IsOnline = false;
            NetworkStatusText = "Modo Offline (Sin Conexión)";
        }
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            HasError = true;
            ErrorMessage = "Por favor ingrese su usuario y contraseña.";
            return;
        }

        ErrorMessage = null;
        HasError = false;
        IsBusy = true;
        IsSyncing = false;
        BusyMessage = "Validando credenciales y preparando estación...";

        try
        {
            var authResult = await _authService.AuthenticateAsync(Username.Trim(), Password);

            if (!authResult.Success || authResult.User == null)
            {
                HasError = true;
                ErrorMessage = authResult.ErrorMessage ?? "Usuario o contraseña incorrectos. Por favor verifique sus datos.";
                return;
            }

            if (!_permissionService.IsAdmin && _permissionService.GrantedPermissions.Count == 0)
            {
                await _authService.LogoutAsync();
                ModernMessageDialog.ShowAlert(
                    Application.Current?.MainWindow,
                    "Acceso Denegado",
                    "Su usuario no tiene permisos configurados para operar en la aplicación de escritorio POS.\n\nPor favor contacte al administrador del sistema para que le asigne facultades operativas a su rol.",
                    DialogNotificationType.Warning,
                    "Entendido");
                HasError = true;
                ErrorMessage = "Acceso denegado: Su usuario no cuenta con permisos operativos asignados.";
                return;
            }

            var branches = authResult.Branches;

            // Escenario 1: 0 Sedes disponibles en el sistema o asignadas
            if (branches == null || branches.Count == 0)
            {
                HasError = true;
                ErrorMessage = "No existen sedes registradas en el sistema o no tienes sedes asignadas. Por favor ingresa a la administración web (PWA) y crea tu primera sede de parqueadero antes de operar en la terminal.";
                return;
            }

            BranchModel? selectedBranch = null;

            // Escenario 2: 1 Sede asignada (Login directo)
            if (branches.Count == 1)
            {
                selectedBranch = branches[0];
            }
            else
            {
                // Escenario 3: Más de 1 Sede asignada (Modal interactivo)
                var dialog = new BranchSelectionDialog(branches);
                if (Application.Current?.MainWindow != null && Application.Current.MainWindow.IsVisible)
                {
                    dialog.Owner = Application.Current.MainWindow;
                }

                var dialogResult = dialog.ShowDialog();
                if (dialogResult == true && dialog.SelectedBranch != null)
                {
                    selectedBranch = dialog.SelectedBranch;
                }
                else
                {
                    HasError = true;
                    ErrorMessage = "Debe seleccionar una sede para ingresar al sistema.";
                    return;
                }
            }

            _sessionService.SetSession(authResult.User, branches, selectedBranch);

            // Ejecutar Sincronización Visual con Barra de Progreso
            IsSyncing = true;
            BusyMessage = "Sincronizando con Servidor Central...";
            SyncProgressPercentage = 10;
            SyncStepDescription = "Iniciando transferencia de datos...";

            var progress = new Progress<SyncProgressReport>(report =>
            {
                SyncProgressPercentage = report.Percentage;
                SyncStepDescription = !string.IsNullOrWhiteSpace(report.DetailMessage)
                    ? report.DetailMessage
                    : report.CurrentStepTitle;
            });

            try
            {
                var syncResult = await _syncEngine.PerformFullSyncWithProgressAsync(progress);
                if (syncResult.Success)
                {
                    IsOnline = true;
                    NetworkStatusText = "API Central Online";
                }
                else
                {
                    IsOnline = false;
                    NetworkStatusText = "Modo Offline (Sin Conexión)";
                }
            }
            catch
            {
                IsOnline = false;
                NetworkStatusText = "Modo Offline (Sin Conexión)";
            }

            await Task.Delay(250);
            LoginSuccessful?.Invoke();
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"Error al iniciar sesión: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            IsSyncing = false;
            BusyMessage = null;
        }
    }
}
