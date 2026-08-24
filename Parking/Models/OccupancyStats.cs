using System;

namespace Parking.Models;

public class OccupancyStats
{
    public int TotalCapacity { get; set; }
    public int OccupiedSpots { get; set; }
    public bool IsConfigured => TotalCapacity > 0;
    public int AvailableSpots => IsConfigured ? Math.Max(0, TotalCapacity - OccupiedSpots) : 0;
    public double OccupancyPercentage => IsConfigured ? (double)OccupiedSpots / TotalCapacity * 100.0 : 0.0;
    public string OccupancySummary => IsConfigured
        ? $"{AvailableSpots} disponibles / {OccupiedSpots} ocupados"
        : $"Capacidad: Sin configurar / {OccupiedSpots} ocupados";
}
