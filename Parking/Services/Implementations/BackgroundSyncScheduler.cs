using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using Parking.Services.Contracts;

namespace Parking.Services.Implementations;

public class BackgroundSyncScheduler : IBackgroundSyncScheduler
{
    private readonly ISyncEngineService _syncEngine;
    private readonly DispatcherTimer _hourlyTimer;

    public event EventHandler? SyncTriggered;

    public BackgroundSyncScheduler(ISyncEngineService syncEngine)
    {
        _syncEngine = syncEngine;

        _hourlyTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromHours(1)
        };
        _hourlyTimer.Tick += async (s, e) =>
        {
            await _syncEngine.PerformFullSyncAsync();
            SyncTriggered?.Invoke(this, EventArgs.Empty);
        };
    }

    public void Start()
    {
        _hourlyTimer.Start();
        _ = _syncEngine.PerformFullSyncAsync();
    }

    public void Stop()
    {
        _hourlyTimer.Stop();
    }

    public async Task TriggerManualSyncAsync()
    {
        await _syncEngine.PerformFullSyncAsync();
        SyncTriggered?.Invoke(this, EventArgs.Empty);
    }
}
