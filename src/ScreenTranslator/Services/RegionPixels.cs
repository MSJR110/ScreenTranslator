using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ScreenTranslator.Services;

/// <summary>
/// A captured region's pixels, kept around so the live overlay can read colours, stroke weight and background
/// texture straight from what was on screen. Immutable after construction, so it is safe to hand across threads.
/// </summary>
public sealed class RegionPixels
{
    private readonly byte[] _px;
    private readonly int _stride;

    public int Width { get; }
    public int Height { get; }

    public RegionPixels(Bitmap bmp)
    {
        Width = bmp.Width; Height = bmp.Height;
        var data = bmp.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            _stride = data.Stride;
            _px = new byte[_stride * Height];
            Marshal.Copy(data.Scan0, _px, 0, _px.Length);
        }
        finally { bmp.UnlockBits(data); }
    }

    public (int r, int g, int b) At(int x, int y)
    {
        x = Math.Clamp(x, 0, Width - 1); y = Math.Clamp(y, 0, Height - 1);
        int o = y * _stride + x * 4;
        return (_px[o + 2], _px[o + 1], _px[o]);
    }

    /// <summary>Background and glyph colour of a text box.</summary>
    public (Color bg, Color fg) Colors(Rectangle rect)
    {
        // Background: per-channel median of a thin ring just outside the box. The ring carries no glyphs, and a
        // median survives both gradients (where no single colour dominates) and a border the ring happens to cross.
        var ring = new List<(int r, int g, int b)>();
        int pad = 3;
        for (int x = rect.Left - pad; x <= rect.Right + pad; x += 2)
        {
            ring.Add(At(x, rect.Top - pad));
            ring.Add(At(x, rect.Bottom + pad));
        }
        for (int y = rect.Top; y < rect.Bottom; y += 2)
        {
            ring.Add(At(rect.Left - pad, y));
            ring.Add(At(rect.Right + pad, y));
        }
        var bg = Median(ring);

        // Foreground: the pixels inside that differ most from the background — the solid cores of the glyphs,
        // not their anti-aliased fringes, which would wash the colour out.
        var ink = new List<((int r, int g, int b) c, int d)>();
        int stepX = Math.Max(1, rect.Width / 160), stepY = Math.Max(1, rect.Height / 40);
        for (int y = rect.Top; y < rect.Bottom; y += stepY)
            for (int x = rect.Left; x < rect.Right; x += stepX)
            {
                var p = At(x, y);
                int d = Distance(p, bg);
                if (d > 90) ink.Add((p, d));
            }

        Color fgColor;
        if (ink.Count >= 4)
        {
            ink.Sort((a, b) => b.d.CompareTo(a.d));
            int take = Math.Max(4, ink.Count / 3);
            long sr = 0, sg = 0, sb = 0;
            for (int i = 0; i < take; i++) { sr += ink[i].c.r; sg += ink[i].c.g; sb += ink[i].c.b; }
            fgColor = Color.FromArgb((int)(sr / take), (int)(sg / take), (int)(sb / take));
            // Guarantee legibility even if sampling picked up anti-aliasing.
            if (Contrast(fgColor, bg) < 3.0)
                fgColor = Luminance(bg) > 0.5 ? Color.FromArgb(20, 20, 20) : Color.White;
        }
        else
        {
            fgColor = Luminance(bg) > 0.5 ? Color.FromArgb(20, 20, 20) : Color.White;
        }

        return (Color.FromArgb(bg.r, bg.g, bg.b), fgColor);
    }

    private static (int r, int g, int b) Median(List<(int r, int g, int b)> px)
    {
        var r = px.Select(p => p.r).OrderBy(v => v).ToList();
        var g = px.Select(p => p.g).OrderBy(v => v).ToList();
        var b = px.Select(p => p.b).OrderBy(v => v).ToList();
        int m = px.Count / 2;
        return (r[m], g[m], b[m]);
    }

    /// <summary>
    /// Typical stem thickness of the glyphs relative to the line height: ~0.03–0.08 for regular text, ~0.12+ for bold.
    /// Runs of ink through the middle band of each line are weighted by coverage, so anti-aliased 1.3px stems and
    /// solid 2px stems come apart even at small sizes; the 40th percentile favours vertical stems over crossbars.
    /// </summary>
    public double StrokeRatio(Rectangle rect, int lineCount, Color bg, Color fg)
    {
        var bgT = (bg.R, bg.G, bg.B);
        double full = Math.Max(60, Distance((fg.R, fg.G, fg.B), bgT));
        double lineHeight = rect.Height / (double)Math.Max(1, lineCount);
        if (lineHeight < 6) return 0;

        var runs = new List<double>();
        for (int line = 0; line < lineCount; line++)
        {
            int top = rect.Top + (int)(line * lineHeight);
            int y0 = top + (int)(lineHeight * 0.35), y1 = top + (int)(lineHeight * 0.65);
            for (int y = y0; y <= y1; y++)
            {
                double weight = 0; int len = 0;
                for (int x = rect.Left; x <= rect.Right; x++)
                {
                    double cover = x < rect.Right ? Math.Clamp(Distance(At(x, y), bgT) / full, 0, 1) : 0;
                    if (cover > 0.25) { weight += cover; len++; }
                    else if (len > 0) { runs.Add(Math.Min(weight, lineHeight)); weight = 0; len = 0; }
                }
            }
        }

        if (runs.Count < 12) return 0;
        runs.Sort();
        // Anti-aliasing adds roughly half a pixel of coverage to every stem regardless of size; take it off
        // so small regular text is not mistaken for bold.
        return Math.Max(0, runs[(int)(runs.Count * 0.4)] - 0.6) / lineHeight;
    }

    /// <summary>
    /// True when the pixels just above and below the text are high-frequency (a photo or pattern) rather than a flat
    /// or smoothly graded surface, so a box would look like a patch and an outlined caption is the better choice.
    /// </summary>
    public bool IsBusy(Rectangle rect)
    {
        int jumpy = 0, total = 0;
        foreach (int y in new[] { rect.Top - 3, rect.Bottom + 3 })
        {
            var prev = At(rect.Left, y);
            for (int x = rect.Left + 1; x < rect.Right; x++)
            {
                var p = At(x, y);
                if (Distance(p, prev) > 40) jumpy++;
                total++;
                prev = p;
            }
        }
        return total > 20 && jumpy > total * 0.18;
    }

    /// <summary>A colour jump between neighbouring columns bigger than this is an edge, not a gradient.</summary>
    private const int EdgeJump = 40;

    private (int r, int g, int b) Above(Rectangle src, int x) => Avg(At(x, src.Top - 2), At(x, src.Top - 3));
    private (int r, int g, int b) Below(Rectangle src, int x) => Avg(At(x, src.Bottom + 1), At(x, src.Bottom + 2));

    /// <summary>
    /// How far the surface under a line continues to the left and right of it before hitting an edge (card border,
    /// button outline, another element) on both sides of the text — the pixel columns [left, right) a box may occupy.
    /// </summary>
    public (int left, int right) SurfaceExtent(Rectangle src)
    {
        int Walk(int from, int step, int limit)
        {
            var prevA = Above(src, from); var prevB = Below(src, from);
            int x = from;
            while (x + step >= 0 && x + step < Width && Math.Abs(x + step - from) < limit)
            {
                x += step;
                var a = Above(src, x); var b = Below(src, x);
                if (Distance(a, prevA) > EdgeJump && Distance(b, prevB) > EdgeJump) return x;   // edge on both rows
                prevA = a; prevB = b;
            }
            return x + step;
        }

        int reach = src.Width + 200;
        int left = Walk(src.Left, -1, reach) + 1;
        int right = Walk(Math.Max(src.Left, src.Right - 1), +1, reach);
        return (Math.Max(0, left), Math.Min(Width, right));
    }

    /// <summary>
    /// A two-row texture (row 0 = just above the text, row 1 = just below) for pixel columns [x0, x1), as BGRA bytes.
    /// Stretched over the overlay box it reproduces horizontal and vertical gradients of the surface underneath.
    /// The rows are read outward from the middle of the text; where one row runs into something (an icon, a rule)
    /// the other row stands in for it, so the box never picks up a stripe of a neighbouring element.
    /// </summary>
    public byte[] EdgeTexture(Rectangle src, int x0, int x1, Color bg)
    {
        int w = Math.Max(1, x1 - x0);
        var bytes = new byte[w * 4 * 2];
        int mid = Math.Clamp(src.Left + src.Width / 2, x0, x1 - 1);
        var bgT = (bg.R, bg.G, bg.B);

        // Reference colours at the middle of the line. If the two rows already disagree there (a heading sitting
        // on its underline), the row that matches the surface colour stands in for both.
        var startA = Above(src, mid); var startB = Below(src, mid);
        if (Distance(startA, startB) > 60)
        {
            if (Distance(startA, bgT) <= Distance(startB, bgT)) startB = startA; else startA = startB;
        }

        void Fill(int from, int step)
        {
            var prevA = startA; var prevB = startB;
            for (int x = from; x >= x0 && x < x1; x += step)
            {
                var a = Above(src, x); var b = Below(src, x);
                if (Distance(a, b) > 60)
                {
                    bool aOk = Distance(a, prevA) <= EdgeJump, bOk = Distance(b, prevB) <= EdgeJump;
                    if (aOk && !bOk) b = a;
                    else if (bOk && !aOk) a = b;
                    else if (!aOk && !bOk) { a = prevA; b = prevB; }
                }
                prevA = a; prevB = b;
                Put(bytes, (x - x0) * 4, a);
                Put(bytes, w * 4 + (x - x0) * 4, b);
            }
        }

        Fill(mid, +1);
        Fill(mid, -1);
        return bytes;
    }

    private static void Put(byte[] dst, int o, (int r, int g, int b) c)
    {
        dst[o] = (byte)c.b; dst[o + 1] = (byte)c.g; dst[o + 2] = (byte)c.r; dst[o + 3] = 255;
    }

    private static (int r, int g, int b) Avg((int r, int g, int b) a, (int r, int g, int b) b)
        => ((a.r + b.r) / 2, (a.g + b.g) / 2, (a.b + b.b) / 2);

    private static int Distance((int r, int g, int b) a, (int r, int g, int b) b)
        => Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b);

    public static double Luminance(Color c) => Luminance((c.R, c.G, c.B));
    private static double Luminance((int r, int g, int b) c) => Luminance(c.r, c.g, c.b);
    private static double Luminance(int r, int g, int b)
    {
        static double Lin(int v) { double s = v / 255.0; return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }
        return 0.2126 * Lin(r) + 0.7152 * Lin(g) + 0.0722 * Lin(b);
    }

    private static double Contrast(Color fg, (int r, int g, int b) bg)
    {
        double l1 = Luminance(fg.R, fg.G, fg.B), l2 = Luminance(bg);
        return (Math.Max(l1, l2) + 0.05) / (Math.Min(l1, l2) + 0.05);
    }
}
