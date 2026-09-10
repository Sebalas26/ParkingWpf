using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Parking.Core.Helpers;

/// \u003csummary\u003e
/// Helper y Attached Property para TextBox que ajusta autom\u00e1ticamente el tama\u00f1o de fuente
/// (Auto-Shrink) cuando el texto es largo o el ancho disponible de pantalla se reduce.
/// \u003c/summary\u003e
public static class AutoShrinkFontHelper
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(AutoShrinkFontHelper),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty MaxFontSizeProperty =
        DependencyProperty.RegisterAttached(
            "MaxFontSize",
            typeof(double),
            typeof(AutoShrinkFontHelper),
            new PropertyMetadata(84.0, OnFontSizeBoundaryChanged));

    public static readonly DependencyProperty MinFontSizeProperty =
        DependencyProperty.RegisterAttached(
            "MinFontSize",
            typeof(double),
            typeof(AutoShrinkFontHelper),
            new PropertyMetadata(28.0, OnFontSizeBoundaryChanged));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    public static double GetMaxFontSize(DependencyObject obj) => (double)obj.GetValue(MaxFontSizeProperty);
    public static void SetMaxFontSize(DependencyObject obj, double value) => obj.SetValue(MaxFontSizeProperty, value);

    public static double GetMinFontSize(DependencyObject obj) => (double)obj.GetValue(MinFontSizeProperty);
    public static void SetMinFontSize(DependencyObject obj, double value) => obj.SetValue(MinFontSizeProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox textBox) return;

        textBox.TextChanged -= OnTextBoxTextChanged;
        textBox.SizeChanged -= OnTextBoxSizeChanged;
        textBox.Loaded -= OnTextBoxLoaded;

        if (e.NewValue is true)
        {
            textBox.TextChanged += OnTextBoxTextChanged;
            textBox.SizeChanged += OnTextBoxSizeChanged;
            textBox.Loaded += OnTextBoxLoaded;

            if (textBox.IsLoaded && textBox.ActualWidth > 0)
            {
                AdjustFontSize(textBox);
            }
        }
    }

    private static void OnFontSizeBoundaryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextBox textBox && GetIsEnabled(textBox) && textBox.IsLoaded && textBox.ActualWidth > 0)
        {
            AdjustFontSize(textBox);
        }
    }

    private static void OnTextBoxLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            AdjustFontSize(textBox);
        }
    }

    private static void OnTextBoxTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            AdjustFontSize(textBox);
        }
    }

    private static void OnTextBoxSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is TextBox textBox && (e.WidthChanged || e.PreviousSize.Width <= 0))
        {
            AdjustFontSize(textBox);
        }
    }

    /// \u003csummary\u003e
    /// Ajusta din\u00e1micamente la propiedad FontSize del TextBox en funci\u00f3n del texto actual y el ancho disponible.
    /// \u003c/summary\u003e
    public static void AdjustFontSize(TextBox textBox)
    {
        if (textBox == null || !GetIsEnabled(textBox)) return;

        double maxFontSize = GetMaxFontSize(textBox);
        if (double.IsNaN(maxFontSize) || maxFontSize <= 0) maxFontSize = 84.0;

        double minFontSize = GetMinFontSize(textBox);
        if (double.IsNaN(minFontSize) || minFontSize <= 0) minFontSize = 28.0;

        string text = textBox.Text ?? string.Empty;
        if (string.IsNullOrEmpty(text))
        {
            if (Math.Abs(textBox.FontSize - maxFontSize) > 0.1)
            {
                textBox.FontSize = maxFontSize;
            }
            return;
        }

        if (textBox.ActualWidth <= 0) return;

        double horizontalPadding = textBox.Padding.Left + textBox.Padding.Right;
        double horizontalBorder = textBox.BorderThickness.Left + textBox.BorderThickness.Right;
        // Margen defensivo interno (24px) para acomodar cursor (caret), efectos de sombra y separaci\u00f3n est\u00e9tica
        double availableWidth = textBox.ActualWidth - horizontalPadding - horizontalBorder - 24;
        if (availableWidth <= 20) return;

        double pixelsPerDip = 1.0;
        try
        {
            pixelsPerDip = VisualTreeHelper.GetDpi(textBox).PixelsPerDip;
        }
        catch
        {
            pixelsPerDip = 1.0;
        }

        double optimalFontSize = CalculateFittingFontSize(
            text,
            availableWidth,
            maxFontSize,
            minFontSize,
            textBox.FontFamily,
            textBox.FontWeight,
            textBox.FontStyle,
            textBox.FontStretch,
            textBox.FlowDirection,
            pixelsPerDip);

        if (Math.Abs(textBox.FontSize - optimalFontSize) > 0.1)
        {
            textBox.FontSize = optimalFontSize;
        }

        // Asegura que el texto no quede desplazado horizontalmente si previamente sobrepasaba el ancho
        textBox.ScrollToHome();
    }

    /// \u003csummary\u003e
    /// M\u00e9todo puro y testeable que calcula el tama\u00f1o de fuente \u00f3ptimo para que el texto quepa en el ancho disponible.
    /// \u003c/summary\u003e
    public static double CalculateFittingFontSize(
        string text,
        double availableWidth,
        double maxFontSize,
        double minFontSize,
        FontFamily fontFamily,
        FontWeight fontWeight,
        FontStyle fontStyle,
        FontStretch fontStretch,
        FlowDirection flowDirection,
        double pixelsPerDip = 1.0)
    {
        if (string.IsNullOrEmpty(text) || availableWidth <= 0)
        {
            return maxFontSize;
        }

        if (minFontSize > maxFontSize)
        {
            minFontSize = maxFontSize;
        }

        var typeface = new Typeface(fontFamily, fontStyle, fontWeight, fontStretch);

        double Measure(double size)
        {
            var formattedText = new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                flowDirection,
                typeface,
                size,
                Brushes.Black,
                pixelsPerDip);

            return formattedText.WidthIncludingTrailingWhitespace;
        }

        double textWidthAtMax = Measure(maxFontSize);
        if (textWidthAtMax <= availableWidth)
        {
            return maxFontSize;
        }

        // Estimaci\u00f3n proporcional r\u00e1pida
        double ratio = availableWidth / textWidthAtMax;
        double estimatedSize = Math.Floor(maxFontSize * ratio);
        estimatedSize = Math.Clamp(estimatedSize, minFontSize, maxFontSize);

        // Ajuste fino descendente en caso de variaciones menores en el rasterizado o kerning
        while (estimatedSize > minFontSize && Measure(estimatedSize) > availableWidth)
        {
            estimatedSize -= 1.0;
        }

        return Math.Max(minFontSize, estimatedSize);
    }
}
