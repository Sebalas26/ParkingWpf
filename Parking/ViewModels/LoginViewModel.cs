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

namespace Parking.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private readonly IAuthService _authService;
    private readonly ISessionService _sessionService;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _hasError;

    public event Action? LoginSuccessful;

    public LoginViewModel(
        IAuthService authService,
        ISessionService sessionService)
    {
        _authService = authService;
        _sessionService = sessionService;
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
        BusyMessage = "Validando credenciales y preparando estación de trabajo...";

        try
        {
            var authResult = await _authService.AuthenticateAsync(Username.Trim(), Password);

            if (!authResult.Success || authResult.User == null)
            {
                HasError = true;
                ErrorMessage = authResult.ErrorMessage ?? "Usuario o contraseña incorrectos. Por favor verifique sus datos.";
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

            // Escenario 2: 1 Sede asignada (Login directo)
            if (branches.Count == 1)
            {
                _sessionService.SetSession(authResult.User, branches, branches[0]);
                LoginSuccessful?.Invoke();
                return;
            }

            // Escenario 3: Más de 1 Sede asignada (Modal interactivo)
            var dialog = new BranchSelectionDialog(branches);
            if (Application.Current?.MainWindow != null && Application.Current.MainWindow.IsVisible)
            {
                dialog.Owner = Application.Current.MainWindow;
            }

            var dialogResult = dialog.ShowDialog();
            if (dialogResult == true && dialog.SelectedBranch != null)
            {
                _sessionService.SetSession(authResult.User, branches, dialog.SelectedBranch);
                LoginSuccessful?.Invoke();
            }
            else
            {
                HasError = true;
                ErrorMessage = "Debe seleccionar una sede para ingresar al sistema.";
            }
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"Error al iniciar sesión: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            BusyMessage = null;
        }
    }
}
