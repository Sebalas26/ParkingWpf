using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
    private readonly ISessionService _sessionService;
    private readonly IConfiguration _configuration;
    private int _totalCapacity = 0;

    public event EventHandler<ParkingTicket>? TicketRegistered;
    public event EventHandler<ParkingTicket>? TicketCompleted;
    public event EventHandler<OccupancyStats>? OccupancyChanged;

    public EfParkingTicketService(
        IDbConnectionManager connectionManager,
        IPricingCalculatorService pricingCalculator,
        IApiClientService apiClient,
        ISyncEngineService syncEngine,
        ISessionService sessionService,
        IConfiguration configuration)
    {
        _connectionManager = connectionManager;
        _pricingCalculator = pricingCalculator;
        _apiClient = apiClient;
        _syncEngine = syncEngine;
        _sessionService = sessionService;
        _configuration = configuration;
        _totalCapacity = int.TryParse(_configuration["ParkingSettings:TotalCapacity"], out var cap) ? cap : 0;

        _syncEngine.TotalCapacityChanged += newCap =>
        {
            UpdateTotalCapacity(newCap);
        };

        _sessionService.ActiveBranchChanged += async branch =>
        {
            if (branch != null && branch.TotalCapacity > 0)
            {
                UpdateTotalCapacity(branch.TotalCapacity);
            }
            try
            {
                OccupancyChanged?.Invoke(this, await GetOccupancyStatsAsync());
            }
            catch { }
        };
    }

    public async Task<ParkingTicket> RegisterEntryAsync(string plateNumber, VehicleType vehicleType, string? phoneNumber, string? notes, string operatorName, decimal? customHourlyRate = null)
    {
        var normalizedPlate = plateNumber.Trim().ToUpperInvariant();
        using var db = _connectionManager.CreateDbContext();

        // 1. Validar si el vehículo se encuentra registrado y activo adentro
        var isAlreadyParked = await db.ParkingTickets.AnyAsync(t =>
            t.Status == TicketStatus.Active &&
            t.PlateNumber == normalizedPlate);

        if (isAlreadyParked)
        {
            throw new InvalidOperationException($"El vehículo con placa '{normalizedPlate}' ya se encuentra registrado y activo adentro.");
        }

        var currentBranchId = _sessionService.CurrentBranch?.Id;
        VehicleIncident? blockedIncident = null;
        try
        {
            blockedIncident = await db.VehicleIncidents
                .AsNoTracking()
                .Include(i => i.IncidentBranches)
                .FirstOrDefaultAsync(i =>
                    i.PlateNumber == normalizedPlate &&
                    i.IsBlocked &&
                    i.Status == "Activa" &&
                    (i.IsGlobal || (i.BranchId == null && !i.IncidentBranches.Any()) || !currentBranchId.HasValue || i.BranchId == currentBranchId.Value || i.IncidentBranches.Any(ib => ib.BranchId == currentBranchId.Value)));
        }
        catch { }

        if (blockedIncident != null)
        {
            throw new InvalidOperationException($"El vehículo con placa '{normalizedPlate}' tiene un BLOQUEO ACTIVO ({blockedIncident.IncidentType}): {blockedIncident.Description}");
        }

        // Asegurar columnas requeridas en SQLite antes de insertar
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"ParkingTickets\" ADD COLUMN \"BranchId\" INTEGER NULL;"); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"ParkingTickets\" ADD COLUMN \"OperatorEntryId\" TEXT NULL;"); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"ParkingTickets\" ADD COLUMN \"OperatorExitId\" TEXT NULL;"); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"ParkingTickets\" ADD COLUMN \"BayNumber\" TEXT NULL;"); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"ParkingTickets\" ADD COLUMN \"CreatedAtUtc\" TEXT DEFAULT '';"); } catch { }

        var todayStart = DateTime.UtcNow.Date;
        var todayEnd = todayStart.AddDays(1);
        var baseCount = await db.ParkingTickets.CountAsync(t => t.EntryTimeUtc >= todayStart && t.EntryTimeUtc < todayEnd);
        var seq = baseCount + 1;
        var ticketNumber = $"PKF-{DateTime.Now:yyyyMMdd}-{seq:D3}";
        while (await db.ParkingTickets.AnyAsync(t => t.TicketNumber == ticketNumber))
        {
            seq++;
            ticketNumber = $"PKF-{DateTime.Now:yyyyMMdd}-{seq:D3}";
        }

        var rate = _pricingCalculator.GetRate(vehicleType);
        var hourlyRate = customHourlyRate ?? (rate?.HourRate ?? 0m);

        var ticket = new ParkingTicket
        {
            TicketId = Guid.NewGuid(),
            BranchId = _sessionService.CurrentBranch?.Id,
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
                    BranchId = ticket.BranchId,
                    PlateNumber = ticket.PlateNumber,
                    VehicleType = ticket.VehicleType,
                    CustomerPhone = ticket.CustomerPhone,
                    Notes = ticket.Notes,
                    HourlyRate = ticket.HourlyRate,
                    OperatorName = ticket.OperatorName
                });

                if (apiResponse != null && !string.IsNullOrWhiteSpace(apiResponse.TicketNumber))
                {
                    var existsDifferent = await db.ParkingTickets.AnyAsync(t => t.TicketNumber == apiResponse.TicketNumber && t.TicketId != ticket.TicketId);
                    if (!existsDifferent)
                    {
                        ticket.TicketNumber = apiResponse.TicketNumber;
                    }
                    ticket.IsSynchronized = true;
                }
                else
                {
                    await _syncEngine.EnqueueOfflineCheckInAsync(ticket);
                }
            }
            catch (InvalidOperationException)
            {
                // Errores de validación de negocio del servidor (placa bloqueada o ya adentro). Propagar inmediatamente.
                throw;
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
        string? exitNotes = null,
        DateTime? customExitTimeUtc = null)
    {
        using var db = _connectionManager.CreateDbContext();
        var ticket = await db.ParkingTickets.FindAsync(ticketId);
        if (ticket == null || ticket.Status != TicketStatus.Active)
        {
            return null;
        }

        var exitTime = customExitTimeUtc ?? DateTime.UtcNow;
        var gross = _pricingCalculator.CalculateFee(ticket.VehicleType, ticket.EntryTimeUtc, exitTime);
        var net = Math.Max(0m, gross - discountAmount);

        // Garantizar que el ID de la sede activa quede asignado al tiquete
        var currentBranchId = _sessionService.CurrentBranch?.Id;
        if (currentBranchId.HasValue)
        {
            ticket.BranchId = currentBranchId.Value;
        }

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

        // Intentar registrar salida en el API si está en línea
        if (_syncEngine.IsOnline)
        {
            try
            {
                var apiResponse = await _apiClient.CheckOutAsync(new CheckOutApiRequest
                {
                    TicketId = ticket.TicketId,
                    BranchId = ticket.BranchId ?? currentBranchId,
                    PaymentMethod = paymentMethod,
                    PaymentMethodId = paymentMethodId,
                    AmountPaid = amountPaid,
                    ChangeGiven = ticket.ChangeGiven,
                    GrossAmount = gross,
                    NetAmount = net,
                    StoreId = storeId,
                    AgreementId = agreementId,
                    InvoiceNumber = invoiceNumber,
                    PurchaseAmount = purchaseAmount,
                    DiscountAmount = discountAmount,
                    ExitNotes = exitNotes,
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
        var currentBranchId = _sessionService.CurrentBranch?.Id;

        return await db.ParkingTickets
            .Where(t => t.Status == TicketStatus.Active &&
                        (!currentBranchId.HasValue || t.BranchId == null || t.BranchId == currentBranchId.Value))
            .OrderByDescending(t => t.EntryTimeUtc)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ParkingTicket>> GetCompletedTicketsAsync()
    {
        using var db = _connectionManager.CreateDbContext();
        var currentBranchId = _sessionService.CurrentBranch?.Id;

        return await db.ParkingTickets
            .Where(t => t.Status == TicketStatus.Completed &&
                        (!currentBranchId.HasValue || t.BranchId == null || t.BranchId == currentBranchId.Value))
            .OrderByDescending(t => t.ExitTimeUtc)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ParkingTicket>> GetAllTicketsAsync()
    {
        using var db = _connectionManager.CreateDbContext();
        var currentBranchId = _sessionService.CurrentBranch?.Id;

        return await db.ParkingTickets
            .Where(t => !currentBranchId.HasValue || t.BranchId == null || t.BranchId == currentBranchId.Value)
            .OrderByDescending(t => t.EntryTimeUtc)
            .ToListAsync();
    }

    public async Task<ParkingTicket?> FindActiveTicketAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;

        var normalized = query.Trim().ToUpperInvariant();
        using var db = _connectionManager.CreateDbContext();
        var currentBranchId = _sessionService.CurrentBranch?.Id;

        return await db.ParkingTickets.FirstOrDefaultAsync(t =>
            t.Status == TicketStatus.Active &&
            (!currentBranchId.HasValue || t.BranchId == null || t.BranchId == currentBranchId.Value) &&
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
        var currentBranchId = _sessionService.CurrentBranch?.Id;

        var occupied = await db.ParkingTickets.CountAsync(t =>
            t.Status == TicketStatus.Active &&
            (!currentBranchId.HasValue || t.BranchId == null || t.BranchId == currentBranchId.Value));

        var capacity = _sessionService.CurrentBranch?.TotalCapacity ?? 0;
        if (capacity <= 0 && currentBranchId.HasValue)
        {
            var branch = await db.Branches.FirstOrDefaultAsync(b => b.Id == currentBranchId.Value);
            if (branch != null && branch.TotalCapacity > 0)
            {
                capacity = branch.TotalCapacity;
            }
        }

        if (capacity <= 0)
        {
            capacity = _totalCapacity > 0 ? _totalCapacity : _syncEngine.ServerConfiguredCapacity;
        }

        return new OccupancyStats
        {
            TotalCapacity = capacity,
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

    public async Task<VehicleIncident?> GetActiveBlockAsync(string plateNumber)
    {
        if (string.IsNullOrWhiteSpace(plateNumber)) return null;

        var normalized = plateNumber.Trim().ToUpperInvariant();
        using var db = _connectionManager.CreateDbContext();
        var currentBranchId = _sessionService.CurrentBranch?.Id;

        try
        {
            return await db.VehicleIncidents
                .AsNoTracking()
                .Include(i => i.IncidentBranches)
                .FirstOrDefaultAsync(i =>
                    i.PlateNumber == normalized &&
                    i.IsBlocked &&
                    i.Status == "Activa" &&
                    (i.IsGlobal || (i.BranchId == null && !i.IncidentBranches.Any()) || !currentBranchId.HasValue || i.BranchId == currentBranchId.Value || i.IncidentBranches.Any(ib => ib.BranchId == currentBranchId.Value)));
        }
        catch
        {
            return null;
        }
    }
}
