using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using Parking.Data.Factories;
using Parking.Services.Contracts;

namespace Parking.Services.Implementations;

public class SessionHeartbeatService : ISessionHeartbeatService
{
    private readonly IAuthService _authService;
    private readonly IDbConnectionManager _connectionManager;
    private readonly DispatcherTimer _timer;
    private bool _isChecking;

    public event EventHandler<string>? SessionRevoked;

    public SessionHeartbeatService(IAuthService authService, IDbConnectionManager connectionManager)
    {
        _authService = authService;
        _connectionManager = connectionManager;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _timer.Tick += async (s, e) => await CheckSessionHeartbeatAsync();
    }

    public void StartMonitoring()
    {
        if (!_timer.IsEnabled)
        {
            _timer.Start();
        }
    }

    public void StopMonitoring()
    {
        if (_timer.IsEnabled)
        {
            _timer.Stop();
        }
    }

    private async Task CheckSessionHeartbeatAsync()
    {
        if (_isChecking || !_authService.IsAuthenticated || _authService.CurrentUser == null)
        {
            return;
        }

        _isChecking = true;
        try
        {
            using var db = _connectionManager.CreateDbContext();
            var sessionToken = _authService.CurrentUser.SessionToken;
            var session = await db.UserSessions.FirstOrDefaultAsync(s => s.SessionToken == sessionToken);

            if (session == null || !session.IsActive)
            {
                StopMonitoring();
                await _authService.LogoutAsync();
                SessionRevoked?.Invoke(this, "Se ha iniciado sesión en otro dispositivo.");
            }
            else
            {
                session.LastHeartbeatUtc = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
        }
        catch
        {
        }
        finally
        {
            _isChecking = false;
        }
    }
}
