using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Parking.Views;

public partial class CheckOutView : UserControl
{
    public CheckOutView()
    {
        InitializeComponent();
        Loaded += CheckOutView_Loaded;
        IsVisibleChanged += CheckOutView_IsVisibleChanged;
        DataContextChanged += CheckOutView_DataContextChanged;
    }

    private void CheckOutView_Loaded(object sender, RoutedEventArgs e)
    {
        FocusSearchTextBox();
    }

    private void CheckOutView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            FocusSearchTextBox();
        }
    }

    private void CheckOutView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
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
        if (e.PropertyName == "SearchQuery" || e.PropertyName == "SelectedTicket")
        {
            FocusSearchTextBox();
        }
    }

    private void FocusSearchTextBox()
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            if (SearchTextBox != null && SearchTextBox.IsVisible && SearchTextBox.IsEnabled)
            {
                SearchTextBox.Focus();
                Keyboard.Focus(SearchTextBox);
            }
        }));
    }
}
