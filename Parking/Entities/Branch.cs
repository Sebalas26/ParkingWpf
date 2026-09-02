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
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
