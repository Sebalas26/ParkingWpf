using System;
using System.Collections.Generic;
using Parking.Models;

namespace Parking.Services.Contracts;

public interface IPermissionService
{
    event EventHandler? PermissionsChanged;
    IReadOnlySet<string> CurrentPermissions { get; }
    bool IsAdmin { get; }

    void LoadPermissions(UserSessionModel? userSession);
    bool HasPermission(string? permissionSlug);
    bool HasAnyPermission(params string[] permissionSlugs);
    bool HasAllPermissions(params string[] permissionSlugs);
}
