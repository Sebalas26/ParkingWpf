using System;
using System.Threading;
using System.Threading.Tasks;
using Parking.Entities;

namespace Parking.Services.Contracts;

public class SyncProgressReport
{
    public int Percentage { get; set; }
    public string CurrentStepTitle { get; set; } = string.Empty;
    public string DetailMessage { get; set; } = string.Empty;
    public int StepIndex { get; set; } // 1 a 8
    public bool IsSuccessStep { get; set; } = true;
}

public class SyncResultReport
{
    public bool Success { get; set; }
    public bool IsOnline { get; set; }
    public int SyncedUsersCount { get; set; }
    public int SyncedPaymentMethodsCount { get; set; }
    public int SyncedRatesCount { get; set; }
    public int SyncedAgreementsCount { get; set; }
    public int SyncedShiftsCount { get; set; }
    public int SyncedSubscriptionsCount { get; set; }
    public int SyncedTicketsCount { get; set; }
    public int DispatchedOfflineItemsCount { get; set; }
    public string Message { get; set; } = string.Empty;
}

public interface ISyncEngineService
{
    event EventHandler<string>? SyncStatusChanged;
    event Action<int>? TotalCapacityChanged;
    event Action? DataSynchronized;

    bool IsOnline { get; }
    string SyncStatusDescription { get; }
    int PendingItemsCount { get; }
    DateTime? LastSyncTime { get; }
    int ServerConfiguredCapacity { get; }

    Task<bool> PerformFullSyncAsync();
    Task<SyncResultReport> PerformFullSyncWithProgressAsync(IProgress<SyncProgressReport> progress, CancellationToken ct = default);
    Task<bool> ForceCleanResyncAsync();
    Task EnqueueOfflineCheckInAsync(ParkingTicket ticket);
    Task EnqueueOfflineCheckOutAsync(ParkingTicket ticket);
    Task ProcessPendingQueueAsync();
    Task ClearLocalTicketsMemoryAsync();
}
