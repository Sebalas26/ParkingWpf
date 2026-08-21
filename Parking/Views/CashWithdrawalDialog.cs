using System.Threading.Tasks;
using System.Windows;
using Parking.Services.Contracts;

namespace Parking.Views;

public static class CashWithdrawalDialog
{
    public static Task<bool> ShowDialogAsync(Window? owner, IAuthService authService, IShiftService shiftService)
    {
        // Implementación mínima para compilar y permitir registrar retiros.
        // La lógica real puede reescribirse posteriormente con una ventana WPF propia.
        return Task.FromResult(true);
    }
}
