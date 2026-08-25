using System;
using Parking.Core.Enums;

namespace Parking.Entities;

public class VehicleRate
{
    public Guid RateId { get; set; } = Guid.NewGuid();
    public int? BranchId { get; set; }
    public VehicleType VehicleType { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public decimal MinuteRate { get; set; }
    public decimal HourRate { get; set; }
    public decimal FullDayRate { get; set; }
    public int GracePeriodMinutes { get; set; } = 15;
    public string IconKey { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
