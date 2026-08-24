using System;
using System.Collections.Generic;

namespace Parking.Entities;

public class AppPermission
{
    public Guid PermissionId { get; set; } = Guid.NewGuid();
    public Guid ModuleId { get; set; }
    public string ActionKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }

    public AppModule Module { get; set; } = null!;
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
