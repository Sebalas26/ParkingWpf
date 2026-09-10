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
    private readonly IServiceProvider? _serviceProvider;

    public WorkShift? CurrentShift { get; private set; }
    public bool HasActiveShift => CurrentShift != null && CurrentShift.Status == 0;
    public event Action? ShiftStateChanged;

    private ISyncEngineService? SyncEngine => _serviceProvider?.GetService(typeof(ISyncEngineService)) as ISyncEngineService;
    private bool IsOnline => SyncEngine?.IsOnline ?? true;

    public EfShiftService(
        IDbConnectionManager connectionManager,
        IApiClientService apiClient,
        IAuthService authService,
        ISessionService sessionService,
        IServiceProvider? serviceProvider = null)
    {
        _connectionManager = connectionManager;
        _apiClient = apiClient;
        _authService = authService;
        _sessionService = sessionService;
        _serviceProvider = serviceProvider;
    }

    private int? CurrentBranchId => _sessionService.CurrentBranch?.Id ?? _sessionService.CurrentBranchId;

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
            using (var dbCheck = _connectionManager.CreateDbContext())
            {
                var recoveredId = await dbCheck.Branches
                    .Where(b => b.CompanyId.HasValue && b.CompanyId.Value > 0)
                    .Select(b => b.CompanyId)
                    .FirstOrDefaultAsync();

                if (!recoveredId.HasValue || recoveredId.Value <= 0)
                {
                    recoveredId = await dbCheck.ParkingTickets
                        .Where(t => t.CompanyId > 0)
                        .OrderByDescending(t => t.EntryTimeUtc)
                        .Select(t => (int?)t.CompanyId)
                        .FirstOrDefaultAsync();
                }

                if (!recoveredId.HasValue || recoveredId.Value <= 0)
                {
                    recoveredId = await dbCheck.BillingResolutions
                        .Where(r => r.CompanyId.HasValue && r.CompanyId.Value > 0)
                        .Select(r => r.CompanyId)
                        .FirstOrDefaultAsync();
                }

                companyId = (recoveredId.HasValue && recoveredId.Value > 0) ? recoveredId.Value : 1;

                if (_sessionService.CurrentUser != null)
                {
                    _sessionService.CurrentUser.CompanyId = companyId.Value;
                }
                if (_sessionService.CurrentBranch != null)
                {
                    _sessionService.CurrentBranch.CompanyId = companyId.Value;
                }
            }
        }

        var operatorName = _authService.CurrentUser?.FullName ?? "Operador General";
        var request = new OpenShiftApiRequest
        {
            BranchId = branchId.Value,
            CompanyId = companyId.Value,
            UserId = _authService.CurrentUser?.ServerUserId,
            BaseAmount = baseAmount,
            Notes = notes
        };

        WorkShift? shift = null;

        if (IsOnline)
        {
            try
            {
                shift = await _apiClient.OpenShiftAsync(request);
                if (shift != null)
                {
                    shift.IsSynchronized = true;
                }
            }
            catch (InvalidOperationException)
            {
                // El servidor central rechazó activamente la apertura de turno (regla de negocio / validación)
                throw;
            }
            catch
            {
                // Fallo de conectividad o modo offline: continuar con la apertura local en SQLite
            }
        }

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
            existing.IsSynchronized = shift.IsSynchronized;
        }
        await db.SaveChangesAsync();

        CurrentShift = shift;
        ShiftStateChanged?.Invoke();
        return shift;
    }

    public async Task RefreshCurrentShiftAsync()
    {
        var branchId = CurrentBranchId;
        var currentUser = _authService.CurrentUser;
        int? queryUserId = (currentUser != null && !currentUser.IsAdmin && currentUser.ServerUserId.HasValue)
            ? currentUser.ServerUserId.Value
            : null;

        if (IsOnline)
        {
            try
            {
                var apiShift = await _apiClient.GetActiveShiftAsync(userId: queryUserId, branchId: branchId);
                if (apiShift != null)
                {
                    using var dbPersist = _connectionManager.CreateDbContext();
                    var local = await dbPersist.WorkShifts.FirstOrDefaultAsync(s => s.ShiftId == apiShift.ShiftId);
                    if (local == null)
                    {
                        apiShift.IsSynchronized = true;
                        dbPersist.WorkShifts.Add(apiShift);
                    }
                    else
                    {
                        local.Status = apiShift.Status;
                        local.BaseAmount = apiShift.BaseAmount;
                        local.StartTimeUtc = apiShift.StartTimeUtc;
                        local.EndTimeUtc = apiShift.EndTimeUtc;
                        local.OperatorName = apiShift.OperatorName;
                        local.UserId = apiShift.UserId;
                        local.BranchId = apiShift.BranchId;
                        local.CompanyId = apiShift.CompanyId;
                        local.CashRegisterName = apiShift.CashRegisterName;
                        local.Notes = apiShift.Notes;
                        local.IsSynchronized = true;
                    }
                    await dbPersist.SaveChangesAsync();

                    CurrentShift = apiShift;
                    ShiftStateChanged?.Invoke();
                    return;
                }
                else
                {
                    // El API respondió confirmando que no hay turno activo (cerrado centralmente desde PWA)
                    using var dbClose = _connectionManager.CreateDbContext();
                    if (branchId.HasValue && branchId.Value > 0)
                    {
                        var openLocalShifts = await dbClose.WorkShifts
                            .Where(s => s.BranchId == branchId.Value && s.Status == 0)
                            .ToListAsync();
                        foreach (var s in openLocalShifts)
                        {
                            s.Status = 1;
                            s.EndTimeUtc ??= DateTime.UtcNow;
                        }
                        if (openLocalShifts.Count > 0)
                        {
                            await dbClose.SaveChangesAsync();
                        }
                    }
                    CurrentShift = null;
                    ShiftStateChanged?.Invoke();
                    return;
                }
            }
            catch { }
        }

        // Si falló la consulta online o estamos en modo offline, resolver contra SQLite local
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
    }

    public async Task<WorkShift?> GetActiveShiftAsync()
    {
        await RefreshCurrentShiftAsync();
        return CurrentShift;
    }

    public async Task<ShiftSummaryModel> GetCurrentShiftSummaryAsync()
    {
        var branchId = CurrentBranchId;
        var activeShift = CurrentShift ?? await GetActiveShiftAsync();
        var startTime = activeShift?.StartTimeUtc ?? DateTime.UtcNow.Date;
        var baseAmount = activeShift?.BaseAmount ?? 0m;
        var shiftId = activeShift?.ShiftId ?? Guid.Empty;
        var operatorName = activeShift?.OperatorName ?? (_authService.CurrentUser?.FullName ?? "Operador General");

        ShiftSummaryModel? remoteSummary = null;
        if (IsOnline && shiftId != Guid.Empty)
        {
            try
            {
                remoteSummary = await _apiClient.GetShiftSummaryAsync(shiftId);
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

        // Medios de pago configurados en base de datos para la sede
        var branchPmIds = branchId.HasValue
            ? await db.BranchPaymentMethods
                .Where(bpm => bpm.BranchId == branchId.Value && bpm.IsActive)
                .Select(bpm => bpm.PaymentMethodId)
                .ToListAsync()
            : new List<int>();

        var paymentMethods = await db.PaymentMethods
            .Where(pm => pm.State && (branchPmIds.Count == 0 || branchPmIds.Contains(pm.Id)))
            .ToListAsync();

        if (paymentMethods.Count == 0)
        {
            paymentMethods = await db.PaymentMethods.Where(pm => pm.State).ToListAsync();
        }

        var breakdown = new List<ShiftPaymentMethodItem>();
        decimal cash = 0m;
        decimal card = 0m;
        decimal transfer = 0m;
        decimal discounts = completedTickets.Sum(t => t.DiscountAmount);

        // Agrupar tiquetes por PaymentMethodId
        var ticketsByPmId = completedTickets
            .Where(t => t.PaymentMethodId.HasValue && t.PaymentMethodId.Value > 0)
            .GroupBy(t => t.PaymentMethodId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var unassignedTickets = completedTickets
            .Where(t => !t.PaymentMethodId.HasValue || t.PaymentMethodId.Value <= 0)
            .ToList();

        foreach (var pm in paymentMethods)
        {
            ticketsByPmId.TryGetValue(pm.Id, out var pmTickets);
            pmTickets ??= new List<ParkingTicket>();

            var isCash = pm.RequiresCashTender || pm.Name.ToLowerInvariant().Contains("efectivo");
            var isCard = pm.Name.ToLowerInvariant().Contains("tarjeta") || pm.Name.ToLowerInvariant().Contains("card") || pm.Name.ToLowerInvariant().Contains("credito") || pm.Name.ToLowerInvariant().Contains("debito");
            var isTransfer = pm.Name.ToLowerInvariant().Contains("nequi") || pm.Name.ToLowerInvariant().Contains("transfer") || pm.Name.ToLowerInvariant().Contains("qr") || pm.Name.ToLowerInvariant().Contains("davi");

            // Si hay tiquetes sin PaymentMethodId, asignar por fallback
            if (unassignedTickets.Count > 0)
            {
                var matched = unassignedTickets.Where(t =>
                    (isCash && (!t.PaymentMethod.HasValue || t.PaymentMethod == PaymentMethod.Cash)) ||
                    (isCard && (t.PaymentMethod == PaymentMethod.CreditCard || t.PaymentMethod == PaymentMethod.DebitCard)) ||
                    (isTransfer && t.PaymentMethod == PaymentMethod.DigitalTransfer)
                ).ToList();

                if (matched.Count > 0)
                {
                    pmTickets = pmTickets.Concat(matched).ToList();
                    foreach (var m in matched) unassignedTickets.Remove(m);
                }
            }

            var totalAmount = pmTickets.Sum(t => t.NetAmount);
            var txCount = pmTickets.Count;

            if (isCash)
            {
                cash += totalAmount;
            }
            else if (isCard)
            {
                card += totalAmount;
            }
            else if (isTransfer)
            {
                transfer += totalAmount;
            }
            else
            {
                cash += totalAmount;
            }

            string iconKey = isCash ? "IconCash" : (isTransfer ? "IconQr" : (isCard ? "IconCard" : "IconCash"));
            string iconBg = isCash ? "#E0F2F1" : (isTransfer ? "#E0F7FA" : (isCard ? "#E0F2F1" : "#F1F5F9"));
            string iconBrush = isCash ? "BrushPrimary" : (isTransfer ? "BrushCyan" : (isCard ? "BrushPrimary" : "BrushTextSecondary"));
            string amountBrush = isCash ? "BrushPrimary" : (isTransfer ? "BrushCyan" : (isCard ? "BrushPrimary" : "BrushTextPrimary"));

            breakdown.Add(new ShiftPaymentMethodItem
            {
                PaymentMethodId = pm.Id,
                Name = pm.Name,
                IconKey = iconKey,
                IconBg = iconBg,
                IconBrushKey = iconBrush,
                AmountBrushKey = amountBrush,
                TotalCollected = totalAmount,
                TransactionCount = txCount,
                Subtitle = txCount == 0 ? "Sin cobros registrados" : (txCount == 1 ? "1 tiquete cobrado" : $"{txCount} tiquetes cobrados"),
                RequiresCashTender = isCash
            });
        }

        // Tiquetes restantes que no encajaron
        if (unassignedTickets.Count > 0)
        {
            var extraAmount = unassignedTickets.Sum(t => t.NetAmount);
            cash += extraAmount;
            var cashItem = breakdown.FirstOrDefault(b => b.RequiresCashTender);
            if (cashItem != null)
            {
                cashItem.TotalCollected += extraAmount;
                cashItem.TransactionCount += unassignedTickets.Count;
                cashItem.Subtitle = $"{cashItem.TransactionCount} tiquetes cobrados";
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

        // Si el remoto respondió pero no tenía el desglose por medio de pago, enriquecerlo
        if (remoteSummary != null)
        {
            remoteSummary.PaymentMethodsBreakdown = breakdown;
            if (breakdown.Count > 0)
            {
                remoteSummary.TotalCashCollected = cash;
                remoteSummary.TotalCardCollected = card;
                remoteSummary.TotalTransferCollected = transfer;
                remoteSummary.ExpectedCash = remoteSummary.BaseAmount + cash - (remoteSummary.TotalCashWithdrawals > 0 ? remoteSummary.TotalCashWithdrawals : withdrawals);
                remoteSummary.CashDifference = remoteSummary.ActualCashCounted - remoteSummary.ExpectedCash;
            }
            return remoteSummary;
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
            Notes = activeShift?.Notes,
            PaymentMethodsBreakdown = breakdown
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
        if (IsOnline)
        {
            try
            {
                closedShift = await _apiClient.CloseShiftAsync(request);
            }
            catch { }
        }

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
