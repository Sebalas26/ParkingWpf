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

    public UserSessionModel? CurrentUser { get; private set; }
    public bool IsAuthenticated => CurrentUser != null;

    public AuthService(IDbConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
    }

    public async Task<bool> LoginAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        using var db = _connectionManager.CreateDbContext();
        var normalizedUser = username.Trim().ToLowerInvariant();
        var passwordHash = DbConnectionManager.HashPassword(password);

        var user = await db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Username.ToLower() == normalizedUser && u.IsActive);

        if (user == null || user.PasswordHash != passwordHash)
        {
            return false;
        }

        var activeSessions = await db.UserSessions
            .Where(s => s.UserId == user.UserId && s.IsActive)
            .ToListAsync();

        foreach (var session in activeSessions)
        {
            session.IsActive = false;
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

        var grantedPerms = await db.RolePermissions
            .Where(rp => rp.RoleId == user.RoleId && rp.IsGranted)
            .Include(rp => rp.Permission)
            .ThenInclude(p => p.Module)
            .Select(rp => $"{rp.Permission.Module.ModuleKey}.{rp.Permission.ActionKey}")
            .ToListAsync();

        CurrentUser = new UserSessionModel
        {
            UserId = user.UserId,
            Username = user.Username,
            FullName = user.FullName,
            RoleName = user.Role.Name,
            RoleId = user.RoleId,
            SessionToken = sessionToken,
            LoginTime = DateTime.Now,
            GrantedPermissions = new HashSet<string>(grantedPerms, StringComparer.OrdinalIgnoreCase)
        };

        return true;
    }

    public async Task LogoutAsync()
    {
        if (CurrentUser != null)
        {
            using var db = _connectionManager.CreateDbContext();
            var session = await db.UserSessions
                .FirstOrDefaultAsync(s => s.SessionToken == CurrentUser.SessionToken);

            if (session != null)
            {
                session.IsActive = false;
                await db.SaveChangesAsync();
            }

            CurrentUser = null;
        }
    }
}
