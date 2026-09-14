using System;

namespace Parking.Entities;

public class CustomerVehicle
{
    public int Id { get; set; }
    public Guid CustomerId { get; set; }
    public string PlateNumber { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public virtual Customer? Customer { get; set; }
}
