using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Parking.Services.Contracts;

namespace Parking.Views;

public partial class SyncProgressDialog : Window
{
    private readonly ISyncEngineService _syncEngine;
    public bool SyncSuccessful { get; private set; }

    public SyncProgressDialog(ISyncEngineService syncEngine)
    {
        InitializeComponent();
        _syncEngine = syncEngine;
        Loaded += OnDialogLoaded;
    }

    public static async Task<bool> ShowSyncAsync(Window? owner, ISyncEngineService syncEngine)
    {
        var dialog = new SyncProgressDialog(syncEngine);
        if (owner != null && owner.IsVisible)
        {
            dialog.Owner = owner;
        }
        else if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
        {
            dialog.Owner = Application.Current.MainWindow;
        }

        dialog.ShowDialog();
        return dialog.SyncSuccessful;
    }

    private async void OnDialogLoaded(object sender, RoutedEventArgs e)
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

        var progress = new Progress<SyncProgressReport>(report =>
        {
            Dispatcher.Invoke(() =>
            {
                MainProgressBar.Value = Math.Clamp(report.Percentage, 0, 100);
                PercentageTextBlock.Text = $"{report.Percentage}%";
                StepTitleTextBlock.Text = report.CurrentStepTitle;
                DetailMessageTextBlock.Text = report.DetailMessage;
            });
        });

        try
        {
            var report = await _syncEngine.PerformFullSyncWithProgressAsync(progress);
            SyncSuccessful = report.Success;

            // Mostrar resumen estadístico
            TicketsCountText.Text = report.SyncedTicketsCount.ToString("N0");
            RatesCountText.Text = report.SyncedRatesCount.ToString("N0");
            AgreementsCountText.Text = report.SyncedAgreementsCount.ToString("N0");
            DispatchedCountText.Text = report.DispatchedOfflineItemsCount.ToString("N0");
            SummaryPanel.Visibility = Visibility.Visible;

            ResultBannerBorder.Visibility = Visibility.Visible;
            if (report.Success)
            {
                ResultBannerBorder.Background = (Brush)FindResource("BrushSuccessBg");
                ResultMessageTextBlock.Foreground = (Brush)FindResource("BrushSuccessText");
                ResultMessageTextBlock.Text = string.IsNullOrWhiteSpace(report.Message)
                    ? "✓ Todos los datos fueron sincronizados correctamente con el servidor central."
                    : $"✓ {report.Message}";

                StatusDot.Fill = (Brush)FindResource("BrushSuccess");
                StatusBadgeText.Text = "Sincronizado";
                StatusBadgeText.Foreground = (Brush)FindResource("BrushSuccess");
            }
            else
            {
                ResultBannerBorder.Background = (Brush)FindResource("BrushWarningBg");
                ResultMessageTextBlock.Foreground = (Brush)FindResource("BrushWarningText");
                ResultMessageTextBlock.Text = string.IsNullOrWhiteSpace(report.Message)
                    ? "⚠ El servidor no está disponible. Los datos locales se conservan de forma segura en la terminal."
                    : $"⚠ {report.Message}";

                StatusDot.Fill = (Brush)FindResource("BrushWarning");
                StatusBadgeText.Text = "Modo Offline Activo";
                StatusBadgeText.Foreground = (Brush)FindResource("BrushWarning");
            }
        }
        catch (Exception ex)
        {
            SyncSuccessful = false;
            ResultBannerBorder.Visibility = Visibility.Visible;
            ResultBannerBorder.Background = (Brush)FindResource("BrushDangerBg");
            ResultMessageTextBlock.Foreground = (Brush)FindResource("BrushDangerText");
            ResultMessageTextBlock.Text = $"Error durante la sincronización: {ex.Message}";

            StatusDot.Fill = (Brush)FindResource("BrushDanger");
            StatusBadgeText.Text = "Error de Conexión";
            StatusBadgeText.Foreground = (Brush)FindResource("BrushDanger");
        }
        finally
        {
            ActionButton.IsEnabled = true;
        }
    }

    private void ActionButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
