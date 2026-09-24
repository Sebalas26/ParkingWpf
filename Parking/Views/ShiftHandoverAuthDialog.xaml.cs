using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Parking.Entities;
using Parking.Models;
using Parking.Services.Contracts;

namespace Parking.Views;

public class ShiftHandoverAuthResult
{
    public UserSessionModel Session { get; set; } = null!;
    public decimal VerifiedCashAmount { get; set; }
}

public partial class ShiftHandoverAuthDialog : Window
{
    private readonly IAuthService _authService;
    private readonly User _selectedUser;
    private readonly decimal _expectedCash;

    public UserSessionModel? AuthenticatedSession { get; private set; }
    public decimal VerifiedCashAmount { get; private set; }

    public ShiftHandoverAuthDialog(
        IAuthService authService,
        User selectedUser,
        string operatorName,
        decimal expectedCash,
        decimal? initialCountedCash = null)
    {
        InitializeComponent();
        _authService = authService;
        _selectedUser = selectedUser;
        _expectedCash = expectedCash;

        OutgoingOperatorText.Text = operatorName;
        IncomingOperatorText.Text = selectedUser.FullName;
        IncomingUsernameText.Text = $"(@{selectedUser.Username})";
        ExpectedCashText.Text = expectedCash.ToString("C0");

        var initialCounted = initialCountedCash ?? expectedCash;
        VerifiedCashAmount = initialCounted;
        CashCountedTextBox.Text = initialCounted.ToString("N0");
        UpdateDifference(initialCounted);

        Loaded += ShiftHandoverAuthDialog_Loaded;
    }

    private void ShiftHandoverAuthDialog_Loaded(object sender, RoutedEventArgs e)
    {
        if (Owner != null)
        {
            if (Owner.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Maximized;
            }
            else
            {
                this.WindowState = WindowState.Normal;
                this.Left = Owner.Left;
                this.Top = Owner.Top;
                this.Width = Owner.ActualWidth;
                this.Height = Owner.ActualHeight;
            }
        }
        else if (Application.Current?.MainWindow != null && Application.Current.MainWindow.IsVisible)
        {
            var main = Application.Current.MainWindow;
            if (main.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Maximized;
            }
            else
            {
                this.WindowState = WindowState.Normal;
                this.Left = main.Left;
                this.Top = main.Top;
                this.Width = main.ActualWidth;
                this.Height = main.ActualHeight;
            }
        }
        else
        {
            this.WindowState = WindowState.Maximized;
        }

        ReceiverPasswordBox.Focus();
    }

    private void CashCountedTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var rawText = CashCountedTextBox.Text?.Replace(".", "").Replace(",", "").Replace("$", "").Trim();
        if (decimal.TryParse(rawText, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            VerifiedCashAmount = parsed;
            UpdateDifference(parsed);
        }
        else if (string.IsNullOrWhiteSpace(rawText))
        {
            VerifiedCashAmount = 0m;
            UpdateDifference(0m);
        }
    }

    private void UpdateDifference(decimal counted)
    {
        var diff = counted - _expectedCash;
        if (diff == 0)
        {
            CashDifferenceText.Text = "$ 0 (Cuadrado)";
            CashDifferenceText.Foreground = (Brush)FindResource("BrushSuccess");
        }
        else if (diff > 0)
        {
            CashDifferenceText.Text = $"+${diff:N0} (Sobrante)";
            CashDifferenceText.Foreground = (Brush)FindResource("BrushSuccess");
        }
        else
        {
            CashDifferenceText.Text = $"-${Math.Abs(diff):N0} (Faltante)";
            CashDifferenceText.Foreground = (Brush)FindResource("BrushDanger");
        }
    }

    public static async Task<ShiftHandoverAuthResult?> ShowAuthAsync(
        Window? owner,
        IAuthService authService,
        User selectedUser,
        string operatorName,
        decimal expectedCash,
        decimal? initialCountedCash = null)
    {
        var dialog = new ShiftHandoverAuthDialog(authService, selectedUser, operatorName, expectedCash, initialCountedCash);
        if (owner != null && owner.IsVisible)
        {
            dialog.Owner = owner;
        }
        else if (Application.Current?.MainWindow != null && Application.Current.MainWindow.IsVisible)
        {
            dialog.Owner = Application.Current.MainWindow;
        }

        var result = dialog.ShowDialog();
        if (result == true && dialog.AuthenticatedSession != null)
        {
            return new ShiftHandoverAuthResult
            {
                Session = dialog.AuthenticatedSession,
                VerifiedCashAmount = dialog.VerifiedCashAmount
            };
        }

        return null;
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

        if (VerifiedCashAmount < 0)
        {
            ShowError("El efectivo contado no puede ser un valor negativo.");
            CashCountedTextBox.Focus();
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
