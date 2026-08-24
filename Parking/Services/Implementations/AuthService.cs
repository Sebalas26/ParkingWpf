using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Parking.Data.Factories;
using Parking.Entities;
using Parking.Models;
using Parking.Services.Contracts;

namespace Parking.Services.Implementations;

public class AuthService : IAuthService
{
    private readonly IDbConnectionManager _connectionManager;
    private readonly IApiClientService _apiClient;

    public event Action<UserSessionModel?>? UserSessionChanged;
    public UserSessionModel? CurrentUser { get; private set; }
    public bool IsAuthenticated => CurrentUser != null;

    public AuthService(
        IDbConnectionManager connectionManager,
        IApiClientService apiClient)
    {
        _connectionManager = connectionManager;
        _apiClient = apiClient;
    }

    public async Task<bool> LoginAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        var normalizedUser = username.Trim().ToLowerInvariant();

        // 1. Intentar autenticar contra el API
        try
        {
            var apiLogin = await _apiClient.LoginAsync(username, password);
            if (apiLogin != null && apiLogin.Success)
            {
                CurrentUser = new UserSessionModel
                {
                    UserId = apiLogin.UserId,
                    Username = apiLogin.Username,
                    FullName = apiLogin.FullName,
                    RoleName = apiLogin.RoleName,
                    SessionToken = apiLogin.Token ?? Guid.NewGuid().ToString(),
                    LoginTime = DateTime.Now
                };
                UserSessionChanged?.Invoke(CurrentUser);
                return true;
            }
        }
        catch
        {
            // Falla de red, continuar con verificación local
        }

        // 2. Autenticación contra Caché Local SQLite (Modo Offline)
        using var db = _connectionManager.CreateDbContext();
        var passwordHash = DbConnectionManager.HashPassword(password);

        var user = await db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Username.ToLower() == normalizedUser && u.IsActive);

        var isValidLocal = user != null && (
            user.PasswordHash == passwordHash ||
            user.PasswordHash == password
        );

        if (user == null || !isValidLocal)
        {
            return false;
        }

        var sessionToken = Guid.NewGuid().ToString();
        var newSession = new UserSession
        {
            SessionId = Guid.NewGuid(),
            UserId = user.UserId,
            SessionToken = sessionToken,
            DeviceIdentifier = Environment.MachineName,
            IpAddress = "127.0.0.1",
            StartedAtUtc = DateTime.UtcNow,
            LastHeartbeatUtc = DateTime.UtcNow,
            IsActive = true
        };

        db.UserSessions.Add(newSession);
        await db.SaveChangesAsync();

        CurrentUser = new UserSessionModel
        {
            UserId = user.UserId,
            Username = user.Username,
            FullName = user.FullName,
            RoleName = user.Role?.Name ?? "Operador",
            RoleId = user.RoleId,
            SessionToken = sessionToken,
            LoginTime = DateTime.Now
        };
        UserSessionChanged?.Invoke(CurrentUser);

        return true;
    }

    public async Task<UserSessionModel?> ValidateCredentialsAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)) return null;

        var normalizedUser = username.Trim().ToLowerInvariant();

        // 1. Intentar validar contra API
        try
        {
            var apiLogin = await _apiClient.LoginAsync(username, password);
            if (apiLogin != null && apiLogin.Success)
            {
                return new UserSessionModel
                {
                    UserId = apiLogin.UserId,
                    Username = apiLogin.Username,
                    FullName = apiLogin.FullName,
                    RoleName = apiLogin.RoleName,
                    SessionToken = apiLogin.Token ?? Guid.NewGuid().ToString(),
                    LoginTime = DateTime.Now
                };
            }
        }
        catch { }

        // 2. Validar contra BD Local SQLite
        using var db = _connectionManager.CreateDbContext();
        var passwordHash = DbConnectionManager.HashPassword(password);

        var user = await db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Username.ToLower() == normalizedUser && u.IsActive);

        var isValidLocal = user != null && (
            user.PasswordHash == passwordHash ||
            user.PasswordHash == password
        );

        if (user != null && isValidLocal)
        {
            return new UserSessionModel
            {
                UserId = user.UserId,
                Username = user.Username,
                FullName = user.FullName,
                RoleName = user.Role?.Name ?? "Operador",
                RoleId = user.RoleId,
                SessionToken = Guid.NewGuid().ToString(),
                LoginTime = DateTime.Now
            };
        }

        return null;
    }

    public async Task<UserSessionModel?> ValidateAdminAuthorizationAsync(string adminPasswordOrPin)
    {
        if (string.IsNullOrWhiteSpace(adminPasswordOrPin)) return null;

        var cleanPass = adminPasswordOrPin.Trim();
        var passwordHash = DbConnectionManager.HashPassword(cleanPass);

        using var db = _connectionManager.CreateDbContext();
        var adminUsers = await db.Users
            .Include(u => u.Role)
            .Where(u => u.IsActive && (u.Role.Name == "Administrador" || u.Role.Name == "Admin" || u.Role.RoleId == Guid.Parse("11111111-1111-1111-1111-111111111111")))
            .ToListAsync();

        foreach (var admin in adminUsers)
        {
            if (admin.PasswordHash == passwordHash || admin.PasswordHash == cleanPass || cleanPass == "Admin2026*" || cleanPass == "9988")
            {
                return new UserSessionModel
                {
                    UserId = admin.UserId,
                    Username = admin.Username,
                    FullName = admin.FullName,
                    RoleName = admin.Role?.Name ?? "Administrador",
                    RoleId = admin.RoleId,
                    SessionToken = Guid.NewGuid().ToString(),
                    LoginTime = DateTime.Now
                };
            }
        }

        // Si la clave ingresada es la clave maestra general de administración
        if (cleanPass == "Admin2026*" || cleanPass == "9988")
        {
            var firstAdmin = adminUsers.FirstOrDefault();
            return new UserSessionModel
            {
                UserId = firstAdmin?.UserId ?? Guid.NewGuid(),
                Username = firstAdmin?.Username ?? "admin",
                FullName = firstAdmin?.FullName ?? "Administrador Autorizante",
                RoleName = "Administrador",
                RoleId = firstAdmin?.RoleId ?? Guid.Parse("11111111-1111-1111-1111-111111111111"),
                SessionToken = Guid.NewGuid().ToString(),
                LoginTime = DateTime.Now
            };
        }

        return null;
    }

    public void SwitchCurrentUser(UserSessionModel newUser)
    {
        CurrentUser = newUser;
        UserSessionChanged?.Invoke(CurrentUser);
    }

    public async Task LogoutAsync()
    {
        if (CurrentUser != null)
        {
            try
            {
                await _apiClient.LogoutAsync();
            }
            catch { }

            using var db = _connectionManager.CreateDbContext();
            var session = await db.UserSessions
                .FirstOrDefaultAsync(s => s.SessionToken == CurrentUser.SessionToken);

            if (session != null)
            {
                session.IsActive = false;
                await db.SaveChangesAsync();
            }

            CurrentUser = null;
            UserSessionChanged?.Invoke(null);
        }
    }
}
