using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Parking.Core.Enums;
using Parking.Data.Factories;
using Parking.Entities;
using Parking.Services.Contracts;

namespace Parking.Services.Implementations;

public class EfMonthlySubscriptionService : IMonthlySubscriptionService
{
    private readonly IDbConnectionManager _connectionManager;
    private readonly ISessionService _sessionService;

    public event EventHandler? SubscriptionsChanged;

    public EfMonthlySubscriptionService(IDbConnectionManager connectionManager, ISessionService sessionService)
    {
        _connectionManager = connectionManager;
        _sessionService = sessionService;
    }

    public async Task<IReadOnlyList<MonthlySubscription>> GetAllSubscriptionsAsync()
    {
        using var db = _connectionManager.CreateDbContext();
        return await db.MonthlySubscriptions
            .OrderByDescending(s => s.StartDateUtc)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<MonthlySubscription>> GetActiveSubscriptionsAsync()
    {
        using var db = _connectionManager.CreateDbContext();
        var all = await db.MonthlySubscriptions.ToListAsync();
        var now = DateTime.UtcNow;
        return all
            .Where(s => s.IsActive && s.EndDateUtc >= now)
            .OrderByDescending(s => s.StartDateUtc)
            .ToList();
    }

    public async Task<MonthlySubscription?> GetActiveSubscriptionByPlateAsync(string plateNumber)
    {
        if (string.IsNullOrWhiteSpace(plateNumber)) return null;
        var normalized = plateNumber.Trim().ToUpperInvariant();

        using var db = _connectionManager.CreateDbContext();
        var all = await db.MonthlySubscriptions.ToListAsync();
        var now = DateTime.UtcNow;

        return all
            .Where(s => s.IsActive && s.PlateNumber.Equals(normalized, StringComparison.OrdinalIgnoreCase) && s.EndDateUtc >= now)
            .OrderByDescending(s => s.EndDateUtc)
            .FirstOrDefault();
    }

    public async Task<MonthlySubscription> CreateSubscriptionAsync(MonthlySubscription subscription)
    {
        var branchId = subscription.BranchId ?? _sessionService.CurrentBranch?.Id ?? _sessionService.CurrentBranchId;
        if (!branchId.HasValue || branchId.Value <= 0)
        {
            throw new InvalidOperationException("Debe seleccionar una sede activa antes de registrar la mensualidad.");
        }

        var companyId = subscription.CompanyId ?? _sessionService.CurrentCompanyId;
        if (!companyId.HasValue || companyId.Value <= 0)
        {
            throw new InvalidOperationException("La sesión no cuenta con una empresa (CompanyId) asignada.");
        }

        subscription.BranchId = branchId.Value;
        subscription.CompanyId = companyId.Value;
        subscription.PlateNumber = (subscription.PlateNumber ?? string.Empty).Trim().ToUpperInvariant();
        subscription.CreatedAtUtc = DateTime.UtcNow;
        subscription.IsActive = true;

        using var db = _connectionManager.CreateDbContext();
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"MonthlySubscriptions\" ADD COLUMN \"CompanyId\" INTEGER NULL;"); } catch { }
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"MonthlySubscriptions\" ADD COLUMN \"BranchId\" INTEGER NULL;"); } catch { }

        db.MonthlySubscriptions.Add(subscription);
        await db.SaveChangesAsync();

        SubscriptionsChanged?.Invoke(this, EventArgs.Empty);
        return subscription;
    }

    public async Task<MonthlySubscription?> RenewSubscriptionAsync(Guid subscriptionId, int additionalMonths, decimal amountPaid, PaymentMethod paymentMethod, string? notes)
    {
        using var db = _connectionManager.CreateDbContext();
        var sub = await db.MonthlySubscriptions.FindAsync(subscriptionId);
        if (sub == null) return null;

        var baseDate = sub.EndDateUtc > DateTime.UtcNow ? sub.EndDateUtc : DateTime.UtcNow;
        sub.EndDateUtc = baseDate.AddMonths(additionalMonths > 0 ? additionalMonths : 1);
        sub.AmountPaid += amountPaid;
        sub.PaymentMethod = paymentMethod;
        sub.IsActive = true;

        if (!string.IsNullOrWhiteSpace(notes))
        {
            sub.Notes = string.IsNullOrWhiteSpace(sub.Notes)
                ? notes.Trim()
                : $"{sub.Notes} | Renovación: {notes.Trim()}";
        }

        await db.SaveChangesAsync();
        SubscriptionsChanged?.Invoke(this, EventArgs.Empty);
        return sub;
    }

    public async Task<bool> CancelSubscriptionAsync(Guid subscriptionId)
    {
        using var db = _connectionManager.CreateDbContext();
        var sub = await db.MonthlySubscriptions.FindAsync(subscriptionId);
        if (sub == null) return false;

        sub.IsActive = false;
        await db.SaveChangesAsync();
        SubscriptionsChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }
}
