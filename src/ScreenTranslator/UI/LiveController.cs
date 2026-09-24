using System.Windows.Threading;
using ScreenTranslator.Native;
using ScreenTranslator.Services;
using ScreenTranslator.Services.Translation;
using DrawingRectangle = System.Drawing.Rectangle;

namespace ScreenTranslator.UI;

/// <summary>Owns one live-translation run: the analysis session, the overlay, and the floating pill.</summary>
public sealed class LiveController : IDisposable
{
    private readonly OcrService _ocr;
    private readonly ITranslator _translator;
    private readonly AppSettings _settings;
    private readonly Dispatcher _dispatcher;

    private LiveSession? _session;
    private OverlayWindow? _overlay;
    private LivePillWindow? _pill;
    private int _frames;

    public bool IsRunning => _session is not null;

    /// <summary>The pill's "re-select" button was pressed; the host should pick a new region and call <see cref="StartRegion"/>.</summary>
    public event Action? ReselectRequested;

    public event Action? Stopped;

    public LiveController(OcrService ocr, ITranslator translator, AppSettings settings, Dispatcher dispatcher)
    {
        _ocr = ocr;
        _translator = translator;
        _settings = settings;
        _dispatcher = dispatcher;
    }

    public void StartRegion(DrawingRectangle px) => Start(() => px, canReselect: true);

    public void StartScreen(DrawingRectangle bounds) => Start(() => bounds, canReselect: false);

    /// <summary>Follows a top-level window: the overlay moves with it and pauses while it is minimized.</summary>
    public void StartWindow(IntPtr hwnd) => Start(() => WindowEffects.GetWindowBounds(hwnd), canReselect: false);

    private void Start(Func<DrawingRectangle?> regionProvider, bool canReselect)
    {
        Stop(raiseStopped: false);

        _overlay = new OverlayWindow
        {
            BoxOpacity = _settings.OverlayOpacity,
            FontScale = _settings.OverlayFontScale,
            TargetLanguage = _settings.TargetLanguage,
        };

        _pill = new LivePillWindow(canReselect);
        _pill.StopRequested += () => Stop();
        _pill.ReselectRequested += () => ReselectRequested?.Invoke();
        _pill.PauseToggled += () =>
        {
            if (_session is null) return;
            _session.IsPaused = !_session.IsPaused;
            _pill.SetState(_session.IsPaused ? LivePillState.Paused : LivePillState.Running);
            if (_session.IsPaused) _overlay?.Clear();
        };

        _session = new LiveSession(regionProvider, _ocr, _translator, _settings.TargetLanguage, _dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(_settings.LiveIntervalMs),
        };
        _session.RegionChanged += OnRegionChanged;
        _session.FrameReady += OnFrame;
        _session.Error += ex =>
        {
            _pill?.SetState(LivePillState.Error);
            _pill?.SetStats(Shorten(ex.Message));
        };

        var initial = regionProvider();
        if (initial is { } r)
        {
            _overlay.SetRegion(r);
            _pill.DockTo(r);
        }
        _pill.SetState(LivePillState.Running);
        _pill.SetStats(Loc.T("live.waiting.text"));

        _session.Start();
    }

    /// <summary>Temporarily hide the overlay/pill (e.g. while the user picks a new region).</summary>
    public void Suspend()
    {
        if (_session is not null) _session.IsPaused = true;
        _overlay?.Hide();
        _pill?.Hide();
    }

    /// <summary>Undo <see cref="Suspend"/>.</summary>
    public void Resume()
    {
        if (_session is null) return;
        _overlay?.Show();
        _pill?.Show();
        _session.IsPaused = false;
        _pill?.SetState(LivePillState.Running);
    }

    private void OnRegionChanged(DrawingRectangle? region)
    {
        if (_overlay is null || _pill is null) return;

        if (region is null)
        {
            _overlay.Clear();
            _overlay.Hide();
            _pill.SetState(LivePillState.Waiting);
            return;
        }

        _overlay.Clear();
        _overlay.SetRegion(region.Value);
        _pill.DockTo(region.Value);
        if (_session is { IsPaused: false }) _pill.SetState(LivePillState.Running);
    }

    private void OnFrame(LiveFrame frame)
    {
        if (_overlay is null || _pill is null) return;
        _frames++;

        _overlay.Apply(frame);
        _pill.SetState(_session is { IsPaused: true } ? LivePillState.Paused : LivePillState.Running);

        var parts = new List<string> { Loc.T("live.blocks", frame.Blocks.Count), $"OCR {frame.OcrTime.TotalMilliseconds:0}ms" };
        parts.Add(frame.NetworkRequests == 0 ? Loc.T("common.cached") : Loc.T("live.translate.ms", frame.TranslateTime.TotalMilliseconds.ToString("0")));
        _pill.SetStats(string.Join("  ·  ", parts));
    }

    public void Stop(bool raiseStopped = true)
    {
        _session?.Dispose();
        _session = null;

        _overlay?.Close();
        _overlay = null;

        _pill?.Close();
        _pill = null;

        _frames = 0;
        if (raiseStopped) Stopped?.Invoke();
    }

    private static string Shorten(string s) => s.Length > 60 ? s[..57] + "…" : s;

    public void Dispose() => Stop(raiseStopped: false);
}
