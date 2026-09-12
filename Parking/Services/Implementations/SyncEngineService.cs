using System;
using System.Collections.Generic;
using System.Globalization;
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
    private readonly ISessionService _sessionService;
    private readonly IShiftService? _shiftService;
    private readonly ISignalRClientService? _signalRClient;
    private bool _isOnline;
    private int _pendingItemsCount;
    private DateTime? _lastSyncTime;
    private CancellationTokenSource? _offlineProbeCts;
    private readonly object _offlineProbeLock = new();
    private readonly SemaphoreSlim _pendingQueueLock = new(1, 1);
    private int _offlineAttemptCount = 0;

    public event EventHandler<string>? SyncStatusChanged;
    public event Action<int>? TotalCapacityChanged;
    public event Action? DataSynchronized;

    private void NotifyDataSynchronized()
    {
        var app = System.Windows.Application.Current;
        if (app?.Dispatcher != null && !app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.InvokeAsync(() =>
            {
                try { DataSynchronized?.Invoke(); } catch { }
            });
        }
        else
        {
            try { DataSynchronized?.Invoke(); } catch { }
        }
    }

    private void NotifySyncStatusChanged(string status)
    {
        var app = System.Windows.Application.Current;
        if (app?.Dispatcher != null && !app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.InvokeAsync(() =>
            {
                try { SyncStatusChanged?.Invoke(this, status); } catch { }
            });
        }
        else
        {
            try { SyncStatusChanged?.Invoke(this, status); } catch { }
        }
    }

    private void NotifyTotalCapacityChanged(int capacity)
    {
        var app = System.Windows.Application.Current;
        if (app?.Dispatcher != null && !app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.InvokeAsync(() =>
            {
                try { TotalCapacityChanged?.Invoke(capacity); } catch { }
            });
        }
        else
        {
            try { TotalCapacityChanged?.Invoke(capacity); } catch { }
        }
    }

    public bool IsOnline => _isOnline;
    public int PendingItemsCount => _pendingItemsCount;
    public DateTime? LastSyncTime => _lastSyncTime;
    public int ServerConfiguredCapacity { get; private set; } = 100;

    public string SyncStatusDescription => _isOnline
        ? (_lastSyncTime.HasValue ? $"Sincronizado ({FormatSyncTime(_lastSyncTime.Value)})" : "Sincronizado")
        : (_pendingItemsCount > 0 ? $"Modo Offline Local • {_pendingItemsCount} pendientes" : "Modo Offline Local (Sin Conexión)");

    private static string FormatSyncTime(DateTime time)
    {
        var localTime = time.Kind == DateTimeKind.Utc ? time.ToLocalTime() : time;
        return localTime.ToString("hh:mm tt", CultureInfo.InvariantCulture);
    }

    public SyncEngineService(
        IApiClientService apiClient,
        IDbConnectionManager dbManager,
        ISessionService sessionService,
        IShiftService? shiftService = null,
        ISignalRClientService? signalRClient = null)
    {
        _apiClient = apiClient;
        _dbManager = dbManager;
        _sessionService = sessionService;
        _shiftService = shiftService;
        _signalRClient = signalRClient;

        _apiClient.ConnectionStateChanged += isOnline =>
        {
            SetOnlineStatus(isOnline);
        };

        if (signalRClient != null)
        {
            signalRClient.ConnectionStatusChanged += isConnected =>
            {
                SetOnlineStatus(isConnected);
            };
        }

        try
        {
            System.Net.NetworkInformation.NetworkChange.NetworkAvailabilityChanged += (s, e) =>
            {
                if (!e.IsAvailable)
                {
                    SetOnlineStatus(false);
                }
                else
                {
                    // Enlace de hardware restaurado (cable de red o Wi-Fi activo en Windows)
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(1000); // Pausa de 1s para negociación de IP/DNS
                            var isAlive = await _apiClient.PingAsync();
                            if (isAlive)
                            {
                                SetOnlineStatus(true);
                            }
                        }
                        catch { }
                    });
                }
            };
        }
        catch { }
    }

    public async Task<bool> PerformFullSyncAsync()
    {
        var report = await PerformFullSyncWithProgressAsync(new Progress<SyncProgressReport>());
        return report.Success;
    }

    public async Task<SyncResultReport> PerformFullSyncWithProgressAsync(IProgress<SyncProgressReport> progress, CancellationToken ct = default)
    {
        var result = new SyncResultReport();
        try
        {
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
                NotifySyncStatusChanged(SyncStatusDescription);
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

            var currentBranchId = _sessionService.CurrentBranch?.Id;
            var bootstrap = await _apiClient.GetBootstrapAsync(currentBranchId);
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
                if (_sessionService.CurrentBranch != null && _sessionService.CurrentBranch.TotalCapacity != bootstrap.TotalCapacity)
                {
                    _sessionService.UpdateCurrentBranch(b => b.TotalCapacity = bootstrap.TotalCapacity);
                }
                NotifyTotalCapacityChanged(bootstrap.TotalCapacity);
            }

            if (bootstrap.CompanyId.HasValue && bootstrap.CompanyId.Value > 0)
            {
                if (_sessionService.CurrentUser != null && (!_sessionService.CurrentUser.CompanyId.HasValue || _sessionService.CurrentUser.CompanyId.Value <= 0))
                {
                    _sessionService.CurrentUser.CompanyId = bootstrap.CompanyId.Value;
                }
                if (_sessionService.CurrentBranch != null && (!_sessionService.CurrentBranch.CompanyId.HasValue || _sessionService.CurrentBranch.CompanyId.Value <= 0))
                {
                    _sessionService.UpdateCurrentBranch(b => b.CompanyId = bootstrap.CompanyId.Value);
                }
            }

            if (_sessionService.CurrentUser != null)
            {
                _sessionService.CurrentUser.RequireOpenShiftToOperate = bootstrap.RequireOpenShiftToOperate;
                _sessionService.CurrentUser.RequireInitialCashAmount = bootstrap.RequireInitialCashAmount;
                _sessionService.CurrentUser.AllowMultipleSessions = bootstrap.AllowMultipleSessions;
                _sessionService.CurrentUser.MaxActiveSessionsPerUser = bootstrap.MaxActiveSessionsPerUser;
                _sessionService.CurrentUser.AllowMultipleOpenShifts = bootstrap.AllowMultipleOpenShifts;
                _sessionService.CurrentUser.MaxOpenShiftsPerUser = bootstrap.MaxOpenShiftsPerUser;
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

            // Sincronizar Catálogo de Roles RBAC si vienen en el Bootstrap
            var roleMapping = new Dictionary<int, Guid>();
            roleMapping[1] = adminRole.RoleId;
            roleMapping[2] = operatorRole.RoleId;

            if (bootstrap.UserRoles != null && bootstrap.UserRoles.Count > 0)
            {
                var existingRoles = await db.Roles.ToListAsync(ct);
                foreach (var ur in bootstrap.UserRoles)
                {
                    var match = existingRoles.FirstOrDefault(r => r.Name.Equals(ur.Role, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        match.Description = ur.Description ?? match.Description;
                        match.IsActive = ur.IsActive;
                        roleMapping[ur.Id] = match.RoleId;
                    }
                    else
                    {
                        var newRole = new Role
                        {
                            RoleId = Guid.NewGuid(),
                            Name = ur.Role,
                            Description = ur.Description,
                            IsActive = ur.IsActive
                        };
                        db.Roles.Add(newRole);
                        roleMapping[ur.Id] = newRole.RoleId;
                    }
                }
                await db.SaveChangesAsync(ct);
            }

            // Sincronizar Permisos de Roles (RoleActions -> RolePermissions)
            if (bootstrap.RoleActions != null && bootstrap.RoleActions.Count > 0)
            {
                var allModules = await db.AppModules.ToListAsync(ct);
                var defaultModule = allModules.FirstOrDefault() ?? new AppModule
                {
                    ModuleId = Guid.NewGuid(),
                    ModuleKey = "general",
                    DisplayName = "General",
                    IconKey = "IconSettings"
                };
                if (!allModules.Contains(defaultModule))
                {
                    db.AppModules.Add(defaultModule);
                    await db.SaveChangesAsync(ct);
                }

                var allPermissions = await db.AppPermissions.ToListAsync(ct);
                var allRolePermissions = await db.RolePermissions.ToListAsync(ct);

                foreach (var ra in bootstrap.RoleActions)
                {
                    if (string.IsNullOrWhiteSpace(ra.ActionSlug)) continue;

                    var perm = allPermissions.FirstOrDefault(p => p.ActionKey.Equals(ra.ActionSlug, StringComparison.OrdinalIgnoreCase));
                    if (perm == null)
                    {
                        perm = new AppPermission
                        {
                            PermissionId = Guid.NewGuid(),
                            ModuleId = defaultModule.ModuleId,
                            ActionKey = ra.ActionSlug,
                            DisplayName = ra.ActionName ?? ra.ActionSlug,
                            Description = ra.ActionName
                        };
                        db.AppPermissions.Add(perm);
                        allPermissions.Add(perm);
                        await db.SaveChangesAsync(ct);
                    }

                    if (roleMapping.TryGetValue(ra.RoleId, out var targetRoleId))
                    {
                        var rolePerm = allRolePermissions.FirstOrDefault(rp => rp.RoleId == targetRoleId && rp.PermissionId == perm.PermissionId);
                        if (rolePerm != null)
                        {
                            rolePerm.IsGranted = ra.IsActive;
                        }
                        else
                        {
                            var newRp = new RolePermission
                            {
                                RolePermissionId = Guid.NewGuid(),
                                RoleId = targetRoleId,
                                PermissionId = perm.PermissionId,
                                IsGranted = ra.IsActive,
                                GrantedAtUtc = DateTime.UtcNow
                            };
                            db.RolePermissions.Add(newRp);
                            allRolePermissions.Add(newRp);
                        }
                    }
                }
                await db.SaveChangesAsync(ct);
            }

            var mockUsers = await db.Users.Where(u => u.FullName == "Alexander Wright" || u.FullName == "Elena Vance" || u.Username == "alexander" || u.Username == "elena").ToListAsync(ct);
            if (mockUsers.Count > 0)
            {
                db.Users.RemoveRange(mockUsers);
                await db.SaveChangesAsync(ct);
            }

            int usersCount = 0;
            if (bootstrap.Users != null)
            {
                var incomingUsernames = bootstrap.Users.Select(u => u.Username.ToLowerInvariant()).ToHashSet();
                var localUsers = await db.Users.ToListAsync(ct);
                var currentUsername = _sessionService.CurrentUser?.Username?.ToLowerInvariant();
                var usersToDelete = localUsers.Where(u => 
                    !incomingUsernames.Contains(u.Username.ToLowerInvariant()) 
                    && u.Username != "admin" 
                    && (string.IsNullOrEmpty(currentUsername) || u.Username.ToLowerInvariant() != currentUsername)
                    && string.IsNullOrWhiteSpace(u.PasswordHash)
                ).ToList();
                if (usersToDelete.Count > 0)
                {
                    db.Users.RemoveRange(usersToDelete);
                }

                foreach (var apiUser in bootstrap.Users)
                {
                    Guid targetRoleId;
                    if (roleMapping.TryGetValue(apiUser.UserRoleId, out var mappedId))
                    {
                        targetRoleId = mappedId;
                    }
                    else
                    {
                        targetRoleId = apiUser.UserRoleId == 1 ? adminRole.RoleId : operatorRole.RoleId;
                    }

                    var fullName = !string.IsNullOrWhiteSpace(apiUser.FullName)
                        ? apiUser.FullName
                        : $"{apiUser.FirstName} {apiUser.FirstSurname}".Trim();

                    var existing = localUsers.FirstOrDefault(u => u.Username.ToLower() == apiUser.Username.ToLower());
                    if (existing != null)
                    {
                        existing.FullName = fullName;
                        existing.Email = apiUser.Email;
                        if (apiUser.CompanyId.HasValue && apiUser.CompanyId.Value > 0)
                        {
                            existing.CompanyId = apiUser.CompanyId;
                        }
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
                            CompanyId = apiUser.CompanyId,
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

            if (bootstrap.Branches != null)
            {
                var incomingBranchIds = bootstrap.Branches.Select(b => b.Id).ToHashSet();
                var localBranches = await db.Branches.ToListAsync(ct);
                var branchesToDelete = localBranches.Where(b => !incomingBranchIds.Contains(b.Id)).ToList();
                if (branchesToDelete.Count > 0)
                {
                    db.Branches.RemoveRange(branchesToDelete);
                }

                foreach (var br in bootstrap.Branches)
                {
                    var existingBranch = localBranches.FirstOrDefault(b => b.Id == br.Id);
                    if (existingBranch != null)
                    {
                        if (br.CompanyId.HasValue && br.CompanyId.Value > 0)
                        {
                            existingBranch.CompanyId = br.CompanyId.Value;
                        }
                        existingBranch.Code = br.Code;
                        existingBranch.Name = br.Name;
                        existingBranch.Address = br.Address;
                        existingBranch.Phone = br.Phone;
                        existingBranch.City = br.City;
                        existingBranch.TotalCapacity = br.TotalCapacity;
                        existingBranch.Notes = br.Notes;
                        existingBranch.LogoBase64 = br.LogoBase64;
                        existingBranch.PaperWidth = br.PaperWidth > 0 ? br.PaperWidth : 80;
                        existingBranch.DefaultInitialCash = br.DefaultInitialCash ?? 0;
                        existingBranch.AllowChargeByMinute = br.AllowChargeByMinute;
                        existingBranch.AllowChargeByHour = br.AllowChargeByHour;
                        existingBranch.AllowChargeByDay = br.AllowChargeByDay;
                        existingBranch.AllowChargeByNight = br.AllowChargeByNight;
                        existingBranch.LostTicketFee = br.LostTicketFee;
                        existingBranch.FullDayThresholdMinutes = br.FullDayThresholdMinutes;
                        existingBranch.FullDayApplicableDays = br.FullDayApplicableDays;
                        existingBranch.FullDayStartTime = br.FullDayStartTime;
                        existingBranch.FullDayEndTime = br.FullDayEndTime;
                        existingBranch.FullDayRulesJson = br.FullDayRulesJson;
                        existingBranch.NightApplicableDays = br.NightApplicableDays;
                        existingBranch.NightStartTime = br.NightStartTime;
                        existingBranch.NightEndTime = br.NightEndTime;
                        existingBranch.NightStayMinMinutes = br.NightStayMinMinutes;
                        existingBranch.EntryGracePeriodMinutes = br.EntryGracePeriodMinutes;
                        existingBranch.ExitGracePeriodMinutes = br.ExitGracePeriodMinutes;
                        existingBranch.IsActive = br.IsActive;
                    }
                    else
                    {
                        db.Branches.Add(new Branch
                        {
                            Id = br.Id,
                            CompanyId = br.CompanyId,
                            Code = br.Code,
                            Name = br.Name,
                            Address = br.Address,
                            Phone = br.Phone,
                            City = br.City,
                            TotalCapacity = br.TotalCapacity,
                            Notes = br.Notes,
                            LogoBase64 = br.LogoBase64,
                            PaperWidth = br.PaperWidth > 0 ? br.PaperWidth : 80,
                            DefaultInitialCash = br.DefaultInitialCash ?? 0,
                            AllowChargeByMinute = br.AllowChargeByMinute,
                            AllowChargeByHour = br.AllowChargeByHour,
                            AllowChargeByDay = br.AllowChargeByDay,
                            AllowChargeByNight = br.AllowChargeByNight,
                            LostTicketFee = br.LostTicketFee,
                            FullDayThresholdMinutes = br.FullDayThresholdMinutes,
                            FullDayApplicableDays = br.FullDayApplicableDays,
                            FullDayStartTime = br.FullDayStartTime,
                            FullDayEndTime = br.FullDayEndTime,
                            FullDayRulesJson = br.FullDayRulesJson,
                            NightApplicableDays = br.NightApplicableDays,
                            NightStartTime = br.NightStartTime,
                            NightEndTime = br.NightEndTime,
                            NightStayMinMinutes = br.NightStayMinMinutes,
                            EntryGracePeriodMinutes = br.EntryGracePeriodMinutes,
                            ExitGracePeriodMinutes = br.ExitGracePeriodMinutes,
                            IsActive = br.IsActive,
                            CreatedAtUtc = br.CreatedAtUtc
                        });
                    }
                    if (_sessionService.CurrentBranch != null && _sessionService.CurrentBranch.Id == br.Id)
                    {
                        _sessionService.UpdateCurrentBranch(b =>
                        {
                            b.TotalCapacity = br.TotalCapacity;
                            b.Name = br.Name;
                            b.Address = br.Address;
                            b.Phone = br.Phone;
                            b.City = br.City;
                            b.PaperWidth = br.PaperWidth > 0 ? br.PaperWidth : 80;
                            b.DefaultInitialCash = br.DefaultInitialCash ?? 0;
                            b.AllowChargeByMinute = br.AllowChargeByMinute;
                            b.AllowChargeByHour = br.AllowChargeByHour;
                            b.AllowChargeByDay = br.AllowChargeByDay;
                            b.AllowChargeByNight = br.AllowChargeByNight;
                            b.LostTicketFee = br.LostTicketFee;
                            b.FullDayThresholdMinutes = br.FullDayThresholdMinutes;
                            b.FullDayApplicableDays = br.FullDayApplicableDays;
                            b.FullDayStartTime = br.FullDayStartTime;
                            b.FullDayEndTime = br.FullDayEndTime;
                            b.FullDayRulesJson = br.FullDayRulesJson;
                            b.NightApplicableDays = br.NightApplicableDays;
                            b.NightStartTime = br.NightStartTime;
                            b.NightEndTime = br.NightEndTime;
                            b.NightStayMinMinutes = br.NightStayMinMinutes;
                            b.EntryGracePeriodMinutes = br.EntryGracePeriodMinutes;
                            b.ExitGracePeriodMinutes = br.ExitGracePeriodMinutes;
                            if (br.CompanyId.HasValue && br.CompanyId.Value > 0)
                            {
                                b.CompanyId = br.CompanyId.Value;
                            }
                        });
                    }
                }
                await db.SaveChangesAsync(ct);
            }

            // 4.1 Sincronizar Horarios de Operación de Sedes
            if (bootstrap.OperatingHours != null)
            {
                var incomingOhIds = bootstrap.OperatingHours.Select(o => o.Id).ToHashSet();
                var localOhs = await db.BranchOperatingHours.ToListAsync(ct);
                var ohsToDelete = localOhs.Where(o => !incomingOhIds.Contains(o.Id)).ToList();
                if (ohsToDelete.Count > 0)
                {
                    db.BranchOperatingHours.RemoveRange(ohsToDelete);
                }

                foreach (var oh in bootstrap.OperatingHours)
                {
                    var existing = localOhs.FirstOrDefault(o => o.Id == oh.Id);
                    if (existing != null)
                    {
                        existing.BranchId = oh.BranchId;
                        existing.DayOfWeek = oh.DayOfWeek;
                        existing.IsOpen = oh.IsOpen;
                        existing.OpeningTime = oh.OpeningTime;
                        existing.ClosingTime = oh.ClosingTime;
                        existing.BufferMinutesBefore = oh.BufferMinutesBefore;
                        existing.BufferMinutesAfter = oh.BufferMinutesAfter;
                    }
                    else
                    {
                        db.BranchOperatingHours.Add(new BranchOperatingHour
                        {
                            Id = oh.Id,
                            BranchId = oh.BranchId,
                            DayOfWeek = oh.DayOfWeek,
                            IsOpen = oh.IsOpen,
                            OpeningTime = oh.OpeningTime,
                            ClosingTime = oh.ClosingTime,
                            BufferMinutesBefore = oh.BufferMinutesBefore,
                            BufferMinutesAfter = oh.BufferMinutesAfter
                        });
                    }
                }
                await db.SaveChangesAsync(ct);

                // Actualizar OperatingHours en la sede activa de la sesión
                if (_sessionService.CurrentBranch != null)
                {
                    var currentBranchOhs = await db.BranchOperatingHours
                        .Where(o => o.BranchId == _sessionService.CurrentBranch.Id)
                        .ToListAsync(ct);
                    _sessionService.UpdateCurrentBranch(b => b.OperatingHours = currentBranchOhs);
                }
            }

            int paymentMethodsCount = 0;
            if (bootstrap.PaymentMethods != null)
            {
                var incomingPmIds = bootstrap.PaymentMethods.Select(p => p.Id).ToHashSet();
                var localPms = await db.PaymentMethods.ToListAsync(ct);
                var pmsToDelete = localPms.Where(p => !incomingPmIds.Contains(p.Id)).ToList();
                if (pmsToDelete.Count > 0)
                {
                    db.PaymentMethods.RemoveRange(pmsToDelete);
                }

                foreach (var pm in bootstrap.PaymentMethods)
                {
                    var existing = localPms.FirstOrDefault(p => p.Id == pm.Id);
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

            // 5.1 Sincronizar Medios de Pago por Sede
            if (bootstrap.BranchPaymentMethods != null)
            {
                var incomingBpmIds = bootstrap.BranchPaymentMethods.Select(bpm => bpm.Id).ToHashSet();
                var localBpms = await db.BranchPaymentMethods.ToListAsync(ct);
                var bpmsToDelete = localBpms.Where(b => !incomingBpmIds.Contains(b.Id)).ToList();
                if (bpmsToDelete.Count > 0)
                {
                    db.BranchPaymentMethods.RemoveRange(bpmsToDelete);
                }

                foreach (var bpm in bootstrap.BranchPaymentMethods)
                {
                    var existing = localBpms.FirstOrDefault(b => b.Id == bpm.Id);
                    if (existing != null)
                    {
                        existing.BranchId = bpm.BranchId;
                        existing.PaymentMethodId = bpm.PaymentMethodId;
                        existing.RequiresCashTender = bpm.RequiresCashTender;
                        existing.IsActive = bpm.IsActive;
                    }
                    else
                    {
                        db.BranchPaymentMethods.Add(new BranchPaymentMethodEntity
                        {
                            Id = bpm.Id,
                            BranchId = bpm.BranchId,
                            PaymentMethodId = bpm.PaymentMethodId,
                            RequiresCashTender = bpm.RequiresCashTender,
                            IsActive = bpm.IsActive
                        });
                    }
                }
                await db.SaveChangesAsync(ct);
            }

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
                var incomingRateIds = bootstrap.Rates.Select(r => r.GetRateId()).ToHashSet();
                var incomingVehicleTypes = bootstrap.Rates.Select(r => r.GetVehicleType()).ToHashSet();
                var localRates = await db.VehicleRates.ToListAsync(ct);

                // 1. Eliminar tarifas obsoletas que ya no existan en el backend (estrictamente por RateId)
                var ratesToDelete = currentBranchId.HasValue
                    ? localRates.Where(r => (r.BranchId == currentBranchId.Value || r.BranchId == null) && !incomingRateIds.Contains(r.RateId)).ToList()
                    : localRates.Where(r => !incomingRateIds.Contains(r.RateId)).ToList();

                if (ratesToDelete.Count > 0)
                {
                    db.VehicleRates.RemoveRange(ratesToDelete);
                    await db.SaveChangesAsync(ct);
                    localRates = await db.VehicleRates.ToListAsync(ct);
                }

                // 2. Upsert por RateId canónico (evitando colisiones por VehicleType compartido)
                foreach (var rate in bootstrap.Rates)
                {
                    var rateId = rate.GetRateId();
                    var vehicleType = rate.GetVehicleType();
                    var targetBranchId = rate.GetBranchId() ?? currentBranchId;

                    // Omitir registros de plantilla general sin sede que tengan tarifa 0 (catálogo no asignado a la sede)
                    if (!rate.GetBranchId().HasValue && rate.GetHourRate() == 0 && rate.GetMinuteRate() == 0)
                    {
                        continue;
                    }

                    var displayName = rate.GetDisplayName();
                    var hourRate = rate.GetHourRate();
                    var minuteRate = rate.GetMinuteRate();
                    var fullDayRate = rate.GetFullDayRate();
                    var nightRate = rate.GetNightRate();
                    var grace = rate.GetGracePeriodMinutes();
                    var iconKey = rate.GetIconKey();
                    var isActive = rate.GetEffectiveActive();

                    var existing = localRates.FirstOrDefault(r => r.RateId == rateId);
                    if (existing == null && rateId == Guid.Empty)
                    {
                        existing = localRates.FirstOrDefault(r => r.BranchId == targetBranchId && r.VehicleType == vehicleType && r.DayOfWeek == rate.DayOfWeek);
                    }

                    if (existing != null)
                    {
                        existing.BranchId = targetBranchId;
                        existing.VehicleType = vehicleType;
                        existing.DisplayName = displayName;
                        existing.HourRate = hourRate;
                        existing.MinuteRate = minuteRate;
                        existing.FullDayRate = fullDayRate;
                        existing.FullDayStartTime = rate.FullDayStartTime;
                        existing.FullDayEndTime = rate.FullDayEndTime;
                        existing.FullDayThresholdMinutes = rate.FullDayThresholdMinutes;
                        existing.FullDayCoverageMinutes = rate.FullDayCoverageMinutes;
                        existing.NightRate = nightRate;
                        existing.NightStartTime = rate.NightStartTime;
                        existing.NightEndTime = rate.NightEndTime;
                        existing.NightStayMinMinutes = rate.NightStayMinMinutes;
                        existing.GracePeriodMinutes = grace;
                        existing.DayOfWeek = rate.DayOfWeek;
                        existing.IconKey = string.IsNullOrWhiteSpace(iconKey) ? "IconCar" : iconKey;
                        existing.IsActive = isActive;
                        existing.UpdatedAtUtc = rate.UpdatedAtUtc ?? DateTime.UtcNow;
                    }
                    else
                    {
                        var newRate = new VehicleRate
                        {
                            RateId = rateId,
                            BranchId = targetBranchId,
                            VehicleType = vehicleType,
                            DisplayName = displayName,
                            MinuteRate = minuteRate,
                            HourRate = hourRate,
                            FullDayRate = fullDayRate,
                            FullDayStartTime = rate.FullDayStartTime,
                            FullDayEndTime = rate.FullDayEndTime,
                            FullDayThresholdMinutes = rate.FullDayThresholdMinutes,
                            FullDayCoverageMinutes = rate.FullDayCoverageMinutes,
                            NightRate = nightRate,
                            NightStartTime = rate.NightStartTime,
                            NightEndTime = rate.NightEndTime,
                            NightStayMinMinutes = rate.NightStayMinMinutes,
                            GracePeriodMinutes = grace,
                            DayOfWeek = rate.DayOfWeek,
                            IconKey = string.IsNullOrWhiteSpace(iconKey) ? "IconCar" : iconKey,
                            IsActive = isActive,
                            UpdatedAtUtc = rate.UpdatedAtUtc ?? DateTime.UtcNow
                        };
                        db.VehicleRates.Add(newRate);
                        localRates.Add(newRate);
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

            if (bootstrap.Agreements != null)
            {
                var incomingAgIds = bootstrap.Agreements.Select(a => a.AgreementId).ToHashSet();
                var localAgreements = await db.CommercialAgreements.ToListAsync(ct);
                var agsToDelete = localAgreements.Where(a => !incomingAgIds.Contains(a.AgreementId)).ToList();
                if (agsToDelete.Count > 0)
                {
                    db.CommercialAgreements.RemoveRange(agsToDelete);
                }
            }

            if (bootstrap.Stores != null)
            {
                var incomingStoreIds = bootstrap.Stores.Select(s => s.StoreId).ToHashSet();
                var localStores = await db.Stores.ToListAsync(ct);
                var storesToDelete = localStores.Where(s => !incomingStoreIds.Contains(s.StoreId)).ToList();
                if (storesToDelete.Count > 0)
                {
                    db.Stores.RemoveRange(storesToDelete);
                }

                foreach (var store in bootstrap.Stores)
                {
                    var existing = localStores.FirstOrDefault(s => s.StoreId == store.StoreId);
                    if (existing != null)
                    {
                        existing.Name = store.Name;
                        existing.TaxId = store.TaxId ?? string.Empty;
                        existing.PhoneNumber = store.PhoneNumber;
                        existing.IsActive = store.IsActive;
                    }
                    else
                    {
                        db.Stores.Add(new Store
                        {
                            StoreId = store.StoreId,
                            Name = store.Name,
                            TaxId = store.TaxId ?? string.Empty,
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
                var localAgreements = await db.CommercialAgreements.ToListAsync(ct);
                foreach (var ag in bootstrap.Agreements)
                {
                    var existing = localAgreements.FirstOrDefault(a => a.AgreementId == ag.AgreementId);
                    if (existing != null)
                    {
                        existing.Name = ag.Name;
                        existing.StoreId = ag.StoreId;
                        existing.CompanyId = ag.CompanyId;
                        existing.DiscountType = ag.DiscountType;
                        existing.DiscountPercentage = ag.DiscountPercentage;
                        existing.DiscountFixedAmount = ag.DiscountFixedAmount;
                        existing.FreeMinutes = ag.FreeMinutes;
                        existing.FreeHours = ag.FreeHours;
                        existing.MaxHoursApplicable = ag.MaxHoursApplicable;
                        existing.MaxMinutesApplicable = ag.MaxMinutesApplicable;
                        existing.MinPurchaseAmount = ag.MinPurchaseAmount;
                        existing.IsActive = ag.IsActive;
                        existing.ImageUrl = ag.ImageUrl;
                    }
                    else
                    {
                        db.CommercialAgreements.Add(new CommercialAgreement
                        {
                            AgreementId = ag.AgreementId,
                            StoreId = ag.StoreId,
                            CompanyId = ag.CompanyId,
                            Name = ag.Name,
                            DiscountType = ag.DiscountType,
                            MinPurchaseAmount = ag.MinPurchaseAmount,
                            DiscountPercentage = ag.DiscountPercentage,
                            DiscountFixedAmount = ag.DiscountFixedAmount,
                            FreeMinutes = ag.FreeMinutes,
                            FreeHours = ag.FreeHours,
                            MaxHoursApplicable = ag.MaxHoursApplicable,
                            MaxMinutesApplicable = ag.MaxMinutesApplicable,
                            IsActive = ag.IsActive,
                            ImageUrl = ag.ImageUrl,
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
                var incomingSubIds = bootstrap.MonthlySubscriptions.Select(s => s.SubscriptionId).ToHashSet();
                var localSubs = await db.MonthlySubscriptions.ToListAsync(ct);
                var subsToDelete = localSubs.Where(s => !incomingSubIds.Contains(s.SubscriptionId)).ToList();
                if (subsToDelete.Count > 0)
                {
                    db.MonthlySubscriptions.RemoveRange(subsToDelete);
                }

                foreach (var sub in bootstrap.MonthlySubscriptions)
                {
                    var existing = localSubs.FirstOrDefault(s => s.SubscriptionId == sub.SubscriptionId);
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

                if (_shiftService != null)
                {
                    try
                    {
                        await _shiftService.RefreshCurrentShiftAsync();
                    }
                    catch { }
                }
            }
            result.SyncedShiftsCount = shiftsCount;
            await Task.Delay(50, ct);

            // 8.5. Sincronizar Novedades y Bloqueos de Placas
            int incidentsCount = 0;
            if (bootstrap.Incidents != null)
            {
                foreach (var inc in bootstrap.Incidents)
                {
                    var existingInc = await db.VehicleIncidents
                        .Include(i => i.IncidentBranches)
                        .FirstOrDefaultAsync(i => i.IncidentId == inc.IncidentId, ct);

                    if (existingInc != null)
                    {
                        existingInc.BranchId = inc.BranchId;
                        existingInc.PlateNumber = inc.PlateNumber.Trim().ToUpperInvariant();
                        existingInc.IncidentType = inc.IncidentType;
                        existingInc.Description = inc.Description;
                        existingInc.IsBlocked = inc.IsBlocked;
                        existingInc.IsGlobal = inc.IsGlobal;
                        existingInc.Status = inc.Status;
                        existingInc.ReportedBy = inc.ReportedBy;
                        existingInc.ResolvedBy = inc.ResolvedBy;
                        existingInc.ResolvedNotes = inc.ResolvedNotes;
                        existingInc.ResolvedAtUtc = inc.ResolvedAtUtc;

                        existingInc.IncidentBranches.Clear();
                        if (inc.BranchIds != null && inc.BranchIds.Count > 0)
                        {
                            foreach (var bId in inc.BranchIds)
                            {
                                existingInc.IncidentBranches.Add(new VehicleIncidentBranch
                                {
                                    IncidentId = existingInc.IncidentId,
                                    BranchId = bId
                                });
                            }
                        }
                        else if (inc.BranchId.HasValue)
                        {
                            existingInc.IncidentBranches.Add(new VehicleIncidentBranch
                            {
                                IncidentId = existingInc.IncidentId,
                                BranchId = inc.BranchId.Value
                            });
                        }
                    }
                    else
                    {
                        var newInc = new VehicleIncident
                        {
                            IncidentId = inc.IncidentId,
                            BranchId = inc.BranchId,
                            PlateNumber = inc.PlateNumber.Trim().ToUpperInvariant(),
                            IncidentType = inc.IncidentType,
                            Description = inc.Description,
                            IsBlocked = inc.IsBlocked,
                            IsGlobal = inc.IsGlobal,
                            Status = inc.Status,
                            ReportedBy = inc.ReportedBy,
                            ResolvedBy = inc.ResolvedBy,
                            ResolvedNotes = inc.ResolvedNotes,
                            CreatedAtUtc = inc.CreatedAtUtc,
                            ResolvedAtUtc = inc.ResolvedAtUtc
                        };

                        if (inc.BranchIds != null && inc.BranchIds.Count > 0)
                        {
                            foreach (var bId in inc.BranchIds)
                            {
                                newInc.IncidentBranches.Add(new VehicleIncidentBranch
                                {
                                    IncidentId = newInc.IncidentId,
                                    BranchId = bId
                                });
                            }
                        }
                        else if (inc.BranchId.HasValue)
                        {
                            newInc.IncidentBranches.Add(new VehicleIncidentBranch
                            {
                                IncidentId = newInc.IncidentId,
                                BranchId = inc.BranchId.Value
                            });
                        }

                        db.VehicleIncidents.Add(newInc);
                    }
                    incidentsCount++;
                }
                await db.SaveChangesAsync(ct);
            }

            // 8.5 Sincronizar Resoluciones de Facturación DIAN
            if (bootstrap.Resolutions != null)
            {
                var incomingResIds = bootstrap.Resolutions.Select(r => r.ResolutionId).ToHashSet();
                var localResolutions = await db.BillingResolutions.ToListAsync(ct);
                var resToDelete = localResolutions.Where(r => !incomingResIds.Contains(r.ResolutionId)).ToList();
                if (resToDelete.Count > 0)
                {
                    db.BillingResolutions.RemoveRange(resToDelete);
                }

                foreach (var res in bootstrap.Resolutions)
                {
                    var existing = localResolutions.FirstOrDefault(r => r.ResolutionId == res.ResolutionId);
                    if (existing != null)
                    {
                        existing.CompanyId = res.CompanyId;
                        existing.BranchId = res.BranchId;
                        existing.Name = res.Name;
                        existing.DocumentType = res.DocumentType;
                        existing.Prefix = res.Prefix;
                        existing.ResolutionNumber = res.ResolutionNumber;
                        existing.FromNumber = res.FromNumber;
                        existing.ToNumber = res.ToNumber;
                        existing.CurrentNumber = res.CurrentNumber;
                        existing.ValidFrom = res.ValidFrom;
                        existing.ValidTo = res.ValidTo;
                        existing.TechnicalKey = res.TechnicalKey;
                        existing.IsActive = res.IsActive;
                    }
                    else
                    {
                        db.BillingResolutions.Add(new BillingResolution
                        {
                            ResolutionId = res.ResolutionId,
                            CompanyId = res.CompanyId,
                            BranchId = res.BranchId,
                            Name = res.Name,
                            DocumentType = res.DocumentType,
                            Prefix = res.Prefix,
                            ResolutionNumber = res.ResolutionNumber,
                            FromNumber = res.FromNumber,
                            ToNumber = res.ToNumber,
                            CurrentNumber = res.CurrentNumber,
                            ValidFrom = res.ValidFrom,
                            ValidTo = res.ValidTo,
                            TechnicalKey = res.TechnicalKey,
                            IsActive = res.IsActive,
                            CreatedAtUtc = DateTime.UtcNow
                        });
                    }
                }
                await db.SaveChangesAsync(ct);
            }

            // 9. Paso 8: Sincronizar Tiquetes y Consolidar (98%)
            progress.Report(new SyncProgressReport
            {
                Percentage = 98,
                StepIndex = 8,
                CurrentStepTitle = "Sincronizando Tiquetes activos y registros...",
                DetailMessage = "Consolidando registros de acceso y guardando en SQLite..."
            });

            // Reconciliación: Si un tiquete figura como Activo en SQLite pero ya NO está activo en el servidor central (salida dada desde PWA u otra terminal)
            if (bootstrap.ActiveTickets != null)
            {
                var serverActiveTicketIds = bootstrap.ActiveTickets.Select(t => t.TicketId).ToHashSet();
                var serverActivePlates = bootstrap.ActiveTickets.Select(t => t.PlateNumber.Trim().ToUpperInvariant()).ToHashSet();

                var localActiveTickets = await db.ParkingTickets.Where(t => t.Status == TicketStatus.Active).ToListAsync(ct);
                foreach (var localActive in localActiveTickets)
                {
                    var normalizedLocalPlate = localActive.PlateNumber.Trim().ToUpperInvariant();
                    if (!serverActiveTicketIds.Contains(localActive.TicketId) && !serverActivePlates.Contains(normalizedLocalPlate))
                    {
                        localActive.Status = TicketStatus.Completed;
                        localActive.ExitTimeUtc ??= DateTime.UtcNow;
                        localActive.IsSynchronized = true;
                    }
                }
            }

            var allIncomingTickets = new List<ApiParkingTicketSyncDto>();
            if (bootstrap.ActiveTickets != null) allIncomingTickets.AddRange(bootstrap.ActiveTickets);
            if (bootstrap.RecentTickets != null) allIncomingTickets.AddRange(bootstrap.RecentTickets);

            // Deduplicar lista entrante primero por TicketId y luego por TicketNumber
            var deduplicatedIncoming = allIncomingTickets
                .Where(t => !string.IsNullOrWhiteSpace(t.TicketNumber))
                .GroupBy(t => t.TicketId)
                .Select(g => g.First())
                .GroupBy(t => t.TicketNumber.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            var localTickets = await db.ParkingTickets.ToListAsync(ct);
            var localByTicketId = localTickets.ToDictionary(t => t.TicketId);
            var localByTicketNumber = new Dictionary<string, ParkingTicket>(StringComparer.OrdinalIgnoreCase);
            foreach (var lt in localTickets)
            {
                if (!string.IsNullOrWhiteSpace(lt.TicketNumber))
                {
                    localByTicketNumber[lt.TicketNumber.Trim()] = lt;
                }
            }

            int ticketsCount = 0;
            foreach (var ticket in deduplicatedIncoming)
            {
                var vehicleType = ticket.GetVehicleType();
                var status = ticket.GetTicketStatus();
                var paymentMethod = ticket.GetPaymentMethod();
                var targetBranchId = ticket.BranchId ?? currentBranchId;
                var normalizedTicketNumber = ticket.TicketNumber.Trim();

                ParkingTicket? existing = null;
                if (localByTicketId.TryGetValue(ticket.TicketId, out var matchById))
                {
                    existing = matchById;
                }
                else if (localByTicketNumber.TryGetValue(normalizedTicketNumber, out var matchByNum))
                {
                    existing = matchByNum;
                }

                if (existing != null)
                {
                    existing.BranchId = targetBranchId;
                    existing.TicketNumber = normalizedTicketNumber;
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

                    localByTicketId[existing.TicketId] = existing;
                    localByTicketNumber[normalizedTicketNumber] = existing;
                }
                else
                {
                    var newTicket = new ParkingTicket
                    {
                        TicketId = ticket.TicketId,
                        BranchId = targetBranchId,
                        TicketNumber = normalizedTicketNumber,
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
                    };

                    db.ParkingTickets.Add(newTicket);
                    localByTicketId[ticket.TicketId] = newTicket;
                    localByTicketNumber[normalizedTicketNumber] = newTicket;
                }
                ticketsCount++;
            }
            result.SyncedTicketsCount = ticketsCount;

            await db.SaveChangesAsync(ct);
            _lastSyncTime = DateTime.Now;
            _isOnline = true;
            result.Success = true;

            var activeBranch = bootstrap.Branches?.FirstOrDefault(b => b.Id == currentBranchId)
                            ?? bootstrap.Branches?.FirstOrDefault();
            int effectiveCapacity = activeBranch?.TotalCapacity ?? (bootstrap.TotalCapacity > 0 ? bootstrap.TotalCapacity : ServerConfiguredCapacity);
            string branchName = activeBranch?.Name ?? _sessionService.CurrentBranch?.Name ?? "Sede Activa";

            result.TotalCapacity = effectiveCapacity;
            result.BranchName = branchName;
            result.Message = $"Sincronización total exitosa en '{branchName}' ({effectiveCapacity} cupos configurados): {usersCount} usuarios, {paymentMethodsCount} medios de pago, {ratesCount} tarifas, {agCount} convenios, {subsCount} mensualidades, {shiftsCount} turnos y {ticketsCount} tiquetes actualizados.";
            NotifyTotalCapacityChanged(effectiveCapacity);

            // Notificar a todos los módulos y viewmodels para actualización reactiva en memoria
            NotifyDataSynchronized();

            // Paso 9: Finalizado (100%)
            progress.Report(new SyncProgressReport
            {
                Percentage = 100,
                StepIndex = 8,
                CurrentStepTitle = "¡Sincronización Total Completada!",
                DetailMessage = result.Message,
                IsSuccessStep = true
            });

            NotifySyncStatusChanged(SyncStatusDescription);
            return result;
        }
        catch (Exception ex)
        {
            var detailedError = ex.InnerException != null ? $"{ex.Message} -> {ex.InnerException.Message}" : ex.Message;
            progress.Report(new SyncProgressReport
            {
                Percentage = 100,
                StepIndex = 99,
                CurrentStepTitle = "Error en la sincronización",
                DetailMessage = $"Novedad: {detailedError}",
                IsSuccessStep = false
            });
            result.Success = false;
            result.Message = $"Error durante la sincronización: {detailedError}";
            NotifySyncStatusChanged(SyncStatusDescription);
            return result;
        }
    }

    public async Task<bool> ForceCleanResyncAsync()
    {
        var report = await ResetLocalDatabaseFromCloudAsync();
        return report.Success;
    }

    public async Task<SyncResultReport> ResetLocalDatabaseFromCloudAsync(IProgress<SyncProgressReport>? progress = null)
    {
        progress ??= new Progress<SyncProgressReport>();

        progress.Report(new SyncProgressReport
        {
            Percentage = 10,
            StepIndex = 1,
            CurrentStepTitle = "Verificando conectividad con la Nube...",
            DetailMessage = "Comprobando que el API Central esté en línea antes de restablecer..."
        });

        var isApiAvailable = await _apiClient.PingAsync();
        _isOnline = isApiAvailable;

        if (!isApiAvailable)
        {
            progress.Report(new SyncProgressReport
            {
                Percentage = 100,
                StepIndex = 1,
                CurrentStepTitle = "Servidor no disponible",
                DetailMessage = "No se puede restablecer la base de datos local porque el servidor central está desconectado. Se conservaron los datos locales intactos.",
                IsSuccessStep = false
            });
            return new SyncResultReport
            {
                Success = false,
                Message = "El servidor central no está en línea. No se modificaron los datos locales por seguridad."
            };
        }

        progress.Report(new SyncProgressReport
        {
            Percentage = 25,
            StepIndex = 2,
            CurrentStepTitle = "Despachando transacciones pendientes...",
            DetailMessage = "Asegurando que todas las transacciones locales queden guardadas en MySQL..."
        });

        // 1. Despachar cola de transacciones locales pendientes antes de purgar
        await ProcessPendingQueueAsync();

        progress.Report(new SyncProgressReport
        {
            Percentage = 45,
            StepIndex = 2,
            CurrentStepTitle = "Purgando caché y reconstruyendo esquema...",
            DetailMessage = "Limpiando tablas locales de SQLite y asegurando esquema actualizado..."
        });

        // 2. Limpiar tablas locales de caché transaccional y catálogos
        using (var db = _dbManager.CreateDbContext())
        {
            db.PendingSyncItems.RemoveRange(db.PendingSyncItems.Where(p => p.IsProcessed));
            db.TicketDiscounts.RemoveRange(db.TicketDiscounts);
            db.ParkingTickets.RemoveRange(db.ParkingTickets);
            db.MonthlySubscriptions.RemoveRange(db.MonthlySubscriptions);
            db.VehicleIncidents.RemoveRange(db.VehicleIncidents);
            db.VehicleIncidentBranches.RemoveRange(db.VehicleIncidentBranches);
            db.BranchOperatingHours.RemoveRange(db.BranchOperatingHours);
            db.BranchPaymentMethods.RemoveRange(db.BranchPaymentMethods);
            db.VehicleRates.RemoveRange(db.VehicleRates);
            db.CommercialAgreements.RemoveRange(db.CommercialAgreements);
            db.BillingResolutions.RemoveRange(db.BillingResolutions);
            await db.SaveChangesAsync();

            // 3. Ejecutar auto-migración dinámica para garantizar que cualquier columna o tabla nueva exista
            if (_dbManager is DbConnectionManager concreteManager)
            {
                await concreteManager.AutoMigrateDatabaseAsync(db);
            }
        }

        progress.Report(new SyncProgressReport
        {
            Percentage = 60,
            StepIndex = 3,
            CurrentStepTitle = "Descargando catálogo completo desde la Nube...",
            DetailMessage = "Descargando sedes, tarifas, usuarios, convenios y vehículos activos..."
        });

        // 4. Descargar el 100% del Bootstrap desde el API
        var syncResult = await PerformFullSyncWithProgressAsync(progress);
        return syncResult;
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
                BranchId = ticket.BranchId,
                CompanyId = ticket.CompanyId,
                TicketNumber = ticket.TicketNumber,
                PlateNumber = ticket.PlateNumber,
                VehicleType = ticket.VehicleType,
                PhoneNumber = ticket.CustomerPhone,
                Notes = ticket.Notes,
                HourlyRate = ticket.HourlyRate,
                OperatorName = ticket.OperatorName,
                EntryTimeUtc = ticket.EntryTimeUtc
            }),
            CreatedAtUtc = DateTime.UtcNow,
            IsProcessed = false
        };

        db.PendingSyncItems.Add(pending);
        await db.SaveChangesAsync();

        await RefreshPendingCountAsync();
        NotifySyncStatusChanged(SyncStatusDescription);
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
            NotifySyncStatusChanged(SyncStatusDescription);
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
                BranchId = ticket.BranchId,
                CompanyId = ticket.CompanyId,
                PaymentMethod = ticket.PaymentMethod ?? PaymentMethod.Cash,
                PaymentMethodId = ticket.PaymentMethodId,
                AmountPaid = ticket.AmountPaid,
                ChangeGiven = ticket.ChangeGiven,
                GrossAmount = ticket.GrossAmount,
                NetAmount = ticket.NetAmount,
                DiscountAmount = ticket.DiscountAmount,
                ExitNotes = ticket.ExitNotes,
                ResolutionId = ticket.ResolutionId,
                ResolutionName = ticket.ResolutionName,
                FiscalInvoiceNumber = ticket.InvoiceNumber,
                IsLostTicket = ticket.IsLostTicket,
                LostTicketFee = ticket.LostTicketFee,
                ExitTimeUtc = ticket.ExitTimeUtc ?? DateTime.UtcNow
            }),
            CreatedAtUtc = DateTime.UtcNow,
            IsProcessed = false
        };

        db.PendingSyncItems.Add(pending);
        await db.SaveChangesAsync();

        await RefreshPendingCountAsync();
        NotifySyncStatusChanged(SyncStatusDescription);
    }

    public async Task ProcessPendingQueueAsync()
    {
        if (!_isOnline) return;

        if (!await _pendingQueueLock.WaitAsync(0))
        {
            return; // Ya hay un procesamiento de cola en curso
        }

        try
        {
            using var db = _dbManager.CreateDbContext();
            var items = await db.PendingSyncItems
                .Where(p => !p.IsProcessed)
                .OrderBy(p => p.CreatedAtUtc)
                .ToListAsync();

            if (items.Count == 0) return;

            foreach (var item in items)
            {
                try
                {
                    if (item.OperationType == "CheckIn")
                    {
                        var req = JsonSerializer.Deserialize<CheckInApiRequest>(item.PayloadJson, ParkingApiClient.JsonOptions);
                        if (req != null)
                        {
                            var result = await _apiClient.CheckInAsync(req);
                            if (result != null) item.IsProcessed = true;
                        }
                    }
                    else if (item.OperationType == "CheckOut")
                    {
                        var req = JsonSerializer.Deserialize<CheckOutApiRequest>(item.PayloadJson, ParkingApiClient.JsonOptions);
                        if (req != null)
                        {
                            var result = await _apiClient.CheckOutAsync(req);
                            if (result != null)
                            {
                                item.IsProcessed = true;

                                // Bajar la data de la nube a tierra: conciliar SQLite local con la verdad canónica del servidor
                                var localTicket = await db.ParkingTickets.FirstOrDefaultAsync(t => t.TicketId == result.TicketId);
                                if (localTicket != null)
                                {
                                    localTicket.Status = TicketStatus.Completed;
                                    if (result.ExitTimeUtc.HasValue) localTicket.ExitTimeUtc = result.ExitTimeUtc.Value;
                                    localTicket.GrossAmount = result.GrossAmount;
                                    localTicket.NetAmount = result.NetAmount;
                                    localTicket.PaymentMethod = result.PaymentMethod;
                                    localTicket.IsSynchronized = true;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    item.RetryCount++;
                    item.LastError = ex.Message;
                }
            }

            var processed = items.Where(p => p.IsProcessed).ToList();
            if (processed.Count > 0)
            {
                db.PendingSyncItems.RemoveRange(processed);
                await db.SaveChangesAsync();
            }

            await RefreshPendingCountAsync();
            NotifySyncStatusChanged(SyncStatusDescription);
        }
        catch
        {
            // Protección contra tablas desincronizadas en SQLite
        }
        finally
        {
            _pendingQueueLock.Release();
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

    public void SetOnlineStatus(bool isOnline)
    {
        bool changed = _isOnline != isOnline;
        _isOnline = isOnline;

        if (isOnline)
        {
            StopOfflineReconnectionProbe();

            if (changed)
            {
                _lastSyncTime = DateTime.Now;
                _ = ProcessPendingQueueAsync();
                DataSynchronized?.Invoke();
            }
        }
        else
        {
            StartOfflineReconnectionProbe();
        }

        NotifySyncStatusChanged(SyncStatusDescription);
    }

    private void StartOfflineReconnectionProbe()
    {
        lock (_offlineProbeLock)
        {
            if (_offlineProbeCts != null && !_offlineProbeCts.IsCancellationRequested)
            {
                return; // Tarea de sondeo ya activa
            }

            _offlineProbeCts?.Cancel();
            _offlineProbeCts?.Dispose();
            _offlineProbeCts = new CancellationTokenSource();
            var ct = _offlineProbeCts.Token;
            _offlineAttemptCount = 0;

            _ = Task.Run(async () =>
            {
                while (!ct.IsCancellationRequested && !_isOnline)
                {
                    try
                    {
                        // Backoff progresivo inteligente: 5s, 15s, 30s, luego techo en 60s
                        int delaySeconds = _offlineAttemptCount switch
                        {
                            0 => 5,
                            1 => 15,
                            2 => 30,
                            _ => 60
                        };

                        await Task.Delay(TimeSpan.FromSeconds(delaySeconds), ct);
                        if (ct.IsCancellationRequested || _isOnline) break;

                        _offlineAttemptCount++;

                        // Si Windows indica que no hay enlace de red disponible, no gastar peticiones HTTP
                        if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
                        {
                            continue;
                        }

                        var isAlive = await _apiClient.PingAsync();
                        if (isAlive)
                        {
                            SetOnlineStatus(true);
                            break;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch
                    {
                        // Falló intento de conexión, continuará en el siguiente ciclo
                    }
                }
            }, ct);
        }
    }

    private void StopOfflineReconnectionProbe()
    {
        lock (_offlineProbeLock)
        {
            _offlineAttemptCount = 0;
            if (_offlineProbeCts != null)
            {
                try
                {
                    _offlineProbeCts.Cancel();
                    _offlineProbeCts.Dispose();
                }
                catch { }
                _offlineProbeCts = null;
            }
        }
    }
}
