using System;
using System.Collections.Generic;
using System.Linq;
using Parking.Models;
using Parking.Services.Contracts;

namespace Parking.Services.Implementations;

public class PermissionService : IPermissionService
{
    private readonly HashSet<string> _permissions = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string[]> _permissionAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        { "shifts.view_current", new[] { "wpf.shifts.view_current", "shift.view", "shift.open", "shifts.open", "shifts.view", "shifts.current" } },
        { "shifts.open", new[] { "wpf.shifts.open", "shift.open", "shifts.view_current" } },
        { "shifts.close", new[] { "wpf.shifts.close", "shift.close", "shift.closure" } },
        { "shifts.blind_count", new[] { "wpf.shifts.blind_count", "shift.blind_count", "shifts.blind_count", "shift.cash_withdrawal" } },
        { "shifts.view_history", new[] { "wpf.shifts.view_history", "shift.history", "shifts.history" } },
        { "shifts.reprint_closure", new[] { "wpf.shifts.reprint_closure", "shift.reprint", "shifts.reprint", "shift.export" } },

        { "monitoring.view_occupancy", new[] { "wpf.monitoring.view_occupancy", "recent_entries.view", "monitoring.view", "recent_entries.view_list", "monitoring.occupancy" } },
        { "monitoring.search_vehicles", new[] { "wpf.monitoring.search_vehicles", "recent_entries.search", "monitoring.search" } },
        { "monitoring.force_exit", new[] { "wpf.monitoring.force_exit", "recent_entries.force_exit", "monitoring.force_exit" } },
        { "monitoring.export", new[] { "wpf.monitoring.export", "recent_entries.export", "monitoring.export" } },

        { "analytics.view_dashboard", new[] { "analytics.view", "analytics.dashboard", "analytics.income_reports" } },
        { "analytics.income_reports", new[] { "analytics.income", "analytics.view" } },
        { "analytics.occupancy_reports", new[] { "analytics.occupancy", "analytics.view" } },
        { "analytics.audit_reports", new[] { "analytics.audit", "analytics.view" } },
        { "analytics.export", new[] { "analytics.export" } },

        { "checkin.create_ticket", new[] { "wpf.checkin.create", "checkin.create", "checkin.view", "wpf.checkin.view" } },
        { "checkin.view", new[] { "wpf.checkin.view", "wpf.checkin.create", "checkin.create_ticket", "checkin.create" } },
        { "checkin.create", new[] { "wpf.checkin.create", "checkin.create_ticket", "checkin.view" } },
        { "checkin.reprint", new[] { "wpf.checkin.reprint" } },
        { "checkin.edit_plate", new[] { "wpf.checkin.edit_plate" } },
        { "checkin.manual_barrier", new[] { "wpf.checkin.manual_barrier" } },

        { "checkout.process_payment", new[] { "wpf.checkout.process_payment", "checkout.process", "checkout.view", "wpf.checkout.view", "checkout.liquidate" } },
        { "checkout.view", new[] { "wpf.checkout.view", "wpf.checkout.process_payment", "checkout.process_payment", "checkout.process" } },
        { "checkout.apply_discount", new[] { "wpf.checkout.apply_discount" } },
        { "checkout.waive_fee", new[] { "wpf.checkout.waive_fee" } },
        { "checkout.reprint_receipt", new[] { "wpf.checkout.reprint_receipt" } },
        { "checkout.manual_barrier", new[] { "wpf.checkout.manual_barrier" } },
        
        { "subscriptions.view_list", new[] { "wpf.subscriptions.view", "subscriptions.view", "subscriptions.list" } },
        { "subscriptions.view", new[] { "wpf.subscriptions.view", "subscriptions.view_list", "subscriptions.list" } },
        { "subscriptions.create_subscription", new[] { "wpf.subscriptions.create", "subscriptions.create", "subscriptions.new" } },
        { "subscriptions.create", new[] { "wpf.subscriptions.create", "subscriptions.create_subscription", "subscriptions.new" } },
        { "subscriptions.edit_subscription", new[] { "subscriptions.edit" } },
        { "subscriptions.cancel_subscription", new[] { "wpf.subscriptions.cancel", "subscriptions.cancel" } },
        { "subscriptions.cancel", new[] { "wpf.subscriptions.cancel", "subscriptions.cancel_subscription" } },
        { "subscriptions.renew_subscription", new[] { "wpf.subscriptions.renew", "subscriptions.renew" } },
        { "subscriptions.renew", new[] { "wpf.subscriptions.renew", "subscriptions.renew_subscription" } },

        { "rates.view_list", new[] { "rates.view", "rates.list" } },
        { "agreements.view_list", new[] { "agreements.view", "agreements.list" } },
        { "branches.view_list", new[] { "branches.view", "branches.list" } },
        { "users.view_list", new[] { "users.view", "users.list" } },
        { "roles.view_list", new[] { "roles.view", "roles.list" } },
        { "audit.view_logs", new[] { "audit.view", "audit.logs" } }
    };

    public static PermissionService Current { get; } = new();

    public event Action? PermissionsChanged;

    public bool IsAdmin { get; private set; }
    public IReadOnlySet<string> GrantedPermissions => _permissions;

    public bool HasPermission(string? permissionSlug)
    {
        if (string.IsNullOrWhiteSpace(permissionSlug)) return true;
        if (IsAdmin) return true;
        
        var slug = permissionSlug.Trim();

        // 1. Coincidencia exacta directa
        if (_permissions.Contains(slug)) return true;

        // 1.1 Resolución automática prefijo wpf.*
        if (slug.StartsWith("wpf.", StringComparison.OrdinalIgnoreCase))
        {
            var unprefixed = slug[4..];
            if (_permissions.Contains(unprefixed)) return true;
        }
        else
        {
            var prefixed = $"wpf.{slug}";
            if (_permissions.Contains(prefixed)) return true;
        }

        // 2. Comodín global
        if (_permissions.Contains("*") || _permissions.Contains("all")) return true;

        // 3. Comodín a nivel de módulo (ej: el usuario tiene "shifts.*" o "shifts" y se evalúa "shifts.close")
        var dotIndex = slug.IndexOf('.');
        if (dotIndex > 0)
        {
            var module = slug[..dotIndex];
            if (_permissions.Contains($"{module}.*") || _permissions.Contains(module)) return true;

            // Variaciones semánticas plural/singular
            if (module.Equals("shift", StringComparison.OrdinalIgnoreCase) && (_permissions.Contains("shifts.*") || _permissions.Contains("shifts"))) return true;
            if (module.Equals("shifts", StringComparison.OrdinalIgnoreCase) && (_permissions.Contains("shift.*") || _permissions.Contains("shift"))) return true;
            if (module.Equals("recent_entries", StringComparison.OrdinalIgnoreCase) && (_permissions.Contains("monitoring.*") || _permissions.Contains("monitoring"))) return true;
            if (module.Equals("monitoring", StringComparison.OrdinalIgnoreCase) && (_permissions.Contains("recent_entries.*") || _permissions.Contains("recent_entries"))) return true;
        }

        // 4. Búsqueda por alias directo
        if (_permissionAliases.TryGetValue(slug, out var aliases))
        {
            if (aliases.Any(a => _permissions.Contains(a))) return true;
        }

        // 5. Búsqueda inversa: si slug es un alias de un permiso otorgado
        foreach (var (canonical, aliasList) in _permissionAliases)
        {
            if (aliasList.Contains(slug, StringComparer.OrdinalIgnoreCase) && _permissions.Contains(canonical))
            {
                return true;
            }
        }

        return false;
    }

    public bool HasAnyPermission(params string[] permissionSlugs)
    {
        if (IsAdmin) return true;
        if (permissionSlugs == null || permissionSlugs.Length == 0) return true;
        return permissionSlugs.Any(HasPermission);
    }

    public bool HasAllPermissions(params string[] permissionSlugs)
    {
        if (IsAdmin) return true;
        if (permissionSlugs == null || permissionSlugs.Length == 0) return true;
        return permissionSlugs.All(HasPermission);
    }

    public void LoadPermissions(IEnumerable<string> permissionSlugs, bool isAdmin = false)
    {
        IsAdmin = isAdmin;
        _permissions.Clear();

        if (permissionSlugs != null)
        {
            foreach (var p in permissionSlugs.Where(p => !string.IsNullOrWhiteSpace(p)))
            {
                _permissions.Add(p.Trim());
            }
        }

        if (this != Current)
        {
            Current.LoadPermissions(permissionSlugs ?? Array.Empty<string>(), isAdmin);
        }

        PermissionsChanged?.Invoke();
    }

    public void LoadPermissions(UserSessionModel? user)
    {
        if (user == null)
        {
            Clear();
            return;
        }

        var isAdmin = user.IsAdmin;

        LoadPermissions(user.GrantedPermissions ?? (IEnumerable<string>)Array.Empty<string>(), isAdmin);
    }

    public void Clear()
    {
        IsAdmin = false;
        _permissions.Clear();

        if (this != Current)
        {
            Current.Clear();
        }

        PermissionsChanged?.Invoke();
    }
}
