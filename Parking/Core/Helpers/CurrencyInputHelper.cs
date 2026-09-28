using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Parking.Core.Helpers;

/// <summary>
/// Helper y Attached Property para TextBox que proporciona formateo en vivo de moneda
/// en pesos colombianos ($ X.XXX), restringiendo entradas a dígitos y seleccionando todo al recibir foco.
/// </summary>
public static class CurrencyInputHelper
{
    private static readonly CultureInfo ColombianCulture = new("es-CO")
    {
        NumberFormat =
        {
            NumberGroupSeparator = ".",
            NumberDecimalSeparator = ",",
            CurrencySymbol = "$"
        }
    };

    public static readonly DependencyProperty IsCurrencyPesosProperty =
        DependencyProperty.RegisterAttached(
            "IsCurrencyPesos",
            typeof(bool),
            typeof(CurrencyInputHelper),
            new UIPropertyMetadata(false, OnIsCurrencyPesosChanged));

    public static bool GetIsCurrencyPesos(DependencyObject obj) =>
        (bool)obj.GetValue(IsCurrencyPesosProperty);

    public static void SetIsCurrencyPesos(DependencyObject obj, bool value) =>
        obj.SetValue(IsCurrencyPesosProperty, value);

    private static readonly DependencyProperty IsFormattingProperty =
        DependencyProperty.RegisterAttached(
            "IsFormatting",
            typeof(bool),
            typeof(CurrencyInputHelper),
            new UIPropertyMetadata(false));

    private static bool GetIsFormatting(DependencyObject obj) =>
        (bool)obj.GetValue(IsFormattingProperty);

    private static void SetIsFormatting(DependencyObject obj, bool value) =>
        obj.SetValue(IsFormattingProperty, value);

    private static void OnIsCurrencyPesosChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextBox textBox)
        {
            textBox.TextChanged -= TextBox_TextChanged;
            textBox.PreviewTextInput -= TextBox_PreviewTextInput;
            textBox.PreviewKeyDown -= TextBox_PreviewKeyDown;
            textBox.GotFocus -= TextBox_GotFocus;
            textBox.PreviewMouseDown -= TextBox_PreviewMouseDown;
            textBox.LostFocus -= TextBox_LostFocus;
            DataObject.RemovePastingHandler(textBox, TextBox_Pasting);

            if ((bool)e.NewValue)
            {
                textBox.TextChanged += TextBox_TextChanged;
                textBox.PreviewTextInput += TextBox_PreviewTextInput;
                textBox.PreviewKeyDown += TextBox_PreviewKeyDown;
                textBox.GotFocus += TextBox_GotFocus;
                textBox.PreviewMouseDown += TextBox_PreviewMouseDown;
                textBox.LostFocus += TextBox_LostFocus;
                DataObject.AddPastingHandler(textBox, TextBox_Pasting);

                FormatTextBoxText(textBox);
            }
        }
    }

    private static void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !e.Text.All(char.IsDigit);
    }

    private static void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            e.Handled = true;
        }
    }

    private static void TextBox_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetDataPresent(DataFormats.Text))
        {
            var text = e.DataObject.GetData(DataFormats.Text) as string;
            if (string.IsNullOrWhiteSpace(text) || !text.Any(char.IsDigit))
            {
                e.CancelCommand();
            }
        }
        else
        {
            e.CancelCommand();
        }
    }

    private static void TextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            textBox.SelectAll();
        }
    }

    private static void TextBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is TextBox textBox && !textBox.IsKeyboardFocusWithin)
        {
            textBox.Focus();
            textBox.SelectAll();
            e.Handled = true;
        }
    }

    private static void TextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            var raw = textBox.Text ?? string.Empty;
            var digitsOnly = new string(raw.Where(char.IsDigit).ToArray());
            if (string.IsNullOrEmpty(digitsOnly) || digitsOnly == "0")
            {
                SetIsFormatting(textBox, true);
                try
                {
                    textBox.Text = "$ 0";
                }
                finally
                {
                    SetIsFormatting(textBox, false);
                }
            }
        }
    }

    private static void TextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            if (GetIsFormatting(textBox)) return;
            FormatTextBoxText(textBox);
        }
    }

    private static void FormatTextBoxText(TextBox textBox)
    {
        if (GetIsFormatting(textBox)) return;

        var raw = textBox.Text ?? string.Empty;
        var digitsOnly = new string(raw.Where(char.IsDigit).ToArray());

        if (string.IsNullOrEmpty(digitsOnly))
        {
            return;
        }

        if (decimal.TryParse(digitsOnly, NumberStyles.None, CultureInfo.InvariantCulture, out var amount))
        {
            var formatted = $"$ {amount.ToString("N0", ColombianCulture)}";
            if (textBox.Text != formatted)
            {
                SetIsFormatting(textBox, true);
                try
                {
                    textBox.Text = formatted;
                    textBox.CaretIndex = formatted.Length;
                }
                finally
                {
                    SetIsFormatting(textBox, false);
                }
            }
        }
    }
}
