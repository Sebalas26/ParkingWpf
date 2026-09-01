using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Parking.Core.Enums;

namespace Parking.Views;

public partial class ModernMessageDialog : Window
{
    public bool UserConfirmed { get; private set; }

    public ModernMessageDialog()
    {
        InitializeComponent();
        Loaded += ModernMessageDialog_Loaded;
    }

    private void ModernMessageDialog_Loaded(object sender, RoutedEventArgs e)
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

    public static void ShowAlert(
        Window? owner,
        string title,
        string message,
        DialogNotificationType type = DialogNotificationType.Information,
        string buttonText = "Entendido")
    {
        var dialog = new ModernMessageDialog();
        if (owner != null && owner.IsVisible)
        {
            dialog.Owner = owner;
        }
        else if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
        {
            dialog.Owner = Application.Current.MainWindow;
        }

        dialog.Configure(title, message, type, isConfirmation: false, confirmText: buttonText, cancelText: string.Empty);
        dialog.ShowDialog();
    }

    public static bool ShowConfirmation(
        Window? owner,
        string title,
        string message,
        DialogNotificationType type = DialogNotificationType.Question,
        string confirmText = "Confirmar",
        string cancelText = "Cancelar")
    {
        var dialog = new ModernMessageDialog();
        if (owner != null && owner.IsVisible)
        {
            dialog.Owner = owner;
        }
        else if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
        {
            dialog.Owner = Application.Current.MainWindow;
        }

        dialog.Configure(title, message, type, isConfirmation: true, confirmText: confirmText, cancelText: cancelText);
        dialog.ShowDialog();
        return dialog.UserConfirmed;
    }

    public void Configure(
        string title,
        string message,
        DialogNotificationType type,
        bool isConfirmation,
        string confirmText,
        string cancelText)
    {
        TitleTextBlock.Text = title;
        MessageTextBlock.Text = message;
        ConfirmButton.Content = string.IsNullOrWhiteSpace(confirmText) ? "Entendido" : confirmText;

        if (isConfirmation)
        {
            CancelButton.Visibility = Visibility.Visible;
            CancelButton.Content = string.IsNullOrWhiteSpace(cancelText) ? "Cancelar" : cancelText;
        }
        else
        {
            CancelButton.Visibility = Visibility.Collapsed;
        }

        ApplyNotificationStyle(type);
    }

    private void ApplyNotificationStyle(DialogNotificationType type)
    {
        try
        {
            switch (type)
            {
                case DialogNotificationType.Success:
                    CategoryTextBlock.Text = "Operación Exitosa";
                    IconBadgeBorder.Background = (Brush)FindResource("BrushSuccessBg");
                    IconPath.Data = (Geometry)FindResource("IconCheck");
                    IconPath.Fill = (Brush)FindResource("BrushSuccess");
                    ConfirmButton.Style = (Style)FindResource("SuccessButton");
                    break;

                case DialogNotificationType.Warning:
                    CategoryTextBlock.Text = "Atención / Advertencia";
                    IconBadgeBorder.Background = (Brush)FindResource("BrushWarningBg");
                    IconPath.Data = (Geometry)FindResource("IconWarning");
                    IconPath.Fill = (Brush)FindResource("BrushWarning");
                    ConfirmButton.Style = (Style)FindResource("ModernButton");
                    break;

                case DialogNotificationType.Error:
                    CategoryTextBlock.Text = "Error en Operación";
                    IconBadgeBorder.Background = (Brush)FindResource("BrushDangerBg");
                    IconPath.Data = (Geometry)FindResource("IconAlert");
                    IconPath.Fill = (Brush)FindResource("BrushDanger");
                    ConfirmButton.Style = (Style)FindResource("DangerButton");
                    break;

                case DialogNotificationType.Question:
                    CategoryTextBlock.Text = "Confirmación Requerida";
                    IconBadgeBorder.Background = (Brush)FindResource("BrushPrimaryLight");
                    IconPath.Data = (Geometry)FindResource("IconKey");
                    IconPath.Fill = (Brush)FindResource("BrushPrimary");
                    ConfirmButton.Style = (Style)FindResource("ModernButton");
                    break;

                case DialogNotificationType.Information:
                default:
                    CategoryTextBlock.Text = "Aviso del Sistema";
                    IconBadgeBorder.Background = (Brush)FindResource("BrushPrimaryLight");
                    IconPath.Data = (Geometry)FindResource("IconInfo");
                    IconPath.Fill = (Brush)FindResource("BrushPrimary");
                    ConfirmButton.Style = (Style)FindResource("ModernButton");
                    break;
            }
        }
        catch
        {
            // Fallback silencioso si no se encuentra algún recurso estático en tiempo de diseño
        }
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        UserConfirmed = true;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        UserConfirmed = false;
        DialogResult = false;
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        UserConfirmed = false;
        DialogResult = false;
        Close();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            UserConfirmed = false;
            DialogResult = false;
            Close();
        }
    }
}
