using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using ScreenTranslator.Models;
using ScreenTranslator.Native;
using ScreenTranslator.Services;
using ScreenTranslator.Services.Translation;
using ScreenTranslator.UI;

namespace ScreenTranslator;

public partial class App : Application
{
    private Mutex? _singleInstance;
    private HotkeyManager? _hotkeys;
    private TrayIconHost? _tray;
    private OcrService? _ocr;
    private ITranslator? _translator;
    private SpeechService? _speech;
    private LiveController? _live;
    private HistoryStore? _history;
    private AppSettings _settings = new();
    private ResultWindow? _result;
    private SettingsWindow? _settingsWindow;
    private HistoryWindow? _historyWindow;
    private WelcomeWindow? _welcomeWindow;
    private WordWindow? _wordWindow;
    private int _jobId;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Diagnostics/demos may run beside the tray instance.
        bool isFirst = true;
        bool diagnostic = e.Args.Length > 0 && e.Args[0].StartsWith("--", StringComparison.Ordinal);
        if (!diagnostic)
            _singleInstance = new Mutex(true, @"Local\ScreenTranslator.SingleInstance", out isFirst);
        if (!isFirst)
        {
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += (_, args) => args.SetObserved();

        _settings = AppSettings.Load();
        Loc.Use(Environment.GetEnvironmentVariable("ST_LANG") ?? _settings.UiLanguage);   // env overrides for visual checks
        Theme.Apply(Environment.GetEnvironmentVariable("ST_THEME") ?? _settings.Theme);
        _translator = TranslatorFactory.Create(_settings);

        try
        {
            _ocr = new OcrService(_settings.OcrLanguage);
        }
        catch (Exception ex)
        {
            UI.DialogWindow.Alert(null, ex.Message, null, UI.DialogKind.Error);
            Shutdown();
            return;
        }

        if (TryRunDiagnostics(e.Args)) return;

        StartupManager.RepairIfStale();
        _speech = new SpeechService();
        _history = HistoryStore.Load();
        CreateLiveController();

        _tray = new TrayIconHost();
        _tray.TranslateRegionRequested += () => Run(TranslateRegionAsync);
        _tray.TranslateSelectionRequested += () => Run(TranslateSelectionAsync);
        _tray.LookupWordRequested += () => Run(LookupWordAsync);
        _tray.CopyTextRequested += () => Run(CopyTextAsync);
        _tray.LiveRegionRequested += () => Run(StartLiveRegionAsync);
        _tray.LiveWindowRequested += () => Run(_ => { StartLiveWindow(); return Task.CompletedTask; });
        _tray.LiveScreenRequested += () => Run(_ => { StartLiveScreen(); return Task.CompletedTask; });
        _tray.LiveStopRequested += () => _live?.Stop();
        _tray.SettingsRequested += ShowSettings;
        _tray.HistoryRequested += ShowHistory;
        _tray.HelpRequested += ShowWelcome;
        _tray.ExitRequested += Shutdown;

        _settings.Saved += OnSettingsSaved;

        _hotkeys = new HotkeyManager();
        RegisterHotkeys(announce: _settings.WelcomeShown);

        if (!_settings.WelcomeShown)
            ShowWelcome();

        MemoryTrim.TrimLater();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _live?.Dispose();
        _hotkeys?.Dispose();
        _tray?.Dispose();
        _speech?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    // ---- Wiring ----------------------------------------------------------

    private void CreateLiveController()
    {
        _live?.Dispose();
        _live = new LiveController(_ocr!, _translator!, _settings, Dispatcher);
        _live.Stopped += () => { _tray?.SetLive(false); MemoryTrim.TrimLater(); };
        _live.ReselectRequested += () => Run(ReselectLiveRegionAsync);
    }

    private void RegisterHotkeys(bool announce)
    {
        _hotkeys!.UnregisterAll();
        var failed = new List<string>();

        void Bind(string chord, Action action)
        {
            var hk = Hotkey.Parse(chord, Hotkey.None);
            if (hk.IsEmpty) return;
            if (!_hotkeys.Register(hk.Modifiers, hk.Key, action)) failed.Add(hk.ToString());
        }

        Bind(_settings.HotkeyRegion, () => Run(TranslateRegionAsync));
        Bind(_settings.HotkeySelection, () => Run(TranslateSelectionAsync));
        Bind(_settings.HotkeyLiveRegion, () => { if (_live!.IsRunning) _live.Stop(); else Run(StartLiveRegionAsync); });
        Bind(_settings.HotkeyLiveWindow, () => { if (_live!.IsRunning) _live.Stop(); else StartLiveWindow(); });
        Bind(_settings.HotkeyWord, () => Run(LookupWordAsync));
        Bind(_settings.HotkeyCopyText, () => Run(CopyTextAsync));

        _tray?.UpdateShortcuts(_settings.HotkeyRegion, _settings.HotkeySelection, _settings.HotkeyLiveRegion, _settings.HotkeyLiveWindow, _settings.HotkeyWord, _settings.HotkeyCopyText);

        if (failed.Count > 0)
            ToastWindow.Show(Loc.T("toast.hotkey.title"), Loc.T("toast.hotkey.body", string.Join(Loc.IsFa ? "، " : ", ", failed)), ToastKind.Warning, 5000);
        else if (announce)
            ToastWindow.Show(Loc.T("toast.ready.title"), Loc.T("toast.ready.body", _settings.HotkeyRegion, _settings.HotkeyWord, _settings.HotkeyLiveRegion), ToastKind.Info, 3500);
    }

    private void OnSettingsSaved()
    {
        try
        {
            _ocr = new OcrService(_settings.OcrLanguage);
            _translator = TranslatorFactory.Create(_settings);
            CreateLiveController();
            _tray?.SetLive(false);
            RegisterHotkeys(announce: false);
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    // ---- Job plumbing --------------------------------------------------

    /// <summary>Starts a translation job; a new job supersedes any running one.</summary>
    private void Run(Func<int, Task> job)
    {
        int id = ++_jobId;
        _ = Dispatcher.InvokeAsync(async () =>
        {
            try { await job(id); }
            catch (Exception ex) when (id == _jobId)
            {
                ShowError(FriendlyError(ex));
            }
        }, DispatcherPriority.Normal);
    }

    private static string FriendlyError(Exception ex)
    {
        if (ex is HttpRequestException http)
        {
            if (http.Message.Contains("429")) return Loc.T("error.rate");
            if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable()) return Loc.T("error.offline");
            return Loc.T("error.connect");
        }
        if (ex is TaskCanceledException) return Loc.T("error.timeout");
        return ex.Message;
    }

    private bool Stale(int id) => id != _jobId;

    private ResultWindow OpenResult()
    {
        CloseResult();
        var remembered = _settings.PopupHeight > 0 ? new Size(_settings.PopupWidth, _settings.PopupHeight) : (Size?)null;
        _result = new ResultWindow(_settings.SpeakEnabled ? _speech : null, _settings.PopupFontSize, remembered)
        {
            TargetLanguage = _settings.TargetLanguage,
        };
        _result.Closed += (s, _) => { if (ReferenceEquals(s, _result)) { _result = null; MemoryTrim.TrimLater(); } };
        _result.SizeSettled += size =>
        {
            var (w, h) = size is { } s ? (Math.Round(s.Width), Math.Round(s.Height)) : (0, 0);
            if (w == _settings.PopupWidth && h == _settings.PopupHeight) return;
            _settings.PopupWidth = w;
            _settings.PopupHeight = h;
            _settings.SaveQuiet();
        };
        return _result;
    }

    private void CloseResult()
    {
        if (_result is { } r)
        {
            _result = null;
            r.Close();
        }
        if (_wordWindow is { } w)
        {
            _wordWindow = null;
            w.Close();
        }
    }

    private void ShowError(string message)
    {
        if (_result is { IsVisible: true } r) r.ShowError(message);
        else if (_wordWindow is { IsVisible: true } w) w.SetTranslationError(message);
        else ToastWindow.Show(message, null, ToastKind.Error, 4000);
    }

    private void Record(string mode, string source, TranslationResult t)
    {
        if (!_settings.HistoryEnabled || _history is null) return;
        _history.Add(new HistoryEntry(DateTime.Now, source, t.Text, t.SourceLanguage, t.Engine, mode));
    }

    // ---- Windows -----------------------------------------------------------

    private void ShowSettings()
    {
        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }
        _settingsWindow = new SettingsWindow(_settings);
        _settingsWindow.WelcomeRequested += ShowWelcome;
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void ShowHistory()
    {
        if (_historyWindow is { IsVisible: true })
        {
            _historyWindow.Activate();
            return;
        }
        _historyWindow = new HistoryWindow(_history!);
        _historyWindow.EntryChosen += entry =>
        {
            NativeMethods.GetCursorPos(out var c);
            var w = OpenResult();
            w.SetOriginal(entry.Source);
            w.ShowAt(c.X, c.Y);
            w.SetTranslation(new TranslationResult(entry.Translation, entry.SourceLanguage, entry.Engine, true, TimeSpan.Zero));
        };
        _historyWindow.Closed += (_, _) => _historyWindow = null;
        _historyWindow.Show();
        _historyWindow.Activate();
    }

    private void ShowWelcome()
    {
        if (_welcomeWindow is { IsVisible: true })
        {
            _welcomeWindow.Activate();
            return;
        }
        _welcomeWindow = new WelcomeWindow(_settings);
        _welcomeWindow.TryLiveRequested += hwnd =>
        {
            if (_live!.IsRunning) { _live.Stop(); return; }
            _live.StartWindow(hwnd);
            _tray?.SetLive(true);
        };
        _welcomeWindow.Closed += (_, _) => { _welcomeWindow = null; if (_live is { IsRunning: true }) _live.Stop(); };
        _welcomeWindow.Show();
        _welcomeWindow.Activate();
    }

    // ---- Mode 1: pick a region, OCR it, translate ------------------------

    private async Task TranslateRegionAsync(int id)
    {
        CloseResult();

        var pick = await RegionSelector.PickAsync(RegionSelector.Purpose.Translate);
        if (pick is null || Stale(id)) return;

        // Give DWM a frame to remove the overlay before we read the screen.
        await Task.Delay(90);
        if (Stale(id)) return;

        using var bitmap = ScreenCapture.Capture(pick.PixelRect);

        var window = OpenResult();
        window.SetStatus(Loc.T("result.reading"));
        window.ShowNear(pick.PixelRect);

        var page = await _ocr!.RecognizeAsync(bitmap);
        if (Stale(id)) return;

        if (page.Lines.Count == 0)
        {
            window.ShowError(Loc.T("error.notext.region"));
            return;
        }

        var text = TextLayout.Compose(page.Lines);
        window.SetOriginal(text);
        window.SetStatus($"OCR {page.Elapsed.TotalMilliseconds:0}ms  ·  {Loc.T("common.translating")}");

        var translation = await _translator!.TranslateAsync(text, _settings.TargetLanguage);
        if (Stale(id)) return;

        window.SetTranslation(translation, page.Elapsed);
        Record("mode.region", text, translation);
    }

    // ---- Mode 2: live overlay ---------------------------------------------

    private async Task StartLiveRegionAsync(int id)
    {
        CloseResult();
        bool wasRunning = _live!.IsRunning;
        _live.Suspend();

        var pick = await RegionSelector.PickAsync(RegionSelector.Purpose.Live);
        if (pick is null || Stale(id))
        {
            if (wasRunning) _live.Resume(); else _live.Stop();
            return;
        }

        await Task.Delay(60);
        _live.StartRegion(pick.PixelRect);
        _tray?.SetLive(true);
    }

    private Task ReselectLiveRegionAsync(int id) => StartLiveRegionAsync(id);

    private void StartLiveWindow()
    {
        CloseResult();
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero || hwnd == NativeMethods.GetShellWindow() || hwnd == NativeMethods.GetDesktopWindow()
            || WindowEffects.GetWindowBounds(hwnd) is null)
        {
            _tray?.ShowBalloon(Loc.T("toast.livewindow.title"), Loc.T("toast.livewindow.body", _settings.HotkeyLiveWindow), System.Windows.Forms.ToolTipIcon.Info);
            return;
        }
        _live!.StartWindow(hwnd);
        _tray?.SetLive(true);
    }

    private void StartLiveScreen()
    {
        CloseResult();
        NativeMethods.GetCursorPos(out var p);
        var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(p.X, p.Y));
        _live!.StartScreen(screen.Bounds);
        _tray?.SetLive(true);
    }

    // ---- Mode 3: translate the text selected in the foreground app --------

    private async Task TranslateSelectionAsync(int id)
    {
        CloseResult();

        var selection = await SelectionReader.ReadAsync();
        if (Stale(id)) return;

        NativeMethods.GetCursorPos(out var cursor);
        var window = OpenResult();

        if (selection is null)
        {
            window.SetStatus(Loc.T("result.selection"));
            window.ShowAt(cursor.X, cursor.Y);
            window.ShowError(Loc.T("error.noselection"));
            return;
        }

        window.SetOriginal(selection.Text);
        window.SetStatus(selection.FromClipboardFallback
            ? $"{Loc.T("result.fromclipboard")}  ·  {Loc.T("common.translating")}"
            : Loc.T("common.translating"));
        window.ShowAt(cursor.X, cursor.Y);

        var translation = await _translator!.TranslateAsync(selection.Text, _settings.TargetLanguage);
        if (Stale(id)) return;

        window.SetTranslation(translation);
        Record(selection.FromClipboardFallback ? "mode.clipboard" : "mode.selection", selection.Text, translation);
    }

    // ---- Mode 4: dictionary card for the word under the mouse -------------

    private async Task LookupWordAsync(int id)
    {
        CloseResult();

        NativeMethods.GetCursorPos(out var cursor);
        var scale = Dpi.ScaleAt(cursor.X, cursor.Y);
        var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(cursor.X, cursor.Y)).Bounds;

        // A strip around the cursor is plenty for one line; small captures also get upscaled by the OCR (better accuracy).
        var region = new System.Drawing.Rectangle(cursor.X - (int)(300 * scale), cursor.Y - (int)(70 * scale), (int)(600 * scale), (int)(140 * scale));
        region.Intersect(screen);
        if (region.Width < 8 || region.Height < 8) return;

        using var bitmap = ScreenCapture.Capture(region);
        var page = await _ocr!.RecognizeAsync(bitmap);
        if (Stale(id)) return;

        var word = page.WordAt(new System.Drawing.Point(cursor.X - region.X, cursor.Y - region.Y));
        var clean = word is null ? "" : DictionaryService.CleanWord(word.Text);
        if (word is null || !clean.Any(char.IsLetter))
        {
            ToastWindow.Show(Loc.T("toast.noword.title"), Loc.T("toast.noword.body", _settings.HotkeyWord), ToastKind.Info);
            return;
        }

        var wordPx = word.Rect; wordPx.Offset(region.X, region.Y);
        var line = page.Lines.FirstOrDefault(l => l.Words?.Contains(word) == true);
        bool hasLine = line is not null && line.Words!.Count > 1;

        HighlightWindow.Flash(wordPx);
        var card = new WordWindow(clean, _settings.SpeakEnabled ? _speech : null, hasLine) { TargetLanguage = _settings.TargetLanguage };
        _wordWindow = card;
        card.Closed += (s, _) => { if (ReferenceEquals(s, _wordWindow)) { _wordWindow = null; MemoryTrim.TrimLater(); } };
        card.TranslateLineRequested += () =>
        {
            var lineText = line!.Text;
            var linePx = line.Rect; linePx.Offset(region.X, region.Y);
            Run(async jid =>
            {
                var w = OpenResult();
                w.SetOriginal(lineText);
                w.SetStatus(Loc.T("common.translating"));
                w.ShowNear(linePx);
                var t = await _translator!.TranslateAsync(lineText, _settings.TargetLanguage);
                if (Stale(jid)) return;
                w.SetTranslation(t);
                Record("خط", lineText, t);
            });
        };
        card.ShowNear(wordPx);

        // Translation and dictionary run in parallel; each fills its part of the card as it lands.
        var translationTask = _translator!.TranslateAsync(clean, _settings.TargetLanguage);
        var dictionaryTask = DictionaryService.LooksEnglish(clean)
            ? DictionaryService.LookupAsync(clean)
            : Task.FromResult<DictionaryEntry?>(null);

        try
        {
            var t = await translationTask;
            if (Stale(id)) return;
            card.SetTranslation(t);
            Record("کلمه", clean, t);
        }
        catch (Exception ex) when (!Stale(id))
        {
            card.SetTranslationError(FriendlyError(ex));
        }

        var entry = await dictionaryTask;
        if (Stale(id)) return;
        card.SetDictionary(entry);
    }

    // ---- Mode 5: OCR a region straight to the clipboard -------------------

    private async Task CopyTextAsync(int id)
    {
        CloseResult();

        var pick = await RegionSelector.PickAsync(RegionSelector.Purpose.Copy);
        if (pick is null || Stale(id)) return;

        await Task.Delay(90);
        if (Stale(id)) return;

        using var bitmap = ScreenCapture.Capture(pick.PixelRect);
        var page = await _ocr!.RecognizeAsync(bitmap);
        if (Stale(id)) return;

        if (page.Lines.Count == 0)
        {
            ToastWindow.Show(Loc.T("toast.notext.title"), null, ToastKind.Warning);
            return;
        }

        var text = TextLayout.Compose(page.Lines);
        try { Clipboard.SetDataObject(text, true); }
        catch { ToastWindow.Show(Loc.T("toast.clipboard.title"), Loc.T("toast.clipboard.body"), ToastKind.Error); return; }

        int words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        ToastWindow.Show(Loc.T("toast.copied.title"),
            Loc.T("toast.copied.body", page.Lines.Count, words, page.Elapsed.TotalMilliseconds.ToString("0")), ToastKind.Success);
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        ShowError(FriendlyError(e.Exception));
    }
}
