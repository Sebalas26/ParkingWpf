using System;
using System.Collections.Generic;
using System.Linq;
using Parking.Models;
using Parking.Services.Contracts;

namespace Parking.Services.Implementations;

public class PermissionService : IPermissionService
{
    private readonly HashSet<string> _permissions = new(StringComparer.OrdinalIgnoreCase);

    public static PermissionService Current { get; } = new();

    public event Action? PermissionsChanged;

    public bool IsAdmin { get; private set; }
    public IReadOnlySet<string> GrantedPermissions => _permissions;

    public bool HasPermission(string? permissionSlug)
    {
        if (string.IsNullOrWhiteSpace(permissionSlug)) return true;
        if (IsAdmin) return true;
        return _permissions.Contains(permissionSlug.Trim());
    }

    public bool HasAnyPermission(params string[] permissionSlugs)
    {
        if (IsAdmin) return true;
        if (permissionSlugs == null || permissionSlugs.Length == 0) return true;
        return permissionSlugs.Any(HasPermission);
    }

    public bool HasAllPermissions(params string[] permissionSlugs)
    {
        if (IsAdmin) return true;
        if (permissionSlugs == null || permissionSlugs.Length == 0) return true;
        return permissionSlugs.All(HasPermission);
    }

    public void LoadPermissions(IEnumerable<string> permissionSlugs, bool isAdmin = false)
    {
        IsAdmin = isAdmin;
        _permissions.Clear();

        if (permissionSlugs != null)
        {
            foreach (var p in permissionSlugs.Where(p => !string.IsNullOrWhiteSpace(p)))
            {
                _permissions.Add(p.Trim());
            }
        }

        if (this != Current)
        {
            Current.LoadPermissions(permissionSlugs, isAdmin);
        }

        PermissionsChanged?.Invoke();
    }

    public void LoadPermissions(UserSessionModel? user)
    {
        if (user == null)
        {
            Clear();
            return;
        }

        var isAdmin = user.RoleName.Equals("Administrador", StringComparison.OrdinalIgnoreCase) ||
                      user.RoleName.Equals("Admin", StringComparison.OrdinalIgnoreCase);

        LoadPermissions(Array.Empty<string>(), isAdmin);
    }

    public void Clear()
    {
        IsAdmin = false;
        _permissions.Clear();

        if (this != Current)
        {
            Current.Clear();
        }

        PermissionsChanged?.Invoke();
    }
}
