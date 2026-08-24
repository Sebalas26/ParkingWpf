using System;

namespace Parking.Core.Security;

[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public class RequirePermissionAttribute : Attribute
{
    public string PermissionKey { get; }
    public string DisplayModuleName { get; set; } = string.Empty;

    public RequirePermissionAttribute(string permissionKey)
    {
        PermissionKey = permissionKey;
    }

    public RequirePermissionAttribute(string permissionKey, string displayModuleName)
    {
        PermissionKey = permissionKey;
        DisplayModuleName = displayModuleName;
    }
}
