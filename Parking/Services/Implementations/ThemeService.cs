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
        new(AppTheme.FigmaTeal, "Teal PWA Figma", "#174B5C", "#F4F7F6"),
        new(AppTheme.PureLight, "Blanco / Claro Empresarial", "#2563EB", "#F8FAFC"),
        new(AppTheme.MidnightDark, "Oscuro Midnight", "#6366F1", "#0B0F19"),
        new(AppTheme.OceanBlue, "Azul Océano Zafiro", "#0284C7", "#F0F9FF"),
        new(AppTheme.ForestEmerald, "Verde Esmeralda Menta", "#10B981", "#ECFDF5"),
        new(AppTheme.RoyalPurple, "Púrpura Real Neón", "#9333EA", "#FAF5FF")
    };

    public IReadOnlyList<ThemeInfo> GetAvailableThemes() => AvailableThemes;

    public void SetTheme(AppTheme theme)
    {
        CurrentTheme = theme;
        ApplyThemeColors(theme);
        ThemeChanged?.Invoke(this, theme);
    }

    private void ApplyThemeColors(AppTheme theme)
    {
        var app = Application.Current;
        if (app == null)
        {
            return;
        }

        switch (theme)
        {
            case AppTheme.FigmaTeal:
                // 1. Teal PWA Figma (Verde Azulado Original)
                SetBrushHex(app, "BrushAppBackground", "#F4F7F6");
                SetBrushHex(app, "BrushSurfaceBackground", "#FFFFFF");
                SetBrushHex(app, "BrushSurfaceElevated", "#F8FAFC");
                SetBrushHex(app, "BrushSurfaceLight", "#EBF6F8");
                SetBrushHex(app, "BrushInputBackground", "#FFFFFF");
                SetBrushHex(app, "BrushSidebarBackground", "#174B5C");
                SetBrushHex(app, "BrushSidebarItemActive", "#123B49");
                SetBrushHex(app, "BrushSidebarText", "#94A3B8");
                SetBrushHex(app, "BrushSidebarTextActive", "#FFFFFF");

                SetBrushHex(app, "BrushBorderSubtle", "#E2E8F0");
                SetBrushHex(app, "BrushBorderMedium", "#CBD5E1");
                SetBrushHex(app, "BrushBorderFocused", "#174B5C");

                SetBrushHex(app, "BrushPrimary", "#174B5C");
                SetBrushHex(app, "BrushPrimaryHover", "#123B49");
                SetBrushHex(app, "BrushPrimaryActive", "#0E2E38");
                SetBrushHex(app, "BrushPrimaryLight", "#1F647B");
                SetBrushHex(app, "BrushPrimaryGlow", "#22174B5C");

                SetBrushHex(app, "BrushTableHeaderBackground", "#F8FAFC");
                SetBrushHex(app, "BrushTableRowHover", "#F1F5F9");
                SetBrushHex(app, "BrushTableRowSelected", "#E2E8F0");

                SetBrushHex(app, "BrushTextPrimary", "#1E293B");
                SetBrushHex(app, "BrushTextSecondary", "#64748B");
                SetBrushHex(app, "BrushTextMuted", "#94A3B8");
                SetBrushHex(app, "BrushTextWhite", "#FFFFFF");

                SetBrushHex(app, "BrushSuccess", "#16A34A");
                SetBrushHex(app, "BrushSuccessHover", "#15803D");
                SetBrushHex(app, "BrushSuccessBg", "#DCFCE7");
                SetBrushHex(app, "BrushSuccessText", "#166534");

                SetBrushHex(app, "BrushWarning", "#D97706");
                SetBrushHex(app, "BrushWarningHover", "#B45309");
                SetBrushHex(app, "BrushWarningBg", "#FEF3C7");
                SetBrushHex(app, "BrushWarningText", "#92400E");

                SetBrushHex(app, "BrushDanger", "#DC2626");
                SetBrushHex(app, "BrushDangerHover", "#B91C1C");
                SetBrushHex(app, "BrushDangerBg", "#FEE2E2");
                SetBrushHex(app, "BrushDangerText", "#991B1B");

                SetBrushHex(app, "BrushCyan", "#0284C7");
                SetBrushHex(app, "BrushViolet", "#7C3AED");
                break;

            case AppTheme.PureLight:
                // 2. Blanco / Claro Empresarial (Azul Corporativo Limpio)
                SetBrushHex(app, "BrushAppBackground", "#F8FAFC");
                SetBrushHex(app, "BrushSurfaceBackground", "#FFFFFF");
                SetBrushHex(app, "BrushSurfaceElevated", "#F1F5F9");
                SetBrushHex(app, "BrushSurfaceLight", "#E2E8F0");
                SetBrushHex(app, "BrushInputBackground", "#FFFFFF");
                SetBrushHex(app, "BrushSidebarBackground", "#1E293B");
                SetBrushHex(app, "BrushSidebarItemActive", "#0F172A");
                SetBrushHex(app, "BrushSidebarText", "#94A3B8");
                SetBrushHex(app, "BrushSidebarTextActive", "#FFFFFF");

                SetBrushHex(app, "BrushBorderSubtle", "#CBD5E1");
                SetBrushHex(app, "BrushBorderMedium", "#94A3B8");
                SetBrushHex(app, "BrushBorderFocused", "#2563EB");

                SetBrushHex(app, "BrushPrimary", "#2563EB");
                SetBrushHex(app, "BrushPrimaryHover", "#1D4ED8");
                SetBrushHex(app, "BrushPrimaryActive", "#1E40AF");
                SetBrushHex(app, "BrushPrimaryLight", "#3B82F6");
                SetBrushHex(app, "BrushPrimaryGlow", "#222563EB");

                SetBrushHex(app, "BrushTableHeaderBackground", "#F1F5F9");
                SetBrushHex(app, "BrushTableRowHover", "#E2E8F0");
                SetBrushHex(app, "BrushTableRowSelected", "#DBEAFE");

                SetBrushHex(app, "BrushTextPrimary", "#0F172A");
                SetBrushHex(app, "BrushTextSecondary", "#64748B");
                SetBrushHex(app, "BrushTextMuted", "#94A3B8");
                SetBrushHex(app, "BrushTextWhite", "#FFFFFF");

                SetBrushHex(app, "BrushSuccess", "#16A34A");
                SetBrushHex(app, "BrushSuccessHover", "#15803D");
                SetBrushHex(app, "BrushSuccessBg", "#DCFCE7");
                SetBrushHex(app, "BrushSuccessText", "#166534");

                SetBrushHex(app, "BrushWarning", "#D97706");
                SetBrushHex(app, "BrushWarningHover", "#B45309");
                SetBrushHex(app, "BrushWarningBg", "#FEF3C7");
                SetBrushHex(app, "BrushWarningText", "#92400E");

                SetBrushHex(app, "BrushDanger", "#DC2626");
                SetBrushHex(app, "BrushDangerHover", "#B91C1C");
                SetBrushHex(app, "BrushDangerBg", "#FEE2E2");
                SetBrushHex(app, "BrushDangerText", "#991B1B");

                SetBrushHex(app, "BrushCyan", "#0284C7");
                SetBrushHex(app, "BrushViolet", "#7C3AED");
                break;

            case AppTheme.MidnightDark:
                // 3. Oscuro Midnight (Modo Oscuro Elegante)
                SetBrushHex(app, "BrushAppBackground", "#0B0F19");
                SetBrushHex(app, "BrushSurfaceBackground", "#111827");
                SetBrushHex(app, "BrushSurfaceElevated", "#1F2937");
                SetBrushHex(app, "BrushSurfaceLight", "#374151");
                SetBrushHex(app, "BrushInputBackground", "#1F2937");
                SetBrushHex(app, "BrushSidebarBackground", "#030712");
                SetBrushHex(app, "BrushSidebarItemActive", "#1F2937");
                SetBrushHex(app, "BrushSidebarText", "#94A3B8");
                SetBrushHex(app, "BrushSidebarTextActive", "#FFFFFF");

                SetBrushHex(app, "BrushBorderSubtle", "#1F2937");
                SetBrushHex(app, "BrushBorderMedium", "#374151");
                SetBrushHex(app, "BrushBorderFocused", "#6366F1");

                SetBrushHex(app, "BrushPrimary", "#6366F1");
                SetBrushHex(app, "BrushPrimaryHover", "#4F46E5");
                SetBrushHex(app, "BrushPrimaryActive", "#4338CA");
                SetBrushHex(app, "BrushPrimaryLight", "#818CF8");
                SetBrushHex(app, "BrushPrimaryGlow", "#226366F1");

                SetBrushHex(app, "BrushTableHeaderBackground", "#1F2937");
                SetBrushHex(app, "BrushTableRowHover", "#374151");
                SetBrushHex(app, "BrushTableRowSelected", "#4F46E5");

                SetBrushHex(app, "BrushTextPrimary", "#F8FAFC");
                SetBrushHex(app, "BrushTextSecondary", "#94A3B8");
                SetBrushHex(app, "BrushTextMuted", "#64748B");
                SetBrushHex(app, "BrushTextWhite", "#FFFFFF");

                SetBrushHex(app, "BrushSuccess", "#10B981");
                SetBrushHex(app, "BrushSuccessHover", "#059669");
                SetBrushHex(app, "BrushSuccessBg", "#064E3B");
                SetBrushHex(app, "BrushSuccessText", "#6EE7B7");

                SetBrushHex(app, "BrushWarning", "#F59E0B");
                SetBrushHex(app, "BrushWarningHover", "#D97706");
                SetBrushHex(app, "BrushWarningBg", "#451A03");
                SetBrushHex(app, "BrushWarningText", "#FCD34D");

                SetBrushHex(app, "BrushDanger", "#EF4444");
                SetBrushHex(app, "BrushDangerHover", "#DC2626");
                SetBrushHex(app, "BrushDangerBg", "#450A0A");
                SetBrushHex(app, "BrushDangerText", "#FCA5A5");

                SetBrushHex(app, "BrushCyan", "#06B6D4");
                SetBrushHex(app, "BrushViolet", "#8B5CF6");
                break;

            case AppTheme.OceanBlue:
                // 4. Azul Océano Zafiro (Zafiro Fresco)
                SetBrushHex(app, "BrushAppBackground", "#F0F9FF");
                SetBrushHex(app, "BrushSurfaceBackground", "#FFFFFF");
                SetBrushHex(app, "BrushSurfaceElevated", "#E0F2FE");
                SetBrushHex(app, "BrushSurfaceLight", "#BAE6FD");
                SetBrushHex(app, "BrushInputBackground", "#FFFFFF");
                SetBrushHex(app, "BrushSidebarBackground", "#0C4A6E");
                SetBrushHex(app, "BrushSidebarItemActive", "#075985");
                SetBrushHex(app, "BrushSidebarText", "#BAE6FD");
                SetBrushHex(app, "BrushSidebarTextActive", "#FFFFFF");

                SetBrushHex(app, "BrushBorderSubtle", "#BAE6FD");
                SetBrushHex(app, "BrushBorderMedium", "#7DD3FC");
                SetBrushHex(app, "BrushBorderFocused", "#0284C7");

                SetBrushHex(app, "BrushPrimary", "#0284C7");
                SetBrushHex(app, "BrushPrimaryHover", "#0369A1");
                SetBrushHex(app, "BrushPrimaryActive", "#075985");
                SetBrushHex(app, "BrushPrimaryLight", "#38BDF8");
                SetBrushHex(app, "BrushPrimaryGlow", "#220284C7");

                SetBrushHex(app, "BrushTableHeaderBackground", "#E0F2FE");
                SetBrushHex(app, "BrushTableRowHover", "#BAE6FD");
                SetBrushHex(app, "BrushTableRowSelected", "#7DD3FC");

                SetBrushHex(app, "BrushTextPrimary", "#0F172A");
                SetBrushHex(app, "BrushTextSecondary", "#475569");
                SetBrushHex(app, "BrushTextMuted", "#64748B");
                SetBrushHex(app, "BrushTextWhite", "#FFFFFF");

                SetBrushHex(app, "BrushSuccess", "#10B981");
                SetBrushHex(app, "BrushSuccessHover", "#059669");
                SetBrushHex(app, "BrushSuccessBg", "#DCFCE7");
                SetBrushHex(app, "BrushSuccessText", "#166534");

                SetBrushHex(app, "BrushWarning", "#F59E0B");
                SetBrushHex(app, "BrushWarningHover", "#D97706");
                SetBrushHex(app, "BrushWarningBg", "#FEF3C7");
                SetBrushHex(app, "BrushWarningText", "#92400E");

                SetBrushHex(app, "BrushDanger", "#EF4444");
                SetBrushHex(app, "BrushDangerHover", "#DC2626");
                SetBrushHex(app, "BrushDangerBg", "#FEE2E2");
                SetBrushHex(app, "BrushDangerText", "#991B1B");

                SetBrushHex(app, "BrushCyan", "#38BDF8");
                SetBrushHex(app, "BrushViolet", "#8B5CF6");
                break;

            case AppTheme.ForestEmerald:
                // 5. Verde Esmeralda Menta (Esmeralda & Menta)
                SetBrushHex(app, "BrushAppBackground", "#ECFDF5");
                SetBrushHex(app, "BrushSurfaceBackground", "#FFFFFF");
                SetBrushHex(app, "BrushSurfaceElevated", "#D1FAE5");
                SetBrushHex(app, "BrushSurfaceLight", "#A7F3D0");
                SetBrushHex(app, "BrushInputBackground", "#FFFFFF");
                SetBrushHex(app, "BrushSidebarBackground", "#064E3B");
                SetBrushHex(app, "BrushSidebarItemActive", "#065F46");
                SetBrushHex(app, "BrushSidebarText", "#A7F3D0");
                SetBrushHex(app, "BrushSidebarTextActive", "#FFFFFF");

                SetBrushHex(app, "BrushBorderSubtle", "#A7F3D0");
                SetBrushHex(app, "BrushBorderMedium", "#6EE7B7");
                SetBrushHex(app, "BrushBorderFocused", "#10B981");

                SetBrushHex(app, "BrushPrimary", "#10B981");
                SetBrushHex(app, "BrushPrimaryHover", "#059669");
                SetBrushHex(app, "BrushPrimaryActive", "#047857");
                SetBrushHex(app, "BrushPrimaryLight", "#34D399");
                SetBrushHex(app, "BrushPrimaryGlow", "#2210B981");

                SetBrushHex(app, "BrushTableHeaderBackground", "#D1FAE5");
                SetBrushHex(app, "BrushTableRowHover", "#A7F3D0");
                SetBrushHex(app, "BrushTableRowSelected", "#6EE7B7");

                SetBrushHex(app, "BrushTextPrimary", "#064E3B");
                SetBrushHex(app, "BrushTextSecondary", "#475569");
                SetBrushHex(app, "BrushTextMuted", "#64748B");
                SetBrushHex(app, "BrushTextWhite", "#FFFFFF");

                SetBrushHex(app, "BrushSuccess", "#10B981");
                SetBrushHex(app, "BrushSuccessHover", "#059669");
                SetBrushHex(app, "BrushSuccessBg", "#D1FAE5");
                SetBrushHex(app, "BrushSuccessText", "#065F46");

                SetBrushHex(app, "BrushWarning", "#F59E0B");
                SetBrushHex(app, "BrushWarningHover", "#D97706");
                SetBrushHex(app, "BrushWarningBg", "#FEF3C7");
                SetBrushHex(app, "BrushWarningText", "#92400E");

                SetBrushHex(app, "BrushDanger", "#EF4444");
                SetBrushHex(app, "BrushDangerHover", "#DC2626");
                SetBrushHex(app, "BrushDangerBg", "#FEE2E2");
                SetBrushHex(app, "BrushDangerText", "#991B1B");

                SetBrushHex(app, "BrushCyan", "#34D399");
                SetBrushHex(app, "BrushViolet", "#8B5CF6");
                break;

            case AppTheme.RoyalPurple:
                // 6. Púrpura Real Neón (Púrpura Neón)
                SetBrushHex(app, "BrushAppBackground", "#FAF5FF");
                SetBrushHex(app, "BrushSurfaceBackground", "#FFFFFF");
                SetBrushHex(app, "BrushSurfaceElevated", "#F3E8FF");
                SetBrushHex(app, "BrushSurfaceLight", "#E9D5FF");
                SetBrushHex(app, "BrushInputBackground", "#FFFFFF");
                SetBrushHex(app, "BrushSidebarBackground", "#3B0764");
                SetBrushHex(app, "BrushSidebarItemActive", "#581C87");
                SetBrushHex(app, "BrushSidebarText", "#E9D5FF");
                SetBrushHex(app, "BrushSidebarTextActive", "#FFFFFF");

                SetBrushHex(app, "BrushBorderSubtle", "#E9D5FF");
                SetBrushHex(app, "BrushBorderMedium", "#D8B4FE");
                SetBrushHex(app, "BrushBorderFocused", "#9333EA");

                SetBrushHex(app, "BrushPrimary", "#9333EA");
                SetBrushHex(app, "BrushPrimaryHover", "#7E22CE");
                SetBrushHex(app, "BrushPrimaryActive", "#6B21A8");
                SetBrushHex(app, "BrushPrimaryLight", "#A855F7");
                SetBrushHex(app, "BrushPrimaryGlow", "#229333EA");

                SetBrushHex(app, "BrushTableHeaderBackground", "#F3E8FF");
                SetBrushHex(app, "BrushTableRowHover", "#E9D5FF");
                SetBrushHex(app, "BrushTableRowSelected", "#D8B4FE");

                SetBrushHex(app, "BrushTextPrimary", "#3B0764");
                SetBrushHex(app, "BrushTextSecondary", "#6B21A8");
                SetBrushHex(app, "BrushTextMuted", "#9333EA");
                SetBrushHex(app, "BrushTextWhite", "#FFFFFF");

                SetBrushHex(app, "BrushSuccess", "#10B981");
                SetBrushHex(app, "BrushSuccessHover", "#059669");
                SetBrushHex(app, "BrushSuccessBg", "#D1FAE5");
                SetBrushHex(app, "BrushSuccessText", "#065F46");

                SetBrushHex(app, "BrushWarning", "#F59E0B");
                SetBrushHex(app, "BrushWarningHover", "#D97706");
                SetBrushHex(app, "BrushWarningBg", "#FEF3C7");
                SetBrushHex(app, "BrushWarningText", "#92400E");

                SetBrushHex(app, "BrushDanger", "#EF4444");
                SetBrushHex(app, "BrushDangerHover", "#DC2626");
                SetBrushHex(app, "BrushDangerBg", "#FEE2E2");
                SetBrushHex(app, "BrushDangerText", "#991B1B");

                SetBrushHex(app, "BrushCyan", "#C084FC");
                SetBrushHex(app, "BrushViolet", "#9333EA");
                break;
        }
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
