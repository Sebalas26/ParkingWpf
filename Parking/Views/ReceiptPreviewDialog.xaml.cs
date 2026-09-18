using System.Windows;
using System.Windows.Input;
using Parking.ViewModels;

namespace Parking.Views;

public partial class ReceiptPreviewDialog : Window
{
    public ReceiptPreviewDialog()
    {
        InitializeComponent();
        Loaded += ReceiptPreviewDialog_Loaded;
        PreviewKeyDown += ReceiptPreviewDialog_PreviewKeyDown;
    }

    private async void ReceiptPreviewDialog_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Return)
        {
            e.Handled = true;
            if (DataContext is ReceiptPreviewViewModel vm)
            {
                if (vm.PrintTicketCommand.CanExecute(null))
                {
                    await vm.PrintTicketCommand.ExecuteAsync(null);
                }
            }
            Close();
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }

    private void ReceiptPreviewDialog_Loaded(object sender, RoutedEventArgs e)
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

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
