using System;

namespace Parking.Entities;

public class Branch
{
    public int Id { get; set; }
    public int? CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? City { get; set; }
    public int TotalCapacity { get; set; } = 100;
    public string? Notes { get; set; }
    public string? LogoBase64 { get; set; }
    public int PaperWidth { get; set; } = 80;
    public decimal DefaultInitialCash { get; set; } = 0;
    public bool AllowChargeByMinute { get; set; } = true;
    public bool AllowChargeByHour { get; set; } = true;
    public bool AllowChargeByDay { get; set; } = true;
    public bool AllowChargeByNight { get; set; }
    public decimal LostTicketFee { get; set; } = 0m;
    public int? FullDayThresholdMinutes { get; set; }
    public string? FullDayApplicableDays { get; set; }
    public TimeSpan? FullDayStartTime { get; set; }
    public TimeSpan? FullDayEndTime { get; set; }
    public string? NightApplicableDays { get; set; }
    public TimeSpan? NightStartTime { get; set; }
    public TimeSpan? NightEndTime { get; set; }
    public int? NightStayMinMinutes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public virtual ICollection<BranchOperatingHour> OperatingHours { get; set; } = new List<BranchOperatingHour>();
}
