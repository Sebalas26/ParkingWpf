using System.Windows;
using Parking.ViewModels;

namespace Parking.Views;

public partial class AppUpdateDialog : Window
{
    public AppUpdateDialog(AppUpdateViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.UpdateCompleted += () =>
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
