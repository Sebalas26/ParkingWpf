using System.Windows;

namespace Parking.Core.Helpers;

/// <summary>
/// Helper y Attached Property para botones que gestiona el estado de carga (IsLoading).
/// Permite conmutar visualmente entre el contenido del botón y el RadialSpinner sin deshabilitar
/// visualmente el botón a opacidad 0.45, manteniendo la consistencia visual y bloqueando clics repetidos.
/// </summary>
public static class ButtonHelper
{
    public static readonly DependencyProperty IsLoadingProperty =
        DependencyProperty.RegisterAttached(
            "IsLoading",
            typeof(bool),
            typeof(ButtonHelper),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static bool GetIsLoading(DependencyObject obj) =>
        (bool)obj.GetValue(IsLoadingProperty);

    public static void SetIsLoading(DependencyObject obj, bool value) =>
        obj.SetValue(IsLoadingProperty, value);
}
