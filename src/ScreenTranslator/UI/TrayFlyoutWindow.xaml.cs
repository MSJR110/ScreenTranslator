using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ScreenTranslator.Native;

namespace ScreenTranslator.UI;

/// <summary>Hotkey labels and live state the flyout displays; owned by <see cref="TrayIconHost"/>.</summary>
public sealed record TrayState(string Region, string Selection, string Word, string Copy, string LiveRegion, string LiveWindow, bool Live);

/// <summary>
/// The tray menu, as a Windows 11-style flyout: acrylic card anchored above the notification area with the four
/// quick actions as tiles, a live-translation section and the secondary items in the footer. One instance per
/// open; it closes itself when it loses focus or after any action.
/// </summary>
public partial class TrayFlyoutWindow : Window
{
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

    private bool _closing;

    public TrayFlyoutWindow(TrayState state)
    {
        InitializeComponent();
        Logo.Source = AppIcon.Bitmap;

        RegionKey.Text = state.Region;
        SelectionKey.Text = state.Selection;
        WordKey.Text = state.Word;
        CopyKey.Text = state.Copy;
        LiveRegionButton.ToolTip = string.IsNullOrEmpty(state.LiveRegion) ? null : state.LiveRegion;
        LiveWindowButton.ToolTip = string.IsNullOrEmpty(state.LiveWindow) ? null : state.LiveWindow;
        SetLive(state.Live);

        SourceInitialized += (_, _) =>
        {
            WindowEffects.HideFromAltTab(this);
            if (!WindowEffects.TryApplyAcrylic(this, Theme.IsDark))
            {
                Shell.Background = Theme.Brush("Surface");
                WindowEffects.SetRoundedCorners(this);
            }
        };
    }

    private void SetLive(bool live)
    {
        LiveChoices.Visibility = live ? Visibility.Collapsed : Visibility.Visible;
        LiveStop.Visibility = live ? Visibility.Visible : Visibility.Collapsed;
        LiveHint.Text = Loc.T(live ? "tray.live.running" : "tray.live.hint");
        StatusText.Text = Loc.T(live ? "tray.status.live" : "tray.status.ready");
        StatusDot.Fill = Theme.Brush(live ? "Success" : "Muted");

        if (live)
        {
            var pulse = new DoubleAnimation(0.6, 0, TimeSpan.FromMilliseconds(1100)) { RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() };
            StatusPulse.BeginAnimation(OpacityProperty, pulse);
        }
    }

    // ---- Placement -------------------------------------------------------

    /// <summary>Show above the notification area of the monitor under the cursor, bottom-aligned with the taskbar edge.</summary>
    public void ShowAtTray()
    {
        NativeMethods.GetCursorPos(out var c);
        var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(c.X, c.Y));
        var area = screen.WorkingArea;
        double scale = Dpi.ScaleAt(c.X, c.Y);

        // land on the right monitor first so WPF measures at that DPI
        var dip = Dpi.ToDip(c.X, c.Y);
        Left = dip.X - Width; Top = dip.Y - 200;
        Show();
        UpdateLayout();

        int w = (int)Math.Round(ActualWidth * scale);
        int h = (int)Math.Round(ActualHeight * scale);
        int gap = (int)Math.Round(12 * scale);

        // Taskbar at the bottom (the common case): sit just above it, right edge near the cursor.
        // Other edges: fall back to hugging the edge the work area is shrunk from.
        int x = Math.Clamp(c.X - w + (int)(20 * scale), area.Left + gap, Math.Max(area.Left + gap, area.Right - w - gap));
        int y;
        if (area.Bottom < screen.Bounds.Bottom) y = area.Bottom - h - gap;              // bottom taskbar
        else if (area.Top > screen.Bounds.Top) y = area.Top + gap;                     // top taskbar
        else y = Math.Clamp(c.Y - h - gap, area.Top + gap, Math.Max(area.Top + gap, area.Bottom - h - gap));

        Dpi.SetPixelPosition(this, x, y);
        Activate();
        AnimateIn();
    }

    private void AnimateIn()
    {
        Root.Opacity = 0;
        Slide.Y = 14;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        Root.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(170)) { EasingFunction = ease });
        Slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(240)) { EasingFunction = ease });
    }

    private void Dismiss()
    {
        if (_closing) return;
        _closing = true;
        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(110)) { EasingFunction = ease };
        fade.Completed += (_, _) => Close();
        Root.BeginAnimation(OpacityProperty, fade);
        Slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(8, TimeSpan.FromMilliseconds(110)) { EasingFunction = ease });
    }

    // ---- Interaction ---------------------------------------------------

    private void OnDeactivated(object? sender, EventArgs e) => Dismiss();

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Dismiss();
    }

    /// <summary>Close first, then run the action: most actions open another window that must take focus.</summary>
    private void Fire(Action? action)
    {
        _closing = true;
        Close();
        action?.Invoke();
    }

    private void OnRegion(object sender, RoutedEventArgs e) => Fire(TranslateRegionRequested);
    private void OnSelection(object sender, RoutedEventArgs e) => Fire(TranslateSelectionRequested);
    private void OnWord(object sender, RoutedEventArgs e) => Fire(LookupWordRequested);
    private void OnCopy(object sender, RoutedEventArgs e) => Fire(CopyTextRequested);
    private void OnLiveRegion(object sender, RoutedEventArgs e) => Fire(LiveRegionRequested);
    private void OnLiveWindow(object sender, RoutedEventArgs e) => Fire(LiveWindowRequested);
    private void OnLiveScreen(object sender, RoutedEventArgs e) => Fire(LiveScreenRequested);
    private void OnLiveStop(object sender, RoutedEventArgs e) => Fire(LiveStopRequested);
    private void OnHistory(object sender, RoutedEventArgs e) => Fire(HistoryRequested);
    private void OnSettings(object sender, RoutedEventArgs e) => Fire(SettingsRequested);
    private void OnHelp(object sender, RoutedEventArgs e) => Fire(HelpRequested);
    private void OnExit(object sender, RoutedEventArgs e) => Fire(ExitRequested);
}
