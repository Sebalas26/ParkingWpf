using System;
using System.IO;
using System.Net.Http;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

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

        ShowLoginWindow();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton(_configuration);

        var apiBaseUrl = _configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7023";

        services.AddSingleton(sp =>
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };
            return new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30)
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

        services.AddSingleton<IDbConnectionManager, DbConnectionManager>();
        services.AddSingleton<IThemeService, ThemeService>();

        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<IStoreService, StoreService>();
        services.AddSingleton<IAgreementService, AgreementService>();
        services.AddSingleton<IPricingCalculatorService, EfPricingCalculatorService>();
        services.AddSingleton<IParkingTicketService, EfParkingTicketService>();
        services.AddSingleton<IReceiptPrinterService, MockReceiptPrinterService>();
        services.AddSingleton<IAnalyticsService, EfAnalyticsService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IDialogService, DialogService>();

        // ViewModels
        services.AddTransient<LoginViewModel>();
        services.AddSingleton<MainShellViewModel>();
        services.AddTransient<CheckInViewModel>();
        services.AddTransient<CheckOutViewModel>();
        services.AddTransient<RecentEntriesViewModel>();
        services.AddTransient<AnalyticsViewModel>();
        services.AddTransient<ReceiptPreviewViewModel>();

        // Windows & Views
        services.AddTransient<LoginWindow>();
        services.AddTransient<MainShellWindow>();
        services.AddTransient<CheckInView>();
        services.AddTransient<CheckOutView>();
        services.AddTransient<RecentEntriesView>();
        services.AddTransient<AnalyticsView>();
    }

    private void ShowLoginWindow()
    {
        var loginWindow = _serviceProvider.GetRequiredService<LoginWindow>();
        var loginViewModel = _serviceProvider.GetRequiredService<LoginViewModel>();

        loginViewModel.LoginSuccessful += () =>
        {
            ShowMainShellWindow();
            loginWindow.Close();
        };

        loginWindow.DataContext = loginViewModel;
        MainWindow = loginWindow;
        loginWindow.Show();
    }

    private async void ShowMainShellWindow()
    {
        var shellWindow = _serviceProvider.GetRequiredService<MainShellWindow>();
        var shellViewModel = _serviceProvider.GetRequiredService<MainShellViewModel>();

        shellViewModel.LogoutRequested += () =>
        {
            ShowLoginWindow();
            shellWindow.Close();
        };

        shellWindow.DataContext = shellViewModel;
        MainWindow = shellWindow;
        shellWindow.Show();

        await shellViewModel.InitializeAsync();
    }
}
