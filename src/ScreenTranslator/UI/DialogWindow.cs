using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace ScreenTranslator.UI;

public enum DialogKind { Question, Warning, Error }

/// <summary>
/// The app's own message box. Windows' MessageBox is a grey system dialog: it ignores the theme, the rounded-glass
/// language every other window speaks, and the interface direction — in Persian it puts right-to-left text in a
/// left-to-right frame under English "Yes/No" buttons. This is the same warm glass as the toast, modal, localized.
/// </summary>
public sealed class DialogWindow : Window
{
    /// <summary>Question with a confirm/cancel pair. True when the user confirmed.</summary>
    public static bool Confirm(Window? owner, string title, string? body = null, string? confirmText = null, string glyph = "\uE897")
        => new DialogWindow(owner, title, body, glyph, DialogKind.Question,
            confirmText ?? Loc.T("common.confirm"), Loc.T("common.cancel")).ShowDialog() == true;

    /// <summary>Notice with a single button.</summary>
    public static void Alert(Window? owner, string title, string? body = null, DialogKind kind = DialogKind.Warning)
        => new DialogWindow(owner, title, body, kind == DialogKind.Error ? "\uE783" : "\uE7BA", kind,
            Loc.T("common.ok"), null).ShowDialog();

    private readonly Border _shell;
    private readonly ScaleTransform _pop = new(0.96, 0.96);

    private DialogWindow(Window? owner, string title, string? body, string glyph, DialogKind kind, string primaryText, string? cancelText)
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        SizeToContent = SizeToContent.Height;
        Width = 420;                       // fixed: the shadow margin takes 48 of it, the text wraps in the rest
        WindowStartupLocation = WindowStartupLocation.Manual;
        UseLayoutRounding = true;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        FlowDirection = Loc.Flow;
        if (owner is { IsVisible: true }) Owner = owner;
        else Topmost = true;               // no owner to sit above (startup failures) — don't get lost behind windows

        // ---- icon tile + text -------------------------------------------------
        var icon = new TextBlock
        {
            Text = glyph,
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 17,
            Foreground = Theme.Brush(kind == DialogKind.Question ? "Accent" : "Error"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FlowDirection = FlowDirection.LeftToRight,
        };
        var tile = new Border
        {
            Width = 42, Height = 42, CornerRadius = new CornerRadius(14),
            Background = Theme.Brush("AccentGradientSoft"),
            BorderBrush = Theme.Brush("Rim"), BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Top,
            Child = icon,
        };

        var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        texts.Children.Add(new TextBlock
        {
            Text = title,
            FontFamily = Theme.AppFont, FontSize = 14.5, FontWeight = FontWeights.SemiBold,
            Foreground = Theme.Brush("Text"), TextWrapping = TextWrapping.Wrap,
            FlowDirection = Loc.DetectFlow(title),          // an OS error message may read the other way
        });
        if (!string.IsNullOrWhiteSpace(body))
            texts.Children.Add(new TextBlock
            {
                Text = body,
                FontFamily = Theme.AppFont, FontSize = 12.5, Foreground = Theme.Brush("Muted"),
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0),
                FlowDirection = Loc.DetectFlow(body),
            });

        var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 18) };
        head.Children.Add(tile);
        head.Children.Add(texts);
        texts.MaxWidth = Width - 2 * 24 - 2 * 22 - 56;      // window − shell margin − padding − tile

        // ---- buttons ----------------------------------------------------------
        var primary = new Button
        {
            Content = primaryText,
            Style = (Style)FindResource("AccentButton"),
            MinWidth = 104,
            IsDefault = true,
            IsCancel = cancelText is null,                   // a one-button notice also closes on Esc
            // A lone button has nothing to sit beside, so it takes the width instead of hugging a corner.
            HorizontalAlignment = cancelText is null ? HorizontalAlignment.Stretch : HorizontalAlignment.Left,
        };
        primary.Click += (_, _) => { DialogResult = true; };

        FrameworkElement actions = primary;
        if (cancelText is not null)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            row.Children.Add(primary);
            row.Children.Add(new Button
            {
                Content = cancelText,
                Style = (Style)FindResource("FlatButton"),
                MinWidth = 104,
                Margin = new Thickness(10, 0, 0, 0),
                IsCancel = true,
            });
            actions = row;
        }

        var content = new StackPanel();
        content.Children.Add(head);
        content.Children.Add(actions);

        _shell = new Border
        {
            Background = Theme.Brush("Surface"),
            BorderBrush = Theme.Brush("Rim"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(22, 20, 22, 18),
            Margin = new Thickness(24, 18, 24, 30),          // room for the shadow
            Child = content,
            RenderTransform = _pop,
            RenderTransformOrigin = new Point(0.5, 0.5),
            Effect = new DropShadowEffect { BlurRadius = 30, ShadowDepth = 6, Opacity = 0.4 },
        };
        _shell.MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
        Content = _shell;

        Loaded += (_, _) => { UpdateLayout(); Place(); Appear(); primary.Focus(); };
    }

    /// <summary>
    /// Centre on the owner, or on the monitor under the cursor when there is none, in physical pixels —
    /// the owner may live on a monitor with a different scale than the one this window was created on.
    /// </summary>
    private void Place()
    {
        var target = Owner is { IsVisible: true } o
            ? Native.WindowEffects.GetWindowBounds(Native.WindowEffects.Handle(o))
            : null;

        if (target is null)
        {
            Native.NativeMethods.GetCursorPos(out var c);
            target = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(c.X, c.Y)).WorkingArea;
        }

        var r = target.Value;
        double scale = Dpi.ScaleFor(r);
        int w = (int)Math.Round(ActualWidth * scale), h = (int)Math.Round(ActualHeight * scale);
        int x = r.Left + (r.Width - w) / 2, y = r.Top + (r.Height - h) / 2;

        // An owner dragged half off-screen must not take the dialog with it.
        var work = System.Windows.Forms.Screen.FromRectangle(r).WorkingArea;
        x = Math.Clamp(x, work.Left, Math.Max(work.Left, work.Right - w));
        y = Math.Clamp(y, work.Top, Math.Max(work.Top, work.Bottom - h));
        Dpi.SetPixelPosition(this, x, y);
    }

    private void Appear()
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        _shell.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)) { EasingFunction = ease });
        var grow = new DoubleAnimation(1, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease };
        _pop.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        _pop.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
    }
}
