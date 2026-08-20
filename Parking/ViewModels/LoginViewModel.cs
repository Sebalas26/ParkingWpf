using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Parking.Core.Enums;
using Parking.Services.Contracts;

namespace Parking.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private readonly IAuthService _authService;
    private readonly IThemeService _themeService;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private AppTheme _currentTheme;

    public IReadOnlyList<ThemeInfo> AvailableThemes => _themeService.GetAvailableThemes();

    public event Action? LoginSuccessful;

    public LoginViewModel(IAuthService authService, IThemeService themeService)
    {
        _authService = authService;
        _themeService = themeService;
        _currentTheme = _themeService.CurrentTheme;
    }

    [RelayCommand]
    private void SelectTheme(AppTheme theme)
    {
        CurrentTheme = theme;
        _themeService.SetTheme(theme);
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
            var success = await _authService.LoginAsync(Username.Trim(), Password);
            if (success)
            {
                LoginSuccessful?.Invoke();
            }
            else
            {
                HasError = true;
                ErrorMessage = "Usuario o contraseña incorrectos. Por favor verifique sus datos.";
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
