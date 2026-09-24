using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace ScreenTranslator.Native;

/// <summary>Win32/DWM tweaks that WPF doesn't expose: click-through, capture exclusion, Windows 11 acrylic.</summary>
internal static class WindowEffects
{
    public static IntPtr Handle(Window w) => new WindowInteropHelper(w).EnsureHandle();

    /// <summary>Mouse events pass straight through the window; it never takes focus and never appears in Alt+Tab.</summary>
    public static void MakeClickThrough(Window w)
    {
        var h = Handle(w);
        int ex = NativeMethods.GetWindowLong(h, NativeMethods.GWL_EXSTYLE);
        ex |= NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE;
        NativeMethods.SetWindowLong(h, NativeMethods.GWL_EXSTYLE, ex);
    }

    public static void HideFromAltTab(Window w)
    {
        var h = Handle(w);
        int ex = NativeMethods.GetWindowLong(h, NativeMethods.GWL_EXSTYLE);
        ex |= NativeMethods.WS_EX_TOOLWINDOW;
        NativeMethods.SetWindowLong(h, NativeMethods.GWL_EXSTYLE, ex);
    }

    /// <summary>The window stays visible on screen but is left out of screen captures (so live mode never OCRs itself).</summary>
    public static bool ExcludeFromCapture(Window w)
        => NativeMethods.SetWindowDisplayAffinity(Handle(w), NativeMethods.WDA_EXCLUDEFROMCAPTURE);

    /// <summary>
    /// Windows 11 acrylic backdrop with rounded corners. Requires a non-layered window (AllowsTransparency=false)
    /// whose background is transparent. Returns false on older Windows so callers can fall back to a painted background.
    /// </summary>
    public static bool TryApplyAcrylic(Window w, bool dark)
    {
        if (Environment.OSVersion.Version.Build < 22621) return false;

        var h = Handle(w);
        var source = HwndSource.FromHwnd(h);
        if (source?.CompositionTarget is { } target)
            target.BackgroundColor = Colors.Transparent;

        int corner = NativeMethods.DWMWCP_ROUND;
        NativeMethods.DwmSetWindowAttribute(h, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));

        int darkMode = dark ? 1 : 0;
        NativeMethods.DwmSetWindowAttribute(h, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

        int backdrop = NativeMethods.DWMSBT_TRANSIENTWINDOW;
        int hr = NativeMethods.DwmSetWindowAttribute(h, NativeMethods.DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
        return hr == 0;
    }

    public static void SetRoundedCorners(Window w)
    {
        if (Environment.OSVersion.Version.Build < 22000) return;
        int corner = NativeMethods.DWMWCP_ROUND;
        NativeMethods.DwmSetWindowAttribute(Handle(w), NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));
    }

    /// <summary>True when Windows apps are set to the light theme.</summary>
    public static bool IsSystemLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v != 0;
        }
        catch { return false; }
    }

    /// <summary>The user's Windows accent color, for highlights.</summary>
    public static Color AccentColor()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
            if (key?.GetValue("AccentColor") is int abgr)
            {
                byte a = (byte)((abgr >> 24) & 0xFF), b = (byte)((abgr >> 16) & 0xFF), g = (byte)((abgr >> 8) & 0xFF), r = (byte)(abgr & 0xFF);
                if (a == 0) a = 255;
                return Color.FromArgb(a, r, g, b);
            }
        }
        catch { }
        return Color.FromRgb(0x4D, 0xA3, 0xFF);
    }

    /// <summary>Visible bounds of a top-level window (excludes the invisible resize border), in physical pixels.</summary>
    public static System.Drawing.Rectangle? GetWindowBounds(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || NativeMethods.IsIconic(hwnd)) return null;

        if (NativeMethods.DwmGetWindowAttribute(hwnd, NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS, out var r, System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.RECT>()) != 0)
        {
            if (!NativeMethods.GetWindowRect(hwnd, out r)) return null;
        }

        if (r.Width <= 0 || r.Height <= 0) return null;
        return new System.Drawing.Rectangle(r.Left, r.Top, r.Width, r.Height);
    }
}
