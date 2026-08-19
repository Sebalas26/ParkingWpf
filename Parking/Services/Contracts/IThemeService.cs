using System;
using System.Collections.Generic;
using Parking.Core.Enums;

namespace Parking.Services.Contracts;

public interface IThemeService
{
    AppTheme CurrentTheme { get; }
    event EventHandler<AppTheme>? ThemeChanged;
    void SetTheme(AppTheme theme);
    IReadOnlyList<ThemeInfo> GetAvailableThemes();
}

public record ThemeInfo(AppTheme Theme, string DisplayName, string PrimaryHex, string BackgroundHex);
