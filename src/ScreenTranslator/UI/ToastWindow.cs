using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using ScreenTranslator.Native;

namespace ScreenTranslator.UI;

public enum ToastKind { Info, Success, Warning, Error }

/// <summary>
/// Small non-activating HUD pill at the top of the monitor under the cursor. Replaces the WinForms balloon for
/// transient feedback ("copied", "ready", soft errors). One instance is reused; a new message restarts the timer.
/// </summary>
public sealed class ToastWindow : Window
{
    private static ToastWindow? _current;

    private readonly TextBlock _icon;
    private readonly TextBlock _title;
    private readonly TextBlock _detail;
    private readonly Border _shell;
    private readonly TranslateTransform _slide = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(2600) };
    private bool _closing;

    public static void Show(string title, string? detail = null, ToastKind kind = ToastKind.Info, int durationMs = 2600)
    {
        if (_current is { _closing: false } t)
        {
            t.Update(title, detail, kind, durationMs);
            return;
        }
        _current = new ToastWindow(title, detail, kind, durationMs);
        _current.Present();
    }

    private ToastWindow(string title, string? detail, ToastKind kind, int durationMs)
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        Focusable = false;
        SizeToContent = SizeToContent.WidthAndHeight;
        UseLayoutRounding = true;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        FlowDirection = Loc.Flow;

        _icon = new TextBlock
        {
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 15,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            FlowDirection = FlowDirection.LeftToRight,
        };
        _title = new TextBlock { FontFamily = Theme.AppFont, FontSize = 13.5, FontWeight = FontWeights.SemiBold, Foreground = Theme.Brush("Text"), VerticalAlignment = VerticalAlignment.Center };
        _detail = new TextBlock { FontFamily = Theme.AppFont, FontSize = 12, Foreground = Theme.Brush("Muted"), Margin = new Thickness(0, 1, 0, 0), TextWrapping = TextWrapping.Wrap, MaxWidth = 380 };

        var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        texts.Children.Add(_title);
        texts.Children.Add(_detail);

        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(new Border
        {
            Width = 28, Height = 28, CornerRadius = new CornerRadius(14),
            Background = Theme.Brush("AccentGradientSoft"), BorderBrush = Theme.Brush("Rim"), BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center, Child = _icon,
        });
        row.Children.Add(texts);

        _shell = new Border
        {
            Background = Theme.Brush("Surface"),
            BorderBrush = Theme.Brush("Rim"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(12, 8, 16, 8),
            Margin = new Thickness(24, 12, 24, 28),   // room for the shadow
            Child = row,
            RenderTransform = _slide,
            Effect = new DropShadowEffect { BlurRadius = 24, ShadowDepth = 4, Opacity = 0.35 },
        };
        Content = _shell;

        Update(title, detail, kind, durationMs);

        SourceInitialized += (_, _) =>
        {
            WindowEffects.HideFromAltTab(this);
            WindowEffects.MakeClickThrough(this);
            WindowEffects.ExcludeFromCapture(this);   // live mode must never OCR our own HUD
        };

        _timer.Tick += (_, _) => Dismiss();
    }

    private void Update(string title, string? detail, ToastKind kind, int durationMs)
    {
        _title.Text = title;
        _detail.Text = detail ?? "";
        _detail.Visibility = string.IsNullOrEmpty(detail) ? Visibility.Collapsed : Visibility.Visible;

        var (glyph, brush) = kind switch
        {
            ToastKind.Success => ("", "Success"),
            ToastKind.Warning => ("", "Error"),
            ToastKind.Error => ("", "Error"),
            _ => ("", "Accent"),
        };
        _icon.Text = glyph;
        _icon.Foreground = Theme.Brush(brush);

        _timer.Stop();
        _timer.Interval = TimeSpan.FromMilliseconds(durationMs);
        _timer.Start();

        if (IsVisible) { UpdateLayout(); Place(); }
    }

    private void Present()
    {
        NativeMethods.GetCursorPos(out var c);
        var area = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(c.X, c.Y)).WorkingArea;
        _anchor = area;

        Left = Dpi.ToDip(area.Left + area.Width / 2, area.Top).X;   // land on the right monitor before measuring
        Top = Dpi.ToDip(area.Left, area.Top).Y;
        Show();
        UpdateLayout();
        Place();

        _shell.Opacity = 0;
        _slide.Y = -14;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        _shell.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(180)) { EasingFunction = ease });
        _slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });
    }

    private System.Drawing.Rectangle _anchor;

    private void Place()
    {
        double scale = Dpi.ScaleFor(_anchor);
        int w = (int)Math.Round(ActualWidth * scale);
        int x = _anchor.Left + (_anchor.Width - w) / 2;
        int y = _anchor.Top + (int)(18 * scale);
        Dpi.SetPixelPosition(this, x, y);
    }

    private void Dismiss()
    {
        if (_closing) return;
        _closing = true;
        _timer.Stop();
        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(160)) { EasingFunction = ease };
        fade.Completed += (_, _) => { if (ReferenceEquals(_current, this)) _current = null; Close(); };
        _shell.BeginAnimation(OpacityProperty, fade);
        _slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-10, TimeSpan.FromMilliseconds(160)) { EasingFunction = ease });
    }
}
