using System;
using System.ComponentModel.DataAnnotations;

namespace Parking.Entities;

public class PendingSyncItem
{
    [Key]
    public Guid PendingSyncItemId { get; set; } = Guid.NewGuid();
    public string OperationType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
    public bool IsProcessed { get; set; }
}
