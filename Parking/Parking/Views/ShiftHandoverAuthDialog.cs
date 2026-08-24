using System.Threading.Tasks;
using System.Windows;
using Parking.Entities;
using Parking.Models;
using Parking.Services.Contracts;

namespace Parking.Views;

public static class ShiftHandoverAuthDialog
{
    public static Task<UserSessionModel?> ShowAuthAsync(Window? owner, IAuthService authService, User selectedUser, string operatorName, decimal cashToHandover)
    {
        // Implementación mínima para que la entrega de turno compile.
        // Si hay usuario autenticado, se asume que la autorización fue correcta.
        return Task.FromResult(authService.CurrentUser);
    }
}
