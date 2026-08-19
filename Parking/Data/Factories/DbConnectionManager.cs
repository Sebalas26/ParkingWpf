using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Parking.Core.Constants;
using Parking.Core.Enums;
using Parking.Entities;

namespace Parking.Data.Factories;

public class DbConnectionManager : IDbConnectionManager
{
    private readonly string _mySqlConnectionString;
    private readonly string _sqliteConnectionString;
    private bool _isOnlineMode;
    private DatabaseProviderType _currentProvider = DatabaseProviderType.Sqlite;

    public bool IsOnlineMode => _isOnlineMode;
    public DatabaseProviderType CurrentProvider => _currentProvider;
    public string StatusDescription => _isOnlineMode ? "MySQL Servidor Central (En Línea)" : "SQLite Local Resiliente (Modo Local)";

    public event EventHandler<bool>? ConnectionStateChanged;

    public DbConnectionManager(string? mySqlConnectionString = null, string? sqliteConnectionString = null)
    {
        _mySqlConnectionString = mySqlConnectionString ?? "Server=localhost;Port=3306;Database=parkflow_db;User=root;Password=root;Connect Timeout=3;";
        _sqliteConnectionString = sqliteConnectionString ?? "Data Source=parkflow_local.db;";
    }

    public ParkFlowDbContext CreateDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<ParkFlowDbContext>();

        if (_currentProvider == DatabaseProviderType.MySql)
        {
            var serverVersion = new MySqlServerVersion(new Version(8, 0, 36));
            optionsBuilder.UseMySql(_mySqlConnectionString, serverVersion, mySqlOptions =>
            {
                mySqlOptions.CommandTimeout(3);
                mySqlOptions.EnableRetryOnFailure(1);
            });
        }
        else
        {
            optionsBuilder.UseSqlite(_sqliteConnectionString);
        }

        return new ParkFlowDbContext(optionsBuilder.Options);
    }

    public async Task InitializeDatabaseAsync()
    {
        var canConnectMySql = await TestMySqlConnectionAsync();
        if (canConnectMySql)
        {
            _currentProvider = DatabaseProviderType.MySql;
            _isOnlineMode = true;
        }
        else
        {
            _currentProvider = DatabaseProviderType.Sqlite;
            _isOnlineMode = false;
        }

        using var db = CreateDbContext();
        await db.Database.EnsureCreatedAsync();

        if (_currentProvider == DatabaseProviderType.Sqlite)
        {
            await db.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""PendingSyncItems"" (
                    ""PendingSyncItemId"" TEXT NOT NULL PRIMARY KEY,
                    ""OperationType"" TEXT NOT NULL,
                    ""PayloadJson"" TEXT NOT NULL,
                    ""CreatedAtUtc"" TEXT NOT NULL,
                    ""RetryCount"" INTEGER NOT NULL,
                    ""LastError"" TEXT NULL,
                    ""IsProcessed"" INTEGER NOT NULL
                );");
        }

        await SeedInitialMetadataAsync(db);

        ConnectionStateChanged?.Invoke(this, _isOnlineMode);
    }

    public async Task<bool> TestConnectionAsync()
    {
        var isAvailable = await TestMySqlConnectionAsync();
        var stateChanged = isAvailable != _isOnlineMode;
        _isOnlineMode = isAvailable;
        _currentProvider = isAvailable ? DatabaseProviderType.MySql : DatabaseProviderType.Sqlite;

        if (stateChanged)
        {
            ConnectionStateChanged?.Invoke(this, _isOnlineMode);
        }

        return _isOnlineMode;
    }

    private async Task<bool> TestMySqlConnectionAsync()
    {
        try
        {
            var optionsBuilder = new DbContextOptionsBuilder<ParkFlowDbContext>();
            var serverVersion = new MySqlServerVersion(new Version(8, 0, 36));
            optionsBuilder.UseMySql(_mySqlConnectionString, serverVersion, mySqlOptions =>
            {
                mySqlOptions.CommandTimeout(3);
            });

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var testDb = new ParkFlowDbContext(optionsBuilder.Options);
            return await testDb.Database.CanConnectAsync(cts.Token);
        }
        catch
        {
            return false;
        }
    }

    private async Task SeedInitialMetadataAsync(ParkFlowDbContext db)
    {
        if (!await db.Roles.AnyAsync())
        {
            var adminRole = new Role
            {
                RoleId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Administrador",
                Description = "Control total y configuración de todo el sistema",
                IsSystemRole = true,
                IsActive = true
            };

            var cashierRole = new Role
            {
                RoleId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Name = "Operador de Caja",
                Description = "Ingreso de vehículos, cobro en caja y aplicación de convenios",
                IsSystemRole = false,
                IsActive = true
            };

            var supervisorRole = new Role
            {
                RoleId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Name = "Supervisor de Turno",
                Description = "Operación de parqueadero, auditoría y consulta de reportes",
                IsSystemRole = false,
                IsActive = true
            };

            await db.Roles.AddRangeAsync(adminRole, cashierRole, supervisorRole);

            var adminUser = new User
            {
                UserId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                Username = "admin",
                PasswordHash = HashPassword("admin"),
                FullName = "Alexander Wright",
                Email = "admin@parkflow.internal",
                RoleId = adminRole.RoleId,
                IsActive = true
            };

            var operatorUser = new User
            {
                UserId = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                Username = "operator",
                PasswordHash = HashPassword("1234"),
                FullName = "Elena Vance",
                Email = "elena.vance@parkflow.internal",
                RoleId = cashierRole.RoleId,
                IsActive = true
            };

            await db.Users.AddRangeAsync(adminUser, operatorUser);
        }

        if (!await db.AppModules.AnyAsync())
        {
            var checkInMod = new AppModule
            {
                ModuleId = Guid.Parse("a1111111-1111-1111-1111-111111111111"),
                ModuleKey = ModuleKeys.CheckIn,
                DisplayName = "Ingreso de Vehículos",
                Description = "Registro de entradas y emisión de tiquetes",
                IconKey = "IconCheckIn",
                DisplayOrder = 1,
                IsActive = true
            };

            var checkOutMod = new AppModule
            {
                ModuleId = Guid.Parse("a2222222-2222-2222-2222-222222222222"),
                ModuleKey = ModuleKeys.CheckOut,
                DisplayName = "Salida y Caja",
                Description = "Liquidación, cobro, convenios y liberación de cupos",
                IconKey = "IconCheckOut",
                DisplayOrder = 2,
                IsActive = true
            };

            var analyticsMod = new AppModule
            {
                ModuleId = Guid.Parse("a3333333-3333-3333-3333-333333333333"),
                ModuleKey = ModuleKeys.Analytics,
                DisplayName = "Reportes y Finanzas",
                Description = "Métricas financieras, auditoría e historial",
                IconKey = "IconAnalytics",
                DisplayOrder = 3,
                IsActive = true
            };

            var storesMod = new AppModule
            {
                ModuleId = Guid.Parse("a4444444-4444-4444-4444-444444444444"),
                ModuleKey = ModuleKeys.Stores,
                DisplayName = "Almacenes Aliados",
                Description = "Gestión de comercios aliados",
                IconKey = "IconReceipt",
                DisplayOrder = 4,
                IsActive = true
            };

            var agreementsMod = new AppModule
            {
                ModuleId = Guid.Parse("a5555555-5555-5555-5555-555555555555"),
                ModuleKey = ModuleKeys.Agreements,
                DisplayName = "Convenios Comerciales",
                Description = "Reglas de descuento por compras",
                IconKey = "IconShield",
                DisplayOrder = 5,
                IsActive = true
            };

            var ratesMod = new AppModule
            {
                ModuleId = Guid.Parse("a6666666-6666-6666-6666-666666666666"),
                ModuleKey = ModuleKeys.Rates,
                DisplayName = "Gestión de Tarifas",
                Description = "Estructura tarifaria por categoría de vehículo",
                IconKey = "IconCash",
                DisplayOrder = 6,
                IsActive = true
            };

            var securityMod = new AppModule
            {
                ModuleId = Guid.Parse("a7777777-7777-7777-7777-777777777777"),
                ModuleKey = ModuleKeys.Security,
                DisplayName = "Usuarios y Seguridad",
                Description = "Administración de usuarios, roles y matriz de permisos",
                IconKey = "IconKey",
                DisplayOrder = 7,
                IsActive = true
            };

            await db.AppModules.AddRangeAsync(checkInMod, checkOutMod, analyticsMod, storesMod, agreementsMod, ratesMod, securityMod);

            var permissions = new List<AppPermission>
            {
                new() { ModuleId = checkInMod.ModuleId, ActionKey = ActionKeys.View, DisplayName = "Ver Módulo de Ingreso", Description = "Acceso a la pantalla de ingreso" },
                new() { ModuleId = checkInMod.ModuleId, ActionKey = ActionKeys.Create, DisplayName = "Registrar Entrada de Vehículo", Description = "Crear nuevo tiquete de ingreso" },

                new() { ModuleId = checkOutMod.ModuleId, ActionKey = ActionKeys.View, DisplayName = "Ver Módulo de Caja", Description = "Acceso a la pantalla de salida y cobro" },
                new() { ModuleId = checkOutMod.ModuleId, ActionKey = ActionKeys.Create, DisplayName = "Procesar Cobro y Liquidación", Description = "Cobrar y liberar cupo de parqueadero" },
                new() { ModuleId = checkOutMod.ModuleId, ActionKey = ActionKeys.ApplyDiscount, DisplayName = "Aplicar Descuento de Convenio", Description = "Validar facturas y aplicar descuentos aliados" },
                new() { ModuleId = checkOutMod.ModuleId, ActionKey = ActionKeys.CancelTicket, DisplayName = "Anular Tiquete", Description = "Anular cobro o cancelar tiquete activo" },

                new() { ModuleId = analyticsMod.ModuleId, ActionKey = ActionKeys.View, DisplayName = "Ver Reportes y Métricas", Description = "Consultar el panel financiero e historial" },
                new() { ModuleId = analyticsMod.ModuleId, ActionKey = ActionKeys.ExportAudit, DisplayName = "Exportar Informes de Auditoría", Description = "Descargar o exportar reportes diarios" },

                new() { ModuleId = storesMod.ModuleId, ActionKey = ActionKeys.View, DisplayName = "Ver Almacenes Aliados", Description = "Consultar lista de comercios aliados" },
                new() { ModuleId = storesMod.ModuleId, ActionKey = ActionKeys.Create, DisplayName = "Crear Almacenes", Description = "Registrar nuevos comercios aliados" },
                new() { ModuleId = storesMod.ModuleId, ActionKey = ActionKeys.Edit, DisplayName = "Editar Almacenes", Description = "Modificar información de comercios" },

                new() { ModuleId = agreementsMod.ModuleId, ActionKey = ActionKeys.View, DisplayName = "Ver Convenios", Description = "Consultar reglas de convenios comerciales" },
                new() { ModuleId = agreementsMod.ModuleId, ActionKey = ActionKeys.Create, DisplayName = "Crear Convenios", Description = "Registrar nuevas reglas de descuento" },
                new() { ModuleId = agreementsMod.ModuleId, ActionKey = ActionKeys.Edit, DisplayName = "Editar Convenios", Description = "Modificar montos y porcentajes de descuento" },

                new() { ModuleId = ratesMod.ModuleId, ActionKey = ActionKeys.View, DisplayName = "Ver Tarifas", Description = "Consultar tarifas del parqueadero" },
                new() { ModuleId = ratesMod.ModuleId, ActionKey = ActionKeys.ModifyRates, DisplayName = "Modificar Tarifas", Description = "Cambiar valores por hora, fracción y gracia" },

                new() { ModuleId = securityMod.ModuleId, ActionKey = ActionKeys.View, DisplayName = "Ver Panel de Seguridad", Description = "Acceso a administración de seguridad" },
                new() { ModuleId = securityMod.ModuleId, ActionKey = ActionKeys.ManageUsers, DisplayName = "Administrar Usuarios", Description = "Crear, editar y asignar roles a usuarios" },
                new() { ModuleId = securityMod.ModuleId, ActionKey = ActionKeys.ManageRoles, DisplayName = "Configurar Permisos de Roles", Description = "Modificar la matriz de permisos por rol" }
            };

            await db.AppPermissions.AddRangeAsync(permissions);

            var adminRoleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var cashierRoleId = Guid.Parse("22222222-2222-2222-2222-222222222222");

            foreach (var perm in permissions)
            {
                db.RolePermissions.Add(new RolePermission
                {
                    RoleId = adminRoleId,
                    PermissionId = perm.PermissionId,
                    IsGranted = true
                });

                var isCashierPerm = perm.ModuleId == checkInMod.ModuleId ||
                                    perm.ModuleId == checkOutMod.ModuleId ||
                                    (perm.ModuleId == storesMod.ModuleId && perm.ActionKey == ActionKeys.View) ||
                                    (perm.ModuleId == agreementsMod.ModuleId && perm.ActionKey == ActionKeys.View);

                if (isCashierPerm)
                {
                    db.RolePermissions.Add(new RolePermission
                    {
                        RoleId = cashierRoleId,
                        PermissionId = perm.PermissionId,
                        IsGranted = true
                    });
                }
            }
        }

        if (!await db.Stores.AnyAsync())
        {
            var store1 = new Store
            {
                StoreId = Guid.Parse("b1111111-1111-1111-1111-111111111111"),
                Name = "Supermercado Metro",
                TaxId = "NIT 900.123.456-1",
                PhoneNumber = "+57 601 4567890",
                IsActive = true
            };

            var store2 = new Store
            {
                StoreId = Guid.Parse("b2222222-2222-2222-2222-222222222222"),
                Name = "Tiendas Falabella",
                TaxId = "NIT 800.987.654-3",
                PhoneNumber = "+57 601 7890123",
                IsActive = true
            };

            var store3 = new Store
            {
                StoreId = Guid.Parse("b3333333-3333-3333-3333-333333333333"),
                Name = "Restaurante El Corral",
                TaxId = "NIT 860.555.333-8",
                PhoneNumber = "+57 601 3216549",
                IsActive = true
            };

            await db.Stores.AddRangeAsync(store1, store2, store3);

            var agreement1 = new CommercialAgreement
            {
                AgreementId = Guid.NewGuid(),
                StoreId = store1.StoreId,
                Name = "20% Descuento en compras > $30.000",
                MinPurchaseAmount = 30000m,
                DiscountPercentage = 20m,
                DiscountFixedAmount = null,
                MaxHoursApplicable = 3,
                IsActive = true
            };

            var agreement2 = new CommercialAgreement
            {
                AgreementId = Guid.NewGuid(),
                StoreId = store2.StoreId,
                Name = "Descuento Fijo $3.000 en compras > $50.000",
                MinPurchaseAmount = 50000m,
                DiscountPercentage = null,
                DiscountFixedAmount = 3000m,
                MaxHoursApplicable = null,
                IsActive = true
            };

            var agreement3 = new CommercialAgreement
            {
                AgreementId = Guid.NewGuid(),
                StoreId = store3.StoreId,
                Name = "15% Descuento en consumo > $25.000",
                MinPurchaseAmount = 25000m,
                DiscountPercentage = 15m,
                DiscountFixedAmount = null,
                MaxHoursApplicable = 2,
                IsActive = true
            };

            await db.CommercialAgreements.AddRangeAsync(agreement1, agreement2, agreement3);
        }

        if (!await db.VehicleRates.AnyAsync())
        {
            await db.VehicleRates.AddRangeAsync(
                new VehicleRate { VehicleType = VehicleType.Motorcycle, DisplayName = "Motocicleta", MinuteRate = 25m, HourRate = 1500m, FullDayRate = 12000m, GracePeriodMinutes = 15, IconKey = "IconMotorcycle", IsActive = true },
                new VehicleRate { VehicleType = VehicleType.Car, DisplayName = "Automóvil / Sedán", MinuteRate = 50m, HourRate = 3000m, FullDayRate = 25000m, GracePeriodMinutes = 15, IconKey = "IconCar", IsActive = true },
                new VehicleRate { VehicleType = VehicleType.Suv, DisplayName = "Camioneta / SUV", MinuteRate = 75m, HourRate = 4500m, FullDayRate = 35000m, GracePeriodMinutes = 15, IconKey = "IconSuv", IsActive = true },
                new VehicleRate { VehicleType = VehicleType.Van, DisplayName = "Furgón / Minibús", MinuteRate = 100m, HourRate = 6000m, FullDayRate = 45000m, GracePeriodMinutes = 15, IconKey = "IconVan", IsActive = true },
                new VehicleRate { VehicleType = VehicleType.HeavyTruck, DisplayName = "Vehículo Pesado", MinuteRate = 150m, HourRate = 9000m, FullDayRate = 70000m, GracePeriodMinutes = 15, IconKey = "IconTruck", IsActive = true }
            );
        }

        await db.SaveChangesAsync();
    }

    public static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes);
    }
}
