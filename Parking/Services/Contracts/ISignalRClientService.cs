using System;
using System.Threading.Tasks;
using Parking.Models.ApiModels;

namespace Parking.Services.Contracts;

public interface ISignalRClientService
{
    bool IsConnected { get; }
    Task StartAsync();
    Task StopAsync();
    Task SetCurrentBranchAsync(int branchId);
    Task EnsureConnectedAsync(int? branchId = null, int? companyId = null);
    event Action<ConfigNotificationDto>? ConfigUpdateRequired;
    event Action<bool>? ConnectionStatusChanged;
}
