using System;

namespace Parking.Models;

public class OccupancyStats
{
    public int TotalCapacity { get; set; } = 120;
    public int OccupiedSpots { get; set; }
    public int AvailableSpots => Math.Max(0, TotalCapacity - OccupiedSpots);
    public double OccupancyPercentage => TotalCapacity > 0 ? (double)OccupiedSpots / TotalCapacity * 100.0 : 0.0;
    public string OccupancySummary => $"{AvailableSpots} disponibles / {OccupiedSpots} ocupados";
}
