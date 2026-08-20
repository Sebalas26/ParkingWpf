using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Parking.Entities;

namespace Parking.Services.Contracts;

public interface IMonthlySubscriptionService
{
    event EventHandler? SubscriptionsChanged;

    Task<IReadOnlyList<MonthlySubscription>> GetAllSubscriptionsAsync();
    Task<IReadOnlyList<MonthlySubscription>> GetActiveSubscriptionsAsync();
    Task<MonthlySubscription?> GetActiveSubscriptionByPlateAsync(string plateNumber);
    Task<MonthlySubscription> CreateSubscriptionAsync(MonthlySubscription subscription);
    Task<MonthlySubscription?> RenewSubscriptionAsync(Guid subscriptionId, int additionalMonths, decimal amountPaid, Core.Enums.PaymentMethod paymentMethod, string? notes);
    Task<bool> CancelSubscriptionAsync(Guid subscriptionId);
}
