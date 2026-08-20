using System;

namespace Parking.Entities;

public class WorkShift
{
    public Guid ShiftId { get; set; } = Guid.NewGuid();
    public int UserId { get; set; }
    public string OperatorName { get; set; } = string.Empty;
    public DateTime StartTimeUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndTimeUtc { get; set; }
    public decimal BaseAmount { get; set; }
    public decimal TotalCashCollected { get; set; }
    public decimal TotalCardCollected { get; set; }
    public decimal TotalTransferCollected { get; set; }
    public decimal TotalDiscounts { get; set; }
    public decimal ExpectedCash { get; set; }
    public decimal ActualCashCounted { get; set; }
    public decimal CashDifference { get; set; }
    public int TotalTicketsProcessed { get; set; }
    public int TotalVehiclesEntered { get; set; }
    public int Status { get; set; } // 0 = Open, 1 = Closed
    public string? Notes { get; set; }
    public Guid? HandoverToUserId { get; set; }
    public string? HandoverToUserName { get; set; }
    public bool IsSynchronized { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAtUtc { get; set; }

    public DateTime StartTime => StartTimeUtc.ToLocalTime();
    public DateTime? EndTime => EndTimeUtc?.ToLocalTime();
    public TimeSpan Duration => (EndTimeUtc ?? DateTime.UtcNow) - StartTimeUtc;
    public string FormattedDuration => $"{(int)Duration.TotalHours}h {Duration.Minutes}m";
    public decimal TotalRevenue => TotalCashCollected + TotalCardCollected + TotalTransferCollected;
}
