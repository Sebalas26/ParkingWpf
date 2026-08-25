using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Parking.Core.Enums;
using Parking.Services.Contracts;

namespace Parking.Services.Implementations;

public class ThemeService : IThemeService
{
    public AppTheme CurrentTheme { get; private set; } = AppTheme.FigmaTeal;

    public event EventHandler<AppTheme>? ThemeChanged;

    private static readonly List<ThemeInfo> AvailableThemes = new()
    {
        new(AppTheme.FigmaTeal, "Park Point Institucional", "#00867A", "#F4F6F7")
    };

    public IReadOnlyList<ThemeInfo> GetAvailableThemes() => AvailableThemes;

    public void SetTheme(AppTheme theme)
    {
        CurrentTheme = theme;
        ApplyThemeColors();
        ThemeChanged?.Invoke(this, theme);
    }

    private void ApplyThemeColors()
    {
        var app = Application.Current;
        if (app == null) return;

        // Paleta Institucional Única: PARK POINT
        // Verde: #00867A | Grafito: #1E2A2F | Gris Concreto: #B9B9B9 | Blanco: #FFFFFF | Amarillo: #FFC107
        SetBrushHex(app, "BrushAppBackground", "#F4F6F7");
        SetBrushHex(app, "BrushSurfaceBackground", "#FFFFFF");
        SetBrushHex(app, "BrushSurfaceElevated", "#FFFFFF");
        SetBrushHex(app, "BrushSurfaceLight", "#EAF2F1");
        SetBrushHex(app, "BrushInputBackground", "#FFFFFF");
        SetBrushHex(app, "BrushSidebarBackground", "#1E2A2F");
        SetBrushHex(app, "BrushSidebarItemActive", "#00867A");
        SetBrushHex(app, "BrushSidebarText", "#B9B9B9");
        SetBrushHex(app, "BrushSidebarTextActive", "#FFFFFF");

        SetBrushHex(app, "BrushBorderSubtle", "#E2E8F0");
        SetBrushHex(app, "BrushBorderMedium", "#B9B9B9");
        SetBrushHex(app, "BrushBorderFocused", "#00867A");

        SetBrushHex(app, "BrushPrimary", "#00867A");
        SetBrushHex(app, "BrushPrimaryHover", "#006E64");
        SetBrushHex(app, "BrushPrimaryActive", "#00574F");
        SetBrushHex(app, "BrushPrimaryLight", "#E0F2F1");
        SetBrushHex(app, "BrushPrimaryGlow", "#2200867A");

        SetBrushHex(app, "BrushTableHeaderBackground", "#F8FAFA");
        SetBrushHex(app, "BrushTableRowHover", "#F0F7F6");
        SetBrushHex(app, "BrushTableRowSelected", "#E0F2F1");

        SetBrushHex(app, "BrushTextPrimary", "#1E2A2F");
        SetBrushHex(app, "BrushTextSecondary", "#5A6E75");
        SetBrushHex(app, "BrushTextMuted", "#8D9FA5");
        SetBrushHex(app, "BrushTextWhite", "#FFFFFF");

        SetBrushHex(app, "BrushSuccess", "#00867A");
        SetBrushHex(app, "BrushSuccessHover", "#006E64");
        SetBrushHex(app, "BrushSuccessBg", "#E0F2F1");
        SetBrushHex(app, "BrushSuccessText", "#00574F");

        SetBrushHex(app, "BrushWarning", "#FFC107");
        SetBrushHex(app, "BrushWarningHover", "#E0A800");
        SetBrushHex(app, "BrushWarningBg", "#FFF8E1");
        SetBrushHex(app, "BrushWarningText", "#8A6D00");

        SetBrushHex(app, "BrushDanger", "#DC2626");
        SetBrushHex(app, "BrushDangerHover", "#B91C1C");
        SetBrushHex(app, "BrushDangerBg", "#FEE2E2");
        SetBrushHex(app, "BrushDangerText", "#991B1B");

        SetBrushHex(app, "BrushCyan", "#00A896");
        SetBrushHex(app, "BrushViolet", "#1E2A2F");
    }

    private static void SetBrushHex(Application app, string key, string hex)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            app.Resources[key] = brush;
        }
        catch { }
    }
}
