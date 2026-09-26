using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Parking.Core.Enums;
using Parking.Data.Factories;
using Parking.Services.Contracts;
using Parking.Services.Implementations;
using Parking.ViewModels;
using Parking.Views;

namespace Parking;

public partial class App : Application
{
    private IServiceProvider _serviceProvider = null!;
    private IConfiguration _configuration = null!;
    private static readonly object _logLock = new();
    private static Mutex? _singleInstanceMutex;

    public IServiceProvider Services => _serviceProvider;

    public App()
    {
        // 1. Capturar excepciones no controladas en el hilo de UI (WPF Dispatcher)
        this.DispatcherUnhandledException += OnDispatcherUnhandledException;

        // 2. Capturar excepciones en hilos de fondo y tareas asíncronas no observadas
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        // 3. Capturar excepciones a nivel de AppDomain
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        // 0. Instancia única a nivel de sistema operativo para prevenir múltiples procesos huérfanos
        _singleInstanceMutex = new Mutex(true, "ParkingFlow_WPF_SingleInstance_Mutex", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "Parking Flow ya se encuentra en ejecución en este equipo.",
                "ParkFlow - Instancia Activa",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown(0);
            return;
        }

        base.OnStartup(e);

        // Prevenir que WPF apague la aplicación si se cierra un diálogo modal previo a la ventana principal
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        try
        {
            var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Development";

            var builder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();

            _configuration = builder.Build();

            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            var connectionManager = _serviceProvider.GetRequiredService<IDbConnectionManager>();
            await connectionManager.InitializeDatabaseAsync();

            // 1. Verificación de Licencia Local y Enlace a Hardware (Anti-Copia)
            var licenseService = _serviceProvider.GetRequiredService<IDeviceLicenseService>();
            if (!licenseService.HasValidLicense())
            {
                var activationDialog = _serviceProvider.GetRequiredService<DeviceActivationDialog>();
                var activated = activationDialog.ShowDialog();
                MainWindow = null;

                if (activated != true || !licenseService.HasValidLicense())
                {
                    Shutdown(0);
                    return;
                }
            }

            // 2. Comprobación de Actualizaciones Remotas en Segundo Plano
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(2000); // Esperar que la UI inicial esté cargada
                    var updateService = _serviceProvider.GetRequiredService<IAppUpdateService>();
                    var release = await updateService.CheckForUpdateAsync();
                    if (release != null && release.HasUpdate)
                    {
                        await Dispatcher.InvokeAsync(() =>
                        {
                            var dialogService = _serviceProvider.GetRequiredService<IDialogService>();
                            _ = dialogService.ShowAppUpdateDialogAsync(release);
                        });
                    }
                }
                catch { }
            });

            ShowLoginWindow();
        }
        catch (Exception ex)
        {
            LogException(ex, "App.OnStartup");
            MessageBox.Show(
                $"Error crítico al iniciar la aplicación:\n\n{ex.Message}\n\nConsulte el archivo de registro de errores en la carpeta Logs.",
                "ParkFlow - Error de Inicialización",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton(_configuration);

        var apiBaseUrl = _configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7023";

        services.AddSingleton(sp =>
        {
            var handler = new HttpClientHandler();
            var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Development";

            // En entorno de desarrollo o servidores localhost, permitir certificados locales auto-firmados
            if (environment.Equals("Development", StringComparison.OrdinalIgnoreCase) ||
                apiBaseUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
                apiBaseUrl.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase))
            {
                handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
            }

            var timeoutSeconds = int.TryParse(_configuration["ApiSettings:TimeoutSeconds"], out var ts) && ts > 0 ? ts : 90;

            return new HttpClient(handler)
            {
                BaseAddress = new Uri(apiBaseUrl.TrimEnd('/') + "/"),
                Timeout = TimeSpan.FromSeconds(timeoutSeconds)
            };
        });

        services.AddSingleton<IApiClientService>(sp =>
        {
            var httpClient = sp.GetRequiredService<HttpClient>();
            return new ParkingApiClient(httpClient)
            {
                BaseUrl = apiBaseUrl
            };
        });
        services.AddSingleton<ISyncEngineService, SyncEngineService>();
        services.AddSingleton<IBackgroundSyncScheduler, BackgroundSyncScheduler>();
        services.AddSingleton<ISignalRClientService, SignalRClientService>();

        services.AddSingleton<IDbConnectionManager, DbConnectionManager>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<ISessionService, SessionService>();
        services.AddSingleton<IPermissionService, PermissionService>();
        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<IShiftService, EfShiftService>();
        services.AddSingleton<IStoreService, StoreService>();
        services.AddSingleton<IAgreementService, AgreementService>();
        services.AddSingleton<IBillingResolutionService, BillingResolutionService>();
        services.AddSingleton<IPricingCalculatorService, EfPricingCalculatorService>();
        services.AddSingleton<IParkingTicketService, EfParkingTicketService>();
        services.AddSingleton<IMonthlySubscriptionService, EfMonthlySubscriptionService>();
        services.AddSingleton<IBarcodeGeneratorService, Code128BarcodeGeneratorService>();
        services.AddSingleton<IReceiptPrinterService, MockReceiptPrinterService>();
        services.AddSingleton<IAnalyticsService, EfAnalyticsService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IDialogService, DialogService>();

        // Módulo Licenciamiento de Dispositivo y Actualizaciones Remotas
        services.AddSingleton<IHardwareFingerprintService, HardwareFingerprintService>();
        services.AddSingleton<IDeviceLicenseService, DeviceLicenseService>();
        services.AddSingleton<IAppUpdateService, AppUpdateService>();

        // ViewModels
        services.AddTransient<LoginViewModel>();
        services.AddSingleton<MainShellViewModel>();
        services.AddSingleton<CheckInViewModel>();
        services.AddSingleton<CheckOutViewModel>();
        services.AddSingleton<RecentEntriesViewModel>();
        services.AddSingleton<AnalyticsViewModel>();
        services.AddSingleton<ShiftClosureViewModel>();
        services.AddSingleton<MonthlySubscriptionsViewModel>();
        services.AddTransient<ReceiptPreviewViewModel>();
        services.AddTransient<CustomersViewModel>();
        services.AddTransient<DeviceActivationViewModel>();
        services.AddTransient<AppUpdateViewModel>();

        // Windows & Views
        services.AddTransient<LoginWindow>();
        services.AddTransient<MainShellWindow>();
        services.AddTransient<CheckInView>();
        services.AddTransient<CheckOutView>();
        services.AddTransient<RecentEntriesView>();
        services.AddTransient<AnalyticsView>();
        services.AddTransient<ShiftClosureView>();
        services.AddTransient<MonthlySubscriptionsView>();
        services.AddTransient<CustomersView>();
        services.AddTransient<DeviceActivationDialog>();
        services.AddTransient<AppUpdateDialog>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            if (_singleInstanceMutex != null)
            {
                _singleInstanceMutex.ReleaseMutex();
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
            }
        }
        catch { }
        base.OnExit(e);
    }

    private bool _isTransitioningToLogin = false;

    private void ShowLoginWindow()
    {
        // Cerrar cualquier ventana residual previa para garantizar una única ventana visible
        foreach (Window w in Current.Windows)
        {
            if (w is not LoginWindow)
            {
                try { w.Close(); } catch { }
            }
        }

        var loginWindow = _serviceProvider.GetRequiredService<LoginWindow>();
        var loginViewModel = _serviceProvider.GetRequiredService<LoginViewModel>();

        bool isNavigatingToShell = false;
        loginViewModel.LoginSuccessful += () =>
        {
            isNavigatingToShell = true;
            ShowMainShellWindow();
            try { loginWindow.Close(); } catch { }
        };

        loginWindow.Closed += (s, e) =>
        {
            // Si el usuario cerró la ventana de login sin haber iniciado sesión y no hay ventanas visibles, salir
            if (!isNavigatingToShell)
            {
                bool hasOtherWindows = false;
                foreach (Window w in Current.Windows)
                {
                    if (w != loginWindow && w.IsVisible)
                    {
                        hasOtherWindows = true;
                        break;
                    }
                }
                if (!hasOtherWindows)
                {
                    Shutdown(0);
                }
            }
        };

        loginWindow.DataContext = loginViewModel;
        MainWindow = loginWindow;
        loginWindow.Show();
    }

    private void OnShellLogoutRequested()
    {
        _isTransitioningToLogin = true;
        try
        {
            ShowLoginWindow();
        }
        finally
        {
            _isTransitioningToLogin = false;
        }
    }

    private async void ShowMainShellWindow()
    {
        // Cerrar ventanas de login existentes antes de mostrar la terminal
        foreach (Window w in Current.Windows)
        {
            if (w is LoginWindow)
            {
                try { w.Close(); } catch { }
            }
        }

        var shellWindow = _serviceProvider.GetRequiredService<MainShellWindow>();
        var shellViewModel = _serviceProvider.GetRequiredService<MainShellViewModel>();

        // Desuscribir previamente para evitar acumulación de delegados en MainShellViewModel (Singleton)
        shellViewModel.LogoutRequested -= OnShellLogoutRequested;
        shellViewModel.LogoutRequested += OnShellLogoutRequested;

        shellWindow.Closed += (s, e) =>
        {
            // Si la terminal se cerró por el usuario (Alt+F4 o botón salir) y no por un logout hacia login, salir
            if (!_isTransitioningToLogin)
            {
                Shutdown(0);
            }
        };

        shellWindow.DataContext = shellViewModel;
        MainWindow = shellWindow;
        shellWindow.Show();

        await shellViewModel.InitializeAsync();
    }

    #region Manejo Global de Excepciones y Registro de Errores

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // 1. Evitar que la aplicación se cierre abruptamente
        e.Handled = true;

        LogException(e.Exception, "WPF.DispatcherUnhandledException");

        try
        {
            var dialogService = _serviceProvider?.GetService<IDialogService>();
            if (dialogService != null && MainWindow != null && MainWindow.IsVisible)
            {
                _ = dialogService.ShowAlertAsync(
                    "Novedad en la Aplicación",
                    $"Se ha presentado una excepción no controlada:\n\n{e.Exception.Message}\n\nEl sistema ha registrado el detalle en el archivo de registro de errores para su diagnóstico.",
                    DialogNotificationType.Warning);
                return;
            }
        }
        catch { }

        // Fallback nativo
        MessageBox.Show(
            $"Se ha presentado una novedad no controlada en la aplicación:\n\n{e.Exception.Message}\n\nEl sistema continuará ejecutándose. El detalle técnico ha sido guardado en la carpeta de Logs.",
            "ParkFlow - Novedad del Sistema",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        // Marcar la excepción como observada para evitar terminación del proceso
        e.SetObserved();
        LogException(e.Exception, "TaskScheduler.UnobservedTaskException");
    }

    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LogException(ex, $"AppDomain.UnhandledException (IsTerminating: {e.IsTerminating})");
        }
    }

    public static void LogException(Exception ex, string source)
    {
        try
        {
            lock (_logLock)
            {
                var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }

                var logFilePath = Path.Combine(logDir, $"ErrorLog_{DateTime.Now:yyyyMMdd}.txt");
                var sb = new StringBuilder();
                sb.AppendLine("================================================================================");
                sb.AppendLine($"FECHA / HORA : {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                sb.AppendLine($"ORIGEN       : {source}");
                sb.AppendLine($"TIPO         : {ex.GetType().FullName}");
                sb.AppendLine($"MENSAJE      : {ex.Message}");
                if (ex.InnerException != null)
                {
                    sb.AppendLine($"INNER EXCP   : {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}");
                }
                sb.AppendLine("STACK TRACE  :");
                sb.AppendLine(ex.StackTrace);
                sb.AppendLine("================================================================================");
                sb.AppendLine();

                File.AppendAllText(logFilePath, sb.ToString(), Encoding.UTF8);
            }
        }
        catch
        {
            // Ignorar errores al escribir logs
        }
    }

    #endregion
}
