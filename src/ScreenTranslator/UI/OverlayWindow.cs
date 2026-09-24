using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ScreenTranslator.Native;
using ScreenTranslator.Services;
using DrawingRectangle = System.Drawing.Rectangle;

namespace ScreenTranslator.UI;

/// <summary>
/// Click-through, capture-excluded window that paints translated text exactly where the original text sits.
/// Each box borrows the surface underneath it: a strip of pixels from just above and below the original line is
/// stretched behind the Persian text (so gradients and cards survive), the edges are feathered, headings keep their
/// weight, and text over photos gets an outline instead of a patch. The result reads as if the page were written in Persian.
/// </summary>
public sealed class OverlayWindow : Window
{
    /// <summary>Breathing room around the text inside a box (DIP).</summary>
    private const double PadX = 4, PadY = 1;

    /// <summary>
    /// How far the surface reaches beyond the box sideways (DIP), fading out over that distance. Vertically it stays
    /// tight so a rule right under a heading is not painted over; the texture makes hard top/bottom edges invisible anyway.
    /// </summary>
    private const double Feather = 4;

    private sealed class Box
    {
        public required Grid Root;
        public required Border Surface;
        public required TextBlock Text;
        public required string SourceText;
        public double Width;
        public bool SingleLine;
    }

    private readonly Canvas _canvas = new();
    private readonly Dictionary<string, Box> _boxes = new();
    private readonly DispatcherTimer _peekTimer;
    private bool _peeking;

    public double BoxOpacity { get; set; } = 1.0;
    /// <summary>Language the boxes are painted in — decides which way each box reads.</summary>
    public string TargetLanguage { get; init; } = "fa";

    public double FontScale { get; set; } = 1.0;

    /// <summary>Hold this virtual key to temporarily hide the overlay and read the original.</summary>
    public int PeekKey { get; set; } = NativeMethods.VK_CONTROL;

    /// <summary>The overlay's content, for offline rendering in demos.</summary>
    public Visual Surface => _canvas;

    public OverlayWindow()
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
        Content = _canvas;
        UseLayoutRounding = true;
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

        SourceInitialized += (_, _) =>
        {
            WindowEffects.MakeClickThrough(this);
            WindowEffects.ExcludeFromCapture(this);
        };

        _peekTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(60) };
        _peekTimer.Tick += (_, _) =>
        {
            bool down = NativeMethods.IsKeyDown(PeekKey);
            if (down == _peeking) return;
            _peeking = down;
            _canvas.BeginAnimation(OpacityProperty, new DoubleAnimation(down ? 0.06 : 1.0, TimeSpan.FromMilliseconds(120)));
        };
    }

    private double _scale = 1.0;

    public void SetRegion(DrawingRectangle px)
    {
        _scale = Dpi.ScaleFor(px);
        Width = px.Width / _scale;
        Height = px.Height / _scale;
        Dpi.SetPixelBounds(this, px);
        if (!IsVisible) Show();
        Dpi.SetPixelBounds(this, px);   // WPF may re-place the window on Show; pin it again
        _peekTimer.Start();
    }

    public void Clear()
    {
        _canvas.Children.Clear();
        _boxes.Clear();
    }

    public void Apply(LiveFrame frame)
    {
        var blocks = frame.Blocks;
        var rects = blocks.Select(b => new Rect(
            b.Source.Rect.X / _scale, b.Source.Rect.Y / _scale,
            b.Source.Rect.Width / _scale, b.Source.Rect.Height / _scale)).ToList();
        var keys = blocks.Select(b => b.Source.Text + "@" + b.Source.Rect.Y / 8).ToList();
        var keep = new HashSet<string>(keys);

        // Boxes whose line disappeared this frame; if the same text shows up elsewhere (the page scrolled),
        // the box slides there instead of blinking out and back in.
        var orphans = _boxes.Where(kv => !keep.Contains(kv.Key))
            .GroupBy(kv => kv.Value.SourceText)
            .ToDictionary(g => g.Key, g => new Queue<KeyValuePair<string, Box>>(g));

        for (int i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            var rect = rects[i];
            var (roomLeft, roomRight) = Room(rects, i, block, frame.Pixels);

            if (_boxes.TryGetValue(keys[i], out var existing))
            {
                Dress(existing, block, frame.Pixels, Place(existing, rect, animate: false));
                continue;
            }

            if (orphans.TryGetValue(block.Source.Text, out var queue) && queue.Count > 0)
            {
                var (oldKey, box) = queue.Dequeue();
                _boxes.Remove(oldKey);
                _boxes[keys[i]] = box;
                Dress(box, block, frame.Pixels, Place(box, rect, animate: true));
                continue;
            }

            var fresh = Build(block, rect, roomLeft, roomRight);
            _boxes[keys[i]] = fresh;
            _canvas.Children.Add(fresh.Root);
            Dress(fresh, block, frame.Pixels, Place(fresh, rect, animate: false));

            fresh.Root.Opacity = 0;
            fresh.Root.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(180)) { EasingFunction = new CubicEase() });
        }

        foreach (var (key, box) in _boxes.ToList())
        {
            if (keep.Contains(key)) continue;
            _boxes.Remove(key);
            var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(120));
            fade.Completed += (_, _) => _canvas.Children.Remove(box.Root);
            box.Root.BeginAnimation(OpacityProperty, fade);
        }
    }

    /// <summary>
    /// How far a single-line box may grow to the left/right of its original before it would run into another line
    /// on the same rows, off the surface it sits on (card, button), or off the region.
    /// </summary>
    private (double left, double right) Room(List<Rect> rects, int i, LiveBlock block, RegionPixels pixels)
    {
        var r = rects[i];
        const double gap = 8;
        var (surfaceLeft, surfaceRight) = pixels.SurfaceExtent(block.Source.Rect);
        double leftEdge = Math.Max(0, surfaceLeft / _scale + 2), rightEdge = Math.Min(Width, surfaceRight / _scale - 2);
        for (int j = 0; j < rects.Count; j++)
        {
            if (j == i) continue;
            var o = rects[j];
            if (o.Top >= r.Bottom || o.Bottom <= r.Top) continue;     // different rows
            if (o.Left >= r.Right - 2) rightEdge = Math.Min(rightEdge, o.Left - gap);
            else if (o.Right <= r.Left + 2) leftEdge = Math.Max(leftEdge, o.Right + gap);
        }
        return (Math.Max(0, r.Right - leftEdge), Math.Max(0, rightEdge - r.Left));
    }

    private Box Build(LiveBlock block, Rect rect, double roomLeft, double roomRight)
    {
        var text = new TextBlock
        {
            Text = block.Translation,
            FontFamily = Theme.AppFont,
            FontWeight = block.Bold ? FontWeights.Bold : FontWeights.Normal,
            FlowDirection = Loc.FlowOf(TargetLanguage),
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = block.Source.LineCount > 1 ? TextAlignment.Justify
                          : Loc.IsRtl(TargetLanguage) ? TextAlignment.Right : TextAlignment.Left,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(PadX - 1, PadY, PadX - 1, PadY),   // 1 DIP slack: glyph overhangs would be clipped otherwise
        };

        // The surface sits behind the text and reaches a little beyond the box, fading out at both ends so it
        // dissolves into the page instead of ending in a hard vertical edge.
        var surface = new Border
        {
            Margin = new Thickness(-Feather, 0, -Feather, 0),
            CornerRadius = new CornerRadius(2),
        };

        var root = new Grid { SnapsToDevicePixels = true };
        root.Children.Add(surface);
        root.Children.Add(text);

        var box = new Box { Root = root, Surface = surface, Text = text, SourceText = block.Source.Text, SingleLine = block.Source.LineCount == 1 };
        box.Width = FitText(text, rect, block.Source.LineCount, block.Source.LineHeight / _scale, roomLeft, roomRight);
        return box;
    }

    /// <summary>Colours and background texture come from the current frame, so a box follows hover/theme changes underneath it.</summary>
    private void Dress(Box box, LiveBlock block, RegionPixels pixels, double left)
    {
        var fg = Color.FromRgb(block.Foreground.R, block.Foreground.G, block.Foreground.B);
        var bg = Color.FromRgb(block.Background.R, block.Background.G, block.Background.B);

        if (block.Busy)
        {
            // Photo caption: no patch, just a soft dark/light veil and a halo around the glyphs.
            bool dark = RegionPixels.Luminance(block.Background) < 0.55;
            box.Text.Foreground = new SolidColorBrush(dark ? Colors.White : Color.FromRgb(20, 20, 20));
            box.Text.Effect = new DropShadowEffect
            {
                ShadowDepth = 0, BlurRadius = 6, Opacity = 0.95, Color = dark ? Colors.Black : Colors.White,
                RenderingBias = RenderingBias.Performance,
            };
            box.Surface.Background = new SolidColorBrush(Color.FromArgb((byte)(BoxOpacity * 110), bg.R, bg.G, bg.B));
            return;
        }

        box.Text.Foreground = new SolidColorBrush(fg);
        box.Text.Effect = null;

        // Box footprint in region pixels, including the feathered margin.
        int x0 = (int)Math.Floor((left - Feather) * _scale);
        int x1 = (int)Math.Ceiling((left + box.Width + 2 * PadX + Feather) * _scale);
        var bytes = pixels.EdgeTexture(block.Source.Rect, x0, x1, block.Background);
        int w = bytes.Length / 8;
        var texture = BitmapSource.Create(w, 2, 96, 96, PixelFormats.Bgra32, null, bytes, w * 4);
        texture.Freeze();
        var brush = new ImageBrush(texture) { Stretch = Stretch.Fill, Opacity = BoxOpacity };
        RenderOptions.SetBitmapScalingMode(brush, BitmapScalingMode.Linear);
        box.Surface.Background = brush;
    }

    /// <summary>
    /// Choose font size and box width. Single lines may grow wider than the original (Persian rarely matches English
    /// width exactly) before we start shrinking; paragraphs keep the original width and may grow a little taller.
    /// Returns the text width to use (without padding).
    /// </summary>
    private double FitText(TextBlock text, Rect rect, int lineCount, double lineHeightDip, double roomLeft, double roomRight)
    {
        double size = Math.Max(9, lineHeightDip * 0.86 * FontScale);
        double minSize = Math.Max(8.5, size * 0.62);

        if (lineCount == 1)
        {
            // Can extend to whichever side has more room, up to 1.7x the original width.
            double maxWidth = Math.Max(rect.Width * 0.6, Math.Min(rect.Width * 1.7 + 24, Math.Max(roomRight, roomLeft) - 2 * PadX));
            for (int i = 0; i < 16; i++)
            {
                text.FontSize = size;
                text.LineHeight = size * 1.4;
                text.TextWrapping = TextWrapping.NoWrap;
                text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                if (text.DesiredSize.Width - 2 * PadX <= maxWidth || size <= minSize) break;
                size *= 0.94;
            }

            double measured = text.DesiredSize.Width - 2 * PadX;
            if (measured > maxWidth)
            {
                // Still too long at the minimum size: wrap onto two lines instead.
                text.TextWrapping = TextWrapping.Wrap;
                text.Measure(new Size(maxWidth + 2 * PadX, double.PositiveInfinity));
                return maxWidth;
            }
            return Math.Max(rect.Width, measured);
        }

        double width = Math.Max(24, rect.Width);
        double maxHeight = rect.Height * 1.12 + 2;
        for (int i = 0; i < 16; i++)
        {
            text.FontSize = size;
            text.LineHeight = size * 1.42;
            text.Measure(new Size(width + 2 * PadX, double.PositiveInfinity));
            if (text.DesiredSize.Height - 2 * PadY <= maxHeight || size <= minSize) break;
            size *= 0.94;
        }
        return width;
    }

    /// <summary>Positions the box over its original line; returns the final left edge (DIP).</summary>
    private double Place(Box box, Rect rect, bool animate)
    {
        double width = box.Width + 2 * PadX;

        // Anchor to the original's left edge; if the wider box would run off the region, anchor to its right edge instead.
        double left = rect.X - PadX;
        if (left + width > Width && rect.Right + PadX - width >= 0)
            left = rect.Right + PadX - width;
        double top = rect.Y - PadY;
        double surfaceHeight = rect.Height + 2 * PadY;
        box.Root.Width = width;
        box.Root.MinHeight = surfaceHeight;

        if (box.SingleLine)
        {
            // Persian ascenders/descenders run taller than the Latin line; let the glyphs overhang the painted
            // surface rather than growing it over whatever sits right above or below the original line.
            box.Surface.Height = surfaceHeight;
            box.Surface.VerticalAlignment = VerticalAlignment.Center;
            box.Root.Measure(new Size(width, double.PositiveInfinity));
            top -= Math.Max(0, box.Root.DesiredSize.Height - surfaceHeight) / 2;
        }

        double oldLeft = Canvas.GetLeft(box.Root), oldTop = Canvas.GetTop(box.Root);
        Canvas.SetLeft(box.Root, left);
        Canvas.SetTop(box.Root, top);

        double total = width + 2 * Feather, f = Feather / total;
        box.Surface.OpacityMask = new LinearGradientBrush(
            new GradientStopCollection
            {
                new(Colors.Transparent, 0), new(Colors.Black, f), new(Colors.Black, 1 - f), new(Colors.Transparent, 1),
            }, new Point(0, 0), new Point(1, 0));

        if (animate && !double.IsNaN(oldLeft) && !double.IsNaN(oldTop) && (oldLeft != left || oldTop != top))
        {
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var dur = TimeSpan.FromMilliseconds(170);
            box.Root.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation(oldLeft, left, dur) { EasingFunction = ease, FillBehavior = FillBehavior.Stop });
            box.Root.BeginAnimation(Canvas.TopProperty, new DoubleAnimation(oldTop, top, dur) { EasingFunction = ease, FillBehavior = FillBehavior.Stop });
        }
        return left;
    }

    protected override void OnClosed(EventArgs e)
    {
        _peekTimer.Stop();
        base.OnClosed(e);
    }
}
