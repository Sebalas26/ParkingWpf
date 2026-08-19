using System.Threading.Tasks;
using Parking.Models;

namespace Parking.Services.Contracts;

public interface IAuthService
{
    UserSessionModel? CurrentUser { get; }
    bool IsAuthenticated { get; }
    Task<bool> LoginAsync(string username, string password);
    Task LogoutAsync();
}
