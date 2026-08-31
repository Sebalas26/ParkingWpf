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
    }
}