using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Parking.Data.Factories;
using Parking.Entities;
using Parking.Models;
using Parking.Services.Contracts;

namespace Parking.Services.Implementations;

public class AuthService : IAuthService
{
    private readonly IDbConnectionManager _connectionManager;
    private readonly IApiClientService _apiClient;
    private readonly ISessionService _sessionService;
    private readonly IPermissionService _permissionService;

    public event Action<UserSessionModel?>? UserSessionChanged;
    public UserSessionModel? CurrentUser { get; private set; }
    public bool IsAuthenticated => CurrentUser != null;

    public AuthService(
        IDbConnectionManager connectionManager,
        IApiClientService apiClient,
        ISessionService sessionService,
        IPermissionService permissionService)
    {
        _connectionManager = connectionManager;
        _apiClient = apiClient;
        _sessionService = sessionService;
        _permissionService = permissionService;
    }

    public async Task<LoginResultModel> AuthenticateAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return new LoginResultModel { Success = false, ErrorMessage = "Por favor ingrese su usuario y contraseña." };
        }

        var normalizedUser = username.Trim().ToLowerInvariant();

        // 1. Intentar autenticar contra el API Central (Online)
        try 
        {
            var apiLogin = await _apiClient.LoginAsync(username.Trim(), password);
            if (apiLogin != null && apiLogin.Success)
            {
                var roleName = string.IsNullOrWhiteSpace(apiLogin.RoleName) ? "Usuario" : apiLogin.RoleName;
                var isSuperAdmin = apiLogin.IsSuperAdmin;
                var isAdmin = apiLogin.IsAdmin || isSuperAdmin;

                var userModel = new UserSessionModel
                {
                    ServerUserId = apiLogin.UserId,
                    ServerRoleId = apiLogin.RoleId > 0 ? apiLogin.RoleId : (isAdmin ? 1 : 2),
                    UserId = Guid.NewGuid(),
                    Username = apiLogin.Username,
                    FullName = apiLogin.FullName,
                    RoleName = roleName,
                    IsAdmin = isAdmin,
                    IsSuperAdmin = isSuperAdmin,
                    CompanyId = apiLogin.CompanyId,
                    CompanyName = apiLogin.CompanyName,
                    AllowMultipleSessions = apiLogin.AllowMultipleSessions,
                    MaxActiveSessionsPerUser = apiLogin.MaxActiveSessionsPerUser > 1 ? apiLogin.MaxActiveSessionsPerUser : 1,
                    AllowMultipleOpenShifts = apiLogin.AllowMultipleOpenShifts,
                    MaxOpenShiftsPerUser = apiLogin.MaxOpenShiftsPerUser > 1 ? apiLogin.MaxOpenShiftsPerUser : 1,
                    RequireOpenShiftToOperate = apiLogin.RequireOpenShiftToOperate,
                    RequireInitialCashAmount = apiLogin.RequireInitialCashAmount,
                    SessionToken = apiLogin.Token ?? Guid.NewGuid().ToString(),
                    LoginTime = DateTime.Now
                };
                
                CurrentUser = userModel;
                var permissions = apiLogin.Permissions ?? new List<string>();
                _permissionService.LoadPermissions(permissions, isAdmin);

                var branches = apiLogin.Branches ?? new List<BranchModel>();
                foreach (var b in branches)
                {
                    if (!b.CompanyId.HasValue && apiLogin.CompanyId.HasValue)
                    {
                        b.CompanyId = apiLogin.CompanyId;
                    }
                }

                return new LoginResultModel
                {
                    Success = true,
                    User = userModel,
                    Branches = branches
                };
            }
            else if (apiLogin != null && !string.IsNullOrWhiteSpace(apiLogin.ErrorMessage))
            {
                return new LoginResultModel { Success = false, ErrorMessage = apiLogin.ErrorMessage };
            }
        }
        catch
        {
            // Servidor offline, continuar con validación local en SQLite
        }

        // 2. Autenticación contra Caché Local SQLite (Offline)
        using var db = _connectionManager.CreateDbContext();
        var passwordHash = DbConnectionManager.HashPassword(password);

        var user = await db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => (u.Username.ToLower() == normalizedUser || (u.Email != null && u.Email.ToLower() == normalizedUser)) && u.IsActive);

        var isValidLocal = user != null && (
            user.PasswordHash == passwordHash ||
            user.PasswordHash == password
        );

        if (user == null || !isValidLocal)
        {
            return new LoginResultModel { Success = false, ErrorMessage = "Usuario o contraseña incorrectos. Por favor verifique sus datos." };
        }

        var sessionToken = Guid.NewGuid().ToString();
        var newSession = new UserSession
        {
            SessionId = Guid.NewGuid(),
            UserId = user.UserId,
            SessionToken = sessionToken,
            DeviceIdentifier = Environment.MachineName,
            IpAddress = "127.0.0.1",
            StartedAtUtc = DateTime.UtcNow,
            LastHeartbeatUtc = DateTime.UtcNow,
            IsActive = true
        };

        db.UserSessions.Add(newSession);
        await db.SaveChangesAsync();

        var localRoleName = user.Role?.Name ?? "Operador";
        var isLocalAdmin = localRoleName.Equals("Administrador", StringComparison.OrdinalIgnoreCase) ||
                           localRoleName.Equals("Admin", StringComparison.OrdinalIgnoreCase);

        var localUserModel = new UserSessionModel
        {
            UserId = user.UserId,
            Username = user.Username,
            FullName = user.FullName,
            RoleName = localRoleName,
            RoleId = user.RoleId,
            IsAdmin = isLocalAdmin,
            IsSuperAdmin = false,
            SessionToken = sessionToken,
            LoginTime = DateTime.Now
        };

        CurrentUser = localUserModel;

        // Cargar sedes locales de SQLite
        var localBranches = await db.Branches.Where(b => b.IsActive).ToListAsync();
        var branchesList = localBranches.Select(b => new BranchModel
        {
            Id = b.Id,
            CompanyId = b.CompanyId,
            Code = b.Code,
            Name = b.Name,
            Address = b.Address,
            Phone = b.Phone,
            City = b.City,
            TotalCapacity = b.TotalCapacity,
            Notes = b.Notes,
            IsActive = b.IsActive
        }).ToList();

        localUserModel.CompanyId = branchesList.FirstOrDefault(b => b.CompanyId.HasValue)?.CompanyId;

        var localPermissions = isLocalAdmin
            ? new List<string>()
            : await db.RolePermissions
                .Include(rp => rp.Permission)
                .Where(rp => rp.RoleId == user.RoleId && rp.IsGranted && rp.Permission.ActionKey != null)
                .Select(rp => rp.Permission.ActionKey)
                .ToListAsync();

        // Resiliencia: Si SQLite no tiene registros de permisos aún (base de datos local recién inicializada),
        // se activan los permisos operativos de terminal para no bloquear al operador.
        if (!isLocalAdmin && (localPermissions == null || localPermissions.Count == 0))
        {
            localPermissions = new List<string>
            {
                "checkin.view", "checkin.create_ticket", "checkin.create", "checkin.reprint",
                "checkout.view", "checkout.search", "checkout.apply_discount", "checkout.process_payment", "checkout.reprint_receipt",
                "subscriptions.view", "subscriptions.view_list", "subscriptions.create_subscription", "subscriptions.create", "subscriptions.renew_subscription", "subscriptions.renew",
                "monitoring.view_occupancy", "monitoring.search_vehicles", "monitoring.force_exit", "monitoring.export", "recent_entries.view", "recent_entries.reprint",
                "shifts.view_current", "shifts.open", "shifts.blind_count", "shifts.close", "shifts.view_history", "shifts.reprint_closure",
                "shift.view", "shift.open", "shift.cash_withdrawal", "shift.close", "shift.handover", "shift.history", "shift.export",
                "analytics.view_dashboard", "analytics.income_reports", "analytics.occupancy_reports", "analytics.audit_reports", "analytics.export", "analytics.view"
            };
        }

        _permissionService.LoadPermissions(localPermissions, isLocalAdmin);

        return new LoginResultModel
        {
            Success = true,
            User = localUserModel,
            Branches = branchesList
        };
    }

    public async Task<bool> LoginAsync(string username, string password)
    {
        var result = await AuthenticateAsync(username, password);
        if (result.Success && result.User != null)
        {
            _sessionService.SetSession(result.User, result.Branches);
            UserSessionChanged?.Invoke(CurrentUser);
            return true;
        }
        return false;
    }

    public async Task<UserSessionModel?> ValidateCredentialsAsync(string username, string password)
    {
        var result = await AuthenticateAsync(username, password);
        return result.Success ? result.User : null;
    }

    public async Task<UserSessionModel?> ValidateAdminAuthorizationAsync(string adminPasswordOrPin)
    {
        if (string.IsNullOrWhiteSpace(adminPasswordOrPin)) return null;

        var cleanPass = adminPasswordOrPin.Trim();
        var passwordHash = DbConnectionManager.HashPassword(cleanPass);

        using var db = _connectionManager.CreateDbContext();
        var adminUsers = await db.Users
            .Include(u => u.Role)
            .Where(u => u.IsActive && (u.Role.Name == "Administrador" || u.Role.Name == "Admin" || u.Role.RoleId == Guid.Parse("11111111-1111-1111-1111-111111111111")))
            .ToListAsync();

        foreach (var admin in adminUsers)
        {
            if (admin.PasswordHash == passwordHash || admin.PasswordHash == cleanPass)
            {
                return new UserSessionModel
                {
                    UserId = admin.UserId,
                    Username = admin.Username,
                    FullName = admin.FullName,
                    RoleName = admin.Role?.Name ?? "Administrador",
                    RoleId = admin.RoleId,
                    IsAdmin = true,
                    IsSuperAdmin = false,
                    SessionToken = Guid.NewGuid().ToString(),
                    LoginTime = DateTime.Now
                };
            }
        }

        return null;
    }

    public void SwitchCurrentUser(UserSessionModel newUser)
    {
        CurrentUser = newUser;
        var isAdmin = newUser.IsAdmin;

        List<string> permissions = new();
        if (!isAdmin && newUser.RoleId != Guid.Empty)
        {
            using var db = _connectionManager.CreateDbContext();
            permissions = db.RolePermissions
                .Include(rp => rp.Permission)
                .Where(rp => rp.RoleId == newUser.RoleId && rp.IsGranted && rp.Permission.ActionKey != null)
                .Select(rp => rp.Permission.ActionKey)
                .ToList();
        }

        _permissionService.LoadPermissions(permissions, isAdmin);
        _sessionService.SetSession(newUser, _sessionService.UserBranches);
        UserSessionChanged?.Invoke(CurrentUser);
    }

    public async Task LogoutAsync()
    {
        if (CurrentUser != null)
        {
            try
            {
                await _apiClient.LogoutAsync();
            }
            catch { }

            using var db = _connectionManager.CreateDbContext();
            var session = await db.UserSessions
                .FirstOrDefaultAsync(s => s.SessionToken == CurrentUser.SessionToken);

            if (session != null)
            {
                session.IsActive = false;
                await db.SaveChangesAsync();
            }

            CurrentUser = null;
            _sessionService.Clear();
            _permissionService.Clear();
            UserSessionChanged?.Invoke(null);
        }
    }
}
