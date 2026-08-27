using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Parking.Services.Contracts;
using Parking.Services.Implementations;

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
        }
        return _permissionService ?? PermissionService.Current;
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
        if (d is not FrameworkElement element) return;

        ApplyAuthorization(element);

        element.Loaded -= Element_Loaded;
        element.Loaded += Element_Loaded;
    }

    private static void Element_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element) return;

        ApplyAuthorization(element);

        var permService = GetPermissionService();
        if (permService != null)
        {
            permService.PermissionsChanged -= PermissionService_PermissionsChanged;
            permService.PermissionsChanged += PermissionService_PermissionsChanged;
        }

        PermissionService.Current.PermissionsChanged -= PermissionService_PermissionsChanged;
        PermissionService.Current.PermissionsChanged += PermissionService_PermissionsChanged;

        void PermissionService_PermissionsChanged()
        {
            if (element.Dispatcher.CheckAccess())
            {
                ApplyAuthorization(element);
            }
            else
            {
                element.Dispatcher.InvokeAsync(() => ApplyAuthorization(element));
            }
        }

        element.Unloaded += (us, ue) =>
        {
            if (permService != null)
            {
                permService.PermissionsChanged -= PermissionService_PermissionsChanged;
            }
            PermissionService.Current.PermissionsChanged -= PermissionService_PermissionsChanged;
        };
    }

    public static void ApplyAuthorization(UIElement element)
    {
        var permission = GetPermission(element);
        if (string.IsNullOrWhiteSpace(permission)) return;

        var permService = GetPermissionService();
        var isAuthorized = permService?.HasPermission(permission) ?? PermissionService.Current.HasPermission(permission);
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
