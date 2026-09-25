using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using ScreenTranslator.Native;
using ScreenTranslator.Services;

namespace ScreenTranslator;

/// <summary>Headless diagnostics: `ScreenTranslator.exe --selftest <out.txt>` / `--selftest-affinity <out.txt>`.</summary>
public partial class App
{
    private bool TryRunDiagnostics(string[] args)
    {
        if (args.Length < 2) return false;
        switch (args[0])
        {
            case "--selftest":
                Guard(SelfTestAsync(args[1]), args[1]);
                return true;
            case "--selftest-affinity":
                Guard(AffinityTestAsync(args[1]), args[1]);
                return true;
            case "--demo-popup":
                Guard(DemoPopupAsync(args[1]), args[1]);
                return true;
            case "--demo-settings":
                Guard(DemoSettingsAsync(args[1], args.Length > 2 ? args[2] : ""), args[1]);
                return true;
            case "--demo-welcome":
                Guard(DemoWindowAsync(new UI.WelcomeWindow(_settings), args[1]), args[1]);
                return true;
            case "--demo-history":
                Guard(DemoHistoryAsync(args[1]), args[1]);
                return true;
            case "--demo-word":
                Guard(DemoWordAsync(args[1], args.Length > 2 ? args[2] : "serendipity"), args[1]);
                return true;
            case "--demo-lookup":
                Guard(DemoLookupAsync(args[1], args.Length > 2 ? args[2] : "600,400"), args[1]);
                return true;
            case "--demo-selector":
                Guard(DemoSelectorAsync(args[1], args.Length > 2 && args[2] == "drag"), args[1]);
                return true;
            case "--demo-tray":
                Guard(DemoTrayAsync(args[1], args.Length > 2 && args[2] == "live"), args[1]);
                return true;
            case "--demo-dialog":
                Guard(DemoDialogAsync(args[1], args.Length > 2 && args[2] == "alert"), args[1]);
                return true;
            case "--demo-toast":
                Guard(DemoToastAsync(args[1]), args[1]);
                return true;
            case "--demo-live":
                Guard(DemoLiveAsync(args[1], args.Length > 2 ? args[2] : "400,300,1400,700"), args[1]);
                return true;
            case "--demo-live-file":
                Guard(DemoLiveFileAsync(args[1], args.Length > 2 ? args[2] : args[1] + ".live.png"), args[1]);
                return true;
            default:
                return false;
        }
    }

    /// <summary>Demo tasks are fire-and-forget; write their failure next to the requested output instead of hanging.</summary>
    private void Guard(Task task, string outPath) => task.ContinueWith(t =>
    {
        try { File.WriteAllText(outPath + ".err.txt", t.Exception!.ToString()); } catch { }
        Dispatcher.InvokeAsync(Shutdown);
    }, TaskContinuationOptions.OnlyOnFaulted);

    // ---- visual demos: open a window, wait, screenshot it to PNG, exit --------

    private async Task DemoPopupAsync(string png)
    {
        var w = new UI.ResultWindow(new SpeechService(), _settings.PopupFontSize) { TargetLanguage = _settings.TargetLanguage };
        w.SetOriginal("Live translation paints Persian text directly over the original, sampling the page's own colors so it looks native. Hold Ctrl to peek at the source.");
        w.SetStatus(UI.Loc.T("common.translating"));
        w.ShowNear(new System.Drawing.Rectangle(300, 260, 520, 40));
        await Task.Delay(700);
        w.SetTranslation(new Models.TranslationResult(
            "ترجمه‌ی زنده متن فارسی را مستقیماً روی متن اصلی می‌کشد و رنگ‌های خود صفحه را نمونه‌برداری می‌کند تا طبیعی به نظر برسد. برای دیدن متن اصلی، Ctrl را نگه دار.",
            "en", "Google", false, TimeSpan.FromMilliseconds(412)), TimeSpan.FromMilliseconds(96));
        await Task.Delay(900);
        SaveScreenshot(png, new Rect(w.Left - 40, w.Top - 40, w.ActualWidth + 80, w.ActualHeight + 80));
        Shutdown();
    }

    private async Task DemoWordAsync(string png, string word)
    {
        var w = new UI.WordWindow(word, new SpeechService(), hasLine: true) { TargetLanguage = _settings.TargetLanguage };
        w.ShowNear(new System.Drawing.Rectangle(400, 300, 120, 22));
        UI.HighlightWindow.Flash(new System.Drawing.Rectangle(400, 300, 120, 22));
        var tTask = _translator!.TranslateAsync(word, _settings.TargetLanguage);
        var dTask = DictionaryService.LookupAsync(word);
        w.SetTranslation(await tTask);
        w.SetDictionary(await dTask);
        await Task.Delay(900);
        SaveScreenshot(png, new Rect(w.Left - 40, w.Top - 60, w.ActualWidth + 80, w.ActualHeight + 100));
        Shutdown();
    }

    /// <summary>Runs the real word-lookup flow at a screen coordinate (pixels) and screenshots the result.</summary>
    private async Task DemoLookupAsync(string png, string at)
    {
        var p = at.Split(',').Select(int.Parse).ToArray();
        NativeMethods.SetCursorPos(p[0], p[1]);
        _speech = new SpeechService();
        await Task.Delay(100);
        await LookupWordAsync(++_jobId);
        await Task.Delay(2500);
        var dip = UI.Dpi.ToDip(new System.Drawing.Rectangle(p[0] - 250, p[1] - 560, 700, 640));
        SaveScreenshot(png, dip, includeOverlay: true);
        Shutdown();
    }

    private async Task DemoSelectorAsync(string png, bool drag)
    {
        var screen = System.Windows.Forms.Screen.PrimaryScreen!;
        NativeMethods.SetCursorPos(820, 420);
        var w = new UI.RegionSelectorWindow(screen, UI.RegionSelector.Purpose.Translate);
        w.Show();
        w.Activate();
        await Task.Delay(500);
        if (drag) w.DemoDrag(new Point(500, 300), new Point(1100, 620));
        await Task.Delay(400);
        SaveScreenshot(png, new Rect(380, 0, 900, 720));
        Shutdown();
    }

    private async Task DemoTrayAsync(string png, bool live)
    {
        var w = new UI.TrayFlyoutWindow(new UI.TrayState(_settings.HotkeyRegion, _settings.HotkeySelection, _settings.HotkeyWord, _settings.HotkeyCopyText, _settings.HotkeyLiveRegion, _settings.HotkeyLiveWindow, live));
        w.ShowAtTray();
        await Task.Delay(900);
        SaveScreenshot(png, new Rect(w.Left - 40, w.Top - 40, w.ActualWidth + 80, w.ActualHeight + 80));
        Shutdown();
    }

    private async Task DemoToastAsync(string png)
    {
        UI.ToastWindow.Show(UI.Loc.T("toast.copied.title"), UI.Loc.T("toast.copied.body", 4, 31, 96), UI.ToastKind.Success, 4000);
        await Task.Delay(700);
        var area = System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea;
        var dip = UI.Dpi.ToDip(area);
        SaveScreenshot(png, new Rect(dip.X + dip.Width / 2 - 300, dip.Y, 600, 110), includeOverlay: true);
        Shutdown();
    }

    private async Task DemoDialogAsync(string png, bool alert)
    {
        var owner = new UI.HistoryWindow(DemoHistory());
        owner.Show();
        await Task.Delay(500);

        // The dialog blocks in ShowDialog, but its nested message loop keeps this continuation running.
        _ = Dispatcher.InvokeAsync(() =>
        {
            if (alert) UI.DialogWindow.Alert(owner, UI.Loc.T("settings.hotkey.duplicate"));
            else UI.DialogWindow.Confirm(owner, UI.Loc.T("history.confirm"), UI.Loc.T("history.confirm.body"), UI.Loc.T("history.clear"), "\uE74D");
        });
        await Task.Delay(700);

        SaveScreenshot(png, new Rect(owner.Left - 30, owner.Top - 30, owner.ActualWidth + 60, owner.ActualHeight + 60));
        Shutdown();
    }

    private Task DemoSettingsAsync(string png, string tab)
    {
        var w = new UI.SettingsWindow(_settings);
        w.ShowTab(tab);
        return DemoWindowAsync(w, png);
    }

    private Task DemoHistoryAsync(string png) => DemoWindowAsync(new UI.HistoryWindow(DemoHistory()), png);

    /// <summary>A few believable entries, so the history and dialog demos aren't shot against an empty list.</summary>
    private static HistoryStore DemoHistory()
    {
        var store = new HistoryStore();
        store.Entries.Add(new HistoryEntry(DateTime.Now.AddMinutes(-3), "Live translation paints Persian text directly over the original.", "ترجمه‌ی زنده متن فارسی را مستقیماً روی متن اصلی می‌کشد.", "en", "Google", "mode.region"));
        store.Entries.Add(new HistoryEntry(DateTime.Now.AddHours(-2), "Hold Ctrl to peek at the source.", "برای دیدن متن اصلی، Ctrl را نگه دار.", "en", "Claude", "mode.selection"));
        store.Entries.Add(new HistoryEntry(DateTime.Now.AddDays(-1), "Settings are stored per user; API keys are encrypted with DPAPI.", "تنظیمات برای هر کاربر ذخیره می‌شود؛ کلیدهای API با DPAPI رمز می‌شوند.", "en", "Google", "mode.clipboard"));
        return store;
    }

    private async Task DemoWindowAsync(Window w, string png)
    {
        w.Show();
        await Task.Delay(1000);
        SaveScreenshot(png, new Rect(w.Left - 30, w.Top - 30, w.ActualWidth + 60, w.ActualHeight + 60));
        Shutdown();
    }

    private async Task DemoLiveAsync(string png, string regionSpec)
    {
        var p = regionSpec.Split(',').Select(int.Parse).ToArray();
        var region = new System.Drawing.Rectangle(p[0], p[1], p[2], p[3]);

        var live = new UI.LiveController(_ocr!, _translator!, _settings, Dispatcher);
        live.StartRegion(region);
        await Task.Delay(4500);

        var dip = UI.Dpi.ToDip(region);
        SaveScreenshot(png, new Rect(dip.X - 20, dip.Y - 70, dip.Width + 40, dip.Height + 90), includeOverlay: true);
        live.Stop();
        Shutdown();
    }

    /// <summary>
    /// Runs the live pipeline over a PNG instead of the screen and composites the overlay onto it offline:
    /// deterministic before/after pictures without touching the desktop. Writes per-block diagnostics next to the output.
    /// </summary>
    private async Task DemoLiveFileAsync(string inputPng, string outputPng)
    {
        using var source = new System.Drawing.Bitmap(inputPng);
        var region = new System.Drawing.Rectangle(0, 0, source.Width, source.Height);

        var overlay = new UI.OverlayWindow { BoxOpacity = _settings.OverlayOpacity, FontScale = _settings.OverlayFontScale };
        overlay.SetRegion(region with { X = -region.Width - 200, Y = 0 });   // off-screen: only the visual is used

        var ready = new TaskCompletionSource<LiveFrame>();
        using var session = new LiveSession(() => region, _ocr!, _translator!, _settings.TargetLanguage, Dispatcher)
        {
            CaptureProvider = _ => new System.Drawing.Bitmap(source),
        };
        session.FrameReady += f => { overlay.Apply(f); ready.TrySetResult(f); };
        session.Error += ex => ready.TrySetException(ex);
        session.Start();

        var frame = await ready.Task.WaitAsync(TimeSpan.FromSeconds(30));
        await Task.Delay(400);   // let fade-ins finish

        var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(region.Width, region.Height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(overlay.Surface);

        var page = new System.Windows.Media.Imaging.BitmapImage(new Uri(Path.GetFullPath(inputPng)));
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawImage(page, new Rect(0, 0, region.Width, region.Height));
            dc.DrawImage(rtb, new Rect(0, 0, region.Width, region.Height));
        }
        var composed = new System.Windows.Media.Imaging.RenderTargetBitmap(region.Width, region.Height, 96, 96, PixelFormats.Pbgra32);
        composed.Render(dv);
        var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
        enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(composed));
        using (var fs = File.Create(outputPng)) enc.Save(fs);

        var sb = new StringBuilder();
        foreach (var b in frame.Blocks)
            sb.AppendLine($"{(b.Bold ? "B" : " ")}{(b.Busy ? "~" : " ")} stroke={b.StrokeRatio:0.000} lines={b.Source.LineCount} h={b.Source.LineHeight:0} rect={b.Source.Rect.X},{b.Source.Rect.Y},{b.Source.Rect.Width},{b.Source.Rect.Height} bg=#{b.Background.R:X2}{b.Background.G:X2}{b.Background.B:X2} fg=#{b.Foreground.R:X2}{b.Foreground.G:X2}{b.Foreground.B:X2}  {b.Source.Text}");
        File.WriteAllText(outputPng + ".txt", sb.ToString());

        overlay.Close();
        Shutdown();
    }

    /// <summary>Screenshot a DIP rectangle. Overlay windows are capture-excluded, so for those we briefly lift the exclusion.</summary>
    private static void SaveScreenshot(string png, Rect dip, bool includeOverlay = false)
    {
        if (includeOverlay)
        {
            foreach (Window w in Current.Windows)
                NativeMethods.SetWindowDisplayAffinity(new System.Windows.Interop.WindowInteropHelper(w).Handle, NativeMethods.WDA_NONE);
            Thread.Sleep(250);
        }

        var px = UI.Dpi.ToPixels(dip);
        var screen = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
        px.Intersect(screen);
        using var bmp = ScreenCapture.Capture(px);
        bmp.Save(png, System.Drawing.Imaging.ImageFormat.Png);
    }

    /// <summary>Captures the primary screen, OCRs it, translates the first lines, checks the cache.</summary>
    private async Task SelfTestAsync(string outPath)
    {
        var sb = new StringBuilder();
        try
        {
            var bounds = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
            sb.AppendLine($"screen: {bounds.Width}x{bounds.Height}  ocr-lang: {_ocr!.LanguageTag}");

            using var bitmap = ScreenCapture.Capture(bounds);
            var page = await _ocr.RecognizeAsync(bitmap);
            sb.AppendLine($"ocr: {page.Lines.Count} lines in {page.Elapsed.TotalMilliseconds:0}ms");

            var blocks = TextLayout.GroupBlocks(page.Lines);
            sb.AppendLine($"blocks: {blocks.Count} (translatable: {blocks.Count(b => TextLayout.IsTranslatable(b.Text))})");

            var texts = blocks.Where(b => TextLayout.IsTranslatable(b.Text)).Select(b => b.Text).Take(12).ToList();
            sb.AppendLine("--- original ---");
            foreach (var t in texts) sb.AppendLine(t);

            if (texts.Count > 0)
            {
                var results = await _translator!.TranslateManyAsync(texts, _settings.TargetLanguage);
                sb.AppendLine($"--- translation ({results[0].SourceLanguage} → {_settings.TargetLanguage}, {results[0].Elapsed.TotalMilliseconds:0}ms) ---");
                foreach (var r in results) sb.AppendLine(r.Text);

                var again = await _translator.TranslateManyAsync(texts, _settings.TargetLanguage);
                sb.AppendLine($"cache hit: {again.All(r => r.FromCache)}");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine("ERROR: " + ex);
        }

        await File.WriteAllTextAsync(outPath, sb.ToString());
        Shutdown();
    }

    /// <summary>Does WDA_EXCLUDEFROMCAPTURE hide a layered WPF window from GDI screen capture on this machine?</summary>
    private async Task AffinityTestAsync(string outPath)
    {
        var sb = new StringBuilder();
        try
        {
            var w = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = new SolidColorBrush(Color.FromRgb(255, 0, 255)),
                Topmost = true,
                ShowInTaskbar = false,
                ShowActivated = false,
                Left = 200, Top = 200, Width = 200, Height = 120,
            };
            w.Show();
            await Task.Delay(400);

            var px = ProbePixel(300, 260);
            sb.AppendLine($"visible window pixel: {px}");

            bool ok = WindowEffects.ExcludeFromCapture(w);
            sb.AppendLine($"SetWindowDisplayAffinity: {ok}");
            await Task.Delay(400);

            var px2 = ProbePixel(300, 260);
            sb.AppendLine($"excluded window pixel: {px2}");
            sb.AppendLine($"result: {(px2 != px ? "EXCLUDED (good)" : "STILL CAPTURED")}");
            w.Close();
        }
        catch (Exception ex)
        {
            sb.AppendLine("ERROR: " + ex);
        }

        await File.WriteAllTextAsync(outPath, sb.ToString());
        Shutdown();

        static string ProbePixel(int x, int y)
        {
            using var g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero);
            double s = g.DpiX / 96.0;
            using var bmp = ScreenCapture.Capture(new System.Drawing.Rectangle((int)(x * s), (int)(y * s), 1, 1));
            var c = bmp.GetPixel(0, 0);
            return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        }
    }
}
