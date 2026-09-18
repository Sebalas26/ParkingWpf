using System.Windows;
using Parking.ViewModels;

namespace Parking.Views;

public partial class DeviceActivationDialog : Window
{
    public DeviceActivationDialog(DeviceActivationViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.ActivationCompleted += () =>
        {
            DialogResult = true;
            Close();
        };

        viewModel.CancelRequested += () =>
        {
            DialogResult = false;
            Close();
        };
    }
}
