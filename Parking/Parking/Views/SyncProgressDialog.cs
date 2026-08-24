using System.Threading.Tasks;
using System.Windows;
using Parking.Services.Contracts;

namespace Parking.Views;

public static class SyncProgressDialog
{
    public static Task<bool> ShowSyncAsync(Window? owner, ISyncEngineService syncEngine)
    {
        // Implementación mínima para mantener la interfaz compilando y funcional.
        // En una versión completa aquí se abriría la ventana de progreso real.
        return Task.FromResult(true);
    }
}
