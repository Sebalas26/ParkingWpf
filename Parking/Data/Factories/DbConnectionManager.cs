using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
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
        _sqliteConnectionString = sqliteConnectionString ?? "Data Source=parkflow_local.db;Cache=Shared;Mode=ReadWriteCreate;Default Timeout=15;";
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

        // Configuración de concurrencia y resiliencia SQLite (Write-Ahead Logging y timeout de bloqueo)
        try
        {
            await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode = WAL; PRAGMA busy_timeout = 5000; PRAGMA synchronous = NORMAL;");
        }
        catch { }

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

                CREATE TABLE IF NOT EXISTS ""BranchPaymentMethods"" (
                    ""Id"" INTEGER PRIMARY KEY AUTOINCREMENT,
                    ""BranchId"" INTEGER NOT NULL,
                    ""PaymentMethodId"" INTEGER NOT NULL,
                    ""RequiresCashTender"" INTEGER NOT NULL DEFAULT 0,
                    ""IsActive"" INTEGER NOT NULL DEFAULT 1
                );

                CREATE TABLE IF NOT EXISTS ""UserBranches"" (
                    ""UserId"" INTEGER NOT NULL,
                    ""BranchId"" INTEGER NOT NULL,
                    PRIMARY KEY (""UserId"", ""BranchId"")
                );

                CREATE TABLE IF NOT EXISTS ""BranchOperatingHours"" (
                    ""Id"" INTEGER PRIMARY KEY AUTOINCREMENT,
                    ""BranchId"" INTEGER NOT NULL,
                    ""DayOfWeek"" INTEGER NOT NULL,
                    ""IsOpen"" INTEGER NOT NULL DEFAULT 1,
                    ""OpeningTime"" TEXT NOT NULL DEFAULT '08:00:00',
                    ""ClosingTime"" TEXT NOT NULL DEFAULT '22:00:00',
                    ""BufferMinutesBefore"" INTEGER NOT NULL DEFAULT 30,
                    ""BufferMinutesAfter"" INTEGER NOT NULL DEFAULT 30
                );
            ");

            // 1. Auto-Migración Dinámica de Esquema SQLite basada en el Modelo de EF Core
            await AutoMigrateDatabaseAsync(context);

            // 2. Ajustes semánticos de compatibilidad histórica
            try { await context.Database.ExecuteSqlRawAsync("UPDATE \"VehicleRates\" SET \"GracePeriodMinutes\" = 0;"); } catch { }
            try { await context.Database.ExecuteSqlRawAsync("UPDATE \"VehicleRates\" SET \"VehicleType\" = 1 WHERE LOWER(\"DisplayName\") LIKE '%moto%';"); } catch { }
            try { await context.Database.ExecuteSqlRawAsync("UPDATE \"VehicleRates\" SET \"VehicleType\" = 4 WHERE LOWER(\"DisplayName\") LIKE '%bici%' OR LOWER(\"DisplayName\") LIKE '%bike%';"); } catch { }
            try { await context.Database.ExecuteSqlRawAsync("UPDATE \"VehicleRates\" SET \"VehicleType\" = 2 WHERE LOWER(\"DisplayName\") LIKE '%camion%' OR LOWER(\"DisplayName\") LIKE '%pesado%';"); } catch { }
            try { await context.Database.ExecuteSqlRawAsync("UPDATE \"VehicleRates\" SET \"VehicleType\" = 5 WHERE LOWER(\"DisplayName\") LIKE '%suv%' OR LOWER(\"DisplayName\") LIKE '%camioneta%';"); } catch { }
        }
        catch { }

    }

    /// <summary>
    /// Inspecciona el modelo relacional de Entity Framework Core contra las tablas existentes en SQLite.
    /// Crea automáticamente cualquier tabla faltante y añade con ALTER TABLE cualquier columna nueva en C#
    /// sin requerir mantenimiento manual ni borrar el archivo de base de datos local.
    /// </summary>
    public async Task AutoMigrateDatabaseAsync(ParkFlowDbContext context)
    {
        try
        {
            var connection = context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            // 1. Obtener nombres de tablas existentes en SQLite
            var existingTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var cmdTables = connection.CreateCommand())
            {
                cmdTables.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' AND name NOT LIKE '__EFMigrations%';";
                using var reader = await cmdTables.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    existingTables.Add(reader.GetString(0));
                }
            }

            // 2. Recorrer cada entidad configurada en el DbContext
            foreach (var entityType in context.Model.GetEntityTypes())
            {
                var tableName = entityType.GetTableName();
                if (string.IsNullOrWhiteSpace(tableName)) continue;

                var storeObject = StoreObjectIdentifier.Table(tableName, null);

                // Escenario A: La tabla no existe en SQLite -> Crearla dinámicamente
                if (!existingTables.Contains(tableName))
                {
                    var primaryKey = entityType.FindPrimaryKey();
                    var pkProps = primaryKey?.Properties;
                    var pkColNames = pkProps?.Select(p => $"\"{p.GetColumnName(storeObject) ?? p.Name}\"").ToList() ?? new List<string>();

                    var colDefs = new List<string>();
                    foreach (var p in entityType.GetProperties())
                    {
                        var cName = p.GetColumnName(storeObject) ?? p.Name;
                        var pClrType = Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType;
                        string pSqlType = (pClrType == typeof(int) || pClrType == typeof(long) || pClrType == typeof(short) || pClrType == typeof(byte) || pClrType == typeof(bool) || pClrType.IsEnum)
                            ? "INTEGER"
                            : (pClrType == typeof(float) || pClrType == typeof(double)) ? "REAL" : (pClrType == typeof(byte[]) ? "BLOB" : "TEXT");

                        var isPk = pkProps != null && pkProps.Contains(p);
                        var nullability = isPk || !p.IsNullable ? "NOT NULL" : "NULL";
                        colDefs.Add($"\"{cName}\" {pSqlType} {nullability}");
                    }

                    if (pkColNames.Count > 0)
                    {
                        colDefs.Add($"PRIMARY KEY ({string.Join(", ", pkColNames)})");
                    }

                    var createTableSql = $"CREATE TABLE IF NOT EXISTS \"{tableName}\" ({string.Join(", ", colDefs)});";
                    using var createCmd = connection.CreateCommand();
                    createCmd.CommandText = createTableSql;
                    await createCmd.ExecuteNonQueryAsync();
                    existingTables.Add(tableName);
                    continue;
                }

                // Escenario B: La tabla existe -> Verificar columnas faltantes con PRAGMA
                var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var pragmaCmd = connection.CreateCommand())
                {
                    pragmaCmd.CommandText = $"PRAGMA table_info(\"{tableName}\");";
                    using var pragmaReader = await pragmaCmd.ExecuteReaderAsync();
                    while (await pragmaReader.ReadAsync())
                    {
                        existingColumns.Add(pragmaReader.GetString(1)); // Columna 'name'
                    }
                }

                // Evaluar cada propiedad de la entidad
                foreach (var prop in entityType.GetProperties())
                {
                    var colName = prop.GetColumnName(storeObject) ?? prop.Name;
                    if (string.IsNullOrWhiteSpace(colName) || existingColumns.Contains(colName))
                    {
                        continue;
                    }

                    // Determinar tipo SQLite
                    var clrType = Nullable.GetUnderlyingType(prop.ClrType) ?? prop.ClrType;
                    string sqlType;
                    string defaultClause;

                    if (clrType == typeof(int) || clrType == typeof(long) || clrType == typeof(short) || 
                        clrType == typeof(byte) || clrType == typeof(bool) || clrType.IsEnum)
                    {
                        sqlType = "INTEGER";
                        defaultClause = prop.IsNullable ? "DEFAULT NULL" : "NOT NULL DEFAULT 0";
                    }
                    else if (clrType == typeof(decimal))
                    {
                        sqlType = "TEXT";
                        defaultClause = prop.IsNullable ? "DEFAULT NULL" : "NOT NULL DEFAULT '0'";
                    }
                    else if (clrType == typeof(string) || clrType == typeof(Guid) || 
                             clrType == typeof(DateTime) || clrType == typeof(DateTimeOffset) || clrType == typeof(TimeSpan))
                    {
                        sqlType = "TEXT";
                        defaultClause = prop.IsNullable ? "DEFAULT NULL" : "NOT NULL DEFAULT ''";
                    }
                    else if (clrType == typeof(float) || clrType == typeof(double))
                    {
                        sqlType = "REAL";
                        defaultClause = prop.IsNullable ? "DEFAULT NULL" : "NOT NULL DEFAULT 0.0";
                    }
                    else if (clrType == typeof(byte[]))
                    {
                        sqlType = "BLOB";
                        defaultClause = "DEFAULT NULL";
                    }
                    else
                    {
                        sqlType = "TEXT";
                        defaultClause = "DEFAULT NULL";
                    }

                    // Ejecutar ALTER TABLE dinámico
                    try
                    {
                        using var alterCmd = connection.CreateCommand();
                        alterCmd.CommandText = $"ALTER TABLE \"{tableName}\" ADD COLUMN \"{colName}\" {sqlType} {defaultClause};";
                        await alterCmd.ExecuteNonQueryAsync();
                    }
                    catch
                    {
                        // Si ya existe o sqlite no permite cierta cláusula, tolerar
                    }
                }
            }
        }
        catch
        {
            // Resiliencia defensiva
        }
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
