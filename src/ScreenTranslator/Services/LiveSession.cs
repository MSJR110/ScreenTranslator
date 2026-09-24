using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using ScreenTranslator.Models;
using ScreenTranslator.Services.Translation;

namespace ScreenTranslator.Services;

/// <param name="Bold">The original text is set in a heavy weight.</param>
/// <param name="Busy">The surface under the text is a photo/pattern, so the overlay outlines the text instead of boxing it.</param>
/// <param name="StrokeRatio">Measured stem thickness / line height (diagnostics).</param>
public sealed record LiveBlock(OcrBlock Source, string Translation, Color Background, Color Foreground, bool Bold, bool Busy, double StrokeRatio);

public sealed record LiveFrame(Rectangle Region, IReadOnlyList<LiveBlock> Blocks, RegionPixels Pixels, TimeSpan OcrTime, TimeSpan TranslateTime, int NetworkRequests);

/// <summary>
/// Live mode: captures a screen region at a low rate, re-OCRs only when the pixels actually changed,
/// translates whatever isn't cached yet, and hands finished frames to the overlay.
/// </summary>
public sealed class LiveSession : IDisposable
{
    private readonly Func<Rectangle?> _regionProvider;
    private readonly OcrService _ocr;
    private readonly ITranslator _translator;
    private readonly string _targetLanguage;
    private readonly Dispatcher _dispatcher;
    private readonly CancellationTokenSource _cts = new();

    private byte[]? _lastThumb;
    private Rectangle _lastRegion;
    private string? _lastSignature;
    private volatile bool _paused;

    public TimeSpan Interval { get; set; } = TimeSpan.FromMilliseconds(700);

    /// <summary>How a region is captured; demos swap in a static image.</summary>
    public Func<Rectangle, Bitmap> CaptureProvider { get; set; } = ScreenCapture.Capture;

    public bool IsPaused
    {
        get => _paused;
        set { _paused = value; if (!value) _lastThumb = null; } // force a refresh on resume
    }

    public int FramesAnalyzed { get; private set; }
    public int NetworkRequests { get; private set; }

    /// <summary>Raised on the dispatcher thread with a fresh set of blocks (only when something changed).</summary>
    public event Action<LiveFrame>? FrameReady;

    /// <summary>Raised on the dispatcher thread when the tracked region moved/resized, or became unavailable (null).</summary>
    public event Action<Rectangle?>? RegionChanged;

    public event Action<Exception>? Error;

    public LiveSession(Func<Rectangle?> regionProvider, OcrService ocr, ITranslator translator, string targetLanguage, Dispatcher dispatcher)
    {
        _regionProvider = regionProvider;
        _ocr = ocr;
        _translator = translator;
        _targetLanguage = targetLanguage;
        _dispatcher = dispatcher;
    }

    public void Start() => _ = Task.Run(LoopAsync);

    /// <summary>Forget the last frame so the next tick re-analyzes even if pixels didn't change.</summary>
    public void Invalidate() { _lastThumb = null; _lastSignature = null; }

    private async Task LoopAsync()
    {
        var ct = _cts.Token;
        while (!ct.IsCancellationRequested)
        {
            var tickStart = Stopwatch.GetTimestamp();
            try
            {
                if (!_paused)
                    await TickAsync(ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _ = _dispatcher.InvokeAsync(() => Error?.Invoke(ex));
                await Task.Delay(1500, ct).ContinueWith(_ => { });
            }

            var spent = Stopwatch.GetElapsedTime(tickStart);
            var wait = Interval - spent;
            if (wait > TimeSpan.Zero)
            {
                try { await Task.Delay(wait, ct); } catch (OperationCanceledException) { break; }
            }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        var region = _regionProvider();
        if (region is null || region.Value.Width < 8 || region.Value.Height < 8)
        {
            if (_lastRegion != Rectangle.Empty)
            {
                _lastRegion = Rectangle.Empty;
                _lastThumb = null;
                _ = _dispatcher.InvokeAsync(() => RegionChanged?.Invoke(null));
            }
            return;
        }

        var r = region.Value;
        if (r != _lastRegion)
        {
            _lastRegion = r;
            _lastThumb = null;
            _ = _dispatcher.InvokeAsync(() => RegionChanged?.Invoke(r));
        }

        using var bitmap = CaptureProvider(r);

        // Cheap change detection on a tiny grayscale thumbnail; skip OCR entirely when the screen is static.
        var thumb = Thumbnail(bitmap);
        if (_lastThumb is not null && !Changed(_lastThumb, thumb))
            return;
        _lastThumb = thumb;
        FramesAnalyzed++;

        var page = await _ocr.RecognizeAsync(bitmap);
        ct.ThrowIfCancellationRequested();

        var blocks = TextLayout.GroupBlocks(page.Lines)
            .Where(b => TextLayout.IsTranslatable(b.Text))
            .ToList();

        // Same text in the same places as last time (e.g. a blinking cursor tripped the diff) — nothing to do.
        var signature = string.Join("|", blocks.Select(b => $"{b.Text}@{b.Rect.X},{b.Rect.Y}"));
        if (signature == _lastSignature) return;
        _lastSignature = signature;

        var sw = Stopwatch.StartNew();
        int requestsBefore = NetworkRequests;
        var translations = blocks.Count == 0
            ? Array.Empty<TranslationResult>()
            : await _translator.TranslateManyAsync(blocks.Select(b => b.Text).ToList(), _targetLanguage, ct);
        if (translations.Any(t => !t.FromCache)) NetworkRequests++;
        sw.Stop();

        var pixels = new RegionPixels(bitmap);
        var live = new List<LiveBlock>(blocks.Count);
        for (int i = 0; i < blocks.Count; i++)
        {
            var t = translations[i];
            if (string.IsNullOrWhiteSpace(t.Text)) continue;
            if (string.Equals(t.Text, blocks[i].Text, StringComparison.OrdinalIgnoreCase)) continue; // untranslatable name/brand
            if (t.SourceLanguage.StartsWith("fa", StringComparison.OrdinalIgnoreCase)) continue;

            var (bg, fg) = pixels.Colors(blocks[i].Rect);
            double stroke = pixels.StrokeRatio(blocks[i].Rect, blocks[i].LineCount, bg, fg);
            live.Add(new LiveBlock(blocks[i], t.Text, bg, fg, stroke >= BoldStrokeRatio, pixels.IsBusy(blocks[i].Rect), stroke));
        }

        var frame = new LiveFrame(r, live, pixels, page.Elapsed, sw.Elapsed, NetworkRequests - requestsBefore);
        _ = _dispatcher.InvokeAsync(() => FrameReady?.Invoke(frame));
    }

    // ---- change detection ---------------------------------------------

    private const int ThumbSize = 48;

    private static byte[] Thumbnail(Bitmap src)
    {
        using var small = new Bitmap(ThumbSize, ThumbSize, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(small))
        {
            g.InterpolationMode = InterpolationMode.Bilinear;
            g.DrawImage(src, new Rectangle(0, 0, ThumbSize, ThumbSize), new Rectangle(0, 0, src.Width, src.Height), GraphicsUnit.Pixel);
        }

        var data = small.LockBits(new Rectangle(0, 0, ThumbSize, ThumbSize), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var raw = new byte[data.Stride * ThumbSize];
            Marshal.Copy(data.Scan0, raw, 0, raw.Length);

            var gray = new byte[ThumbSize * ThumbSize];
            for (int y = 0; y < ThumbSize; y++)
                for (int x = 0; x < ThumbSize; x++)
                {
                    int o = y * data.Stride + x * 4;
                    gray[y * ThumbSize + x] = (byte)((raw[o] * 114 + raw[o + 1] * 587 + raw[o + 2] * 299) / 1000);
                }
            return gray;
        }
        finally { small.UnlockBits(data); }
    }

    private static bool Changed(byte[] a, byte[] b)
    {
        int differing = 0;
        for (int i = 0; i < a.Length; i++)
            if (Math.Abs(a[i] - b[i]) > 18) differing++;

        // ~0.5% of the thumbnail; a blinking caret or a clock digit stays under this.
        return differing > Math.Max(6, a.Length / 200);
    }

    /// <summary>Stem thickness / line height above which text is drawn bold.</summary>
    private const double BoldStrokeRatio = 0.095;

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
