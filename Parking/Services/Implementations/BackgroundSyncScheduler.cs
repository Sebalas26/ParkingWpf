using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using Parking.Services.Contracts;

namespace Parking.Services.Implementations;

public class BackgroundSyncScheduler : IBackgroundSyncScheduler
{
    private readonly ISyncEngineService _syncEngine;
    private readonly DispatcherTimer _hourlyTimer;
    private bool _isSyncInProgress;

    public event EventHandler? SyncTriggered;

    public BackgroundSyncScheduler(ISyncEngineService syncEngine)
    {
        _syncEngine = syncEngine;

        _hourlyTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(15)
        };
        _hourlyTimer.Tick += async (s, e) =>
        {
            if (_isSyncInProgress) return;
            try
            {
                _isSyncInProgress = true;
                await _syncEngine.PerformFullSyncAsync();
                SyncTriggered?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                _isSyncInProgress = false;
            }
        };
    }

    public void Start()
    {
        _hourlyTimer.Start();
        _ = TriggerManualSyncAsync();
    }

    public void Stop()
    {
        _hourlyTimer.Stop();
    }

    public async Task TriggerManualSyncAsync()
    {
        if (_isSyncInProgress) return;
        try
        {
            _isSyncInProgress = true;
            await _syncEngine.PerformFullSyncAsync();
            SyncTriggered?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _isSyncInProgress = false;
        }
    }
}
