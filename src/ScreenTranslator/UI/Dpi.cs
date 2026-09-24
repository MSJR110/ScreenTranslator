using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using DrawingPoint = System.Drawing.Point;
using DrawingRectangle = System.Drawing.Rectangle;

namespace ScreenTranslator.UI;

/// <summary>
/// Pixel ⇄ DIP conversion. Every monitor can have its own scale, so callers pass the on-screen
/// location they care about; the primary-monitor <see cref="Scale"/> remains as a fallback.
/// </summary>
public static class Dpi
{
    private const int MONITOR_DEFAULTTONEAREST = 2;
    private const int MDT_EFFECTIVE_DPI = 0;

    [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(DrawingPoint pt, int flags);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromRect(ref RECT rect, int flags);
    [DllImport("shcore.dll")] private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    private const uint SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010, SWP_NOOWNERZORDER = 0x0200;

    private static double? _primary;

    /// <summary>Primary monitor scale (px per DIP).</summary>
    public static double Scale
    {
        get
        {
            if (_primary is null)
            {
                using var g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero);
                _primary = g.DpiX / 96.0;
            }
            return _primary.Value;
        }
    }

    public static void Reset() => _primary = null;

    /// <summary>Scale of the monitor under a physical-pixel point.</summary>
    public static double ScaleAt(int px, int py)
    {
        try
        {
            var mon = MonitorFromPoint(new DrawingPoint(px, py), MONITOR_DEFAULTTONEAREST);
            if (mon != IntPtr.Zero && GetDpiForMonitor(mon, MDT_EFFECTIVE_DPI, out var dx, out _) == 0)
                return dx / 96.0;
        }
        catch { }
        return Scale;
    }

    /// <summary>Scale of the monitor that contains most of a physical-pixel rectangle.</summary>
    public static double ScaleFor(DrawingRectangle px)
    {
        try
        {
            var r = new RECT { Left = px.Left, Top = px.Top, Right = px.Right, Bottom = px.Bottom };
            var mon = MonitorFromRect(ref r, MONITOR_DEFAULTTONEAREST);
            if (mon != IntPtr.Zero && GetDpiForMonitor(mon, MDT_EFFECTIVE_DPI, out var dx, out _) == 0)
                return dx / 96.0;
        }
        catch { }
        return Scale;
    }

    public static Rect ToDip(DrawingRectangle px)
    {
        double s = ScaleFor(px);
        return new Rect(px.X / s, px.Y / s, px.Width / s, px.Height / s);
    }

    public static Point ToDip(int x, int y)
    {
        double s = ScaleAt(x, y);
        return new Point(x / s, y / s);
    }

    public static DrawingRectangle ToPixels(Rect dip)
    {
        double s = Scale;
        var guess = new DrawingRectangle((int)Math.Round(dip.X * s), (int)Math.Round(dip.Y * s),
            Math.Max(1, (int)Math.Round(dip.Width * s)), Math.Max(1, (int)Math.Round(dip.Height * s)));
        double s2 = ScaleFor(guess);
        if (Math.Abs(s2 - s) < 0.001) return guess;
        return new DrawingRectangle((int)Math.Round(dip.X * s2), (int)Math.Round(dip.Y * s2),
            Math.Max(1, (int)Math.Round(dip.Width * s2)), Math.Max(1, (int)Math.Round(dip.Height * s2)));
    }

    /// <summary>
    /// Position and size a window in physical pixels, bypassing WPF's DIP interpretation (which assumes the
    /// DPI of whichever monitor the window happened to be created on).
    /// </summary>
    public static void SetPixelBounds(Window w, DrawingRectangle px)
    {
        var h = new WindowInteropHelper(w).EnsureHandle();
        SetWindowPos(h, IntPtr.Zero, px.X, px.Y, px.Width, px.Height, SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOOWNERZORDER);
    }

    /// <summary>Move only (keeps the current size).</summary>
    public static void SetPixelPosition(Window w, int x, int y)
    {
        const uint SWP_NOSIZE = 0x0001;
        var h = new WindowInteropHelper(w).EnsureHandle();
        SetWindowPos(h, IntPtr.Zero, x, y, 0, 0, SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOOWNERZORDER | SWP_NOSIZE);
    }
}
