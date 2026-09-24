using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using ScreenTranslator.Models;
using OcrLine = ScreenTranslator.Models.OcrLine;
using OcrWord = ScreenTranslator.Models.OcrWord;

namespace ScreenTranslator.Services;

/// <summary>Wraps the built-in Windows OCR engine (offline, CPU-only).</summary>
public sealed class OcrService
{
    private readonly OcrEngine _engine;

    public string LanguageTag => _engine.RecognizerLanguage.LanguageTag;

    public static IReadOnlyList<string> AvailableLanguages =>
        OcrEngine.AvailableRecognizerLanguages.Select(l => l.LanguageTag).ToList();

    public OcrService(string? languageTag = null)
    {
        OcrEngine? engine = null;
        if (!string.IsNullOrWhiteSpace(languageTag) && Language.IsWellFormed(languageTag))
            engine = OcrEngine.TryCreateFromLanguage(new Language(languageTag));

        engine ??= OcrEngine.TryCreateFromUserProfileLanguages();
        engine ??= OcrEngine.TryCreateFromLanguage(new Language("en-US"));

        _engine = engine ?? throw new InvalidOperationException(
            "No Windows OCR language pack is installed. Install one via Settings > Time & Language > Language.");
    }

    public async Task<OcrPage> RecognizeAsync(Bitmap bitmap)
    {
        var sw = Stopwatch.StartNew();

        // Windows OCR likes text ~20-40px tall. Small captures get upscaled; large ones stay as-is.
        double scale = ChooseScale(bitmap.Width, bitmap.Height);
        using var prepared = scale > 1.0 ? Resize(bitmap, scale) : (Bitmap)bitmap.Clone();

        using var softwareBitmap = ToSoftwareBitmap(prepared);
        var result = await _engine.RecognizeAsync(softwareBitmap);

        var lines = new List<OcrLine>();
        foreach (var line in result.Lines)
        {
            if (line.Words.Count == 0) continue;

            double l = double.MaxValue, t = double.MaxValue, r = double.MinValue, b = double.MinValue;
            var words = new List<OcrWord>(line.Words.Count);
            foreach (var w in line.Words)
            {
                var wr = w.BoundingRect;
                l = Math.Min(l, wr.X);
                t = Math.Min(t, wr.Y);
                r = Math.Max(r, wr.X + wr.Width);
                b = Math.Max(b, wr.Y + wr.Height);

                var wt = w.Text.Trim();
                if (wt.Length > 0)
                    words.Add(new OcrWord(wt, Unscale(wr.X, wr.Y, wr.X + wr.Width, wr.Y + wr.Height, scale)));
            }

            var rect = Unscale(l, t, r, b, scale);

            var text = line.Text.Trim();
            if (text.Length > 0)
                lines.Add(new OcrLine(text, rect, words));
        }

        return new OcrPage(lines, sw.Elapsed);
    }

    private static Rectangle Unscale(double l, double t, double r, double b, double scale) => Rectangle.FromLTRB(
        (int)Math.Floor(l / scale), (int)Math.Floor(t / scale),
        (int)Math.Ceiling(r / scale), (int)Math.Ceiling(b / scale));

    private static double ChooseScale(int width, int height)
    {
        int longest = Math.Max(width, height);
        double scale = longest < 400 ? 3.0 : longest < 900 ? 2.0 : longest < 1400 ? 1.5 : 1.0;

        // Keep within the engine's hard limit.
        uint max = OcrEngine.MaxImageDimension;
        if (longest * scale > max) scale = Math.Max(1.0, (double)max / longest);
        return scale;
    }

    private static Bitmap Resize(Bitmap src, double scale)
    {
        int w = (int)Math.Round(src.Width * scale);
        int h = (int)Math.Round(src.Height * scale);
        var dst = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(dst);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.SmoothingMode = SmoothingMode.HighQuality;
        g.DrawImage(src, new Rectangle(0, 0, w, h), new Rectangle(0, 0, src.Width, src.Height), GraphicsUnit.Pixel);
        return dst;
    }

    private static SoftwareBitmap ToSoftwareBitmap(Bitmap bmp)
    {
        var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
        var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            int stride = data.Stride;
            int tight = bmp.Width * 4;
            var bytes = new byte[tight * bmp.Height];

            if (stride == tight)
            {
                Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
            }
            else
            {
                for (int y = 0; y < bmp.Height; y++)
                    Marshal.Copy(data.Scan0 + y * stride, bytes, y * tight, tight);
            }

            return SoftwareBitmap.CreateCopyFromBuffer(bytes.AsBuffer(), BitmapPixelFormat.Bgra8,
                bmp.Width, bmp.Height, BitmapAlphaMode.Ignore);
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }
}
