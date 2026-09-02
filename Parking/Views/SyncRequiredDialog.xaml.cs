using System;
using System.Threading.Tasks;
using System.Windows;
using Parking.Models.ApiModels;
using Parking.Services.Contracts;

namespace Parking.Views;

public partial class SyncRequiredDialog : Window
{
    private readonly ISyncEngineService _syncEngine;

    public bool SyncSuccessful { get; private set; }

    public SyncRequiredDialog(ConfigNotificationDto notification, ISyncEngineService syncEngine)
    {
        InitializeComponent();
        _syncEngine = syncEngine;

        if (!string.IsNullOrWhiteSpace(notification.Title))
        {
            TitleTextBlock.Text = notification.Title;
        }

        if (!string.IsNullOrWhiteSpace(notification.Message))
        {
            MessageTextBlock.Text = notification.Message;
        }

        Loaded += SyncRequiredDialog_Loaded;
    }

    private void SyncRequiredDialog_Loaded(object sender, RoutedEventArgs e)
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
    }

    public static async Task<bool> ShowDialogAsync(Window? owner, ConfigNotificationDto notification, ISyncEngineService syncEngine)
    {
        var dialog = new SyncRequiredDialog(notification, syncEngine);
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

    private async void SyncButton_Click(object sender, RoutedEventArgs e)
    {
        SyncButton.IsEnabled = false;
        try
        {
            // Ejecutar la sincronización completa usando el modal de progreso oficial
            var success = await SyncProgressDialog.ShowSyncAsync(this, _syncEngine);
            SyncSuccessful = success;
            if (success)
            {
                DialogResult = true;
                Close();
            }
            else
            {
                SyncButton.IsEnabled = true;
                SyncButton.Content = "Reintentar Sincronización";
            }
        }
        catch (Exception ex)
        {
            SyncButton.IsEnabled = true;
            MessageBox.Show($"Error al sincronizar: {ex.Message}", "Sincronización", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
