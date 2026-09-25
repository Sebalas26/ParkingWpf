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
            DataContextChanged += CheckOutDialog_DataContextChanged;
            Closed += (s, e) =>
            {
                if (DataContext is Parking.ViewModels.CheckOutViewModel checkoutVm)
                {
                    checkoutVm.ExitNotes = string.Empty;
                }
                if (DataContext is System.ComponentModel.INotifyPropertyChanged vm)
                {
                    vm.PropertyChanged -= ViewModel_PropertyChanged;
                }
            };
        }

        private void CheckOutDialog_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is System.ComponentModel.INotifyPropertyChanged oldVm)
            {
                oldVm.PropertyChanged -= ViewModel_PropertyChanged;
            }
            if (e.NewValue is System.ComponentModel.INotifyPropertyChanged newVm)
            {
                newVm.PropertyChanged += ViewModel_PropertyChanged;
            }
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Parking.ViewModels.CheckOutViewModel.SelectedAgreement))
            {
                if (DataContext is Parking.ViewModels.CheckOutViewModel vm && vm.SelectedAgreement != null)
                {
                    Dispatcher.InvokeAsync(async () =>
                    {
                        await System.Threading.Tasks.Task.Delay(100);
                        DialogScrollViewer?.ScrollToVerticalOffset(DialogScrollViewer.VerticalOffset + 180);
                    });
                }
            }
            else if (e.PropertyName == nameof(Parking.ViewModels.CheckOutViewModel.SelectedResolution))
            {
                if (DataContext is Parking.ViewModels.CheckOutViewModel vm && vm.SelectedResolution != null)
                {
                    Dispatcher.InvokeAsync(async () =>
                    {
                        await System.Threading.Tasks.Task.Delay(100);
                        DialogScrollViewer?.ScrollToVerticalOffset(DialogScrollViewer.VerticalOffset + 180);
                    });
                }
            }
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
            // Close the dialog ONLY if the user clicks directly on the dark backdrop
            if (e.OriginalSource == sender)
            {
                this.Close();
            }
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