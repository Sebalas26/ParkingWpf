using System.Collections.Generic;
using Parking.Core.Enums;

namespace Parking.Models;

public class FinancialSummary
{
    public decimal TotalRevenueToday { get; set; }
    public int ActiveVehiclesCount { get; set; }
    public int CompletedTransactionsToday { get; set; }
    public double AverageDurationMinutes { get; set; }
    public Dictionary<VehicleType, decimal> RevenueByVehicleType { get; set; } = new();
    public Dictionary<VehicleType, int> CountByVehicleType { get; set; } = new();
}
