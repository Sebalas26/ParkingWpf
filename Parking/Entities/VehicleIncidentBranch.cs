using System;

namespace Parking.Entities;

public class VehicleIncidentBranch
{
    public Guid IncidentId { get; set; }
    public int BranchId { get; set; }

    public virtual VehicleIncident VehicleIncident { get; set; } = null!;
}
