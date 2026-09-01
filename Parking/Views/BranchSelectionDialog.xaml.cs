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
        Loaded += BranchSelectionDialog_Loaded;
    }

    private void BranchSelectionDialog_Loaded(object sender, RoutedEventArgs e)
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
