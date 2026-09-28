using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Parking.Core.Helpers;

public static class WindowIconHelper
{
    private const uint WM_SETICON = 0x0080;
    private static readonly IntPtr ICON_SMALL = new IntPtr(0);
    private static readonly IntPtr ICON_BIG = new IntPtr(1);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

    public static void EnsureWindowIcon(Window window)
    {
        if (window == null) return;

        if (window.IsLoaded)
        {
            ApplyIcon(window);
        }
        else
        {
            window.SourceInitialized += (s, e) => ApplyIcon(window);
        }
    }

    private static void ApplyIcon(Window window)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            // 1. Intentar cargar directamente desde el recurso embebido parkpoint.ico
            try
            {
                var iconUri = new Uri("pack://application:,,,/Parking;component/Resources/parkpoint.ico", UriKind.Absolute);
                var streamInfo = Application.GetResourceStream(iconUri);
                if (streamInfo?.Stream != null)
                {
                    using var stream = streamInfo.Stream;
                    using var icon = new System.Drawing.Icon(stream);
                    SendMessage(hwnd, WM_SETICON, ICON_SMALL, icon.Handle);
                    SendMessage(hwnd, WM_SETICON, ICON_BIG, icon.Handle);
                    return;
                }
            }
            catch { }

            // 2. Fallback: Extraer el icono embebido del ejecutable de la aplicación
            var exePath = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                var hIcon = ExtractIcon(IntPtr.Zero, exePath, 0);
                if (hIcon != IntPtr.Zero)
                {
                    SendMessage(hwnd, WM_SETICON, ICON_SMALL, hIcon);
                    SendMessage(hwnd, WM_SETICON, ICON_BIG, hIcon);
                }
            }
        }
        catch { }
    }
}
