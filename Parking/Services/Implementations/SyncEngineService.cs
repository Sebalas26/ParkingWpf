using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Parking.Core.Enums;
using Parking.Data.Factories;
using Parking.Entities;
using Parking.Models.ApiModels;
using Parking.Services.Contracts;

namespace Parking.Services.Implementations;

public class SyncEngineService : ISyncEngineService
{
    private readonly IApiClientService _apiClient;
    private readonly IDbConnectionManager _dbManager;
    private bool _isOnline;
    private int _pendingItemsCount;
    private DateTime? _lastSyncTime;

    public event EventHandler<string>? SyncStatusChanged;
    public event Action<int>? TotalCapacityChanged;

    public bool IsOnline => _isOnline;
    public int PendingItemsCount => _pendingItemsCount;
    public DateTime? LastSyncTime => _lastSyncTime;
    public int ServerConfiguredCapacity { get; private set; } = 100;

    public string SyncStatusDescription => _isOnline
        ? (_lastSyncTime.HasValue ? $"API Central Online • Sincronizado ({_lastSyncTime.Value:HH:mm})" : "API Central Online • Sincronizado")
        : (_pendingItemsCount > 0 ? $"Modo Offline Local • {_pendingItemsCount} pendientes" : "Modo Offline Local (Sin Conexión)");

    public SyncEngineService(
        IApiClientService apiClient,
        IDbConnectionManager dbManager)
    {
        _apiClient = apiClient;
        _dbManager = dbManager;
    }

    public async Task<bool> PerformFullSyncAsync()
    {
        var report = await PerformFullSyncWithProgressAsync(new Progress<SyncProgressReport>());
        return report.Success;
    }

    public async Task<SyncResultReport> PerformFullSyncWithProgressAsync(IProgress<SyncProgressReport> progress, CancellationToken ct = default)
    {
        var result = new SyncResultReport();

        // 1. Paso 1: Comprobar Conectividad (15%)
        progress.Report(new SyncProgressReport
        {
            Percentage = 15,
            StepIndex = 1,
            CurrentStepTitle = "Comprobando conexión con servidor central...",
            DetailMessage = "Verificando disponibilidad de red y respuesta de MySQL Cloud..."
        });
        await Task.Delay(250, ct);

        var isApiAvailable = await _apiClient.PingAsync();
        _isOnline = isApiAvailable;
        result.IsOnline = isApiAvailable;

        if (!isApiAvailable)
        {
            progress.Report(new SyncProgressReport
            {
                Percentage = 100,
                StepIndex = 1,
                CurrentStepTitle = "Servidor no disponible",
                DetailMessage = "No se pudo establecer conexión con el API Central. El sistema continuará operando en modo offline seguro.",
                IsSuccessStep = false
            });
            result.Success = false;
            result.Message = "No se pudo conectar con el servidor central. Se conservarán los datos locales.";
            SyncStatusChanged?.Invoke(this, SyncStatusDescription);
            return result;
        }

        progress.Report(new SyncProgressReport
        {
            Percentage = 25,
            StepIndex = 1,
            CurrentStepTitle = "Conexión establecida con éxito",
            DetailMessage = "Servidor MySQL en línea y listo para transferencia de datos."
        });
        await Task.Delay(150, ct);

        // 2. Paso 2: Despachar cola offline (35%)
        progress.Report(new SyncProgressReport
        {
            Percentage = 35,
            StepIndex = 2,
            CurrentStepTitle = "Despachando transacciones pendientes locales...",
            DetailMessage = $"Procesando cola de pendientes ({PendingItemsCount} elementos)..."
        });

        var initialPending = _pendingItemsCount;
        await ProcessPendingQueueAsync();
        result.DispatchedOfflineItemsCount = Math.Max(0, initialPending - _pendingItemsCount);

        progress.Report(new SyncProgressReport
        {
            Percentage = 45,
            StepIndex = 2,
            CurrentStepTitle = "Transacciones locales despachadas",
            DetailMessage = $"Cola procesada correctamente ({result.DispatchedOfflineItemsCount} transacciones enviadas)."
        });
        await Task.Delay(150, ct);

        // 3. Descargar Novedades Centrales (Bootstrap)
        progress.Report(new SyncProgressReport
        {
            Percentage = 50,
            StepIndex = 3,
            CurrentStepTitle = "Descargando novedades del servidor...",
            DetailMessage = "Solicitando datos de catálogos y tiquetes desde el API..."
        });

        var bootstrap = await _apiClient.GetBootstrapAsync();
        if (bootstrap == null)
        {
            progress.Report(new SyncProgressReport
            {
                Percentage = 100,
                StepIndex = 3,
                CurrentStepTitle = "Respuesta incompleta",
                DetailMessage = "El servidor no entregó los paquetes de sincronización requeridos.",
                IsSuccessStep = false
            });
            result.Success = false;
            result.Message = "El servidor no respondió con los datos de sincronización.";
            return result;
        }

        if (bootstrap.TotalCapacity > 0)
        {
            ServerConfiguredCapacity = bootstrap.TotalCapacity;
            TotalCapacityChanged?.Invoke(bootstrap.TotalCapacity);
        }

        using var db = _dbManager.CreateDbContext();

        // 4. Paso 3: Sincronizar Usuarios (60%)
        progress.Report(new SyncProgressReport
        {
            Percentage = 60,
            StepIndex = 3,
            CurrentStepTitle = "Sincronizando Usuarios y Operadores...",
            DetailMessage = $"Procesando {bootstrap.Users.Count} usuarios de MySQL..."
        });

        // Asegurar la existencia de roles base en SQLite antes de insertar usuarios (evita FK constraint violation)
        var adminRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "Administrador" || r.Name == "Admin", ct);
        if (adminRole == null)
        {
            adminRole = new Role
            {
                RoleId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Administrador",
                Description = "Control total y administración"
            };
            db.Roles.Add(adminRole);
        }

        var operatorRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "Operador" || r.Name == "Operator", ct);
        if (operatorRole == null)
        {
            operatorRole = new Role
            {
                RoleId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Name = "Operador",
                Description = "Operación de caja y patio"
            };
            db.Roles.Add(operatorRole);
        }
        await db.SaveChangesAsync(ct);

        // Purgar mock users
        var mockUsers = await db.Users.Where(u => u.FullName == "Alexander Wright" || u.FullName == "Elena Vance" || u.Username == "alexander" || u.Username == "elena").ToListAsync(ct);
        if (mockUsers.Count > 0)
        {
            db.Users.RemoveRange(mockUsers);
            await db.SaveChangesAsync(ct);
        }

        int usersCount = 0;
        if (bootstrap.Users != null)
        {
            foreach (var apiUser in bootstrap.Users)
            {
                var targetRoleId = apiUser.UserRoleId == 1 ? adminRole.RoleId : operatorRole.RoleId;
                var fullName = !string.IsNullOrWhiteSpace(apiUser.FullName)
                    ? apiUser.FullName
                    : $"{apiUser.FirstName} {apiUser.FirstSurname}".Trim();

                var existing = await db.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == apiUser.Username.ToLower(), ct);
                if (existing != null)
                {
                    existing.FullName = fullName;
                    existing.Email = apiUser.Email;
                    existing.PasswordHash = apiUser.Password;
                    existing.RoleId = targetRoleId;
                    existing.IsActive = apiUser.IsActive;
                }
                else
                {
                    db.Users.Add(new User
                    {
                        UserId = Guid.NewGuid(),
                        Username = apiUser.Username,
                        FullName = fullName,
                        Email = apiUser.Email,
                        PasswordHash = apiUser.Password,
                        RoleId = targetRoleId,
                        IsActive = apiUser.IsActive,
                        CreatedAtUtc = DateTime.UtcNow
                    });
                }
                usersCount++;
            }
            await db.SaveChangesAsync(ct);
        }
        result.SyncedUsersCount = usersCount;
        await Task.Delay(150, ct);

        // 5. Paso 4: Sincronizar Tarifas (75%)
        progress.Report(new SyncProgressReport
        {
            Percentage = 75,
            StepIndex = 4,
            CurrentStepTitle = "Sincronizando Tarifas y Reglas vehiculares...",
            DetailMessage = $"Actualizando {bootstrap.Rates?.Count ?? 0} tarifas configuradas..."
        });

        int ratesCount = 0;
        if (bootstrap.Rates != null)
        {
            foreach (var rate in bootstrap.Rates)
            {
                var existing = await db.VehicleRates.FirstOrDefaultAsync(r => r.VehicleType == rate.VehicleType, ct);
                if (existing != null)
                {
                    existing.HourRate = rate.HourRate;
                    existing.MinuteRate = rate.MinuteRate;
                    existing.FullDayRate = rate.FullDayRate;
                    existing.GracePeriodMinutes = rate.GracePeriodMinutes;
                    existing.DisplayName = rate.DisplayName;
                    existing.IconKey = rate.IconKey;
                    existing.IsActive = rate.IsActive;
                }
                else
                {
                    db.VehicleRates.Add(rate);
                }
                ratesCount++;
            }
            await db.SaveChangesAsync(ct);
        }
        result.SyncedRatesCount = ratesCount;
        await Task.Delay(150, ct);

        // 6. Paso 5: Sincronizar Comercios y Convenios (85%)
        progress.Report(new SyncProgressReport
        {
            Percentage = 85,
            StepIndex = 5,
            CurrentStepTitle = "Sincronizando Comercios y Convenios...",
            DetailMessage = $"Actualizando {bootstrap.Stores?.Count ?? 0} almacenes y {bootstrap.Agreements?.Count ?? 0} convenios..."
        });

        if (bootstrap.Stores != null)
        {
            foreach (var store in bootstrap.Stores)
            {
                var existing = await db.Stores.FirstOrDefaultAsync(s => s.StoreId == store.StoreId, ct);
                if (existing != null)
                {
                    existing.Name = store.Name;
                    existing.TaxId = store.TaxId;
                    existing.PhoneNumber = store.PhoneNumber;
                    existing.IsActive = store.IsActive;
                }
                else
                {
                    db.Stores.Add(store);
                }
            }
            await db.SaveChangesAsync(ct);
        }

        int agCount = 0;
        if (bootstrap.Agreements != null)
        {
            foreach (var ag in bootstrap.Agreements)
            {
                var existing = await db.CommercialAgreements.FirstOrDefaultAsync(a => a.AgreementId == ag.AgreementId, ct);
                if (existing != null)
                {
                    existing.Name = ag.Name;
                    existing.StoreId = ag.StoreId;
                    existing.DiscountPercentage = ag.DiscountPercentage;
                    existing.DiscountFixedAmount = ag.DiscountFixedAmount;
                    existing.MaxHoursApplicable = ag.MaxHoursApplicable;
                    existing.MinPurchaseAmount = ag.MinPurchaseAmount;
                    existing.IsActive = ag.IsActive;
                }
                else
                {
                    db.CommercialAgreements.Add(ag);
                }
                agCount++;
            }
            await db.SaveChangesAsync(ct);
        }
        result.SyncedAgreementsCount = agCount;
        await Task.Delay(150, ct);

        // 7. Paso 6: Sincronizar Tiquetes y Guardar Cambios (95%)
        progress.Report(new SyncProgressReport
        {
            Percentage = 95,
            StepIndex = 6,
            CurrentStepTitle = "Sincronizando Tiquetes activos y Turnos...",
            DetailMessage = "Consolidando registros de acceso y guardando en SQLite..."
        });

        var allIncomingTickets = new List<ParkingTicket>();
        if (bootstrap.ActiveTickets != null) allIncomingTickets.AddRange(bootstrap.ActiveTickets);
        if (bootstrap.RecentTickets != null) allIncomingTickets.AddRange(bootstrap.RecentTickets);

        int ticketsCount = 0;
        foreach (var ticket in allIncomingTickets)
        {
            var existing = await db.ParkingTickets.FirstOrDefaultAsync(t => t.TicketId == ticket.TicketId || t.TicketNumber == ticket.TicketNumber, ct);
            if (existing != null)
            {
                existing.TicketNumber = ticket.TicketNumber;
                existing.PlateNumber = ticket.PlateNumber;
                existing.VehicleType = ticket.VehicleType;
                existing.CustomerPhone = ticket.CustomerPhone;
                existing.Notes = ticket.Notes;
                existing.OperatorName = !string.IsNullOrWhiteSpace(ticket.OperatorName) ? ticket.OperatorName : "Operador General";
                existing.EntryTimeUtc = ticket.EntryTimeUtc;
                existing.ExitTimeUtc = ticket.ExitTimeUtc;
                existing.TotalDurationMinutes = ticket.TotalDurationMinutes;
                existing.Status = ticket.Status;
                existing.HourlyRate = ticket.HourlyRate;
                existing.GrossAmount = ticket.GrossAmount;
                existing.DiscountAmount = ticket.DiscountAmount;
                existing.NetAmount = ticket.NetAmount;
                existing.AmountPaid = ticket.AmountPaid;
                existing.ChangeGiven = ticket.ChangeGiven;
                existing.PaymentMethod = ticket.PaymentMethod;
                existing.PaymentMethodId = ticket.PaymentMethodId;
                existing.ExitNotes = ticket.ExitNotes;
                existing.IsSynchronized = true;
            }
            else
            {
                ticket.OperatorName = !string.IsNullOrWhiteSpace(ticket.OperatorName) ? ticket.OperatorName : "Operador General";
                ticket.IsSynchronized = true;
                db.ParkingTickets.Add(ticket);
            }
            ticketsCount++;
        }
        result.SyncedTicketsCount = ticketsCount;

        await db.SaveChangesAsync(ct);
        _lastSyncTime = DateTime.UtcNow;
        _isOnline = true;
        result.Success = true;
        result.Message = $"Sincronización completada: {usersCount} usuarios, {ratesCount} tarifas, {agCount} convenios y {ticketsCount} tiquetes actualizados.";

        // Paso 7: Finalizado (100%)
        progress.Report(new SyncProgressReport
        {
            Percentage = 100,
            StepIndex = 6,
            CurrentStepTitle = "¡Sincronización completada con éxito!",
            DetailMessage = result.Message,
            IsSuccessStep = true
        });

        SyncStatusChanged?.Invoke(this, SyncStatusDescription);
        return result;
    }



    public async Task<bool> ForceCleanResyncAsync()
    {
        var isApiAvailable = await _apiClient.PingAsync();
        _isOnline = isApiAvailable;

        if (isApiAvailable)
        {
            using var db = _dbManager.CreateDbContext();

            // 1. Limpiar caché local transaccional
            db.PendingSyncItems.RemoveRange(db.PendingSyncItems);
            db.TicketDiscounts.RemoveRange(db.TicketDiscounts);
            db.ParkingTickets.RemoveRange(db.ParkingTickets);
            await db.SaveChangesAsync();

            // 2. Traer bootstrap limpio desde MySQL
            var bootstrap = await _apiClient.GetBootstrapAsync();
            if (bootstrap != null)
            {
                if (bootstrap.TotalCapacity > 0)
                {
                    ServerConfiguredCapacity = bootstrap.TotalCapacity;
                    TotalCapacityChanged?.Invoke(bootstrap.TotalCapacity);
                }

                var allIncomingTickets = new List<ParkingTicket>();
                if (bootstrap.ActiveTickets != null) allIncomingTickets.AddRange(bootstrap.ActiveTickets);
                if (bootstrap.RecentTickets != null) allIncomingTickets.AddRange(bootstrap.RecentTickets);

                foreach (var t in allIncomingTickets)
                {
                    t.IsSynchronized = true;
                    db.ParkingTickets.Add(t);
                }

                await db.SaveChangesAsync();
            }

            _lastSyncTime = DateTime.Now;
            await RefreshPendingCountAsync();
            SyncStatusChanged?.Invoke(this, SyncStatusDescription);
            return true;
        }

        await ClearLocalTicketsMemoryAsync();
        return false;
    }


    public async Task EnqueueOfflineCheckInAsync(ParkingTicket ticket)
    {
        using var db = _dbManager.CreateDbContext();
        var pending = new PendingSyncItem
        {
            PendingSyncItemId = Guid.NewGuid(),
            OperationType = "CheckIn",
            PayloadJson = JsonSerializer.Serialize(new CheckInApiRequest
            {
                TicketId = ticket.TicketId,
                TicketNumber = ticket.TicketNumber,
                PlateNumber = ticket.PlateNumber,
                VehicleType = ticket.VehicleType,
                PhoneNumber = ticket.CustomerPhone,
                Notes = ticket.Notes,
                OperatorName = ticket.OperatorName,
                EntryTimeUtc = ticket.EntryTimeUtc
            }),
            CreatedAtUtc = DateTime.UtcNow,
            IsProcessed = false
        };

        db.PendingSyncItems.Add(pending);
        await db.SaveChangesAsync();

        await RefreshPendingCountAsync();
        SyncStatusChanged?.Invoke(this, SyncStatusDescription);
    }

    public async Task ClearLocalTicketsMemoryAsync()
    {
        try
        {
            using var db = _dbManager.CreateDbContext();
            db.PendingSyncItems.RemoveRange(db.PendingSyncItems);
            db.TicketDiscounts.RemoveRange(db.TicketDiscounts);
            db.ParkingTickets.RemoveRange(db.ParkingTickets);
            await db.SaveChangesAsync();

            await RefreshPendingCountAsync();
            SyncStatusChanged?.Invoke(this, SyncStatusDescription);
        }
        catch { }
    }

    public async Task EnqueueOfflineCheckOutAsync(ParkingTicket ticket)
    {
        using var db = _dbManager.CreateDbContext();
        var pending = new PendingSyncItem
        {
            PendingSyncItemId = Guid.NewGuid(),
            OperationType = "CheckOut",
            PayloadJson = JsonSerializer.Serialize(new CheckOutApiRequest
            {
                TicketId = ticket.TicketId,
                PaymentMethod = ticket.PaymentMethod ?? PaymentMethod.Cash,
                AmountPaid = ticket.AmountPaid,
                ExitTimeUtc = ticket.ExitTimeUtc ?? DateTime.UtcNow
            }),
            CreatedAtUtc = DateTime.UtcNow,
            IsProcessed = false
        };

        db.PendingSyncItems.Add(pending);
        await db.SaveChangesAsync();

        await RefreshPendingCountAsync();
        SyncStatusChanged?.Invoke(this, SyncStatusDescription);
    }

    public async Task ProcessPendingQueueAsync()
    {
        if (!_isOnline) return;

        try
        {
            using var db = _dbManager.CreateDbContext();
            var items = await db.PendingSyncItems
                .Where(p => !p.IsProcessed)
                .OrderBy(p => p.CreatedAtUtc)
                .ToListAsync();

            foreach (var item in items)
            {
                try
                {
                    if (item.OperationType == "CheckIn")
                    {
                        var req = JsonSerializer.Deserialize<CheckInApiRequest>(item.PayloadJson);
                        if (req != null)
                        {
                            var result = await _apiClient.CheckInAsync(req);
                            if (result != null) item.IsProcessed = true;
                        }
                    }
                    else if (item.OperationType == "CheckOut")
                    {
                        var req = JsonSerializer.Deserialize<CheckOutApiRequest>(item.PayloadJson);
                        if (req != null)
                        {
                            var result = await _apiClient.CheckOutAsync(req);
                            if (result != null) item.IsProcessed = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    item.RetryCount++;
                    item.LastError = ex.Message;
                }
            }

            db.PendingSyncItems.RemoveRange(db.PendingSyncItems.Where(p => p.IsProcessed));
            await db.SaveChangesAsync();

            await RefreshPendingCountAsync();
            SyncStatusChanged?.Invoke(this, SyncStatusDescription);
        }
        catch
        {
            // Protección contra tablas desincronizadas en SQLite
        }
    }

    private async Task RefreshPendingCountAsync()
    {
        try
        {
            using var db = _dbManager.CreateDbContext();
            _pendingItemsCount = await db.PendingSyncItems.CountAsync(p => !p.IsProcessed);
        }
        catch
        {
            _pendingItemsCount = 0;
        }
    }
}

