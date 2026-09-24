using System.Drawing;

namespace ScreenTranslator.Models;

/// <summary>One recognized word. Rect is in pixels relative to the captured image.</summary>
public sealed record OcrWord(string Text, Rectangle Rect);

/// <summary>One recognized line of text. Rect is in pixels relative to the captured image.</summary>
public sealed record OcrLine(string Text, Rectangle Rect, IReadOnlyList<OcrWord>? Words = null);

/// <summary>A paragraph: consecutive lines that visually belong together. Rect is the union of its lines.</summary>
public sealed record OcrBlock(string Text, Rectangle Rect, int LineCount, double LineHeight);

public sealed record OcrPage(IReadOnlyList<OcrLine> Lines, TimeSpan Elapsed)
{
    public static readonly OcrPage Empty = new(Array.Empty<OcrLine>(), TimeSpan.Zero);

    /// <summary>The word whose box contains the point, or the nearest word on the same line within a small tolerance.</summary>
    public OcrWord? WordAt(Point p, int tolerance = 6)
    {
        OcrWord? best = null;
        double bestDist = double.MaxValue;
        foreach (var line in Lines)
        {
            if (line.Words is null) continue;
            var lr = line.Rect; lr.Inflate(tolerance, tolerance);
            if (!lr.Contains(p)) continue;
            foreach (var w in line.Words)
            {
                if (w.Rect.Contains(p)) return w;
                double dx = p.X < w.Rect.Left ? w.Rect.Left - p.X : p.X > w.Rect.Right ? p.X - w.Rect.Right : 0;
                if (dx < bestDist && dx <= tolerance * 2) { bestDist = dx; best = w; }
            }
        }
        return best;
    }
}

public sealed record TranslationResult(string Text, string SourceLanguage, string Engine, bool FromCache, TimeSpan Elapsed);
