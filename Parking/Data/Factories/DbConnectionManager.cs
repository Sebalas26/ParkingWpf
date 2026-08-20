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

        // Asegurar que las tablas auxiliares y de turnos existan
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

                CREATE TABLE IF NOT EXISTS ""WorkShifts"" (
                    ""ShiftId"" TEXT NOT NULL PRIMARY KEY,
                    ""UserId"" INTEGER NOT NULL,
                    ""OperatorName"" TEXT NOT NULL,
                    ""StartTimeUtc"" TEXT NOT NULL,
                    ""EndTimeUtc"" TEXT NULL,
                    ""BaseAmount"" TEXT NOT NULL,
                    ""TotalCashCollected"" TEXT NOT NULL,
                    ""TotalCardCollected"" TEXT NOT NULL,
                    ""TotalTransferCollected"" TEXT NOT NULL,
                    ""TotalDiscounts"" TEXT NOT NULL,
                    ""ExpectedCash"" TEXT NOT NULL,
                    ""ActualCashCounted"" TEXT NOT NULL,
                    ""CashDifference"" TEXT NOT NULL,
                    ""TotalTicketsProcessed"" INTEGER NOT NULL DEFAULT 0,
                    ""TotalVehiclesEntered"" INTEGER NOT NULL DEFAULT 0,
                    ""Status"" INTEGER NOT NULL DEFAULT 0,
                    ""Notes"" TEXT NULL,
                    ""HandoverToUserId"" TEXT NULL,
                    ""HandoverToUserName"" TEXT NULL,
                    ""IsSynchronized"" INTEGER NOT NULL DEFAULT 1,
                    ""CreatedAtUtc"" TEXT NOT NULL,
                    ""ClosedAtUtc"" TEXT NULL
                );

                CREATE TABLE IF NOT EXISTS ""PaymentMethods"" (
                    ""Id"" INTEGER PRIMARY KEY AUTOINCREMENT,
                    ""Name"" TEXT NOT NULL,
                    ""Icon"" TEXT NOT NULL DEFAULT 'IconCash',
                    ""State"" INTEGER NOT NULL DEFAULT 1,
                    ""RequiresCashTender"" INTEGER NOT NULL DEFAULT 1
                );

                CREATE TABLE IF NOT EXISTS ""MonthlySubscriptions"" (
                    ""SubscriptionId"" TEXT NOT NULL PRIMARY KEY,
                    ""CustomerName"" TEXT NOT NULL,
                    ""CustomerDocument"" TEXT NOT NULL,
                    ""CustomerPhone"" TEXT NOT NULL,
                    ""CustomerEmail"" TEXT NULL,
                    ""PlateNumber"" TEXT NOT NULL,
                    ""VehicleType"" INTEGER NOT NULL DEFAULT 0,
                    ""StartDateUtc"" TEXT NOT NULL,
                    ""EndDateUtc"" TEXT NOT NULL,
                    ""MonthlyFee"" TEXT NOT NULL,
                    ""AmountPaid"" TEXT NOT NULL,
                    ""PaymentMethod"" INTEGER NOT NULL DEFAULT 0,
                    ""IsActive"" INTEGER NOT NULL DEFAULT 1,
                    ""Notes"" TEXT NULL,
                    ""CreatedAtUtc"" TEXT NOT NULL
                );
            ");

            try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"WorkShifts\" ADD COLUMN \"HandoverToUserId\" TEXT NULL;"); } catch { }
            try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"WorkShifts\" ADD COLUMN \"HandoverToUserName\" TEXT NULL;"); } catch { }
            try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"ParkingTickets\" ADD COLUMN \"PaymentMethodId\" INTEGER NULL;"); } catch { }
            try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"ParkingTickets\" ADD COLUMN \"ExitNotes\" TEXT NULL;"); } catch { }
            try { await context.Database.ExecuteSqlRawAsync("UPDATE \"VehicleRates\" SET \"GracePeriodMinutes\" = 0;"); } catch { }
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
                        PasswordHash = HashPassword("Admin2026*"),
                        FullName = "Administrador Principal",
                        Email = "admin@parkflow.local",
                        IsActive = true,
                        CreatedAtUtc = DateTime.UtcNow
                    },
                    new User
                    {
                        UserId = operatorUserId,
                        RoleId = operatorRoleId,
                        Username = "operador",
                        PasswordHash = HashPassword("Operador2026*"),
                        FullName = "Operador de Turno",
                        Email = "operador@parkflow.local",
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
                    VehicleType = VehicleType.Motorcycle,
                    DisplayName = "Motocicleta",
                    HourRate = 2000m,
                    MinuteRate = 35m,
                    FullDayRate = 14000m,
                    GracePeriodMinutes = 0,
                    IconKey = "IconMotorcycle",
                    IsActive = true,
                    UpdatedAtUtc = DateTime.UtcNow
                },
                new VehicleRate
                {
                    RateId = Guid.NewGuid(),
                    VehicleType = VehicleType.Car,
                    DisplayName = "Automóvil / Sedán",
                    HourRate = 4000m,
                    MinuteRate = 70m,
                    FullDayRate = 28000m,
                    GracePeriodMinutes = 0,
                    IconKey = "IconCar",
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
                    GracePeriodMinutes = 0,
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
                    GracePeriodMinutes = 0,
                    IconKey = "IconTruck",
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
                    GracePeriodMinutes = 0,
                    IconKey = "IconSuv",
                    IsActive = false,
                    UpdatedAtUtc = DateTime.UtcNow
                }
            );
            await context.SaveChangesAsync();
        }
        else
        {
            // Sincronizar nombres e inactivar Camioneta en base existente
            try
            {
                var allDbRates = await context.VehicleRates.ToListAsync();
                foreach (var r in allDbRates)
                {
                    if (r.VehicleType == VehicleType.Motorcycle)
                    {
                        r.DisplayName = "Motocicleta";
                        r.HourRate = 2000m;
                        r.IconKey = "IconMotorcycle";
                        r.IsActive = true;
                    }
                    else if (r.VehicleType == VehicleType.Car)
                    {
                        r.DisplayName = "Automóvil / Sedán";
                        r.HourRate = 4000m;
                        r.IconKey = "IconCar";
                        r.IsActive = true;
                    }
                    else if (r.VehicleType == VehicleType.Van)
                    {
                        r.DisplayName = "Furgón / Minibús";
                        r.HourRate = 6000m;
                        r.IconKey = "IconVan";
                        r.IsActive = true;
                    }
                    else if (r.VehicleType == VehicleType.HeavyTruck)
                    {
                        r.DisplayName = "Vehículo Pesado / Camión";
                        r.HourRate = 10000m;
                        r.IconKey = "IconTruck";
                        r.IsActive = true;
                    }
                    else if (r.VehicleType == VehicleType.Suv)
                    {
                        r.IsActive = false;
                    }
                }
                await context.SaveChangesAsync();
            }
            catch { }
        }

        if (!await context.PaymentMethods.AnyAsync())
        {
            context.PaymentMethods.Add(new PaymentMethodEntity
            {
                Id = 1,
                Name = "Efectivo",
                Icon = "IconCash",
                State = true,
                RequiresCashTender = true
            });
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
