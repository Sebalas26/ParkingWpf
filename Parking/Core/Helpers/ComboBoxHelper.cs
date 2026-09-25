using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Parking.Core.Helpers;

/// <summary>
/// Helper y Attached Property para ComboBox que:
/// 1. Deshabilita el cambio de selección accidental mediante la rueda del ratón (MouseWheel) cuando el menú desplegable está cerrado.
/// 2. Evita que WPF seleccione/resalte todo el texto cuando el menú desplegable se abre programáticamente al escribir (AutoMoveCaretToEnd),
///    impidiendo la sobreescritura del primer caracter tipeado por el operador.
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

    public static readonly DependencyProperty AutoMoveCaretToEndProperty =
        DependencyProperty.RegisterAttached(
            "AutoMoveCaretToEnd",
            typeof(bool),
            typeof(ComboBoxHelper),
            new UIPropertyMetadata(false, OnAutoMoveCaretToEndChanged));

    public static bool GetAutoMoveCaretToEnd(DependencyObject obj) =>
        (bool)obj.GetValue(AutoMoveCaretToEndProperty);

    public static void SetAutoMoveCaretToEnd(DependencyObject obj, bool value) =>
        obj.SetValue(AutoMoveCaretToEndProperty, value);

    private static void OnAutoMoveCaretToEndChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ComboBox comboBox)
        {
            comboBox.DropDownOpened -= ComboBox_DropDownOpened;
            comboBox.Loaded -= ComboBox_LoadedForCaret;

            if ((bool)e.NewValue)
            {
                comboBox.DropDownOpened += ComboBox_DropDownOpened;
                comboBox.Loaded += ComboBox_LoadedForCaret;
                if (comboBox.IsLoaded)
                {
                    AttachTextBoxHandlers(comboBox);
                }
            }
        }
    }

    private static void ComboBox_LoadedForCaret(object sender, RoutedEventArgs e)
    {
        if (sender is ComboBox cb)
        {
            AttachTextBoxHandlers(cb);
        }
    }

    private static void AttachTextBoxHandlers(ComboBox cb)
    {
        if (cb.Template?.FindName("PART_EditableTextBox", cb) is TextBox tb)
        {
            tb.SelectionChanged -= TextBox_SelectionChangedOnDropDown;
            tb.SelectionChanged += TextBox_SelectionChangedOnDropDown;
        }
    }

    private static void ComboBox_DropDownOpened(object? sender, EventArgs e)
    {
        if (sender is ComboBox cb)
        {
            UnselectAndMoveCaretToEnd(cb);
        }
    }

    private static void TextBox_SelectionChangedOnDropDown(object sender, RoutedEventArgs e)
    {
        // Si el desplegable está abierto y todo el texto fue seleccionado automáticamente por WPF, mover el cursor al final
        if (sender is TextBox tb && tb.TemplatedParent is ComboBox cb && cb.IsDropDownOpen)
        {
            if (tb.SelectionLength > 0 && tb.SelectionLength == tb.Text.Length && !string.IsNullOrEmpty(tb.Text))
            {
                // Solo deseleccionar si no fue una selección intencional explícita por teclado (ej: Ctrl+A)
                if (!Keyboard.IsKeyDown(Key.A))
                {
                    tb.SelectionStart = tb.Text.Length;
                    tb.SelectionLength = 0;
                }
            }
        }
    }

    private static void UnselectAndMoveCaretToEnd(ComboBox cb)
    {
        void Action()
        {
            if (cb.Template?.FindName("PART_EditableTextBox", cb) is TextBox tb && !string.IsNullOrEmpty(tb.Text))
            {
                tb.SelectionStart = tb.Text.Length;
                tb.SelectionLength = 0;
            }
        }

        // Ejecutar síncronamente para neutralizar inmediatamente el SelectAll() de OnDropDownOpened
        Action();
        // Y encolar en el despachador para atender cualquier foco o evento pendiente
        cb.Dispatcher.BeginInvoke(new Action(Action), System.Windows.Threading.DispatcherPriority.Input);
    }
}
