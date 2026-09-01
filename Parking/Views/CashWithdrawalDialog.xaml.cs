using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Parking.Services.Contracts;

namespace Parking.Views;

public partial class CashWithdrawalDialog : Window
{
    private readonly IAuthService _authService;
    private readonly IShiftService _shiftService;

    public bool WithdrawalSuccess { get; private set; }

    public CashWithdrawalDialog(IAuthService authService, IShiftService shiftService)
    {
        InitializeComponent();
        _authService = authService;
        _shiftService = shiftService;

        Loaded += CashWithdrawalDialog_Loaded;
    }

    private void CashWithdrawalDialog_Loaded(object sender, RoutedEventArgs e)
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

        AmountTextBox.Focus();
    }

    public static async Task<bool> ShowDialogAsync(Window? owner, IAuthService authService, IShiftService shiftService)
    {
        var dialog = new CashWithdrawalDialog(authService, shiftService);
        if (owner != null && owner.IsVisible)
        {
            dialog.Owner = owner;
        }
        else if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
        {
            dialog.Owner = Application.Current.MainWindow;
        }

        dialog.ShowDialog();
        return dialog.WithdrawalSuccess;
    }

    private async void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        await ProcessWithdrawalAsync();
    }

    private async Task ProcessWithdrawalAsync()
    {
        ErrorBannerBorder.Visibility = Visibility.Collapsed;

        var amountText = AmountTextBox.Text?.Replace("$", "").Replace(",", "").Trim();
        if (!decimal.TryParse(amountText, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount) &&
            !decimal.TryParse(amountText, NumberStyles.Any, CultureInfo.CurrentCulture, out amount) ||
            amount <= 0)
        {
            ShowError("Debe ingresar un monto válido y mayor a $0 para el retiro.");
            AmountTextBox.Focus();
            return;
        }

        var reason = ReasonTextBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            ShowError("Debe indicar el motivo o concepto del retiro de efectivo.");
            ReasonTextBox.Focus();
            return;
        }

        var adminPassword = AdminPasswordBox.Password;
        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            ShowError("Debe ingresar la clave de un administrador para autorizar el retiro.");
            AdminPasswordBox.Focus();
            return;
        }

        ConfirmButton.IsEnabled = false;

        try
        {
            var adminUser = await _authService.ValidateAdminAuthorizationAsync(adminPassword);
            if (adminUser == null)
            {
                ShowError("La clave ingresada no corresponde a un administrador autorizado.");
                AdminPasswordBox.SelectAll();
                AdminPasswordBox.Focus();
                return;
            }

            var activeShift = await _shiftService.GetActiveShiftAsync();
            if (activeShift == null)
            {
                ShowError("No hay ningún turno activo para procesar el retiro.");
                return;
            }

            var cashierName = _authService.CurrentUser?.FullName ?? "Operador Cajero";
            await _shiftService.RegisterCashWithdrawalAsync(
                activeShift.ShiftId,
                amount,
                reason,
                adminUser.FullName,
                cashierName);

            WithdrawalSuccess = true;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError($"Error al registrar retiro: {ex.Message}");
        }
        finally
        {
            ConfirmButton.IsEnabled = true;
        }
    }

    private void ShowError(string message)
    {
        ErrorMessageTextBlock.Text = message;
        ErrorBannerBorder.Visibility = Visibility.Visible;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        WithdrawalSuccess = false;
        DialogResult = false;
        Close();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            WithdrawalSuccess = false;
            DialogResult = false;
            Close();
        }
    }
}
