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
    public bool IsAdmin { get; set; }
    public bool IsSuperAdmin { get; set; }
    public int? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public bool AllowMultipleSessions { get; set; }
    public int MaxActiveSessionsPerUser { get; set; } = 1;
    public bool AllowMultipleOpenShifts { get; set; }
    public int MaxOpenShiftsPerUser { get; set; } = 1;
    public bool RequireOpenShiftToOperate { get; set; } = true;
    public bool RequireInitialCashAmount { get; set; }
    public bool HasDesktopAccess { get; set; } = true;
    public bool HasWebAccess { get; set; } = true;
    public int MaxUsers { get; set; }

    public HashSet<string> GrantedPermissions { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public bool HasPermission(string moduleKey, string actionKey)
    {
        if (IsSuperAdmin)
        {
            return true;
        }

        return GrantedPermissions.Contains($"{moduleKey}.{actionKey}") || GrantedPermissions.Contains(actionKey);
    }
}
