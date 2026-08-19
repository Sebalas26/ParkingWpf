using System;
using System.Threading.Tasks;
using Parking.Entities;

namespace Parking.Services.Contracts;

public interface ISyncEngineService
{
    event EventHandler<string>? SyncStatusChanged;
    bool IsOnline { get; }
    string SyncStatusDescription { get; }
    int PendingItemsCount { get; }
    DateTime? LastSyncTime { get; }

    Task<bool> PerformFullSyncAsync();
    Task EnqueueOfflineCheckInAsync(ParkingTicket ticket);
    Task EnqueueOfflineCheckOutAsync(ParkingTicket ticket);
    Task ProcessPendingQueueAsync();
}
