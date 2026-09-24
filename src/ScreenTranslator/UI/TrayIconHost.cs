using System.Drawing;
using System.Windows.Forms;

namespace ScreenTranslator.UI;

/// <summary>
/// System-tray icon. Uses WinForms NotifyIcon for the icon itself (WPF's dispatcher pumps it fine); the menu is our
/// own <see cref="TrayFlyoutWindow"/> instead of a ContextMenuStrip, so it matches the rest of the app.
/// </summary>
public sealed class TrayIconHost : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly Icon _idleIcon;
    private readonly Icon _liveIcon;
    private TrayFlyoutWindow? _flyout;
    private bool _live;
    private DateTime _flyoutClosedAt;
    private string _region = "", _selection = "", _word = "", _copy = "", _liveRegion = "", _liveWindow = "";

    public event Action? TranslateRegionRequested;
    public event Action? TranslateSelectionRequested;
    public event Action? LookupWordRequested;
    public event Action? CopyTextRequested;
    public event Action? LiveRegionRequested;
    public event Action? LiveWindowRequested;
    public event Action? LiveScreenRequested;
    public event Action? LiveStopRequested;
    public event Action? SettingsRequested;
    public event Action? HistoryRequested;
    public event Action? HelpRequested;
    public event Action? ExitRequested;

    public TrayIconHost()
    {
        _idleIcon = AppIcon.Icon;
        _liveIcon = AppIcon.WithDot(Color.FromArgb(0x22, 0xC5, 0x5E));

        _icon = new NotifyIcon
        {
            Icon = _idleIcon,
            Text = "ScreenTranslator — Ctrl+Alt+T",
            Visible = true,
        };
        _icon.MouseUp += (_, e) =>
        {
            if (e.Button is MouseButtons.Left or MouseButtons.Right) ToggleFlyout();
        };
        _icon.DoubleClick += (_, _) => { _flyout?.Close(); TranslateRegionRequested?.Invoke(); };
    }

    private void ToggleFlyout()
    {
        // A click on the icon while the flyout is open deactivates it first (it starts closing); don't bounce it back open.
        if (_flyout is { IsLoaded: true } || (DateTime.UtcNow - _flyoutClosedAt).TotalMilliseconds < 350)
        {
            _flyout?.Close();
            return;
        }

        var f = new TrayFlyoutWindow(new TrayState(_region, _selection, _word, _copy, _liveRegion, _liveWindow, _live));
        f.TranslateRegionRequested += () => TranslateRegionRequested?.Invoke();
        f.TranslateSelectionRequested += () => TranslateSelectionRequested?.Invoke();
        f.LookupWordRequested += () => LookupWordRequested?.Invoke();
        f.CopyTextRequested += () => CopyTextRequested?.Invoke();
        f.LiveRegionRequested += () => LiveRegionRequested?.Invoke();
        f.LiveWindowRequested += () => LiveWindowRequested?.Invoke();
        f.LiveScreenRequested += () => LiveScreenRequested?.Invoke();
        f.LiveStopRequested += () => LiveStopRequested?.Invoke();
        f.SettingsRequested += () => SettingsRequested?.Invoke();
        f.HistoryRequested += () => HistoryRequested?.Invoke();
        f.HelpRequested += () => HelpRequested?.Invoke();
        f.ExitRequested += () => ExitRequested?.Invoke();
        f.Closed += (_, _) => { _flyoutClosedAt = DateTime.UtcNow; if (ReferenceEquals(_flyout, f)) _flyout = null; };
        _flyout = f;
        f.ShowAtTray();
    }

    public void UpdateShortcuts(string region, string selection, string liveRegion, string liveWindow, string word, string copy)
    {
        _region = region; _selection = selection; _liveRegion = liveRegion; _liveWindow = liveWindow; _word = word; _copy = copy;
        _icon.Text = string.IsNullOrEmpty(region) ? "ScreenTranslator" : "ScreenTranslator — " + region;
    }

    public void SetLive(bool live)
    {
        _live = live;
        _icon.Icon = live ? _liveIcon : _idleIcon;
        _icon.Text = live ? Loc.T("tray.tooltip.live") : "ScreenTranslator — Ctrl+Alt+T";
    }

    public void ShowBalloon(string title, string text, ToolTipIcon icon = ToolTipIcon.Info)
        => _icon.ShowBalloonTip(3000, title, text, icon);

    public void Dispose()
    {
        _flyout?.Close();
        _icon.Visible = false;
        _icon.Dispose();
        _liveIcon.Dispose();
    }
}
