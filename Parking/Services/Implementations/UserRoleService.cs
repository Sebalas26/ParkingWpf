using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Parking.Data.Factories;
using Parking.Entities;
using Parking.Services.Contracts;

namespace Parking.Services.Implementations;

public class UserRoleService : IUserRoleService
{
    private readonly IDbConnectionManager _connectionManager;

    public UserRoleService(IDbConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
    }

    public async Task<IReadOnlyList<User>> GetAllUsersAsync()
    {
        using var db = _connectionManager.CreateDbContext();
        return await db.Users
            .Include(u => u.Role)
            .OrderBy(u => u.FullName)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Role>> GetAllRolesAsync()
    {
        using var db = _connectionManager.CreateDbContext();
        return await db.Roles
            .OrderBy(r => r.Name)
            .ToListAsync();
    }

    public async Task<User> CreateUserAsync(string username, string password, string fullName, string? email, Guid roleId)
    {
        using var db = _connectionManager.CreateDbContext();
        var normalizedUser = username.Trim().ToLowerInvariant();

        if (await db.Users.AnyAsync(u => u.Username.ToLower() == normalizedUser))
        {
            throw new InvalidOperationException($"El nombre de usuario '{username}' ya se encuentra registrado.");
        }

        var user = new User
        {
            UserId = Guid.NewGuid(),
            Username = normalizedUser,
            PasswordHash = DbConnectionManager.HashPassword(password),
            FullName = fullName.Trim(),
            Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            RoleId = roleId,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        user.Role = (await db.Roles.FindAsync(roleId))!;
        return user;
    }

    public async Task UpdateUserAsync(Guid userId, string fullName, string? email, Guid roleId, bool isActive)
    {
        using var db = _connectionManager.CreateDbContext();
        var user = await db.Users.FindAsync(userId);
        if (user == null)
        {
            throw new KeyNotFoundException("Usuario no encontrado.");
        }

        user.FullName = fullName.Trim();
        user.Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        user.RoleId = roleId;
        user.IsActive = isActive;
        user.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync();
    }

    public async Task ResetPasswordAsync(Guid userId, string newPassword)
    {
        using var db = _connectionManager.CreateDbContext();
        var user = await db.Users.FindAsync(userId);
        if (user == null)
        {
            throw new KeyNotFoundException("Usuario no encontrado.");
        }

        user.PasswordHash = DbConnectionManager.HashPassword(newPassword);
        user.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync();
    }

    public async Task<Role> CreateRoleAsync(string name, string? description)
    {
        using var db = _connectionManager.CreateDbContext();
        var trimmedName = name.Trim();

        if (await db.Roles.AnyAsync(r => r.Name.ToLower() == trimmedName.ToLower()))
        {
            throw new InvalidOperationException($"El rol '{name}' ya existe.");
        }

        var role = new Role
        {
            RoleId = Guid.NewGuid(),
            Name = trimmedName,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            IsSystemRole = false,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.Roles.Add(role);
        await db.SaveChangesAsync();
        return role;
    }

    public async Task UpdateRoleAsync(Guid roleId, string name, string? description, bool isActive)
    {
        using var db = _connectionManager.CreateDbContext();
        var role = await db.Roles.FindAsync(roleId);
        if (role == null)
        {
            throw new KeyNotFoundException("Rol no encontrado.");
        }

        role.Name = name.Trim();
        role.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (!role.IsSystemRole)
        {
            role.IsActive = isActive;
        }

        await db.SaveChangesAsync();
    }
}
