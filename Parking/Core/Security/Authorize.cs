using System;
using System.Windows;
using Parking.Services.Implementations;

namespace Parking.Core.Security;

public enum AuthorizationPolicy
{
    Hide,
    Disable,
    HideOrDisable
}

public static class Authorize
{
    public static readonly DependencyProperty PermissionProperty =
        DependencyProperty.RegisterAttached(
            "Permission",
            typeof(string),
            typeof(Authorize),
            new PropertyMetadata(null, OnPermissionChanged));

    public static readonly DependencyProperty PolicyProperty =
        DependencyProperty.RegisterAttached(
            "Policy",
            typeof(AuthorizationPolicy),
            typeof(Authorize),
            new PropertyMetadata(AuthorizationPolicy.Hide, OnPolicyChanged));

    public static string? GetPermission(DependencyObject obj) => (string?)obj.GetValue(PermissionProperty);
    public static void SetPermission(DependencyObject obj, string? value) => obj.SetValue(PermissionProperty, value);

    public static AuthorizationPolicy GetPolicy(DependencyObject obj) => (AuthorizationPolicy)obj.GetValue(PolicyProperty);
    public static void SetPolicy(DependencyObject obj, AuthorizationPolicy value) => obj.SetValue(PolicyProperty, value);

    private static void OnPermissionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element) return;

        UpdateElementAuthorization(element);

        // Suscribir al evento de cambio de permisos de forma segura
        element.Loaded -= Element_Loaded;
        element.Loaded += Element_Loaded;
    }

    private static void OnPolicyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement element)
        {
            UpdateElementAuthorization(element);
        }
    }

    private static void Element_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            UpdateElementAuthorization(element);
            
            // Suscribir al servicio de permisos
            PermissionService.Current.PermissionsChanged -= PermissionService_PermissionsChanged;
            PermissionService.Current.PermissionsChanged += PermissionService_PermissionsChanged;

            void PermissionService_PermissionsChanged(object? s, EventArgs ev)
            {
                if (element.Dispatcher.CheckAccess())
                {
                    UpdateElementAuthorization(element);
                }
                else
                {
                    element.Dispatcher.Invoke(() => UpdateElementAuthorization(element));
                }
            }

            element.Unloaded += (us, ue) =>
            {
                PermissionService.Current.PermissionsChanged -= PermissionService_PermissionsChanged;
            };
        }
    }

    public static void UpdateElementAuthorization(FrameworkElement element)
    {
        var permission = GetPermission(element);
        if (string.IsNullOrWhiteSpace(permission))
        {
            return; // Sin restricción
        }

        var hasPermission = PermissionService.Current.HasPermission(permission);
        var policy = GetPolicy(element);

        if (hasPermission)
        {
            if (policy is AuthorizationPolicy.Hide or AuthorizationPolicy.HideOrDisable)
            {
                element.Visibility = Visibility.Visible;
            }
            if (policy is AuthorizationPolicy.Disable or AuthorizationPolicy.HideOrDisable)
            {
                element.IsEnabled = true;
            }
        }
        else
        {
            if (policy is AuthorizationPolicy.Hide or AuthorizationPolicy.HideOrDisable)
            {
                element.Visibility = Visibility.Collapsed;
            }
            if (policy is AuthorizationPolicy.Disable or AuthorizationPolicy.HideOrDisable)
            {
                element.IsEnabled = false;
            }
        }
    }
}
