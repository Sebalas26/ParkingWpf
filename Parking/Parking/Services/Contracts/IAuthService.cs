using System;
using System.Threading.Tasks;
using Parking.Models;

namespace Parking.Services.Contracts;

public interface IAuthService
{
    event Action<UserSessionModel?>? UserSessionChanged;
    UserSessionModel? CurrentUser { get; }
    bool IsAuthenticated { get; }
    Task<bool> LoginAsync(string username, string password);
    Task<UserSessionModel?> ValidateCredentialsAsync(string username, string password);
    Task<UserSessionModel?> ValidateAdminAuthorizationAsync(string adminPasswordOrPin);
    void SwitchCurrentUser(UserSessionModel newUser);
    Task LogoutAsync();
}

