using System;

namespace Parking.Entities;

public class RolePermission
{
    public Guid RolePermissionId { get; set; } = Guid.NewGuid();
    public Guid RoleId { get; set; }
    public Guid PermissionId { get; set; }
    public bool IsGranted { get; set; } = true;
    public DateTime GrantedAtUtc { get; set; } = DateTime.UtcNow;

    public Role Role { get; set; } = null!;
    public AppPermission Permission { get; set; } = null!;
}
