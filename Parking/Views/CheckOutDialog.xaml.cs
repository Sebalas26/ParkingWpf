using System.Windows;
using System.Windows.Input;

namespace Parking.Views
{
    public partial class CheckOutDialog : Window
    {
        public CheckOutDialog()
        {
            InitializeComponent();
            Loaded += CheckOutDialog_Loaded;
        }

        private void CheckOutDialog_Loaded(object sender, RoutedEventArgs e)
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

        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Close the dialog if the user clicks the dark background outside the modal
            this.Close();
        }

        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Prevent the click on the card from bubbling up to the Grid
            e.Handled = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Close the dialog when "Cancelar Selección" is clicked
            this.Close();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Return)
            {
                // Forzar actualización inmediata del binding del TextBox con foco
                var focused = Keyboard.FocusedElement as System.Windows.Controls.TextBox;
                focused?.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();

                if (DataContext is Parking.ViewModels.CheckOutViewModel vm && vm.ProcessPaymentCommand.CanExecute(null))
                {
                    _ = vm.ProcessPaymentCommand.ExecuteAsync(null);
                    e.Handled = true;
                }
            }
        }
    }
}