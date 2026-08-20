using System;

namespace Parking.Models.ApiModels;

public class OpenShiftApiRequest
{
    public decimal BaseAmount { get; set; } = 0m;
    public string? Notes { get; set; }
}

public class CloseShiftApiRequest
{
    public Guid ShiftId { get; set; }
    public decimal ActualCashCounted { get; set; }
    public string? Notes { get; set; }
}

public class ShiftSummaryModel
{
    public Guid ShiftId { get; set; }
    public int UserId { get; set; }
    public string OperatorName { get; set; } = string.Empty;
    public DateTime StartTimeUtc { get; set; }
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
    public int Status { get; set; }
    public string? Notes { get; set; }

    public decimal TotalCollectedAllMethods => TotalCashCollected + TotalCardCollected + TotalTransferCollected;
    public bool IsBalanced => Math.Abs(CashDifference) < 0.01m;
    public bool IsSurplus => CashDifference > 0.01m;
    public bool IsDeficit => CashDifference < -0.01m;
}
