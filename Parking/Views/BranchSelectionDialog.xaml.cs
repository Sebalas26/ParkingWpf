using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using Parking.Models;

namespace Parking.Views;

public partial class BranchSelectionDialog : Window
{
    public BranchModel? SelectedBranch { get; private set; }

    public BranchSelectionDialog(IEnumerable<BranchModel> branches)
    {
        InitializeComponent();
        BranchesList.ItemsSource = branches;
    }

    private void OnBranchCardClicked(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is BranchModel branch)
        {
            SelectedBranch = branch;
            DialogResult = true;
            Close();
        }
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        SelectedBranch = null;
        DialogResult = false;
        Close();
    }
}
