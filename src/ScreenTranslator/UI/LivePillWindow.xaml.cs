using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ScreenTranslator.Native;
using DrawingRectangle = System.Drawing.Rectangle;

namespace ScreenTranslator.UI;

/// <summary>Small floating controller shown while live mode runs: status, pause, re-select, stop.</summary>
public partial class LivePillWindow : Window
{
    private bool _paused;
    private bool _userMoved;

    public event Action? PauseToggled;
    public event Action? ReselectRequested;
    public event Action? StopRequested;

    public LivePillWindow(bool canReselect)
    {
        InitializeComponent();
        ReselectButton.Visibility = canReselect ? Visibility.Visible : Visibility.Collapsed;

        SourceInitialized += (_, _) =>
        {
            WindowEffects.HideFromAltTab(this);
            WindowEffects.ExcludeFromCapture(this);   // never OCR our own controls
        };

        Loaded += (_, _) => StartPulse();
    }

    private void StartPulse()
    {
        var scale = new ScaleTransform(1, 1, 5, 5);
        Pulse.RenderTransform = scale;
        var grow = new DoubleAnimation(1, 1.9, TimeSpan.FromMilliseconds(1100)) { RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() };
        var fade = new DoubleAnimation(0.5, 0, TimeSpan.FromMilliseconds(1100)) { RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        Pulse.BeginAnimation(OpacityProperty, fade);
    }

    /// <summary>Dock above the region's top edge (or just inside it when there's no room). Respects a manual drag.</summary>
    public void DockTo(DrawingRectangle regionPx)
    {
        if (_userMoved) return;
        if (!IsVisible) Show();
        UpdateLayout();

        double scale = Dpi.ScaleFor(regionPx);
        int w = (int)Math.Round(ActualWidth * scale);
        int h = (int)Math.Round(ActualHeight * scale);

        int x = regionPx.X + (regionPx.Width - w) / 2;
        int y = regionPx.Y - h + (int)(6 * scale);
        var area = System.Windows.Forms.Screen.FromRectangle(regionPx).WorkingArea;
        if (y < area.Top) y = regionPx.Y + (int)(4 * scale);
        x = Math.Clamp(x, area.Left, Math.Max(area.Left, area.Right - w));
        Dpi.SetPixelPosition(this, x, y);
    }

    public void SetStats(string text) => Stats.Text = text;

    public void SetState(LivePillState state)
    {
        var (brushKey, title) = state switch
        {
            LivePillState.Paused => ("Muted", "مکث"),
            LivePillState.Working => ("Accent", "در حال ترجمه…"),
            LivePillState.Error => ("Error", "خطا"),
            LivePillState.Waiting => ("Muted", "پنجره پیدا نشد"),
            _ => ("Success", "ترجمه‌ی زنده"),
        };
        Dot.Fill = Theme.Brush(brushKey);
        Pulse.Fill = Theme.Brush(brushKey);
        TitleText.Text = title;
    }

    private void OnPause(object sender, RoutedEventArgs e)
    {
        _paused = !_paused;
        PauseButton.Content = _paused ? "" : "";
        PauseButton.ToolTip = _paused ? "ادامه" : "مکث";
        PauseToggled?.Invoke();
    }

    private void OnReselect(object sender, RoutedEventArgs e) => ReselectRequested?.Invoke();
    private void OnStop(object sender, RoutedEventArgs e) => StopRequested?.Invoke();

    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;
        _userMoved = true;
        try { DragMove(); } catch { /* released before drag started */ }
    }
}

public enum LivePillState { Running, Working, Paused, Waiting, Error }
