using System;
using System.Collections.Generic;

namespace Parking.Entities;

public class AppModule
{
    public Guid ModuleId { get; set; } = Guid.NewGuid();
    public string ModuleKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string IconKey { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<AppPermission> Permissions { get; set; } = new List<AppPermission>();
}
