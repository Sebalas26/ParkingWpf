using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Parking.ViewModels;

namespace Parking.Views;

public partial class CheckInView : UserControl
{
    public CheckInView()
    {
        InitializeComponent();
        Loaded += CheckInView_Loaded;
        IsVisibleChanged += CheckInView_IsVisibleChanged;
        DataContextChanged += CheckInView_DataContextChanged;
    }

    private void CheckInView_Loaded(object sender, RoutedEventArgs e)
    {
        FocusPlateTextBox();
    }

    private void CheckInView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            FocusPlateTextBox();
        }
    }

    private void CheckInView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is INotifyPropertyChanged oldVm)
        {
            oldVm.PropertyChanged -= ViewModel_PropertyChanged;
        }
        if (e.NewValue is INotifyPropertyChanged newVm)
        {
            newVm.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "PlateNumber" || e.PropertyName == "IsVirtualKeyboardVisible")
        {
            FocusPlateTextBox();
        }
    }

    private void FocusPlateTextBox()
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            if (PlateTextBox != null && PlateTextBox.IsVisible && PlateTextBox.IsEnabled)
            {
                PlateTextBox.Focus();
                Keyboard.Focus(PlateTextBox);
            }
        }));
    }

    private void PlateTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Return)
        {
            if (DataContext is CheckInViewModel vm && vm.RegisterAndPrintCommand.CanExecute(null))
            {
                vm.RegisterAndPrintCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}
