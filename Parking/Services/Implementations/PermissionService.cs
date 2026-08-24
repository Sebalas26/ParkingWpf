using System;
using System.Collections.Generic;
using System.Windows;
using Parking.Models;
using Parking.Services.Contracts;

namespace Parking.Services.Implementations;

public class PermissionService : IPermissionService
{
    private static IPermissionService? _instance;
    public static IPermissionService Current => _instance ??= new PermissionService();

    private readonly HashSet<string> _permissions = new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler? PermissionsChanged;

    public IReadOnlySet<string> CurrentPermissions => _permissions;
    public bool IsAdmin { get; private set; }

    public PermissionService()
    {
        _instance = this;
    }

    public void LoadPermissions(UserSessionModel? userSession)
    {
        _permissions.Clear();

        if (userSession == null)
        {
            IsAdmin = false;
            NotifyPermissionsChanged();
            return;
        }

        var roleName = userSession.RoleName?.Trim().ToLowerInvariant() ?? string.Empty;
        var username = userSession.Username?.Trim().ToLowerInvariant() ?? string.Empty;
        IsAdmin = roleName == "administrador" || roleName == "admin" || username == "admin";

        if (IsAdmin)
        {
            // El administrador tiene super-acceso total
            NotifyPermissionsChanged();
            return;
        }

        // Permisos predeterminados para Operador de Turno / Cajero
        if (roleName == "operador" || roleName == "cajero" || roleName.Length > 0)
        {
            _permissions.UnionWith(new[]
            {
                "checkin.view",
                "checkin.create",
                "checkin.reprint",
                "checkout.view",
                "checkout.search",
                "checkout.apply_discount",
                "checkout.process_payment",
                "checkout.reprint_receipt",
                "subscriptions.view",
                "subscriptions.create",
                "subscriptions.renew",
                "recent_entries.view",
                "shift.view",
                "shift.open",
                "shift.cash_withdrawal",
                "shift.close",
                "shift.history",
                "system.sync",
                "system.theme"
            });
        }

        NotifyPermissionsChanged();
    }

    public bool HasPermission(string? permissionSlug)
    {
        if (string.IsNullOrWhiteSpace(permissionSlug)) return true;
        if (IsAdmin) return true;

        return _permissions.Contains(permissionSlug.Trim());
    }

    public bool HasAnyPermission(params string[] permissionSlugs)
    {
        if (IsAdmin) return true;
        if (permissionSlugs == null || permissionSlugs.Length == 0) return true;

        foreach (var slug in permissionSlugs)
        {
            if (HasPermission(slug)) return true;
        }

        return false;
    }

    public bool HasAllPermissions(params string[] permissionSlugs)
    {
        if (IsAdmin) return true;
        if (permissionSlugs == null || permissionSlugs.Length == 0) return true;

        foreach (var slug in permissionSlugs)
        {
            if (!HasPermission(slug)) return false;
        }

        return true;
    }

    private void NotifyPermissionsChanged()
    {
        if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
        {
            Application.Current.Dispatcher.Invoke(() => PermissionsChanged?.Invoke(this, EventArgs.Empty));
        }
        else
        {
            PermissionsChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
