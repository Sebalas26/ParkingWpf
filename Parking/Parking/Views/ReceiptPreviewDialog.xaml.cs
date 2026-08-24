using System.Windows;
using System.Windows.Input;

namespace Parking.Views;

public partial class ReceiptPreviewDialog : Window
{
    public ReceiptPreviewDialog()
    {
        InitializeComponent();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
