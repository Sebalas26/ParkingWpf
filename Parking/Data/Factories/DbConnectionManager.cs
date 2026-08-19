using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Parking.Core.Constants;
using Parking.Core.Enums;
using Parking.Entities;

namespace Parking.Data.Factories;

public class DbConnectionManager : IDbConnectionManager
{
    private readonly string _sqliteConnectionString;

    public bool IsOnlineMode => true;
    public DatabaseProviderType CurrentProvider => DatabaseProviderType.Sqlite;
    public string StatusDescription => "SQLite Local Resiliente (Caché Local)";

    public event EventHandler<bool>? ConnectionStateChanged;

    public DbConnectionManager(string? sqliteConnectionString = null)
    {
        _sqliteConnectionString = sqliteConnectionString ?? "Data Source=parkflow_local.db;";
    }

    public ParkFlowDbContext CreateDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<ParkFlowDbContext>();
        optionsBuilder.UseSqlite(_sqliteConnectionString);
        return new ParkFlowDbContext(optionsBuilder.Options);
    }

    public async Task InitializeDatabaseAsync()
    {
        using var context = CreateDbContext();
        await context.Database.EnsureCreatedAsync();

        // Asegurar que la tabla de pendientes de sincronización exista
        try
        {
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""PendingSyncItems"" (
                    ""PendingSyncItemId"" TEXT NOT NULL PRIMARY KEY,
                    ""EntityType"" TEXT NOT NULL,
                    ""Action"" TEXT NOT NULL,
                    ""PayloadJson"" TEXT NOT NULL,
                    ""RetryCount"" INTEGER NOT NULL DEFAULT 0,
                    ""Status"" INTEGER NOT NULL DEFAULT 0,
                    ""ErrorMessage"" TEXT NULL,
                    ""CreatedAtUtc"" TEXT NOT NULL,
                    ""LastAttemptUtc"" TEXT NULL
                );
            ");
        }
        catch { }

        await SeedDefaultDataAsync(context);
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            using var context = CreateDbContext();
            return await context.Database.CanConnectAsync();
        }
        catch
        {
            return false;
        }
    }

    public void SwitchToLocalMode()
    {
        ConnectionStateChanged?.Invoke(this, false);
    }

    public void SwitchToOnlineMode()
    {
        ConnectionStateChanged?.Invoke(this, true);
    }

    private async Task SeedDefaultDataAsync(ParkFlowDbContext context)
    {
        if (!await context.Roles.AnyAsync())
        {
            var adminRoleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var operatorRoleId = Guid.Parse("22222222-2222-2222-2222-222222222222");

            context.Roles.AddRange(
                new Role
                {
                    RoleId = adminRoleId,
                    Name = "Administrador",
                    Description = "Acceso total y configuración del sistema",
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                },
                new Role
                {
                    RoleId = operatorRoleId,
                    Name = "Operador",
                    Description = "Registro de entradas, salidas y cobros",
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                }
            );
            await context.SaveChangesAsync();

            if (!await context.Users.AnyAsync())
            {
                var adminUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
                var operatorUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

                context.Users.AddRange(
                    new User
                    {
                        UserId = adminUserId,
                        RoleId = adminRoleId,
                        Username = "admin",
                        PasswordHash = HashPassword("admin123"),
                        FullName = "Administrador Principal",
                        Email = "admin@parkflow.com",
                        IsActive = true,
                        CreatedAtUtc = DateTime.UtcNow
                    },
                    new User
                    {
                        UserId = operatorUserId,
                        RoleId = operatorRoleId,
                        Username = "operador",
                        PasswordHash = HashPassword("operador123"),
                        FullName = "Operador de Turno",
                        Email = "operador@parkflow.com",
                        IsActive = true,
                        CreatedAtUtc = DateTime.UtcNow
                    }
                );
                await context.SaveChangesAsync();
            }
        }

        if (!await context.VehicleRates.AnyAsync())
        {
            context.VehicleRates.AddRange(
                new VehicleRate
                {
                    RateId = Guid.NewGuid(),
                    VehicleType = VehicleType.Car,
                    DisplayName = "Automóvil / Sedán",
                    HourRate = 4000m,
                    MinuteRate = 70m,
                    FullDayRate = 28000m,
                    GracePeriodMinutes = 15,
                    IconKey = "IconCar",
                    IsActive = true,
                    UpdatedAtUtc = DateTime.UtcNow
                },
                new VehicleRate
                {
                    RateId = Guid.NewGuid(),
                    VehicleType = VehicleType.Motorcycle,
                    DisplayName = "Motocicleta",
                    HourRate = 2000m,
                    MinuteRate = 35m,
                    FullDayRate = 14000m,
                    GracePeriodMinutes = 15,
                    IconKey = "IconMotorcycle",
                    IsActive = true,
                    UpdatedAtUtc = DateTime.UtcNow
                },
                new VehicleRate
                {
                    RateId = Guid.NewGuid(),
                    VehicleType = VehicleType.Suv,
                    DisplayName = "Camioneta / SUV",
                    HourRate = 5000m,
                    MinuteRate = 85m,
                    FullDayRate = 35000m,
                    GracePeriodMinutes = 15,
                    IconKey = "IconSuv",
                    IsActive = true,
                    UpdatedAtUtc = DateTime.UtcNow
                },
                new VehicleRate
                {
                    RateId = Guid.NewGuid(),
                    VehicleType = VehicleType.Van,
                    DisplayName = "Furgón / Minibús",
                    HourRate = 6000m,
                    MinuteRate = 100m,
                    FullDayRate = 42000m,
                    GracePeriodMinutes = 15,
                    IconKey = "IconVan",
                    IsActive = true,
                    UpdatedAtUtc = DateTime.UtcNow
                },
                new VehicleRate
                {
                    RateId = Guid.NewGuid(),
                    VehicleType = VehicleType.HeavyTruck,
                    DisplayName = "Vehículo Pesado / Camión",
                    HourRate = 10000m,
                    MinuteRate = 170m,
                    FullDayRate = 70000m,
                    GracePeriodMinutes = 15,
                    IconKey = "IconTruck",
                    IsActive = true,
                    UpdatedAtUtc = DateTime.UtcNow
                }
            );
            await context.SaveChangesAsync();
        }
    }

    public static string HashPassword(string password)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password + "ParkFlowSalt2026");
        var hash = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}
