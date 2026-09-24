using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using ScreenTranslator.Native;
using DrawingRectangle = System.Drawing.Rectangle;

namespace ScreenTranslator.UI;

/// <summary>A brief accent-colored outline that flashes around something on screen (the word that was looked up).</summary>
public sealed class HighlightWindow : Window
{
    private const int Pad = 5;

    public static void Flash(DrawingRectangle px)
    {
        var w = new HighlightWindow(px);
        w.Show();
    }

    private HighlightWindow(DrawingRectangle px)
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        Focusable = false;
        IsHitTestVisible = false;

        double scale = Dpi.ScaleFor(px);
        var bounds = px;
        bounds.Inflate((int)(Pad * scale) + 8, (int)(Pad * scale) + 8);
        Width = bounds.Width / scale;
        Height = bounds.Height / scale;

        var accent = ((SolidColorBrush)Theme.Brush("Accent")).Color;
        var shape = new Rectangle
        {
            Margin = new Thickness(8),
            RadiusX = 5, RadiusY = 5,
            Stroke = new SolidColorBrush(accent),
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Color.FromArgb(0x30, accent.R, accent.G, accent.B)),
            Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = accent, BlurRadius = 14, ShadowDepth = 0, Opacity = 0.8 },
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new ScaleTransform(1.15, 1.25),
        };
        Content = shape;

        SourceInitialized += (_, _) =>
        {
            WindowEffects.HideFromAltTab(this);
            WindowEffects.MakeClickThrough(this);
            WindowEffects.ExcludeFromCapture(this);
            Dpi.SetPixelBounds(this, bounds);
        };

        Loaded += (_, _) =>
        {
            Dpi.SetPixelBounds(this, bounds);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var st = (ScaleTransform)shape.RenderTransform;
            st.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease });
            st.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease });

            var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(500)) { BeginTime = TimeSpan.FromMilliseconds(600) };
            fade.Completed += (_, _) => Close();
            shape.BeginAnimation(OpacityProperty, fade);
        };
    }
}
