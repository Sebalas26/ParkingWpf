using System.Collections.Generic;
using System.Threading.Tasks;
using Parking.Core.Enums;
using Parking.Entities;
using Parking.Models;

namespace Parking.Services.Contracts;

public interface IAnalyticsService
{
    Task<FinancialSummary> GetDailySummaryAsync();
    Task<IReadOnlyList<ParkingTicket>> GetFilteredTransactionsAsync(string? searchFilter, TicketStatus? statusFilter, VehicleType? vehicleTypeFilter);
}
