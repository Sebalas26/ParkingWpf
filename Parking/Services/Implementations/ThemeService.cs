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
        new(AppTheme.FigmaTeal, "Teal PWA Figma", "#0D4E5B", "#F4F7F9"),
        new(AppTheme.PureLight, "Blanco / Claro Empresarial", "#2563EB", "#FFFFFF"),
        new(AppTheme.MidnightDark, "Oscuro Midnight", "#6366F1", "#0B0F19"),
        new(AppTheme.OceanBlue, "Azul Océano Zafiro", "#0284C7", "#060D1A"),
        new(AppTheme.ForestEmerald, "Verde Esmeralda Menta", "#10B981", "#04140F"),
        new(AppTheme.RoyalPurple, "Púrpura Real Neón", "#A855F7", "#0F071A")
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
                SetBrush(app, "BrushAppBackground", Color.FromRgb(244, 247, 249));
                SetBrush(app, "BrushSurfaceBackground", Color.FromRgb(255, 255, 255));
                SetBrush(app, "BrushSurfaceElevated", Color.FromRgb(248, 250, 252));
                SetBrush(app, "BrushSurfaceLight", Color.FromRgb(237, 242, 247));
                SetBrush(app, "BrushInputBackground", Color.FromRgb(255, 255, 255));
                SetBrush(app, "BrushSidebarBackground", Color.FromRgb(11, 60, 73));
                SetBrush(app, "BrushSidebarItemActive", Color.FromRgb(7, 44, 54));
                SetBrush(app, "BrushSidebarText", Color.FromRgb(148, 163, 184));
                SetBrush(app, "BrushSidebarTextActive", Color.FromRgb(255, 255, 255));

                SetBrush(app, "BrushBorderSubtle", Color.FromRgb(226, 232, 240));
                SetBrush(app, "BrushBorderMedium", Color.FromRgb(203, 213, 225));
                SetBrush(app, "BrushBorderFocused", Color.FromRgb(13, 78, 91));

                SetBrush(app, "BrushPrimary", Color.FromRgb(13, 78, 91));
                SetBrush(app, "BrushPrimaryHover", Color.FromRgb(9, 57, 67));
                SetBrush(app, "BrushPrimaryActive", Color.FromRgb(6, 40, 48));
                SetBrush(app, "BrushPrimaryLight", Color.FromRgb(21, 115, 133));
                SetBrush(app, "BrushPrimaryGlow", Color.FromArgb(34, 13, 78, 91));

                SetBrush(app, "BrushTextPrimary", Color.FromRgb(15, 23, 42));
                SetBrush(app, "BrushTextSecondary", Color.FromRgb(100, 116, 139));
                SetBrush(app, "BrushTextMuted", Color.FromRgb(148, 163, 184));
                SetBrush(app, "BrushTextWhite", Color.FromRgb(255, 255, 255));

                SetBrush(app, "BrushSuccess", Color.FromRgb(22, 163, 74));
                SetBrush(app, "BrushSuccessHover", Color.FromRgb(21, 128, 61));
                SetBrush(app, "BrushSuccessBg", Color.FromRgb(220, 252, 231));
                SetBrush(app, "BrushSuccessText", Color.FromRgb(22, 101, 52));

                SetBrush(app, "BrushWarning", Color.FromRgb(217, 119, 6));
                SetBrush(app, "BrushWarningHover", Color.FromRgb(180, 83, 9));
                SetBrush(app, "BrushWarningBg", Color.FromRgb(254, 243, 199));
                SetBrush(app, "BrushWarningText", Color.FromRgb(146, 64, 14));

                SetBrush(app, "BrushDanger", Color.FromRgb(220, 38, 38));
                SetBrush(app, "BrushDangerHover", Color.FromRgb(185, 28, 28));
                SetBrush(app, "BrushDangerBg", Color.FromRgb(254, 226, 226));
                SetBrush(app, "BrushDangerText", Color.FromRgb(153, 27, 27));

                SetBrush(app, "BrushCyan", Color.FromRgb(2, 132, 199));
                SetBrush(app, "BrushViolet", Color.FromRgb(124, 58, 237));
                break;

            case AppTheme.PureLight:
                SetBrush(app, "BrushAppBackground", Color.FromRgb(241, 245, 249));
                SetBrush(app, "BrushSurfaceBackground", Color.FromRgb(255, 255, 255));
                SetBrush(app, "BrushSurfaceElevated", Color.FromRgb(248, 250, 252));
                SetBrush(app, "BrushSurfaceLight", Color.FromRgb(226, 232, 240));
                SetBrush(app, "BrushInputBackground", Color.FromRgb(255, 255, 255));
                SetBrush(app, "BrushSidebarBackground", Color.FromRgb(255, 255, 255));
                SetBrush(app, "BrushSidebarItemActive", Color.FromRgb(237, 242, 247));
                SetBrush(app, "BrushSidebarText", Color.FromRgb(100, 116, 139));
                SetBrush(app, "BrushSidebarTextActive", Color.FromRgb(37, 99, 235));

                SetBrush(app, "BrushBorderSubtle", Color.FromRgb(226, 232, 240));
                SetBrush(app, "BrushBorderMedium", Color.FromRgb(203, 213, 225));
                SetBrush(app, "BrushBorderFocused", Color.FromRgb(37, 99, 235));

                SetBrush(app, "BrushPrimary", Color.FromRgb(37, 99, 235));
                SetBrush(app, "BrushPrimaryHover", Color.FromRgb(29, 78, 216));
                SetBrush(app, "BrushPrimaryActive", Color.FromRgb(30, 64, 175));
                SetBrush(app, "BrushPrimaryLight", Color.FromRgb(59, 130, 246));
                SetBrush(app, "BrushPrimaryGlow", Color.FromArgb(40, 37, 99, 235));

                SetBrush(app, "BrushTextPrimary", Color.FromRgb(15, 23, 42));
                SetBrush(app, "BrushTextSecondary", Color.FromRgb(71, 85, 105));
                SetBrush(app, "BrushTextMuted", Color.FromRgb(100, 116, 139));
                SetBrush(app, "BrushTextWhite", Color.FromRgb(255, 255, 255));

                SetBrush(app, "BrushSuccess", Color.FromRgb(16, 185, 129));
                SetBrush(app, "BrushSuccessHover", Color.FromRgb(5, 150, 105));
                SetBrush(app, "BrushSuccessBg", Color.FromRgb(220, 252, 231));
                SetBrush(app, "BrushSuccessText", Color.FromRgb(22, 101, 52));

                SetBrush(app, "BrushWarning", Color.FromRgb(245, 158, 11));
                SetBrush(app, "BrushWarningHover", Color.FromRgb(217, 119, 6));
                SetBrush(app, "BrushWarningBg", Color.FromRgb(254, 243, 199));
                SetBrush(app, "BrushWarningText", Color.FromRgb(146, 64, 14));

                SetBrush(app, "BrushDanger", Color.FromRgb(239, 68, 68));
                SetBrush(app, "BrushDangerHover", Color.FromRgb(220, 38, 38));
                SetBrush(app, "BrushDangerBg", Color.FromRgb(254, 226, 226));
                SetBrush(app, "BrushDangerText", Color.FromRgb(153, 27, 27));

                SetBrush(app, "BrushCyan", Color.FromRgb(2, 132, 199));
                SetBrush(app, "BrushViolet", Color.FromRgb(124, 58, 237));
                break;

            case AppTheme.OceanBlue:
                SetBrush(app, "BrushAppBackground", Color.FromRgb(6, 13, 26));
                SetBrush(app, "BrushSurfaceBackground", Color.FromRgb(12, 24, 43));
                SetBrush(app, "BrushSurfaceElevated", Color.FromRgb(20, 37, 64));
                SetBrush(app, "BrushSurfaceLight", Color.FromRgb(29, 53, 87));
                SetBrush(app, "BrushInputBackground", Color.FromRgb(8, 19, 36));
                SetBrush(app, "BrushSidebarBackground", Color.FromRgb(7, 16, 31));
                SetBrush(app, "BrushSidebarItemActive", Color.FromRgb(14, 30, 56));
                SetBrush(app, "BrushSidebarText", Color.FromRgb(147, 197, 253));
                SetBrush(app, "BrushSidebarTextActive", Color.FromRgb(255, 255, 255));

                SetBrush(app, "BrushBorderSubtle", Color.FromRgb(29, 53, 87));
                SetBrush(app, "BrushBorderMedium", Color.FromRgb(46, 80, 125));
                SetBrush(app, "BrushBorderFocused", Color.FromRgb(2, 132, 199));

                SetBrush(app, "BrushPrimary", Color.FromRgb(2, 132, 199));
                SetBrush(app, "BrushPrimaryHover", Color.FromRgb(3, 105, 161));
                SetBrush(app, "BrushPrimaryActive", Color.FromRgb(7, 89, 133));
                SetBrush(app, "BrushPrimaryLight", Color.FromRgb(56, 189, 248));
                SetBrush(app, "BrushPrimaryGlow", Color.FromArgb(40, 2, 132, 199));

                SetBrush(app, "BrushTextPrimary", Color.FromRgb(241, 245, 249));
                SetBrush(app, "BrushTextSecondary", Color.FromRgb(147, 197, 253));
                SetBrush(app, "BrushTextMuted", Color.FromRgb(96, 165, 250));
                SetBrush(app, "BrushTextWhite", Color.FromRgb(255, 255, 255));

                SetBrush(app, "BrushSuccess", Color.FromRgb(16, 185, 129));
                SetBrush(app, "BrushSuccessHover", Color.FromRgb(5, 150, 105));
                SetBrush(app, "BrushSuccessBg", Color.FromRgb(6, 46, 36));
                SetBrush(app, "BrushSuccessText", Color.FromRgb(52, 211, 153));

                SetBrush(app, "BrushWarning", Color.FromRgb(245, 158, 11));
                SetBrush(app, "BrushWarningHover", Color.FromRgb(217, 119, 6));
                SetBrush(app, "BrushWarningBg", Color.FromRgb(59, 43, 17));
                SetBrush(app, "BrushWarningText", Color.FromRgb(251, 191, 36));

                SetBrush(app, "BrushDanger", Color.FromRgb(239, 68, 68));
                SetBrush(app, "BrushDangerHover", Color.FromRgb(220, 38, 38));
                SetBrush(app, "BrushDangerBg", Color.FromRgb(59, 24, 24));
                SetBrush(app, "BrushDangerText", Color.FromRgb(248, 113, 113));

                SetBrush(app, "BrushCyan", Color.FromRgb(56, 189, 248));
                SetBrush(app, "BrushViolet", Color.FromRgb(139, 92, 246));
                break;

            case AppTheme.ForestEmerald:
                SetBrush(app, "BrushAppBackground", Color.FromRgb(4, 20, 15));
                SetBrush(app, "BrushSurfaceBackground", Color.FromRgb(9, 34, 27));
                SetBrush(app, "BrushSurfaceElevated", Color.FromRgb(17, 53, 43));
                SetBrush(app, "BrushSurfaceLight", Color.FromRgb(27, 77, 62));
                SetBrush(app, "BrushInputBackground", Color.FromRgb(6, 27, 21));
                SetBrush(app, "BrushSidebarBackground", Color.FromRgb(5, 24, 18));
                SetBrush(app, "BrushSidebarItemActive", Color.FromRgb(12, 45, 36));
                SetBrush(app, "BrushSidebarText", Color.FromRgb(134, 239, 172));
                SetBrush(app, "BrushSidebarTextActive", Color.FromRgb(255, 255, 255));

                SetBrush(app, "BrushBorderSubtle", Color.FromRgb(27, 77, 62));
                SetBrush(app, "BrushBorderMedium", Color.FromRgb(39, 107, 87));
                SetBrush(app, "BrushBorderFocused", Color.FromRgb(16, 185, 129));

                SetBrush(app, "BrushPrimary", Color.FromRgb(16, 185, 129));
                SetBrush(app, "BrushPrimaryHover", Color.FromRgb(5, 150, 105));
                SetBrush(app, "BrushPrimaryActive", Color.FromRgb(4, 120, 87));
                SetBrush(app, "BrushPrimaryLight", Color.FromRgb(52, 211, 153));
                SetBrush(app, "BrushPrimaryGlow", Color.FromArgb(40, 16, 185, 129));

                SetBrush(app, "BrushTextPrimary", Color.FromRgb(240, 253, 244));
                SetBrush(app, "BrushTextSecondary", Color.FromRgb(134, 239, 172));
                SetBrush(app, "BrushTextMuted", Color.FromRgb(74, 222, 128));
                SetBrush(app, "BrushTextWhite", Color.FromRgb(255, 255, 255));

                SetBrush(app, "BrushSuccess", Color.FromRgb(16, 185, 129));
                SetBrush(app, "BrushSuccessHover", Color.FromRgb(5, 150, 105));
                SetBrush(app, "BrushSuccessBg", Color.FromRgb(6, 46, 36));
                SetBrush(app, "BrushSuccessText", Color.FromRgb(52, 211, 153));

                SetBrush(app, "BrushWarning", Color.FromRgb(245, 158, 11));
                SetBrush(app, "BrushWarningHover", Color.FromRgb(217, 119, 6));
                SetBrush(app, "BrushWarningBg", Color.FromRgb(59, 43, 17));
                SetBrush(app, "BrushWarningText", Color.FromRgb(251, 191, 36));

                SetBrush(app, "BrushDanger", Color.FromRgb(239, 68, 68));
                SetBrush(app, "BrushDangerHover", Color.FromRgb(220, 38, 38));
                SetBrush(app, "BrushDangerBg", Color.FromRgb(59, 24, 24));
                SetBrush(app, "BrushDangerText", Color.FromRgb(248, 113, 113));

                SetBrush(app, "BrushCyan", Color.FromRgb(52, 211, 153));
                SetBrush(app, "BrushViolet", Color.FromRgb(139, 92, 246));
                break;

            case AppTheme.RoyalPurple:
                SetBrush(app, "BrushAppBackground", Color.FromRgb(15, 7, 26));
                SetBrush(app, "BrushSurfaceBackground", Color.FromRgb(26, 13, 46));
                SetBrush(app, "BrushSurfaceElevated", Color.FromRgb(39, 20, 68));
                SetBrush(app, "BrushSurfaceLight", Color.FromRgb(61, 32, 104));
                SetBrush(app, "BrushInputBackground", Color.FromRgb(19, 9, 34));
                SetBrush(app, "BrushSidebarBackground", Color.FromRgb(17, 8, 30));
                SetBrush(app, "BrushSidebarItemActive", Color.FromRgb(33, 16, 58));
                SetBrush(app, "BrushSidebarText", Color.FromRgb(216, 180, 254));
                SetBrush(app, "BrushSidebarTextActive", Color.FromRgb(255, 255, 255));

                SetBrush(app, "BrushBorderSubtle", Color.FromRgb(61, 32, 104));
                SetBrush(app, "BrushBorderMedium", Color.FromRgb(91, 48, 153));
                SetBrush(app, "BrushBorderFocused", Color.FromRgb(168, 85, 247));

                SetBrush(app, "BrushPrimary", Color.FromRgb(168, 85, 247));
                SetBrush(app, "BrushPrimaryHover", Color.FromRgb(147, 51, 234));
                SetBrush(app, "BrushPrimaryActive", Color.FromRgb(126, 34, 206));
                SetBrush(app, "BrushPrimaryLight", Color.FromRgb(192, 132, 252));
                SetBrush(app, "BrushPrimaryGlow", Color.FromArgb(40, 168, 85, 247));

                SetBrush(app, "BrushTextPrimary", Color.FromRgb(250, 245, 255));
                SetBrush(app, "BrushTextSecondary", Color.FromRgb(216, 180, 254));
                SetBrush(app, "BrushTextMuted", Color.FromRgb(192, 132, 252));
                SetBrush(app, "BrushTextWhite", Color.FromRgb(255, 255, 255));

                SetBrush(app, "BrushSuccess", Color.FromRgb(16, 185, 129));
                SetBrush(app, "BrushSuccessHover", Color.FromRgb(5, 150, 105));
                SetBrush(app, "BrushSuccessBg", Color.FromRgb(6, 46, 36));
                SetBrush(app, "BrushSuccessText", Color.FromRgb(52, 211, 153));

                SetBrush(app, "BrushWarning", Color.FromRgb(245, 158, 11));
                SetBrush(app, "BrushWarningHover", Color.FromRgb(217, 119, 6));
                SetBrush(app, "BrushWarningBg", Color.FromRgb(59, 43, 17));
                SetBrush(app, "BrushWarningText", Color.FromRgb(251, 191, 36));

                SetBrush(app, "BrushDanger", Color.FromRgb(239, 68, 68));
                SetBrush(app, "BrushDangerHover", Color.FromRgb(220, 38, 38));
                SetBrush(app, "BrushDangerBg", Color.FromRgb(59, 24, 24));
                SetBrush(app, "BrushDangerText", Color.FromRgb(248, 113, 113));

                SetBrush(app, "BrushCyan", Color.FromRgb(192, 132, 252));
                SetBrush(app, "BrushViolet", Color.FromRgb(168, 85, 247));
                break;

            default:
                SetBrush(app, "BrushAppBackground", Color.FromRgb(11, 15, 25));
                SetBrush(app, "BrushSurfaceBackground", Color.FromRgb(21, 29, 46));
                SetBrush(app, "BrushSurfaceElevated", Color.FromRgb(30, 41, 59));
                SetBrush(app, "BrushSurfaceLight", Color.FromRgb(41, 53, 72));
                SetBrush(app, "BrushInputBackground", Color.FromRgb(15, 23, 42));
                SetBrush(app, "BrushSidebarBackground", Color.FromRgb(13, 19, 34));
                SetBrush(app, "BrushSidebarItemActive", Color.FromRgb(25, 36, 58));
                SetBrush(app, "BrushSidebarText", Color.FromRgb(148, 163, 184));
                SetBrush(app, "BrushSidebarTextActive", Color.FromRgb(255, 255, 255));

                SetBrush(app, "BrushBorderSubtle", Color.FromRgb(42, 55, 78));
                SetBrush(app, "BrushBorderMedium", Color.FromRgb(59, 77, 104));
                SetBrush(app, "BrushBorderFocused", Color.FromRgb(99, 102, 241));

                SetBrush(app, "BrushPrimary", Color.FromRgb(99, 102, 241));
                SetBrush(app, "BrushPrimaryHover", Color.FromRgb(79, 70, 229));
                SetBrush(app, "BrushPrimaryActive", Color.FromRgb(67, 56, 202));
                SetBrush(app, "BrushPrimaryLight", Color.FromRgb(129, 140, 248));
                SetBrush(app, "BrushPrimaryGlow", Color.FromArgb(40, 99, 102, 241));

                SetBrush(app, "BrushTextPrimary", Color.FromRgb(248, 250, 252));
                SetBrush(app, "BrushTextSecondary", Color.FromRgb(148, 163, 184));
                SetBrush(app, "BrushTextMuted", Color.FromRgb(100, 116, 139));
                SetBrush(app, "BrushTextWhite", Color.FromRgb(255, 255, 255));

                SetBrush(app, "BrushSuccess", Color.FromRgb(16, 185, 129));
                SetBrush(app, "BrushSuccessHover", Color.FromRgb(5, 150, 105));
                SetBrush(app, "BrushSuccessBg", Color.FromRgb(21, 56, 43));
                SetBrush(app, "BrushSuccessText", Color.FromRgb(52, 211, 153));

                SetBrush(app, "BrushWarning", Color.FromRgb(245, 158, 11));
                SetBrush(app, "BrushWarningHover", Color.FromRgb(217, 119, 6));
                SetBrush(app, "BrushWarningBg", Color.FromRgb(59, 43, 17));
                SetBrush(app, "BrushWarningText", Color.FromRgb(251, 191, 36));

                SetBrush(app, "BrushDanger", Color.FromRgb(239, 68, 68));
                SetBrush(app, "BrushDangerHover", Color.FromRgb(220, 38, 38));
                SetBrush(app, "BrushDangerBg", Color.FromRgb(59, 24, 24));
                SetBrush(app, "BrushDangerText", Color.FromRgb(248, 113, 113));

                SetBrush(app, "BrushCyan", Color.FromRgb(6, 182, 212));
                SetBrush(app, "BrushViolet", Color.FromRgb(139, 92, 246));
                break;
        }
    }

    private static void SetBrush(Application app, string key, Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        app.Resources[key] = brush;
    }
}
