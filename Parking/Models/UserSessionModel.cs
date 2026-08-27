using System;
using System.Collections.Generic;

namespace Parking.Models;

public class UserSessionModel
{
    public Guid UserId { get; set; }
    public int? ServerUserId { get; set; }
    public int? ServerRoleId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public Guid RoleId { get; set; }
    public string SessionToken { get; set; } = string.Empty;
    public DateTime LoginTime { get; set; } = DateTime.Now;
    public bool IsAdmin => RoleName.Equals("Administrador", StringComparison.OrdinalIgnoreCase) || RoleName.Equals("Admin", StringComparison.OrdinalIgnoreCase);

    public HashSet<string> GrantedPermissions { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public bool HasPermission(string moduleKey, string actionKey)
    {
        if (IsAdmin)
        {
            return true;
        }

        return GrantedPermissions.Contains($"{moduleKey}.{actionKey}");
    }
}
