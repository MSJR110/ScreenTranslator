using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using DrawingIcon = System.Drawing.Icon;

namespace ScreenTranslator.UI;

/// <summary>The app logo, from embedded resources, in the two flavors WPF and WinForms want.</summary>
public static class AppIcon
{
    private static BitmapImage? _bitmap;
    private static DrawingIcon? _icon;

    public static BitmapImage Bitmap
    {
        get
        {
            if (_bitmap is null)
            {
                var img = new BitmapImage();
                img.BeginInit();
                img.UriSource = new Uri("pack://application:,,,/Assets/logo.png");
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.EndInit();
                img.Freeze();
                _bitmap = img;
            }
            return _bitmap;
        }
    }

    public static DrawingIcon Icon
    {
        get
        {
            if (_icon is null)
            {
                using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"))!.Stream;
                _icon = new DrawingIcon(stream);
            }
            return _icon;
        }
    }

    /// <summary>Tray-sized logo with a status dot in the corner (live mode).</summary>
    public static DrawingIcon WithDot(System.Drawing.Color dot)
    {
        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"))!.Stream;
        using var src = new DrawingIcon(stream, 32, 32);
        using var bmp = src.ToBitmap();
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.FillEllipse(System.Drawing.Brushes.White, new System.Drawing.Rectangle(bmp.Width - 14, bmp.Height - 14, 13, 13));
            using var b = new System.Drawing.SolidBrush(dot);
            g.FillEllipse(b, new System.Drawing.Rectangle(bmp.Width - 12, bmp.Height - 12, 9, 9));
        }
        IntPtr h = bmp.GetHicon();
        using var tmp = DrawingIcon.FromHandle(h);
        var result = (DrawingIcon)tmp.Clone();
        Native.NativeMethods.DestroyIcon(h);
        return result;
    }
}
