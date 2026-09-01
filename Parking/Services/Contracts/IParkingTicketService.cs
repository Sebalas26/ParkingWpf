using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Parking.Core.Enums;
using Parking.Entities;
using Parking.Models;

namespace Parking.Services.Contracts;

public interface IParkingTicketService
{
    event EventHandler<ParkingTicket>? TicketRegistered;
    event EventHandler<ParkingTicket>? TicketCompleted;
    event EventHandler<OccupancyStats>? OccupancyChanged;

    Task<ParkingTicket> RegisterEntryAsync(string plateNumber, VehicleType vehicleType, string? phoneNumber, string? notes, string operatorName, decimal? customHourlyRate = null);
    Task<ParkingTicket?> ProcessExitAsync(
        Guid ticketId,
        PaymentMethod paymentMethod,
        decimal amountPaid,
        Guid? storeId,
        Guid? agreementId,
        string? invoiceNumber,
        decimal? purchaseAmount,
        decimal discountAmount,
        int? paymentMethodId = null,
        string? exitNotes = null,
        DateTime? customExitTimeUtc = null,
        Guid? resolutionId = null,
        string? resolutionName = null,
        string? fiscalInvoiceNumber = null);
    Task<IReadOnlyList<ParkingTicket>> GetActiveTicketsAsync();
    Task<IReadOnlyList<ParkingTicket>> GetCompletedTicketsAsync();
    Task<IReadOnlyList<ParkingTicket>> GetAllTicketsAsync();
    Task<ParkingTicket?> FindActiveTicketAsync(string query);
    Task<bool> IsPlateCurrentlyParkedAsync(string plateNumber);
    Task<VehicleIncident?> GetActiveBlockAsync(string plateNumber);
    Task<OccupancyStats> GetOccupancyStatsAsync();
    void UpdateTotalCapacity(int newCapacity);
}
