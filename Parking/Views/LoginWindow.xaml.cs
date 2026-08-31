using System.Windows;
using System.Windows.Input;
using Parking.ViewModels;

namespace Parking.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
        {
            UserPasswordBox.Password = vm.Password;
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void UserPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
        {
            vm.Password = UserPasswordBox.Password;
        }
    }

    private bool _isPasswordVisible = false;

    private void TogglePasswordButton_Click(object sender, RoutedEventArgs e)
    {
        _isPasswordVisible = !_isPasswordVisible;
        if (_isPasswordVisible)
        {
            // Show plain text
            VisiblePasswordTextBox.Text = UserPasswordBox.Password;
            VisiblePasswordTextBox.Visibility = Visibility.Visible;
            UserPasswordBox.Visibility = Visibility.Collapsed;
            TogglePasswordIcon.Data = (System.Windows.Media.Geometry)FindResource("IconEyeOff");
        }
        else
        {
            // Show password box
            UserPasswordBox.Password = VisiblePasswordTextBox.Text;
            VisiblePasswordTextBox.Visibility = Visibility.Collapsed;
            UserPasswordBox.Visibility = Visibility.Visible;
            TogglePasswordIcon.Data = (System.Windows.Media.Geometry)FindResource("IconEye");
        }
    }

    private void VisiblePasswordTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_isPasswordVisible && DataContext is LoginViewModel vm)
        {
            vm.Password = VisiblePasswordTextBox.Text;
        }
    }

    private void UserPasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is LoginViewModel vm)
        {
            vm.Password = _isPasswordVisible ? VisiblePasswordTextBox.Text : UserPasswordBox.Password;
            if (vm.LoginCommand.CanExecute(null))
            {
                vm.LoginCommand.Execute(null);
            }
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
