using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Parking.Core.Enums;
using Parking.Data.Factories;
using Parking.Entities;
using Parking.Models.ApiModels;
using Parking.Services.Contracts;

namespace Parking.Services.Implementations;

public class EfShiftService : IShiftService
{
    private readonly IDbConnectionManager _connectionManager;
    private readonly IApiClientService _apiClient;
    private readonly IAuthService _authService;
    private readonly ISessionService _sessionService;

    public WorkShift? CurrentShift { get; private set; }
    public bool HasActiveShift => CurrentShift != null && CurrentShift.Status == 0;
    public event Action? ShiftStateChanged;

    public EfShiftService(
        IDbConnectionManager connectionManager,
        IApiClientService apiClient,
        IAuthService authService,
        ISessionService sessionService)
    {
        _connectionManager = connectionManager;
        _apiClient = apiClient;
        _authService = authService;
        _sessionService = sessionService;
    }

    private int? CurrentBranchId => _sessionService.CurrentBranch?.Id;

    public async Task<WorkShift> OpenShiftAsync(decimal baseAmount, string? notes = null)
    {
        var branchId = _sessionService.CurrentBranch?.Id ?? _sessionService.CurrentBranchId;
        if (!branchId.HasValue || branchId.Value <= 0)
        {
            throw new InvalidOperationException("Debe seleccionar una sede activa antes de abrir el turno de caja.");
        }

        var companyId = _sessionService.CurrentCompanyId;
        if (!companyId.HasValue || companyId.Value <= 0)
        {
            throw new InvalidOperationException("La sesión no cuenta con una empresa (CompanyId) asignada.");
        }

        var operatorName = _authService.CurrentUser?.FullName ?? "Operador General";
        var request = new OpenShiftApiRequest
        {
            BranchId = branchId.Value,
            CompanyId = companyId.Value,
            BaseAmount = baseAmount,
            Notes = notes
        };

        WorkShift? shift = null;

        try
        {
            shift = await _apiClient.OpenShiftAsync(request);
        }
        catch { }

        shift ??= new WorkShift
        {
            ShiftId = Guid.NewGuid(),
            BranchId = branchId.Value,
            CompanyId = companyId.Value,
            UserId = _authService.CurrentUser?.ServerUserId ?? 1,
            OperatorName = operatorName,
            StartTimeUtc = DateTime.UtcNow,
            BaseAmount = baseAmount,
            Status = 0,
            Notes = notes,
            IsSynchronized = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        if (!shift.BranchId.HasValue)
        {
            shift.BranchId = branchId.Value;
        }

        if (!shift.CompanyId.HasValue)
        {
            shift.CompanyId = companyId.Value;
        }

        using var db = _connectionManager.CreateDbContext();
        var existing = await db.WorkShifts.FirstOrDefaultAsync(s => s.ShiftId == shift.ShiftId);
        if (existing == null)
        {
            db.WorkShifts.Add(shift);
        }
        else
        {
            existing.BranchId = branchId.Value;
            existing.CompanyId = companyId.Value;
            existing.Status = 0;
            existing.BaseAmount = baseAmount;
            existing.Notes = notes;
        }
        await db.SaveChangesAsync();

        CurrentShift = shift;
        ShiftStateChanged?.Invoke();
        return shift;
    }

    public async Task<WorkShift?> GetActiveShiftAsync()
    {
        var branchId = CurrentBranchId;
        try
        {
            var apiShift = await _apiClient.GetActiveShiftAsync(branchId: branchId);
            if (apiShift != null)
            {
                CurrentShift = apiShift;
                ShiftStateChanged?.Invoke();
                return apiShift;
            }
        }
        catch { }

        using var db = _connectionManager.CreateDbContext();
        var query = db.WorkShifts.Where(s => s.Status == 0);
        if (branchId.HasValue && branchId.Value > 0)
        {
            query = query.Where(s => s.BranchId == branchId.Value);
        }

        var localShift = await query
            .OrderByDescending(s => s.StartTimeUtc)
            .FirstOrDefaultAsync();

        CurrentShift = localShift;
        ShiftStateChanged?.Invoke();
        return localShift;
    }

    public async Task<ShiftSummaryModel> GetCurrentShiftSummaryAsync()
    {
        var branchId = CurrentBranchId;
        var activeShift = CurrentShift ?? await GetActiveShiftAsync();
        var startTime = activeShift?.StartTimeUtc ?? DateTime.UtcNow.Date;
        var baseAmount = activeShift?.BaseAmount ?? 0m;
        var shiftId = activeShift?.ShiftId ?? Guid.Empty;
        var operatorName = activeShift?.OperatorName ?? (_authService.CurrentUser?.FullName ?? "Operador General");

        if (shiftId != Guid.Empty)
        {
            try
            {
                var apiSummary = await _apiClient.GetShiftSummaryAsync(shiftId);
                if (apiSummary != null)
                {
                    return apiSummary;
                }
            }
            catch { }
        }

        // Cálculo local en SQLite filtrado por sede
        using var db = _connectionManager.CreateDbContext();
        var ticketsQuery = db.ParkingTickets.AsNoTracking().AsQueryable();
        if (branchId.HasValue && branchId.Value > 0)
        {
            ticketsQuery = ticketsQuery.Where(t => t.BranchId == branchId.Value);
        }

        var allTickets = await ticketsQuery.ToListAsync();

        var completedTickets = allTickets
            .Where(t => t.Status == TicketStatus.Completed && (activeShift == null || t.ExitTimeUtc >= startTime))
            .ToList();

        var enteredTicketsCount = allTickets
            .Count(t => activeShift == null || t.EntryTimeUtc >= startTime);

        decimal cash = 0m;
        decimal card = 0m;
        decimal transfer = 0m;
        decimal discounts = completedTickets.Sum(t => t.DiscountAmount);

        foreach (var t in completedTickets)
        {
            if (t.PaymentMethod == PaymentMethod.DebitCard || t.PaymentMethod == PaymentMethod.CreditCard)
            {
                card += t.NetAmount;
            }
            else if (t.PaymentMethod == PaymentMethod.DigitalTransfer)
            {
                transfer += t.NetAmount;
            }
            else
            {
                cash += t.NetAmount;
            }
        }

        // Obtener retiros de caja (recogidas del dueño/administración)
        decimal withdrawals = 0m;
        if (shiftId != Guid.Empty)
        {
            var shiftWithdrawals = await db.CashWithdrawals
                .Where(w => w.ShiftId == shiftId)
                .ToListAsync();
            withdrawals = shiftWithdrawals.Sum(w => w.Amount);
        }

        var expectedCash = baseAmount + cash - withdrawals;

        return new ShiftSummaryModel
        {
            ShiftId = shiftId,
            BranchId = branchId,
            UserId = activeShift?.UserId ?? (_authService.CurrentUser?.ServerUserId ?? 1),
            OperatorName = operatorName,
            StartTimeUtc = startTime,
            BaseAmount = baseAmount,
            TotalCashCollected = cash,
            TotalCardCollected = card,
            TotalTransferCollected = transfer,
            TotalDiscounts = discounts,
            TotalCashWithdrawals = withdrawals,
            ExpectedCash = expectedCash,
            ActualCashCounted = 0m,
            CashDifference = -expectedCash,
            TotalTicketsProcessed = completedTickets.Count,
            TotalVehiclesEntered = enteredTicketsCount,
            Status = 0,
            Notes = activeShift?.Notes
        };
    }

    public async Task<WorkShift?> CloseShiftAsync(decimal actualCashCounted, string? notes = null, Guid? handoverToUserId = null, string? handoverToUserName = null)
    {
        var activeShift = CurrentShift ?? await GetActiveShiftAsync();
        if (activeShift == null) return null;

        var request = new CloseShiftApiRequest
        {
            ShiftId = activeShift.ShiftId,
            ActualCashCounted = actualCashCounted,
            Notes = notes,
            HandoverToUserId = handoverToUserId,
            HandoverToUserName = handoverToUserName
        };

        WorkShift? closedShift = null;
        try
        {
            closedShift = await _apiClient.CloseShiftAsync(request);
        }
        catch { }

        var summary = await GetCurrentShiftSummaryAsync();
        var endTime = DateTime.UtcNow;

        using var db = _connectionManager.CreateDbContext();
        var local = await db.WorkShifts.FirstOrDefaultAsync(s => s.ShiftId == activeShift.ShiftId);
        if (local != null)
        {
            local.EndTimeUtc = endTime;
            local.ClosedAtUtc = endTime;
            local.TotalCashCollected = summary.TotalCashCollected;
            local.TotalCardCollected = summary.TotalCardCollected;
            local.TotalTransferCollected = summary.TotalTransferCollected;
            local.TotalDiscounts = summary.TotalDiscounts;
            local.TotalCashWithdrawals = summary.TotalCashWithdrawals;
            local.ExpectedCash = summary.ExpectedCash;
            local.ActualCashCounted = actualCashCounted;
            local.CashDifference = actualCashCounted - summary.ExpectedCash;
            local.TotalTicketsProcessed = summary.TotalTicketsProcessed;
            local.TotalVehiclesEntered = summary.TotalVehiclesEntered;
            local.Status = 1;
            local.Notes = notes ?? local.Notes;
            local.HandoverToUserId = handoverToUserId;
            local.HandoverToUserName = handoverToUserName;
            local.IsSynchronized = closedShift != null;
            await db.SaveChangesAsync();

            closedShift ??= local;
        }

        CurrentShift = null;
        ShiftStateChanged?.Invoke();
        return closedShift;
    }

    public async Task<WorkShift> HandoverAndOpenNextShiftAsync(decimal actualCashCounted, string? notes, Guid handoverToUserId, string handoverToUserName, decimal newShiftBaseAmount)
    {
        var branchId = CurrentBranchId;

        // 1. Cerrar el turno saliente
        await CloseShiftAsync(actualCashCounted, notes, handoverToUserId, handoverToUserName);

        // 2. Abrir inmediatamente el nuevo turno a nombre del operador receptor
        var nextShift = new WorkShift
        {
            ShiftId = Guid.NewGuid(),
            BranchId = branchId,
            UserId = 1,
            OperatorName = handoverToUserName,
            StartTimeUtc = DateTime.UtcNow,
            BaseAmount = newShiftBaseAmount,
            Status = 0,
            Notes = $"Turno recibido de relevo por entrega de caja. Base inicial: ${newShiftBaseAmount:N0}",
            IsSynchronized = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        using var db = _connectionManager.CreateDbContext();
        db.WorkShifts.Add(nextShift);
        await db.SaveChangesAsync();

        CurrentShift = nextShift;
        ShiftStateChanged?.Invoke();
        return nextShift;
    }

    public async Task<CashWithdrawal> RegisterCashWithdrawalAsync(Guid shiftId, decimal amount, string reason, string authorizedByAdminName, string cashierName)
    {
        var withdrawal = new CashWithdrawal
        {
            WithdrawalId = Guid.NewGuid(),
            ShiftId = shiftId,
            Amount = amount,
            Reason = reason,
            AuthorizedByAdminName = authorizedByAdminName,
            CashierName = cashierName,
            CreatedAtUtc = DateTime.UtcNow
        };

        using var db = _connectionManager.CreateDbContext();
        db.CashWithdrawals.Add(withdrawal);
        await db.SaveChangesAsync();

        ShiftStateChanged?.Invoke();
        return withdrawal;
    }

    public async Task<IReadOnlyList<CashWithdrawal>> GetShiftCashWithdrawalsAsync(Guid shiftId)
    {
        using var db = _connectionManager.CreateDbContext();
        return await db.CashWithdrawals
            .Where(w => w.ShiftId == shiftId)
            .OrderByDescending(w => w.CreatedAtUtc)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<WorkShift>> GetShiftHistoryAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var branchId = CurrentBranchId;
        try
        {
            var apiHistory = await _apiClient.GetShiftHistoryAsync(fromDate, toDate, branchId);
            if (apiHistory != null && apiHistory.Count > 0)
            {
                return apiHistory;
            }
        }
        catch { }

        using var db = _connectionManager.CreateDbContext();
        var query = db.WorkShifts.AsNoTracking().AsQueryable();

        if (branchId.HasValue && branchId.Value > 0)
        {
            query = query.Where(s => s.BranchId == branchId.Value);
        }

        if (fromDate.HasValue)
        {
            var fromUtc = fromDate.Value.Date;
            query = query.Where(s => s.StartTimeUtc >= fromUtc);
        }

        if (toDate.HasValue)
        {
            var toUtc = toDate.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(s => s.StartTimeUtc <= toUtc);
        }

        return await query.OrderByDescending(s => s.StartTimeUtc).ToListAsync();
    }

    public async Task<WorkShift?> GetLastClosedShiftAsync()
    {
        var branchId = CurrentBranchId;
        using var db = _connectionManager.CreateDbContext();
        var query = db.WorkShifts
            .AsNoTracking()
            .Where(s => s.Status == 1); // 1 = Closed

        if (branchId.HasValue && branchId.Value > 0)
        {
            query = query.Where(s => s.BranchId == branchId.Value);
        }

        return await query
            .OrderByDescending(s => s.EndTimeUtc ?? s.ClosedAtUtc ?? s.CreatedAtUtc)
            .FirstOrDefaultAsync();
    }
}
