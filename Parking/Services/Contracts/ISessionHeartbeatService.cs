using System;

namespace Parking.Services.Contracts;

public interface ISessionHeartbeatService
{
    event EventHandler<string>? SessionRevoked;
    void StartMonitoring();
    void StopMonitoring();
}
