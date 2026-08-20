using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Parking.Core.Enums;
using Parking.Data.Factories;
using Parking.Entities;
using Parking.Models;
using Parking.Services.Contracts;

namespace Parking.Services.Implementations;

public class EfAnalyticsService : IAnalyticsService
{
    private readonly IDbConnectionManager _connectionManager;

    public EfAnalyticsService(IDbConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
    }

    public async Task<FinancialSummary> GetDailySummaryAsync()
    {
        using var db = _connectionManager.CreateDbContext();
        var allTickets = await db.ParkingTickets.ToListAsync();
        var todayLocal = DateTime.Today;
        var todayUtc = DateTime.UtcNow.Date;

        var completedToday = allTickets
            .Where(t => t.Status == TicketStatus.Completed && ((t.ExitTimeUtc.HasValue && t.ExitTimeUtc.Value.Date == todayUtc) || t.ExitTime?.Date == todayLocal))
            .ToList();

        // Si no hay completados hoy por zona horaria pero hay completados en la BD, incluirlos
        if (!completedToday.Any())
        {
            completedToday = allTickets.Where(t => t.Status == TicketStatus.Completed).ToList();
        }

        var activeNowCount = allTickets.Count(t => t.Status == TicketStatus.Active);
        var totalRevenue = completedToday.Sum(t => t.NetAmount);
        var completedCount = completedToday.Count;

        var averageDuration = completedToday.Count > 0
            ? completedToday.Average(t => t.TotalDurationMinutes)
            : 0.0;

        var allToday = allTickets
            .Where(t => t.EntryTimeUtc.Date == todayUtc || t.EntryTime.Date == todayLocal)
            .ToList();

        var totalEntriesToday = allToday.Count > 0 ? allToday.Count : allTickets.Count;

        var revenueByType = new Dictionary<VehicleType, decimal>();
        var countByType = new Dictionary<VehicleType, int>();

        foreach (VehicleType type in Enum.GetValues<VehicleType>())
        {
            revenueByType[type] = completedToday.Where(t => t.VehicleType == type).Sum(t => t.NetAmount);
            countByType[type] = allTickets.Count(t => t.VehicleType == type);
        }

        return new FinancialSummary
        {
            TotalRevenueToday = totalRevenue,
            ActiveVehiclesCount = activeNowCount,
            CompletedTransactionsToday = completedCount,
            TotalEntriesToday = totalEntriesToday,
            AverageDurationMinutes = averageDuration,
            RevenueByVehicleType = revenueByType,
            CountByVehicleType = countByType
        };
    }

    public async Task<IReadOnlyList<ParkingTicket>> GetFilteredTransactionsAsync(string? searchFilter, TicketStatus? statusFilter, VehicleType? vehicleTypeFilter)
    {
        using var db = _connectionManager.CreateDbContext();
        var query = db.ParkingTickets.Include(t => t.Discounts).ThenInclude(d => d.Store).AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchFilter))
        {
            var term = searchFilter.Trim().ToUpperInvariant();
            query = query.Where(t =>
                t.PlateNumber.Contains(term) ||
                t.TicketNumber.Contains(term) ||
                (t.CustomerPhone != null && t.CustomerPhone.Contains(term)));
        }

        if (statusFilter.HasValue)
        {
            query = query.Where(t => t.Status == statusFilter.Value);
        }

        if (vehicleTypeFilter.HasValue)
        {
            query = query.Where(t => t.VehicleType == vehicleTypeFilter.Value);
        }

        return await query.OrderByDescending(t => t.EntryTimeUtc).ToListAsync();
    }
}
