using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    public string AppVersionDisplay
    {
        get
        {
            var asm = Assembly.GetExecutingAssembly();
            // Prioridad: AssemblyVersion real del binario (inyectado por /p:Version en publish-release.ps1)
            var ver = asm.GetName().Version;
            if (ver != null && (ver.Major > 0 || ver.Minor > 0 || ver.Build > 0))
            {
                return $"{ver.Major}.{ver.Minor}.{ver.Build}";
            }
            // Fallback: InformationalVersion (limpia sufijo +hash de git)
            var infoVer = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(infoVer))
            {
                var plusIdx = infoVer.IndexOf('+');
                return plusIdx > 0 ? infoVer[..plusIdx] : infoVer;
            }
            return "1.0.0";
        }
    }
    private readonly IAuthService _authService;
    private readonly ISessionService _sessionService;
    private readonly IApiClientService _apiClient;
    private readonly ISyncEngineService _syncEngine;
    private readonly IPermissionService _permissionService;
    private readonly IAppUpdateService? _updateService;
    private readonly IDialogService? _dialogService;

    [ObservableProperty]
    private bool _showUpdateSuccessMessage;

    [ObservableProperty]
    private string _updateSuccessMessage = "Listo, sistema actualizado";

    public bool IsPostUpdateLaunch
    {
        get => ShowUpdateSuccessMessage;
        set => ShowUpdateSuccessMessage = value;
    }

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
        IPermissionService permissionService,
        IAppUpdateService? updateService = null,
        IDialogService? dialogService = null)
    {
        _authService = authService;
        _sessionService = sessionService;
        _apiClient = apiClient;
        _syncEngine = syncEngine;
        _permissionService = permissionService;
        _updateService = updateService;
        _dialogService = dialogService;

        _apiClient.ConnectionStateChanged += isOnline =>
        {
            var app = Application.Current;
            if (app?.Dispatcher != null && !app.Dispatcher.CheckAccess())
            {
                app.Dispatcher.InvokeAsync(() =>
                {
                    IsOnline = isOnline;
                    NetworkStatusText = isOnline ? "API Central Online" : "Modo Offline (Sin Conexión)";
                });
            }
            else
            {
                IsOnline = isOnline;
                NetworkStatusText = isOnline ? "API Central Online" : "Modo Offline (Sin Conexión)";
            }
        };

        _ = CheckInitialConnectionAsync();
    }

    private async Task CheckInitialConnectionAsync()
    {
        try
        {
            NetworkStatusText = "Comprobando conexión...";
            var isAvailable = await _apiClient.PingAsync(8);
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

            if (!authResult.User.IsSuperAdmin && !authResult.HasDesktopAccess)
            {
                await _authService.LogoutAsync();
                ModernMessageDialog.ShowAlert(
                    Application.Current?.MainWindow,
                    "Plan Sin Acceso de Garita (Solo WEB)",
                    "El plan contratado para su empresa no incluye acceso a la estación de garita de escritorio.\n\nPara habilitar la operación en terminales de garita física, amplíe su plan de suscripción a Garita o Híbrido desde la administración web.",
                    DialogNotificationType.Warning,
                    "Entendido");
                HasError = true;
                ErrorMessage = "Acceso denegado: El plan de su empresa no incluye acceso a la estación de escritorio.";
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

            // Bifurcación Fast-Login: verificar si SQLite local ya tiene catálogo descargado para esta sede
            var hasLocalData = await _syncEngine.HasLocalBranchDataAsync(selectedBranch.Id);

            if (!hasLocalData)
            {
                // Descarga inicial interactiva (PC nuevo o base de datos vacía)
                IsSyncing = true;
                BusyMessage = "Descargando catálogo inicial de la sede...";
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
            }
            else
            {
                // Fast-Login (< 1 segundo): La estación ya cuenta con tarifas y usuarios. Acceso instantáneo
                IsOnline = _syncEngine.IsOnline;
                NetworkStatusText = _syncEngine.IsOnline ? "API Central Online" : "Modo Offline (Sin Conexión)";

                // Sincronización en segundo plano sin bloquear la terminal operativa
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _syncEngine.PerformFullSyncAsync();
                    }
                    catch (Exception syncEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[FastLogin Background Sync] {syncEx.Message}");
                    }
                });
            }

            // Validar Horario de Atención de la Sede para el día de hoy
            var activeBranch = _sessionService.CurrentBranch ?? selectedBranch;
            if (activeBranch?.OperatingHours != null && activeBranch.OperatingHours.Count > 0)
            {
                var today = DateTime.Now.DayOfWeek;
                var todaySchedule = activeBranch.OperatingHours.FirstOrDefault(oh => oh.DayOfWeek == today);
                if (todaySchedule != null && !todaySchedule.IsOpen)
                {
                    var cultureEs = new System.Globalization.CultureInfo("es-CO");
                    var dayName = cultureEs.DateTimeFormat.GetDayName(today);
                    if (!string.IsNullOrEmpty(dayName))
                    {
                        dayName = char.ToUpper(dayName[0]) + dayName.Substring(1);
                    }

                    _sessionService.Clear();
                    _apiClient.ClearAuthToken();
                    HasError = true;
                    ErrorMessage = $"La sede '{activeBranch.Name}' se encuentra cerrada el día de hoy ({dayName}) según el horario de atención configurado.";
                    return;
                }
            }

            // 4. REGLA DE ORO DE ACTUALIZACIÓN: Comprobar si existe una actualización obligatoria disponible
            if (_updateService != null && _dialogService != null)
            {
                try
                {
                    BusyMessage = "Comprobando actualizaciones de sistema...";
                    var release = await _updateService.CheckForUpdateAsync();
                    if (release != null && release.HasUpdate)
                    {
                        // Si aún quedan transacciones pendientes tras el sync, forzar subida total
                        if (_syncEngine.PendingItemsCount > 0)
                        {
                            BusyMessage = $"Subiendo {_syncEngine.PendingItemsCount} transacciones a la nube antes de actualizar...";
                            await _syncEngine.PerformFullSyncAsync();
                        }

                        if (_syncEngine.PendingItemsCount > 0)
                        {
                            // REGLA DE ORO: Bloquear actualización si no se garantiza el 100% de datos en la nube
                            HasError = true;
                            ErrorMessage = $"Existe una actualización obligatoria ({release.LatestVersion}), pero hay {_syncEngine.PendingItemsCount} transacciones locales que no se pudieron subir a la nube. Por seguridad de sus datos, verifique la conexión antes de actualizar.";
                            _sessionService.Clear();
                            _apiClient.ClearAuthToken();
                            return;
                        }

                        // Datos 100% en la nube: Proceder con la actualización obligatoria inmediata
                        IsBusy = false;
                        IsSyncing = false;
                        _sessionService.Clear();
                        _apiClient.ClearAuthToken();

                        await _dialogService.ShowAppUpdateDialogAsync(release);
                        return; // No navegar a MainShellWindow, la app se cerrará y relanzará con el micro-updater
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LOGIN UPDATE CHECK WARNING] {ex.Message}");
                }
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
