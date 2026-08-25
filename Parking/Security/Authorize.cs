using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Parking.Services.Contracts;

namespace Parking.Security;

public enum AuthorizeBehavior
{
    Collapse,
    Hidden,
    Disable
}

public static class Authorize
{
    private static IPermissionService? _permissionService;

    private static IPermissionService GetPermissionService()
    {
        if (_permissionService != null) return _permissionService;

        if (Application.Current is App app && app.Services != null)
        {
            _permissionService = app.Services.GetService<IPermissionService>();
            if (_permissionService != null)
            {
                _permissionService.PermissionsChanged += OnPermissionsChanged;
            }
        }
        return _permissionService!;
    }

    public static readonly DependencyProperty PermissionProperty =
        DependencyProperty.RegisterAttached(
            "Permission",
            typeof(string),
            typeof(Authorize),
            new PropertyMetadata(null, OnPermissionPropertyChanged));

    public static readonly DependencyProperty BehaviorProperty =
        DependencyProperty.RegisterAttached(
            "Behavior",
            typeof(AuthorizeBehavior),
            typeof(Authorize),
            new PropertyMetadata(AuthorizeBehavior.Collapse, OnPermissionPropertyChanged));

    public static string? GetPermission(DependencyObject obj) =>
        (string?)obj.GetValue(PermissionProperty);

    public static void SetPermission(DependencyObject obj, string? value) =>
        obj.SetValue(PermissionProperty, value);

    public static AuthorizeBehavior GetBehavior(DependencyObject obj) =>
        (AuthorizeBehavior)obj.GetValue(BehaviorProperty);

    public static void SetBehavior(DependencyObject obj, AuthorizeBehavior value) =>
        obj.SetValue(BehaviorProperty, value);

    private static void OnPermissionPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UIElement element)
        {
            ApplyAuthorization(element);
        }
    }

    private static void OnPermissionsChanged()
    {
        // Re-evaluate on UI elements if needed
    }

    public static void ApplyAuthorization(UIElement element)
    {
        var permission = GetPermission(element);
        if (string.IsNullOrWhiteSpace(permission)) return;

        var permService = GetPermissionService();
        var isAuthorized = permService?.HasPermission(permission) ?? true;
        var behavior = GetBehavior(element);

        if (isAuthorized)
        {
            if (behavior == AuthorizeBehavior.Disable)
            {
                element.IsEnabled = true;
            }
            else
            {
                element.Visibility = Visibility.Visible;
            }
        }
        else
        {
            switch (behavior)
            {
                case AuthorizeBehavior.Collapse:
                    element.Visibility = Visibility.Collapsed;
                    break;
                case AuthorizeBehavior.Hidden:
                    element.Visibility = Visibility.Hidden;
                    break;
                case AuthorizeBehavior.Disable:
                    element.IsEnabled = false;
                    break;
            }
        }
    }
}
