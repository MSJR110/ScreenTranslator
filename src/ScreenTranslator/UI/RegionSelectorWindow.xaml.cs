using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ScreenTranslator.Native;
using DrawingRectangle = System.Drawing.Rectangle;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace ScreenTranslator.UI;

public sealed record RegionPick(DrawingRectangle PixelRect);

/// <summary>
/// Region picking across all monitors: one dimmed window per screen (each at its own DPI),
/// the drag happens in whichever one the mouse goes down in.
/// </summary>
public static class RegionSelector
{
    public enum Purpose { Translate, Live, Copy }

    public static Task<RegionPick?> PickAsync(Purpose purpose)
    {
        var tcs = new TaskCompletionSource<RegionPick?>();
        var windows = new List<RegionSelectorWindow>();

        void Finish(RegionPick? result)
        {
            foreach (var w in windows.ToList())
            {
                w.Finished = null;
                w.Close();
            }
            tcs.TrySetResult(result);
        }

        NativeMethods.GetCursorPos(out var cursor);
        foreach (var screen in Screen.AllScreens)
        {
            var w = new RegionSelectorWindow(screen, purpose);
            w.Finished = Finish;
            windows.Add(w);
        }

        foreach (var w in windows)
            w.Show();

        // Keyboard focus goes to the monitor the mouse is on so Esc works immediately.
        var under = windows.FirstOrDefault(w => w.ScreenBounds.Contains(cursor.X, cursor.Y)) ?? windows[0];
        under.Activate();
        under.Focus();

        return tcs.Task;
    }
}

public partial class RegionSelectorWindow : Window
{
    private readonly double _scale;
    private Point _start;
    private bool _dragging;

    public DrawingRectangle ScreenBounds { get; }
    internal Action<RegionPick?>? Finished { get; set; }

    public RegionSelectorWindow(Screen screen, RegionSelector.Purpose purpose)
    {
        InitializeComponent();
        ScreenBounds = screen.Bounds;
        _scale = Dpi.ScaleFor(ScreenBounds);

        (HintText.Text, HintIcon.Text, HintIcon.Foreground) = purpose switch
        {
            RegionSelector.Purpose.Live => (Loc.T("select.live"), "", Theme.Brush("Success")),
            RegionSelector.Purpose.Copy => (Loc.T("select.copy"), "", Theme.Brush("Accent")),
            _ => (Loc.T("select.translate"), "", Theme.Brush("Accent")),
        };
        FrameGlow.Color = ((SolidColorBrush)Theme.Brush("Accent")).Color;
        Hint.Visibility = screen.Primary ? Visibility.Visible : Visibility.Collapsed;

        // Size in DIPs for layout; actual placement is done in pixels once the handle exists.
        Width = ScreenBounds.Width / _scale;
        Height = ScreenBounds.Height / _scale;

        SourceInitialized += (_, _) =>
        {
            WindowEffects.HideFromAltTab(this);
            Dpi.SetPixelBounds(this, ScreenBounds);
        };

        Loaded += (_, _) =>
        {
            FullRect.Rect = new Rect(0, 0, ActualWidth, ActualHeight);
            HoleRect.Rect = Rect.Empty;

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            Dim.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(140)) { EasingFunction = ease });
            HintSlide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });

            // Guides follow the mouse until the drag starts.
            NativeMethods.GetCursorPos(out var c);
            if (ScreenBounds.Contains(c.X, c.Y))
                MoveGuides(new Point((c.X - ScreenBounds.X) / _scale, (c.Y - ScreenBounds.Y) / _scale));
        };
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Cancel();
    }

    private void OnCancelClick(object sender, MouseButtonEventArgs e) => Cancel();

    private void Cancel() => Finished?.Invoke(null);

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _start = e.GetPosition(Root);
        _dragging = true;
        Hint.Visibility = Visibility.Collapsed;
        GuideH.Visibility = GuideV.Visibility = Visibility.Collapsed;
        Frame.Visibility = Visibility.Visible;
        Handles.Visibility = Visibility.Visible;
        SizeBadge.Visibility = Visibility.Visible;
        CaptureMouse();
        Activate();
        UpdateSelection(_start);
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var p = e.GetPosition(Root);
        if (_dragging) UpdateSelection(p);
        else MoveGuides(p);
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (!_dragging) GuideH.Visibility = GuideV.Visibility = Visibility.Collapsed;
    }

    private void MoveGuides(Point p)
    {
        GuideH.Visibility = GuideV.Visibility = Visibility.Visible;
        GuideH.X1 = 0; GuideH.X2 = ActualWidth; GuideH.Y1 = GuideH.Y2 = Math.Round(p.Y) + 0.5;
        GuideV.Y1 = 0; GuideV.Y2 = ActualHeight; GuideV.X1 = GuideV.X2 = Math.Round(p.X) + 0.5;
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        ReleaseMouseCapture();

        var local = CurrentRect(e.GetPosition(Root));
        if (local.Width < 4 || local.Height < 4)
        {
            Cancel();
            return;
        }

        Finished?.Invoke(new RegionPick(ToPixels(local)));
    }

    private Rect CurrentRect(Point current)
    {
        var w = Math.Abs(current.X - _start.X);
        var h = Math.Abs(current.Y - _start.Y);
        return new Rect(Math.Min(_start.X, current.X), Math.Min(_start.Y, current.Y), w, h);
    }

    private void UpdateSelection(Point current)
    {
        var r = CurrentRect(current);
        HoleRect.Rect = r;

        System.Windows.Controls.Canvas.SetLeft(Frame, r.X);
        System.Windows.Controls.Canvas.SetTop(Frame, r.Y);
        Frame.Width = r.Width;
        Frame.Height = r.Height;
        PlaceHandles(r);

        var px = ToPixels(r);
        SizeText.Text = $"{px.Width} × {px.Height}";

        double badgeY = r.Bottom + 8;
        if (badgeY + 24 > ActualHeight) badgeY = r.Top - 30;
        System.Windows.Controls.Canvas.SetLeft(SizeBadge, r.X);
        System.Windows.Controls.Canvas.SetTop(SizeBadge, badgeY);
    }

    /// <summary>Short L-shaped brackets on the four corners, drawn just outside the frame.</summary>
    private void PlaceHandles(Rect r)
    {
        double len = Math.Clamp(Math.Min(r.Width, r.Height) * 0.18, 6, 18);
        const double o = 2.5;   // sit slightly outside so the bracket hugs the frame
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        H1.Data = Geometry.Parse(string.Create(ci, $"M{r.Left - o},{r.Top - o + len} V{r.Top - o} H{r.Left - o + len}"));
        H2.Data = Geometry.Parse(string.Create(ci, $"M{r.Right + o - len},{r.Top - o} H{r.Right + o} V{r.Top - o + len}"));
        H3.Data = Geometry.Parse(string.Create(ci, $"M{r.Right + o},{r.Bottom + o - len} V{r.Bottom + o} H{r.Right + o - len}"));
        H4.Data = Geometry.Parse(string.Create(ci, $"M{r.Left - o + len},{r.Bottom + o} H{r.Left - o} V{r.Bottom + o - len}"));
    }

    /// <summary>Demo hook: show the selector mid-drag without any mouse input.</summary>
    internal void DemoDrag(Point from, Point to)
    {
        _start = from;
        Hint.Visibility = Visibility.Collapsed;
        GuideH.Visibility = GuideV.Visibility = Visibility.Collapsed;
        Frame.Visibility = Handles.Visibility = SizeBadge.Visibility = Visibility.Visible;
        UpdateSelection(to);
    }

    private DrawingRectangle ToPixels(Rect local)
    {
        int x = ScreenBounds.X + (int)Math.Round(local.X * _scale);
        int y = ScreenBounds.Y + (int)Math.Round(local.Y * _scale);
        int w = (int)Math.Round(local.Width * _scale);
        int h = (int)Math.Round(local.Height * _scale);
        return new DrawingRectangle(x, y, Math.Max(1, w), Math.Max(1, h));
    }
}
