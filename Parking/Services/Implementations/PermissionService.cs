using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Parking.Core.Constants;
using Parking.Data.Factories;
using Parking.Entities;
using Parking.Models;
using Parking.Services.Contracts;

namespace Parking.Services.Implementations;

public class PermissionService : IPermissionService
{
    private readonly IDbConnectionManager _connectionManager;

    public PermissionService(IDbConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
    }

    public async Task<bool> HasPermissionAsync(Guid userId, string moduleKey, string actionKey)
    {
        using var db = _connectionManager.CreateDbContext();
        var user = await db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null || !user.IsActive)
        {
            return false;
        }

        if (user.Role.Name.Equals("Administrador", StringComparison.OrdinalIgnoreCase) ||
            user.Role.Name.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return await db.RolePermissions
            .AnyAsync(rp => rp.RoleId == user.RoleId &&
                            rp.IsGranted &&
                            rp.Permission.Module.ModuleKey == moduleKey &&
                            rp.Permission.ActionKey == actionKey);
    }

    public async Task<IReadOnlyList<AppModule>> GetAccessibleModulesAsync(Guid userId)
    {
        using var db = _connectionManager.CreateDbContext();
        var user = await db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null || !user.IsActive)
        {
            return new List<AppModule>();
        }

        if (user.Role.Name.Equals("Administrador", StringComparison.OrdinalIgnoreCase) ||
            user.Role.Name.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            return await db.AppModules
                .Where(m => m.IsActive)
                .OrderBy(m => m.DisplayOrder)
                .ToListAsync();
        }

        var allowedModuleIds = await db.RolePermissions
            .Where(rp => rp.RoleId == user.RoleId && rp.IsGranted && rp.Permission.ActionKey == ActionKeys.View)
            .Select(rp => rp.Permission.ModuleId)
            .Distinct()
            .ToListAsync();

        return await db.AppModules
            .Where(m => m.IsActive && allowedModuleIds.Contains(m.ModuleId))
            .OrderBy(m => m.DisplayOrder)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<PermissionMatrixItem>> GetRolePermissionsMatrixAsync(Guid roleId)
    {
        using var db = _connectionManager.CreateDbContext();
        var allPermissions = await db.AppPermissions
            .Include(p => p.Module)
            .OrderBy(p => p.Module.DisplayOrder)
            .ThenBy(p => p.DisplayName)
            .ToListAsync();

        var grantedIds = await db.RolePermissions
            .Where(rp => rp.RoleId == roleId && rp.IsGranted)
            .Select(rp => rp.PermissionId)
            .ToListAsync();

        var grantedSet = new HashSet<Guid>(grantedIds);

        var matrix = allPermissions.Select(p => new PermissionMatrixItem
        {
            PermissionId = p.PermissionId,
            ModuleId = p.ModuleId,
            ModuleKey = p.Module.ModuleKey,
            ModuleDisplayName = p.Module.DisplayName,
            ActionKey = p.ActionKey,
            ActionDisplayName = p.DisplayName,
            Description = p.Description,
            IsGranted = grantedSet.Contains(p.PermissionId)
        }).ToList();

        return matrix;
    }

    public async Task SaveRolePermissionsAsync(Guid roleId, IEnumerable<Guid> grantedPermissionIds)
    {
        using var db = _connectionManager.CreateDbContext();
        var currentRolePerms = await db.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .ToListAsync();

        db.RolePermissions.RemoveRange(currentRolePerms);

        foreach (var permId in grantedPermissionIds)
        {
            db.RolePermissions.Add(new RolePermission
            {
                RolePermissionId = Guid.NewGuid(),
                RoleId = roleId,
                PermissionId = permId,
                IsGranted = true,
                GrantedAtUtc = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();
    }
}
