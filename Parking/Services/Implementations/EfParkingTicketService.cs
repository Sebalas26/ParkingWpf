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

public class EfParkingTicketService : IParkingTicketService
{
    private readonly IDbConnectionManager _connectionManager;
    private readonly IPricingCalculatorService _pricingCalculator;
    private int _totalCapacity = 120;

    public event EventHandler<ParkingTicket>? TicketRegistered;
    public event EventHandler<ParkingTicket>? TicketCompleted;
    public event EventHandler<OccupancyStats>? OccupancyChanged;

    public EfParkingTicketService(IDbConnectionManager connectionManager, IPricingCalculatorService pricingCalculator)
    {
        _connectionManager = connectionManager;
        _pricingCalculator = pricingCalculator;
    }

    public async Task<ParkingTicket> RegisterEntryAsync(string plateNumber, VehicleType vehicleType, string? phoneNumber, string? notes, string operatorName)
    {
        var normalizedPlate = plateNumber.Trim().ToUpperInvariant();
        using var db = _connectionManager.CreateDbContext();

        var isAlreadyParked = await db.ParkingTickets.AnyAsync(t =>
            t.Status == TicketStatus.Active &&
            t.PlateNumber == normalizedPlate);

        if (isAlreadyParked)
        {
            throw new InvalidOperationException($"El vehículo con placa '{normalizedPlate}' ya se encuentra registrado y activo adentro.");
        }

        var todayCount = await db.ParkingTickets.CountAsync(t => t.EntryTimeUtc.Date == DateTime.UtcNow.Date) + 1;
        var ticketNumber = $"PKF-{DateTime.Now:yyyyMMdd}-{todayCount:D3}";
        var rate = _pricingCalculator.GetRate(vehicleType);

        var ticket = new ParkingTicket
        {
            TicketId = Guid.NewGuid(),
            TicketNumber = ticketNumber,
            PlateNumber = normalizedPlate,
            VehicleType = vehicleType,
            CustomerPhone = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim(),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            BayNumber = GenerateBayNumber(vehicleType),
            EntryTimeUtc = DateTime.UtcNow,
            HourlyRate = rate.HourRate,
            Status = TicketStatus.Active,
            OperatorName = operatorName,
            IsSynchronized = _connectionManager.IsOnlineMode
        };

        db.ParkingTickets.Add(ticket);
        await db.SaveChangesAsync();

        TicketRegistered?.Invoke(this, ticket);
        OccupancyChanged?.Invoke(this, await GetOccupancyStatsAsync());

        return ticket;
    }

    public async Task<ParkingTicket?> ProcessExitAsync(
        Guid ticketId,
        PaymentMethod paymentMethod,
        decimal amountPaid,
        Guid? storeId,
        Guid? agreementId,
        string? invoiceNumber,
        decimal? purchaseAmount,
        decimal discountAmount)
    {
        using var db = _connectionManager.CreateDbContext();
        var ticket = await db.ParkingTickets.FindAsync(ticketId);
        if (ticket == null || ticket.Status != TicketStatus.Active)
        {
            return null;
        }

        var exitTime = DateTime.UtcNow;
        var gross = _pricingCalculator.CalculateFee(ticket.VehicleType, ticket.EntryTimeUtc, exitTime);
        var net = Math.Max(0m, gross - discountAmount);

        ticket.ExitTimeUtc = exitTime;
        ticket.TotalDurationMinutes = (int)Math.Max(0, (exitTime - ticket.EntryTimeUtc).TotalMinutes);
        ticket.GrossAmount = gross;
        ticket.DiscountAmount = discountAmount;
        ticket.NetAmount = net;
        ticket.AmountPaid = amountPaid;
        ticket.ChangeGiven = Math.Max(0m, amountPaid - net);
        ticket.PaymentMethod = paymentMethod;
        ticket.Status = TicketStatus.Completed;
        ticket.IsSynchronized = _connectionManager.IsOnlineMode;

        if (storeId.HasValue && agreementId.HasValue && !string.IsNullOrWhiteSpace(invoiceNumber) && discountAmount > 0)
        {
            var discountRecord = new TicketDiscount
            {
                TicketDiscountId = Guid.NewGuid(),
                TicketId = ticket.TicketId,
                StoreId = storeId.Value,
                AgreementId = agreementId.Value,
                InvoiceNumber = invoiceNumber.Trim(),
                PurchaseAmount = purchaseAmount ?? 0m,
                AppliedDiscountAmount = discountAmount,
                ValidatedAtUtc = DateTime.UtcNow,
                IsSynchronized = _connectionManager.IsOnlineMode
            };

            db.TicketDiscounts.Add(discountRecord);
        }

        await db.SaveChangesAsync();

        TicketCompleted?.Invoke(this, ticket);
        OccupancyChanged?.Invoke(this, await GetOccupancyStatsAsync());

        return ticket;
    }

    public async Task<IReadOnlyList<ParkingTicket>> GetActiveTicketsAsync()
    {
        using var db = _connectionManager.CreateDbContext();
        return await db.ParkingTickets
            .Where(t => t.Status == TicketStatus.Active)
            .OrderByDescending(t => t.EntryTimeUtc)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ParkingTicket>> GetCompletedTicketsAsync()
    {
        using var db = _connectionManager.CreateDbContext();
        return await db.ParkingTickets
            .Where(t => t.Status == TicketStatus.Completed)
            .Include(t => t.Discounts)
            .OrderByDescending(t => t.ExitTimeUtc)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ParkingTicket>> GetAllTicketsAsync()
    {
        using var db = _connectionManager.CreateDbContext();
        return await db.ParkingTickets
            .Include(t => t.Discounts)
            .OrderByDescending(t => t.EntryTimeUtc)
            .ToListAsync();
    }

    public async Task<ParkingTicket?> FindActiveTicketAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var normalized = query.Trim().ToUpperInvariant();
        using var db = _connectionManager.CreateDbContext();

        return await db.ParkingTickets
            .FirstOrDefaultAsync(t =>
                t.Status == TicketStatus.Active &&
                (t.PlateNumber == normalized || t.TicketNumber == normalized));
    }

    public async Task<bool> IsPlateCurrentlyParkedAsync(string plateNumber)
    {
        if (string.IsNullOrWhiteSpace(plateNumber))
        {
            return false;
        }

        var normalized = plateNumber.Trim().ToUpperInvariant();
        using var db = _connectionManager.CreateDbContext();

        return await db.ParkingTickets.AnyAsync(t =>
            t.Status == TicketStatus.Active &&
            t.PlateNumber == normalized);
    }

    public async Task<OccupancyStats> GetOccupancyStatsAsync()
    {
        using var db = _connectionManager.CreateDbContext();
        var activeCount = await db.ParkingTickets.CountAsync(t => t.Status == TicketStatus.Active);

        return new OccupancyStats
        {
            TotalCapacity = _totalCapacity,
            OccupiedSpots = activeCount
        };
    }

    public void UpdateTotalCapacity(int newCapacity)
    {
        if (newCapacity > 0)
        {
            _totalCapacity = newCapacity;
            _ = NotifyOccupancyChangedAsync();
        }
    }

    private async Task NotifyOccupancyChangedAsync()
    {
        var stats = await GetOccupancyStatsAsync();
        OccupancyChanged?.Invoke(this, stats);
    }

    private static string GenerateBayNumber(VehicleType vehicleType)
    {
        var random = Random.Shared.Next(1, 40);
        var prefix = vehicleType switch
        {
            VehicleType.Motorcycle => "M",
            VehicleType.Car => "A",
            VehicleType.Suv => "B",
            VehicleType.Van => "C",
            VehicleType.HeavyTruck => "T",
            _ => "P"
        };
        return $"{prefix}-{random:D2}";
    }
}
