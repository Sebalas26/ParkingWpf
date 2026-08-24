using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Parking.Entities;
using Parking.Models;
using Parking.Services.Contracts;

namespace Parking.Views;

public partial class ShiftHandoverAuthDialog : Window
{
    private readonly IAuthService _authService;
    private readonly User _selectedUser;

    public UserSessionModel? AuthenticatedSession { get; private set; }

    public ShiftHandoverAuthDialog(
        IAuthService authService,
        User selectedUser,
        string operatorName,
        decimal cashToHandover)
    {
        InitializeComponent();
        _authService = authService;
        _selectedUser = selectedUser;

        OutgoingOperatorText.Text = operatorName;
        IncomingOperatorText.Text = selectedUser.FullName;
        IncomingUsernameText.Text = $"(@{selectedUser.Username})";
        CashAmountText.Text = cashToHandover.ToString("C0");

        Loaded += (s, e) => ReceiverPasswordBox.Focus();
    }

    public static async Task<UserSessionModel?> ShowAuthAsync(
        Window? owner,
        IAuthService authService,
        User selectedUser,
        string operatorName,
        decimal cashToHandover)
    {
        var dialog = new ShiftHandoverAuthDialog(authService, selectedUser, operatorName, cashToHandover);
        if (owner != null && owner.IsVisible)
        {
            dialog.Owner = owner;
        }
        else if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
        {
            dialog.Owner = Application.Current.MainWindow;
        }

        dialog.ShowDialog();
        return dialog.AuthenticatedSession;
    }

    private async void ConfirmAuthButton_Click(object sender, RoutedEventArgs e)
    {
        await ProcessAuthenticationAsync();
    }

    private async void ReceiverPasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await ProcessAuthenticationAsync();
        }
    }

    private async Task ProcessAuthenticationAsync()
    {
        var password = ReceiverPasswordBox.Password;
        if (string.IsNullOrWhiteSpace(password))
        {
            ShowError("Debe ingresar la contraseña del operador receptor para autorizar.");
            ReceiverPasswordBox.Focus();
            return;
        }

        ConfirmAuthButton.IsEnabled = false;
        ErrorBannerBorder.Visibility = Visibility.Collapsed;

        try
        {
            var session = await _authService.ValidateCredentialsAsync(_selectedUser.Username, password);
            if (session == null)
            {
                ShowError($"La contraseña ingresada no es válida para el usuario '{_selectedUser.FullName}'. Por favor intente de nuevo.");
                ReceiverPasswordBox.SelectAll();
                ReceiverPasswordBox.Focus();
                return;
            }

            AuthenticatedSession = session;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError($"Error al validar credenciales: {ex.Message}");
        }
        finally
        {
            ConfirmAuthButton.IsEnabled = true;
        }
    }

    private void ShowError(string message)
    {
        ErrorMessageTextBlock.Text = message;
        ErrorBannerBorder.Visibility = Visibility.Visible;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        AuthenticatedSession = null;
        DialogResult = false;
        Close();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            AuthenticatedSession = null;
            DialogResult = false;
            Close();
        }
    }
}
