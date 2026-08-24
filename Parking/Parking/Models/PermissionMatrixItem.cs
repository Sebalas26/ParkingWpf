using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Parking.Models;

public partial class PermissionMatrixItem : ObservableObject
{
    public Guid PermissionId { get; set; }
    public Guid ModuleId { get; set; }
    public string ModuleKey { get; set; } = string.Empty;
    public string ModuleDisplayName { get; set; } = string.Empty;
    public string ActionKey { get; set; } = string.Empty;
    public string ActionDisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }

    [ObservableProperty]
    private bool _isGranted;
}
