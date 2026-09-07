using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Parking.Core.Enums;
using Parking.Data;
using Parking.Data.Factories;

namespace Parking.UnitTests.Common;

public class TestDbConnectionManager : IDbConnectionManager, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ParkFlowDbContext> _options;

    public TestDbConnectionManager()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<ParkFlowDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var ctx = CreateDbContext();
        ctx.Database.EnsureCreated();
    }

    public bool IsOnlineMode => false;
    public DatabaseProviderType CurrentProvider => DatabaseProviderType.Sqlite;
    public string StatusDescription => "SQLite InMemory Test Provider";
#pragma warning disable CS0067
    public event EventHandler<bool>? ConnectionStateChanged;
#pragma warning restore CS0067

    public ParkFlowDbContext CreateDbContext()
    {
        return new ParkFlowDbContext(_options);
    }

    public Task InitializeDatabaseAsync() => Task.CompletedTask;
    public Task<bool> TestConnectionAsync() => Task.FromResult(true);

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
