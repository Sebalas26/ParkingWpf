using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Parking.Data;
using Parking.Data.Factories;
using Xunit;

namespace Parking.UnitTests.Common;

public class DbAutoMigrationTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public DbAutoMigrationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    [Fact]
    public async Task AutoMigrateDatabaseAsync_WhenColumnsMissingInExistingTable_ShouldAddColumnsDynamically()
    {
        // 1. Simular una tabla 'Branches' preexistente vieja con solo Id y Name
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = @"
                CREATE TABLE ""Branches"" (
                    ""Id"" INTEGER PRIMARY KEY,
                    ""Name"" TEXT NOT NULL
                );
            ";
            await cmd.ExecuteNonQueryAsync();
        }

        // 2. Configurar EF Core usando la conexión en memoria
        var options = new DbContextOptionsBuilder<ParkFlowDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new ParkFlowDbContext(options);
        var manager = new DbConnectionManager("DataSource=:memory:");

        // 3. Act: Ejecutar la auto-migración dinámica
        await manager.AutoMigrateDatabaseAsync(context);

        // 4. Assert: Verificar que columnas nuevas como 'EntryGracePeriodMinutes' y 'ExitGracePeriodMinutes' existan
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var pragmaCmd = _connection.CreateCommand())
        {
            pragmaCmd.CommandText = "PRAGMA table_info(\"Branches\");";
            using var reader = await pragmaCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add(reader.GetString(1));
            }
        }

        Assert.Contains("EntryGracePeriodMinutes", columns);
        Assert.Contains("ExitGracePeriodMinutes", columns);
        Assert.Contains("Code", columns);
        Assert.Contains("PaperWidth", columns);
    }
}
