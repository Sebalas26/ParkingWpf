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
                    ""OperationType"" TEXT NOT NULL DEFAULT '',
                    ""PayloadJson"" TEXT NOT NULL DEFAULT '',
                    ""RetryCount"" INTEGER NOT NULL DEFAULT 0,
                    ""LastError"" TEXT NULL,
                    ""IsProcessed"" INTEGER NOT NULL DEFAULT 0,
                    ""CreatedAtUtc"" TEXT NOT NULL
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

                CREATE TABLE IF NOT EXISTS ""TicketDiscounts"" (
                    ""TicketDiscountId"" TEXT NOT NULL PRIMARY KEY,
                    ""TicketId"" TEXT NOT NULL,
                    ""StoreId"" TEXT NOT NULL,
                    ""AgreementId"" TEXT NOT NULL,
                    ""InvoiceNumber"" TEXT NOT NULL,
                    ""PurchaseAmount"" TEXT NOT NULL,
                    ""AppliedDiscountAmount"" TEXT NOT NULL,
                    ""ValidatedAtUtc"" TEXT NOT NULL,
                    ""IsSynchronized"" INTEGER NOT NULL DEFAULT 1
                );

                CREATE TABLE IF NOT EXISTS ""CashWithdrawals"" (
                    ""WithdrawalId"" TEXT NOT NULL PRIMARY KEY,
                    ""ShiftId"" TEXT NOT NULL,
                    ""Amount"" TEXT NOT NULL,
                    ""Reason"" TEXT NOT NULL,
                    ""AuthorizedByAdminName"" TEXT NOT NULL,
                    ""CashierName"" TEXT NOT NULL,
                    ""CreatedAtUtc"" TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS ""VehicleIncidents"" (
                    ""IncidentId"" TEXT NOT NULL PRIMARY KEY,
                    ""BranchId"" INTEGER NULL,
                    ""PlateNumber"" TEXT NOT NULL,
                    ""IncidentType"" TEXT NOT NULL,
                    ""Description"" TEXT NOT NULL,
                    ""IsBlocked"" INTEGER NOT NULL DEFAULT 0,
                    ""IsGlobal"" INTEGER NOT NULL DEFAULT 0,
                    ""Status"" TEXT NOT NULL DEFAULT 'Activa',
                    ""ReportedBy"" TEXT NOT NULL DEFAULT '',
                    ""ResolvedBy"" TEXT NULL,
                    ""ResolvedNotes"" TEXT NULL,
                    ""CreatedAtUtc"" TEXT NOT NULL,
                    ""ResolvedAtUtc"" TEXT NULL
                );

                CREATE TABLE IF NOT EXISTS ""VehicleIncidentBranches"" (
                    ""IncidentId"" TEXT NOT NULL,
                    ""BranchId"" INTEGER NOT NULL,
                    PRIMARY KEY (""IncidentId"", ""BranchId"")
                );

                CREATE TABLE IF NOT EXISTS ""BillingResolutions"" (
                    ""ResolutionId"" TEXT NOT NULL PRIMARY KEY,
                    ""CompanyId"" INTEGER NULL,
                    ""BranchId"" INTEGER NULL,
                    ""Name"" TEXT NOT NULL DEFAULT '',
                    ""DocumentType"" TEXT NOT NULL DEFAULT '',
                    ""Prefix"" TEXT NOT NULL DEFAULT '',
                    ""ResolutionNumber"" TEXT NOT NULL DEFAULT '',
                    ""FromNumber"" INTEGER NOT NULL DEFAULT 1,
                    ""ToNumber"" INTEGER NOT NULL DEFAULT 1,
                    ""CurrentNumber"" INTEGER NOT NULL DEFAULT 1,
                    ""ValidFrom"" TEXT NOT NULL,
                    ""ValidTo"" TEXT NOT NULL,
                    ""TechnicalKey"" TEXT NULL,
                    ""IsActive"" INTEGER NOT NULL DEFAULT 1,
                    ""CreatedAtUtc"" TEXT NOT NULL
                );
            ");

            try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"PendingSyncItems\" ADD COLUMN \"OperationType\" TEXT DEFAULT '';"); } catch { }
            try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"PendingSyncItems\" ADD COLUMN \"LastError\" TEXT NULL;"); } catch { }
            try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"PendingSyncItems\" ADD COLUMN \"IsProcessed\" INTEGER DEFAULT 0;"); } catch { }
            try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"WorkShifts\" ADD COLUMN \"HandoverToUserId\" TEXT NULL;"); } catch { }
            try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"WorkShifts\" ADD COLUMN \"HandoverToUserName\" TEXT NULL;"); } catch { }
            try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"WorkShifts\" ADD COLUMN \"TotalCashWithdrawals\" TEXT DEFAULT '0';"); } catch { }
            try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"ParkingTickets\" ADD COLUMN \"PaymentMethodId\" INTEGER NULL;"); } catch { }
            try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"ParkingTickets\" ADD COLUMN \"ExitNotes\" TEXT NULL;"); } catch { }
            try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"VehicleIncidents\" ADD COLUMN \"IsGlobal\" INTEGER DEFAULT 0;"); } catch { }
            try { await context.Database.ExecuteSqlRawAsync("UPDATE \"VehicleRates\" SET \"GracePeriodMinutes\" = 0;"); } catch { }
        }
        catch { }

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

    public static string HashPassword(string password)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password + "ParkFlowSalt2026");
        var hash = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}
