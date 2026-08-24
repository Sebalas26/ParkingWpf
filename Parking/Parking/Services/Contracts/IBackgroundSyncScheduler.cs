using System;
using System.Threading.Tasks;

namespace Parking.Services.Contracts;

public interface IBackgroundSyncScheduler
{
    event EventHandler? SyncTriggered;
    void Start();
    void Stop();
    Task TriggerManualSyncAsync();
}
