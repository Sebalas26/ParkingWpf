using System;

namespace Parking.Entities;

public class UserSession
{
    public Guid SessionId { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string SessionToken { get; set; } = string.Empty;
    public string DeviceIdentifier { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastHeartbeatUtc { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    public User User { get; set; } = null!;
}
