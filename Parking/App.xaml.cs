using System;
using System.Windows;
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

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        var connectionManager = _serviceProvider.GetRequiredService<IDbConnectionManager>();
        await connectionManager.InitializeDatabaseAsync();

        ShowLoginWindow();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IDbConnectionManager, DbConnectionManager>();
        services.AddSingleton<IThemeService, ThemeService>();

        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<ISessionHeartbeatService, SessionHeartbeatService>();
        services.AddSingleton<IPermissionService, PermissionService>();
        services.AddSingleton<IUserRoleService, UserRoleService>();
        services.AddSingleton<IStoreService, StoreService>();
        services.AddSingleton<IAgreementService, AgreementService>();
        services.AddSingleton<IPricingCalculatorService, EfPricingCalculatorService>();
        services.AddSingleton<IParkingTicketService, EfParkingTicketService>();
        services.AddSingleton<IReceiptPrinterService, MockReceiptPrinterService>();
        services.AddSingleton<IAnalyticsService, EfAnalyticsService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IDialogService, DialogService>();

        services.AddTransient<LoginViewModel>();
        services.AddSingleton<MainShellViewModel>();
        services.AddTransient<CheckInViewModel>();
        services.AddTransient<CheckOutViewModel>();
        services.AddTransient<AnalyticsViewModel>();
        services.AddTransient<StoreSettingsViewModel>();
        services.AddTransient<AgreementSettingsViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<SecuritySettingsViewModel>();
        services.AddTransient<ReceiptPreviewViewModel>();

        services.AddTransient<LoginWindow>();
        services.AddTransient<MainShellWindow>();
        services.AddTransient<StoreSettingsView>();
        services.AddTransient<AgreementSettingsView>();
        services.AddTransient<SecuritySettingsView>();
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
