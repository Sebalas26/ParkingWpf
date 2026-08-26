using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
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
    public event Action? DataSynchronized;

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

        // 1. Paso 1: Comprobar Conectividad (10%)
        progress.Report(new SyncProgressReport
        {
            Percentage = 10,
            StepIndex = 1,
            CurrentStepTitle = "Comprobando conexión con servidor central...",
            DetailMessage = "Verificando disponibilidad de red y respuesta del API Central..."
        });
        await Task.Delay(200, ct);

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
            Percentage = 20,
            StepIndex = 1,
            CurrentStepTitle = "Conexión establecida con éxito",
            DetailMessage = "Servidor API Central en línea. Iniciando sincronización integral de 100% de tablas."
        });
        await Task.Delay(150, ct);

        // 2. Paso 2: Despachar cola offline (30%)
        progress.Report(new SyncProgressReport
        {
            Percentage = 30,
            StepIndex = 2,
            CurrentStepTitle = "Despachando transacciones locales pendientes...",
            DetailMessage = $"Procesando cola de pendientes ({PendingItemsCount} elementos)..."
        });

        var initialPending = _pendingItemsCount;
        await ProcessPendingQueueAsync();
        result.DispatchedOfflineItemsCount = Math.Max(0, initialPending - _pendingItemsCount);

        progress.Report(new SyncProgressReport
        {
            Percentage = 40,
            StepIndex = 2,
            CurrentStepTitle = "Transacciones locales procesadas",
            DetailMessage = $"Cola despachada ({result.DispatchedOfflineItemsCount} transacciones enviadas a MySQL)."
        });
        await Task.Delay(150, ct);

        // 3. Descargar Novedades Centrales (Bootstrap 100% Tablas) (50%)
        progress.Report(new SyncProgressReport
        {
            Percentage = 50,
            StepIndex = 3,
            CurrentStepTitle = "Descargando catálogos y registros desde el servidor...",
            DetailMessage = "Solicitando datos de todas las entidades desde el API Central..."
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

        // 4. Paso 3: Sincronizar Roles y Usuarios (60%)
        progress.Report(new SyncProgressReport
        {
            Percentage = 60,
            StepIndex = 3,
            CurrentStepTitle = "Sincronizando Usuarios, Roles y Permisos...",
            DetailMessage = $"Procesando {bootstrap.Users.Count} usuarios de MySQL..."
        });

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
        await Task.Delay(100, ct);

        // 5. Paso 4: Sincronizar Medios de Pago y Sedes (70%)
        progress.Report(new SyncProgressReport
        {
            Percentage = 70,
            StepIndex = 4,
            CurrentStepTitle = "Sincronizando Medios de Pago y Sedes...",
            DetailMessage = $"Actualizando {bootstrap.PaymentMethods?.Count ?? 0} medios de pago y {bootstrap.Branches?.Count ?? 0} sedes..."
        });

        if (bootstrap.Branches != null && bootstrap.Branches.Count > 0)
        {
            foreach (var br in bootstrap.Branches)
            {
                var existingBranch = await db.Branches.FirstOrDefaultAsync(b => b.Id == br.Id, ct);
                if (existingBranch != null)
                {
                    existingBranch.Code = br.Code;
                    existingBranch.Name = br.Name;
                    existingBranch.Address = br.Address;
                    existingBranch.Phone = br.Phone;
                    existingBranch.City = br.City;
                    existingBranch.TotalCapacity = br.TotalCapacity;
                    existingBranch.Notes = br.Notes;
                    existingBranch.LogoBase64 = br.LogoBase64;
                    existingBranch.IsActive = br.IsActive;
                }
                else
                {
                    db.Branches.Add(new Branch
                    {
                        Id = br.Id,
                        Code = br.Code,
                        Name = br.Name,
                        Address = br.Address,
                        Phone = br.Phone,
                        City = br.City,
                        TotalCapacity = br.TotalCapacity,
                        Notes = br.Notes,
                        LogoBase64 = br.LogoBase64,
                        IsActive = br.IsActive,
                        CreatedAtUtc = br.CreatedAtUtc
                    });
                }
            }
            await db.SaveChangesAsync(ct);
        }

        int paymentMethodsCount = 0;
        if (bootstrap.PaymentMethods != null && bootstrap.PaymentMethods.Count > 0)
        {
            foreach (var pm in bootstrap.PaymentMethods)
            {
                var existing = await db.PaymentMethods.FirstOrDefaultAsync(p => p.Id == pm.Id, ct);
                if (existing != null)
                {
                    existing.Name = pm.Name;
                    existing.Icon = string.IsNullOrWhiteSpace(pm.Icon) ? "IconCash" : pm.Icon;
                    existing.State = pm.GetEffectiveActive();
                    existing.RequiresCashTender = pm.RequiresCashTender ?? true;
                }
                else
                {
                    db.PaymentMethods.Add(new PaymentMethodEntity
                    {
                        Id = pm.Id,
                        Name = pm.Name,
                        Icon = string.IsNullOrWhiteSpace(pm.Icon) ? "IconCash" : pm.Icon,
                        State = pm.GetEffectiveActive(),
                        RequiresCashTender = pm.RequiresCashTender ?? true
                    });
                }
                paymentMethodsCount++;
            }
            await db.SaveChangesAsync(ct);
        }
        result.SyncedPaymentMethodsCount = paymentMethodsCount;
        await Task.Delay(100, ct);

        // 6. Paso 5: Sincronizar Tarifas y Reglas (78%)
        progress.Report(new SyncProgressReport
        {
            Percentage = 78,
            StepIndex = 5,
            CurrentStepTitle = "Sincronizando Tarifas y Reglas vehiculares...",
            DetailMessage = $"Actualizando {bootstrap.Rates?.Count ?? 0} tarifas vehiculares configuradas..."
        });

        int ratesCount = 0;
        if (bootstrap.Rates != null)
        {
            foreach (var rate in bootstrap.Rates)
            {
                var vehicleType = rate.GetVehicleType();
                var existing = await db.VehicleRates.FirstOrDefaultAsync(r => r.VehicleType == vehicleType, ct);
                if (existing != null)
                {
                    existing.HourRate = rate.HourRate;
                    existing.MinuteRate = rate.MinuteRate;
                    existing.FullDayRate = rate.FullDayRate;
                    existing.GracePeriodMinutes = rate.GracePeriodMinutes;
                    existing.DisplayName = rate.DisplayName;
                    existing.IconKey = string.IsNullOrWhiteSpace(rate.IconKey) ? "IconCar" : rate.IconKey;
                    existing.IsActive = rate.IsActive;
                }
                else
                {
                    db.VehicleRates.Add(new VehicleRate
                    {
                        RateId = rate.RateId,
                        BranchId = rate.BranchId,
                        VehicleType = vehicleType,
                        DisplayName = rate.DisplayName,
                        MinuteRate = rate.MinuteRate,
                        HourRate = rate.HourRate,
                        FullDayRate = rate.FullDayRate,
                        GracePeriodMinutes = rate.GracePeriodMinutes,
                        IconKey = string.IsNullOrWhiteSpace(rate.IconKey) ? "IconCar" : rate.IconKey,
                        IsActive = rate.IsActive,
                        UpdatedAtUtc = rate.UpdatedAtUtc ?? DateTime.UtcNow
                    });
                }
                ratesCount++;
            }
            await db.SaveChangesAsync(ct);
        }
        result.SyncedRatesCount = ratesCount;
        await Task.Delay(100, ct);

        // 7. Paso 6: Sincronizar Comercios y Convenios (85%)
        progress.Report(new SyncProgressReport
        {
            Percentage = 85,
            StepIndex = 6,
            CurrentStepTitle = "Sincronizando Comercios y Convenios...",
            DetailMessage = $"Actualizando {bootstrap.Stores?.Count ?? 0} comercios y {bootstrap.Agreements?.Count ?? 0} convenios..."
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
                    db.Stores.Add(new Store
                    {
                        StoreId = store.StoreId,
                        Name = store.Name,
                        TaxId = store.TaxId,
                        PhoneNumber = store.PhoneNumber,
                        IsActive = store.IsActive,
                        CreatedAtUtc = DateTime.UtcNow
                    });
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
                    db.CommercialAgreements.Add(new CommercialAgreement
                    {
                        AgreementId = ag.AgreementId,
                        StoreId = ag.StoreId,
                        Name = ag.Name,
                        MinPurchaseAmount = ag.MinPurchaseAmount,
                        DiscountPercentage = ag.DiscountPercentage,
                        DiscountFixedAmount = ag.DiscountFixedAmount,
                        MaxHoursApplicable = ag.MaxHoursApplicable,
                        IsActive = ag.IsActive,
                        CreatedAtUtc = DateTime.UtcNow
                    });
                }
                agCount++;
            }
            await db.SaveChangesAsync(ct);
        }
        result.SyncedAgreementsCount = agCount;
        await Task.Delay(100, ct);

        // 8. Paso 7: Sincronizar Mensualidades y Turnos (92%)
        progress.Report(new SyncProgressReport
        {
            Percentage = 92,
            StepIndex = 7,
            CurrentStepTitle = "Sincronizando Mensualidades y Turnos...",
            DetailMessage = $"Actualizando {bootstrap.MonthlySubscriptions?.Count ?? 0} suscripciones y {bootstrap.WorkShifts?.Count ?? 0} turnos..."
        });

        int subsCount = 0;
        if (bootstrap.MonthlySubscriptions != null)
        {
            foreach (var sub in bootstrap.MonthlySubscriptions)
            {
                var existing = await db.MonthlySubscriptions.FirstOrDefaultAsync(s => s.SubscriptionId == sub.SubscriptionId, ct);
                if (existing != null)
                {
                    existing.PlateNumber = sub.PlateNumber;
                    existing.VehicleType = sub.GetVehicleType();
                    existing.CustomerName = sub.CustomerName;
                    existing.CustomerDocument = sub.CustomerDocument;
                    existing.CustomerPhone = sub.CustomerPhone;
                    existing.StartDateUtc = sub.StartDateUtc;
                    existing.EndDateUtc = sub.EndDateUtc;
                    existing.MonthlyFee = sub.MonthlyFee;
                    existing.AmountPaid = sub.AmountPaid;
                    existing.PaymentMethod = sub.GetPaymentMethod();
                    existing.IsActive = sub.IsActive;
                }
                else
                {
                    db.MonthlySubscriptions.Add(new MonthlySubscription
                    {
                        SubscriptionId = sub.SubscriptionId,
                        BranchId = sub.BranchId,
                        PlateNumber = sub.PlateNumber,
                        VehicleType = sub.GetVehicleType(),
                        CustomerName = sub.CustomerName,
                        CustomerDocument = sub.CustomerDocument,
                        CustomerPhone = sub.CustomerPhone,
                        CustomerEmail = sub.CustomerEmail,
                        StartDateUtc = sub.StartDateUtc,
                        EndDateUtc = sub.EndDateUtc,
                        MonthlyFee = sub.MonthlyFee,
                        AmountPaid = sub.AmountPaid,
                        PaymentMethod = sub.GetPaymentMethod(),
                        IsActive = sub.IsActive,
                        Notes = sub.Notes,
                        CreatedAtUtc = sub.CreatedAtUtc
                    });
                }
                subsCount++;
            }
            await db.SaveChangesAsync(ct);
        }
        result.SyncedSubscriptionsCount = subsCount;

        int shiftsCount = 0;
        if (bootstrap.WorkShifts != null)
        {
            foreach (var ws in bootstrap.WorkShifts)
            {
                var normalizedStatus = ws.GetNormalizedStatus();
                var existing = await db.WorkShifts.FirstOrDefaultAsync(s => s.ShiftId == ws.ShiftId, ct);
                if (existing != null)
                {
                    existing.UserId = ws.UserId;
                    existing.OperatorName = ws.OperatorName;
                    existing.StartTimeUtc = ws.StartTimeUtc;
                    existing.EndTimeUtc = ws.EndTimeUtc;
                    existing.BaseAmount = ws.BaseAmount;
                    existing.TotalCashCollected = ws.TotalCashCollected;
                    existing.TotalCardCollected = ws.TotalCardCollected;
                    existing.TotalTransferCollected = ws.TotalTransferCollected;
                    existing.TotalDiscounts = ws.TotalDiscounts;
                    existing.TotalCashWithdrawals = ws.TotalCashWithdrawals;
                    existing.ExpectedCash = ws.ExpectedCash;
                    existing.ActualCashCounted = ws.ActualCashCounted;
                    existing.CashDifference = ws.CashDifference;
                    existing.TotalTicketsProcessed = ws.TotalTicketsProcessed;
                    existing.TotalVehiclesEntered = ws.TotalVehiclesEntered;
                    existing.Status = normalizedStatus;
                    existing.HandoverToUserId = ws.HandoverToUserId;
                    existing.HandoverToUserName = ws.HandoverToUserName;
                    existing.Notes = ws.Notes;
                }
                else
                {
                    db.WorkShifts.Add(new WorkShift
                    {
                        ShiftId = ws.ShiftId,
                        BranchId = ws.BranchId,
                        UserId = ws.UserId,
                        OperatorName = ws.OperatorName,
                        StartTimeUtc = ws.StartTimeUtc,
                        EndTimeUtc = ws.EndTimeUtc,
                        BaseAmount = ws.BaseAmount,
                        TotalCashCollected = ws.TotalCashCollected,
                        TotalCardCollected = ws.TotalCardCollected,
                        TotalTransferCollected = ws.TotalTransferCollected,
                        TotalDiscounts = ws.TotalDiscounts,
                        TotalCashWithdrawals = ws.TotalCashWithdrawals,
                        ExpectedCash = ws.ExpectedCash,
                        ActualCashCounted = ws.ActualCashCounted,
                        CashDifference = ws.CashDifference,
                        TotalTicketsProcessed = ws.TotalTicketsProcessed,
                        TotalVehiclesEntered = ws.TotalVehiclesEntered,
                        Status = normalizedStatus,
                        HandoverToUserId = ws.HandoverToUserId,
                        HandoverToUserName = ws.HandoverToUserName,
                        Notes = ws.Notes,
                        IsSynchronized = true,
                        CreatedAtUtc = ws.CreatedAtUtc,
                        ClosedAtUtc = ws.ClosedAtUtc
                    });
                }
                shiftsCount++;
            }
            await db.SaveChangesAsync(ct);
        }
        result.SyncedShiftsCount = shiftsCount;
        await Task.Delay(100, ct);

        // 9. Paso 8: Sincronizar Tiquetes y Consolidar (98%)
        progress.Report(new SyncProgressReport
        {
            Percentage = 98,
            StepIndex = 8,
            CurrentStepTitle = "Sincronizando Tiquetes activos y registros...",
            DetailMessage = "Consolidando registros de acceso y guardando en SQLite..."
        });

        var allIncomingTickets = new List<ApiParkingTicketSyncDto>();
        if (bootstrap.ActiveTickets != null) allIncomingTickets.AddRange(bootstrap.ActiveTickets);
        if (bootstrap.RecentTickets != null) allIncomingTickets.AddRange(bootstrap.RecentTickets);

        int ticketsCount = 0;
        foreach (var ticket in allIncomingTickets)
        {
            var vehicleType = ticket.GetVehicleType();
            var status = ticket.GetTicketStatus();
            var paymentMethod = ticket.GetPaymentMethod();

            var existing = await db.ParkingTickets.FirstOrDefaultAsync(t => t.TicketId == ticket.TicketId || t.TicketNumber == ticket.TicketNumber, ct);
            if (existing != null)
            {
                existing.TicketNumber = ticket.TicketNumber;
                existing.PlateNumber = ticket.PlateNumber;
                existing.VehicleType = vehicleType;
                existing.CustomerPhone = ticket.CustomerPhone;
                existing.Notes = ticket.Notes;
                existing.OperatorName = !string.IsNullOrWhiteSpace(ticket.OperatorName) ? ticket.OperatorName : "Operador General";
                existing.EntryTimeUtc = ticket.EntryTimeUtc;
                existing.ExitTimeUtc = ticket.ExitTimeUtc;
                existing.TotalDurationMinutes = ticket.TotalDurationMinutes;
                existing.Status = status;
                existing.HourlyRate = ticket.HourlyRate;
                existing.GrossAmount = ticket.GrossAmount;
                existing.DiscountAmount = ticket.DiscountAmount;
                existing.NetAmount = ticket.NetAmount;
                existing.AmountPaid = ticket.AmountPaid;
                existing.ChangeGiven = ticket.ChangeGiven;
                existing.PaymentMethod = paymentMethod;
                existing.PaymentMethodId = ticket.PaymentMethodId;
                existing.ExitNotes = ticket.ExitNotes;
                existing.IsSynchronized = true;
            }
            else
            {
                db.ParkingTickets.Add(new ParkingTicket
                {
                    TicketId = ticket.TicketId,
                    BranchId = ticket.BranchId,
                    TicketNumber = ticket.TicketNumber,
                    PlateNumber = ticket.PlateNumber,
                    VehicleType = vehicleType,
                    CustomerPhone = ticket.CustomerPhone,
                    BayNumber = ticket.BayNumber,
                    Notes = ticket.Notes,
                    EntryTimeUtc = ticket.EntryTimeUtc,
                    ExitTimeUtc = ticket.ExitTimeUtc,
                    TotalDurationMinutes = ticket.TotalDurationMinutes,
                    HourlyRate = ticket.HourlyRate,
                    GrossAmount = ticket.GrossAmount,
                    DiscountAmount = ticket.DiscountAmount,
                    NetAmount = ticket.NetAmount,
                    AmountPaid = ticket.AmountPaid,
                    ChangeGiven = ticket.ChangeGiven,
                    PaymentMethod = paymentMethod,
                    PaymentMethodId = ticket.PaymentMethodId,
                    ExitNotes = ticket.ExitNotes,
                    Status = status,
                    OperatorName = !string.IsNullOrWhiteSpace(ticket.OperatorName) ? ticket.OperatorName : "Operador General",
                    IsSynchronized = true,
                    CreatedAtUtc = ticket.CreatedAtUtc
                });
            }
            ticketsCount++;
        }
        result.SyncedTicketsCount = ticketsCount;

        await db.SaveChangesAsync(ct);
        _lastSyncTime = DateTime.UtcNow;
        _isOnline = true;
        result.Success = true;
        result.Message = $"Sincronización total exitosa: {usersCount} usuarios, {paymentMethodsCount} medios de pago, {ratesCount} tarifas, {agCount} convenios, {subsCount} mensualidades, {shiftsCount} turnos y {ticketsCount} tiquetes actualizados.";

        // Notificar a todos los módulos y viewmodels para actualización reactiva en memoria
        DataSynchronized?.Invoke();

        // Paso 9: Finalizado (100%)
        progress.Report(new SyncProgressReport
        {
            Percentage = 100,
            StepIndex = 8,
            CurrentStepTitle = "¡Sincronización Total Completada!",
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
            var report = await PerformFullSyncWithProgressAsync(new Progress<SyncProgressReport>());
            return report.Success;
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
