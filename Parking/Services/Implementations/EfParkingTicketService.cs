using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Parking.Core.Enums;
using Parking.Data.Factories;
using Parking.Entities;
using Parking.Models;
using Parking.Models.ApiModels;
using Parking.Services.Contracts;

namespace Parking.Services.Implementations;

public class EfParkingTicketService : IParkingTicketService
{
    private readonly IDbConnectionManager _connectionManager;
    private readonly IPricingCalculatorService _pricingCalculator;
    private readonly IApiClientService _apiClient;
    private readonly ISyncEngineService _syncEngine;
    private int _totalCapacity = 120;

    public event EventHandler<ParkingTicket>? TicketRegistered;
    public event EventHandler<ParkingTicket>? TicketCompleted;
    public event EventHandler<OccupancyStats>? OccupancyChanged;

    public EfParkingTicketService(
        IDbConnectionManager connectionManager,
        IPricingCalculatorService pricingCalculator,
        IApiClientService apiClient,
        ISyncEngineService syncEngine)
    {
        _connectionManager = connectionManager;
        _pricingCalculator = pricingCalculator;
        _apiClient = apiClient;
        _syncEngine = syncEngine;
    }

    public async Task<ParkingTicket> RegisterEntryAsync(string plateNumber, VehicleType vehicleType, string? phoneNumber, string? notes, string operatorName, decimal? customHourlyRate = null)
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
        var hourlyRate = customHourlyRate ?? rate.HourRate;

        var ticket = new ParkingTicket
        {
            TicketId = Guid.NewGuid(),
            TicketNumber = ticketNumber,
            PlateNumber = normalizedPlate,
            VehicleType = vehicleType,
            CustomerPhone = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim(),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            EntryTimeUtc = DateTime.UtcNow,
            HourlyRate = hourlyRate,
            Status = TicketStatus.Active,
            OperatorName = operatorName,
            IsSynchronized = false
        };

        // Intentar registrar en el API si está en línea
        if (_syncEngine.IsOnline)
        {
            try
            {
                var apiResponse = await _apiClient.CheckInAsync(new CheckInApiRequest
                {
                    TicketId = ticket.TicketId,
                    TicketNumber = ticket.TicketNumber,
                    PlateNumber = ticket.PlateNumber,
                    VehicleType = ticket.VehicleType,
                    PhoneNumber = ticket.CustomerPhone,
                    Notes = ticket.Notes,
                    OperatorName = operatorName,
                    EntryTimeUtc = ticket.EntryTimeUtc
                });

                if (apiResponse != null)
                {
                    ticket.IsSynchronized = true;
                }
                else
                {
                    await _syncEngine.EnqueueOfflineCheckInAsync(ticket);
                }
            }
            catch
            {
                await _syncEngine.EnqueueOfflineCheckInAsync(ticket);
            }
        }
        else
        {
            await _syncEngine.EnqueueOfflineCheckInAsync(ticket);
        }

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
        decimal discountAmount,
        int? paymentMethodId = null,
        string? exitNotes = null)
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
        ticket.PaymentMethodId = paymentMethodId;
        ticket.ExitNotes = exitNotes;
        ticket.Status = TicketStatus.Completed;
        ticket.IsSynchronized = false;

        // Intentar registrar en el API si está en línea
        if (_syncEngine.IsOnline)
        {
            try
            {
                var apiResponse = await _apiClient.CheckOutAsync(new CheckOutApiRequest
                {
                    TicketId = ticket.TicketId,
                    PaymentMethod = paymentMethod,
                    AmountPaid = amountPaid,
                    StoreId = storeId,
                    AgreementId = agreementId,
                    InvoiceNumber = invoiceNumber,
                    PurchaseAmount = purchaseAmount,
                    DiscountAmount = discountAmount,
                    ExitTimeUtc = exitTime
                });

                if (apiResponse != null)
                {
                    ticket.IsSynchronized = true;
                }
                else
                {
                    await _syncEngine.EnqueueOfflineCheckOutAsync(ticket);
                }
            }
            catch
            {
                await _syncEngine.EnqueueOfflineCheckOutAsync(ticket);
            }
        }
        else
        {
            await _syncEngine.EnqueueOfflineCheckOutAsync(ticket);
        }

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
                IsSynchronized = ticket.IsSynchronized
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
            .OrderByDescending(t => t.ExitTimeUtc)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ParkingTicket>> GetAllTicketsAsync()
    {
        using var db = _connectionManager.CreateDbContext();
        return await db.ParkingTickets
            .OrderByDescending(t => t.EntryTimeUtc)
            .ToListAsync();
    }

    public async Task<ParkingTicket?> FindActiveTicketAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;

        var normalized = query.Trim().ToUpperInvariant();
        using var db = _connectionManager.CreateDbContext();

        return await db.ParkingTickets.FirstOrDefaultAsync(t =>
            t.Status == TicketStatus.Active &&
            (t.PlateNumber == normalized || t.TicketNumber == normalized));
    }

    public async Task<bool> IsPlateCurrentlyParkedAsync(string plateNumber)
    {
        if (string.IsNullOrWhiteSpace(plateNumber)) return false;

        var normalized = plateNumber.Trim().ToUpperInvariant();
        using var db = _connectionManager.CreateDbContext();

        return await db.ParkingTickets.AnyAsync(t =>
            t.Status == TicketStatus.Active &&
            t.PlateNumber == normalized);
    }

    public async Task<OccupancyStats> GetOccupancyStatsAsync()
    {
        using var db = _connectionManager.CreateDbContext();
        var occupied = await db.ParkingTickets.CountAsync(t => t.Status == TicketStatus.Active);

        return new OccupancyStats
        {
            TotalCapacity = _totalCapacity,
            OccupiedSpots = occupied
        };
    }

    public void UpdateTotalCapacity(int newCapacity)
    {
        if (newCapacity > 0)
        {
            _totalCapacity = newCapacity;
        }
    }
}
