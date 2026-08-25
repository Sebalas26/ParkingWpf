using System;
using System.Collections.Generic;
using Parking.Models;

namespace Parking.Services.Contracts;

public interface IPermissionService
{
    event Action? PermissionsChanged;

    bool IsAdmin { get; }
    IReadOnlySet<string> GrantedPermissions { get; }

    bool HasPermission(string? permissionSlug);
    bool HasAnyPermission(params string[] permissionSlugs);
    bool HasAllPermissions(params string[] permissionSlugs);

    void LoadPermissions(IEnumerable<string> permissionSlugs, bool isAdmin = false);
    void LoadPermissions(UserSessionModel? user);
    void Clear();
}
