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

    public event EventHandler? SubscriptionsChanged;

    public EfMonthlySubscriptionService(IDbConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
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
        subscription.PlateNumber = (subscription.PlateNumber ?? string.Empty).Trim().ToUpperInvariant();
        subscription.CreatedAtUtc = DateTime.UtcNow;
        subscription.IsActive = true;

        using var db = _connectionManager.CreateDbContext();
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
