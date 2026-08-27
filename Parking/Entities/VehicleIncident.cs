using System;
using System.Collections.Generic;

namespace Parking.Entities;

public class VehicleIncident
{
    public Guid IncidentId { get; set; } = Guid.NewGuid();
    public int? BranchId { get; set; }
    public string PlateNumber { get; set; } = string.Empty;
    public string IncidentType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsBlocked { get; set; } = false;
    public bool IsGlobal { get; set; } = false;
    public string Status { get; set; } = "Activa";
    public string ReportedBy { get; set; } = string.Empty;
    public string? ResolvedBy { get; set; }
    public string? ResolvedNotes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAtUtc { get; set; }

    public virtual Branch? Branch { get; set; }
    public virtual ICollection<VehicleIncidentBranch> IncidentBranches { get; set; } = new List<VehicleIncidentBranch>();
}
