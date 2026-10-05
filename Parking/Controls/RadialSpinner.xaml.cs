using System.Windows.Controls;

namespace Parking.Controls;

/// <summary>
/// Indicador de carga radial de 12 segmentos idéntico al de la PWA.
/// Utiliza animación de rotación discreta (steps de 30 grados) y segmentos con opacidades decrecientes.
/// </summary>
public partial class RadialSpinner : UserControl
{
    public RadialSpinner()
    {
        InitializeComponent();
    }
}
