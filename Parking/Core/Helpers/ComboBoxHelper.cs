using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Parking.Core.Helpers;

/// <summary>
/// Helper y Attached Property para ComboBox que deshabilita el cambio de selección
/// accidental mediante la rueda del ratón (MouseWheel) cuando el menú desplegable está cerrado,
/// propagando el evento al contenedor padre (ScrollViewer).
/// </summary>
public static class ComboBoxHelper
{
    public static readonly DependencyProperty DisableWheelWhenClosedProperty =
        DependencyProperty.RegisterAttached(
            "DisableWheelWhenClosed",
            typeof(bool),
            typeof(ComboBoxHelper),
            new UIPropertyMetadata(false, OnDisableWheelWhenClosedChanged));

    public static bool GetDisableWheelWhenClosed(DependencyObject obj) =>
        (bool)obj.GetValue(DisableWheelWhenClosedProperty);

    public static void SetDisableWheelWhenClosed(DependencyObject obj, bool value) =>
        obj.SetValue(DisableWheelWhenClosedProperty, value);

    private static void OnDisableWheelWhenClosedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ComboBox comboBox)
        {
            comboBox.PreviewMouseWheel -= ComboBox_PreviewMouseWheel;
            if ((bool)e.NewValue)
            {
                comboBox.PreviewMouseWheel += ComboBox_PreviewMouseWheel;
            }
        }
    }

    private static void ComboBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ComboBox cb && !cb.IsDropDownOpen)
        {
            e.Handled = true;

            // Propagar el evento al contenedor padre para que el ScrollViewer principal continúe desplazando
            var parent = (VisualTreeHelper.GetParent(cb) ?? cb.Parent) as UIElement;
            parent?.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = sender
            });
        }
    }
}
