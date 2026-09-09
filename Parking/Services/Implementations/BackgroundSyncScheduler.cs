using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using Parking.Services.Contracts;

namespace Parking.Services.Implementations;

public class BackgroundSyncScheduler : IBackgroundSyncScheduler
{
    private readonly ISyncEngineService _syncEngine;
    private readonly IApiClientService? _apiClient;
    private readonly DispatcherTimer _syncTimer;
    private bool _isSyncInProgress;

    public event EventHandler? SyncTriggered;

    public BackgroundSyncScheduler(ISyncEngineService syncEngine, IApiClientService? apiClient = null)
    {
        _syncEngine = syncEngine;
        _apiClient = apiClient;

        _syncTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(5)
        };
        _syncTimer.Tick += async (s, e) =>
        {
            if (_isSyncInProgress || !_syncEngine.IsOnline) return;
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
        _syncTimer.Start();
        _ = TriggerManualSyncAsync();
    }

    public void Stop()
    {
        _syncTimer.Stop();
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
