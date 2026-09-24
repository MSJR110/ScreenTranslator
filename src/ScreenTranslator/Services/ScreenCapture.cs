using System.Drawing;
using System.Drawing.Imaging;

namespace ScreenTranslator.Services;

public static class ScreenCapture
{
    /// <summary>Capture a screen region given in physical pixels.</summary>
    public static Bitmap Capture(Rectangle pixelRect)
    {
        var bmp = new Bitmap(pixelRect.Width, pixelRect.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(pixelRect.Left, pixelRect.Top, 0, 0, pixelRect.Size, CopyPixelOperation.SourceCopy);
        return bmp;
    }
}
