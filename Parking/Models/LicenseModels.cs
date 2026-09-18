using System;

namespace Parking.Models;

public class LocalLicenseData
{
    public string LicenseKey { get; set; } = string.Empty;
    public string DeviceToken { get; set; } = string.Empty;
    public string MachineFingerprint { get; set; } = string.Empty;
    public int? BranchId { get; set; }
    public string? BranchName { get; set; }
    public int? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public DateTime? ExpirationDateUtc { get; set; }
    public DateTime ActivatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class LicenseActivationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public LocalLicenseData? LicenseData { get; set; }
}
