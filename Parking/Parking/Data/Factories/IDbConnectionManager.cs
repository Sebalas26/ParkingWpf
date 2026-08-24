using System;
using System.Threading.Tasks;
using Parking.Core.Enums;

namespace Parking.Data.Factories;

public interface IDbConnectionManager
{
    bool IsOnlineMode { get; }
    DatabaseProviderType CurrentProvider { get; }
    string StatusDescription { get; }
    event EventHandler<bool>? ConnectionStateChanged;

    ParkFlowDbContext CreateDbContext();
    Task InitializeDatabaseAsync();
    Task<bool> TestConnectionAsync();
}
