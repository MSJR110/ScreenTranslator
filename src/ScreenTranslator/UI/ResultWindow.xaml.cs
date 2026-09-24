using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shell;
using ScreenTranslator.Models;
using ScreenTranslator.Native;
using ScreenTranslator.Services;

namespace ScreenTranslator.UI;

public partial class ResultWindow : Window
{
    private readonly SpeechService? _speech;
    private bool _pinned;
    private bool _acrylic;
    private string _sourceLanguage = "en";

    /// <summary>Language the text is translated into — decides which way the translation reads.</summary>
    public string TargetLanguage { get; init; } = "fa";

    private readonly Size? _remembered;

    /// <summary>Raised on close with the size the user dragged the popup to (null when never resized).</summary>
    public event Action<Size?>? SizeSettled;

    public ResultWindow(SpeechService? speech, double fontSize, Size? rememberedSize = null)
    {
        InitializeComponent();
        _speech = speech;
        _remembered = rememberedSize is { Width: >= 320, Height: >= 170 } ? rememberedSize : null;
        TranslationBox.FontSize = fontSize;
        SpeakButton.Visibility = speech is null ? Visibility.Collapsed : Visibility.Visible;

        // Borderless, rounded, acrylic on Windows 11; painted fallback elsewhere.
        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight = 0,
            ResizeBorderThickness = new Thickness(6),
            GlassFrameThickness = new Thickness(-1),
            UseAeroCaptionButtons = false,
        });

        SourceInitialized += (_, _) =>
        {
            WindowEffects.HideFromAltTab(this);
            _acrylic = WindowEffects.TryApplyAcrylic(this, Theme.IsDark);
            if (!_acrylic)
            {
                Shell.Background = Theme.Brush("Surface");
                WindowEffects.SetRoundedCorners(this);
            }
        };

        Loaded += (_, _) => AnimateIn();
        StartShimmer();
    }

    // ---- Content -------------------------------------------------------

    public void SetStatus(string text) => StatusText.Text = text;

    /// <summary>The translated text reads in its own direction, whatever language the interface is in.</summary>
    private void ApplyTargetDirection()
    {
        bool rtl = Loc.IsRtl(TargetLanguage);
        TransGrid.FlowDirection = rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        TranslationBox.FlowDirection = TransGrid.FlowDirection;
        TranslationBox.TextAlignment = rtl ? TextAlignment.Right : TextAlignment.Left;
    }

    public void SetOriginal(string text)
    {
        OriginalBox.Text = text;
        bool has = !string.IsNullOrWhiteSpace(text);
        OriginalBox.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
        Divider.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
    }

    public void SetTranslation(TranslationResult result, TimeSpan? ocrElapsed = null)
    {
        _sourceLanguage = result.SourceLanguage;
        ApplyTargetDirection();
        Loading.Visibility = Visibility.Collapsed;
        TranslationBox.Text = result.Text;
        TranslationBox.Opacity = 0;
        TranslationBox.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(220)));

        var parts = new List<string> { Loc.T("result.langpair", Loc.LanguageName(result.SourceLanguage), Loc.LanguageName(TargetLanguage)) };
        if (ocrElapsed is { } o) parts.Add($"OCR {o.TotalMilliseconds:0}ms");
        parts.Add(result.FromCache ? Loc.T("common.cached") : $"{result.Engine} {result.Elapsed.TotalMilliseconds:0}ms");
        SetStatus(string.Join("  ·  ", parts));
        StatusDot.Fill = Theme.Brush("Success");

        SpeakButton.IsEnabled = _speech is not null;   // online voice covers languages without an installed one
    }

    public void ShowError(string message)
    {
        Loading.Visibility = Visibility.Collapsed;
        TranslationBox.Text = message;
        TransGrid.FlowDirection = Loc.Flow;                      // the error is interface text, not a translation
        TranslationBox.FlowDirection = Loc.Flow;
        TranslationBox.TextAlignment = Loc.IsFa ? TextAlignment.Right : TextAlignment.Left;
        TranslationBox.Foreground = Theme.Brush("Error");
        StatusDot.Fill = Theme.Brush("Error");
        SetStatus(Loc.T("common.error"));
    }

    // ---- Placement -----------------------------------------------------

    private System.Drawing.Rectangle _anchorPx;
    private double _scale = 1.0;

    /// <summary>Place the popup just below (or above) an on-screen rectangle given in physical pixels, clamped to that monitor's work area.</summary>
    public void ShowNear(System.Drawing.Rectangle anchorPx)
    {
        _anchorPx = anchorPx;
        _scale = Dpi.ScaleFor(anchorPx);
        Width = Math.Clamp(anchorPx.Width / _scale + 24, 380, 640);
        if (_remembered is { } size)
        {
            SizeToContent = SizeToContent.Manual;
            Width = size.Width;
            Height = size.Height;
        }

        // Start on the right monitor (so WPF picks the right DPI), then refine once measured.
        var dip = Dpi.ToDip(anchorPx);
        Left = dip.Left - 12;
        Top = dip.Bottom + 6;
        Show();
        UpdateLayout();
        Reposition();
        Activate();
    }

    public void ShowAt(int px, int py) => ShowNear(new System.Drawing.Rectangle(px, py, 0, 0));

    private void Reposition()
    {
        var area = System.Windows.Forms.Screen.FromRectangle(_anchorPx).WorkingArea;
        int w = (int)Math.Round(ActualWidth * _scale);
        int h = (int)Math.Round(ActualHeight * _scale);
        int gap = (int)(6 * _scale);

        int x = _anchorPx.Left - (int)(12 * _scale);
        int y = _anchorPx.Bottom + gap;
        if (y + h > area.Bottom) y = _anchorPx.Top - h - gap;
        if (y < area.Top) y = Math.Max(area.Top, area.Bottom - h);
        x = Math.Clamp(x, area.Left, Math.Max(area.Left, area.Right - w));

        Dpi.SetPixelPosition(this, x, y);
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        // Translation arrives async and can grow the window; keep it on screen.
        // (WPF flips SizeToContent to Manual once the user resizes; from then on the window is theirs.)
        SizeChanged += (_, _) => { if (!_userMoved && SizeToContent != SizeToContent.Manual) Reposition(); };
    }

    private bool _userMoved;

    // ---- Motion ----------------------------------------------------------

    private void AnimateIn()
    {
        Root.Opacity = 0;
        Slide.Y = -8;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        Root.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(160)) { EasingFunction = ease });
        Slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(200)) { EasingFunction = ease });
    }

    private void StartShimmer()
    {
        var anim = new DoubleAnimation(0.35, 1.0, TimeSpan.FromMilliseconds(700))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase(),
        };
        Loading.BeginAnimation(OpacityProperty, anim);
    }

    // ---- Interaction ---------------------------------------------------

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Close(); return; }

        if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control
            && TranslationBox.SelectionLength == 0 && OriginalBox.SelectionLength == 0)
        {
            CopyToClipboard(TranslationBox.Text);
            e.Handled = true;
        }
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (!_pinned) Close();
    }

    private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;
        if (e.ClickCount == 2)
        {
            // Double-click the header: forget the dragged size and go back to fitting the content.
            SizeToContent = SizeToContent.Height;
            Width = 460;
            return;
        }
        _userMoved = true;
        try { DragMove(); } catch { }
    }

    private void OnPinClick(object sender, RoutedEventArgs e)
    {
        _pinned = !_pinned;
        PinButton.Foreground = _pinned ? Theme.Brush("Accent") : Theme.Brush("Muted");
        PinButton.Background = _pinned ? Theme.Brush("AccentSoft") : Brushes.Transparent;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnCopyTranslation(object sender, RoutedEventArgs e)
    {
        CopyToClipboard(TranslationBox.Text);
        FlashCopied(CopyButton, Loc.T("common.copied"), Loc.T("result.copy.translation"));
    }

    private void OnCopyOriginal(object sender, RoutedEventArgs e) => CopyToClipboard(OriginalBox.Text);

    private async void OnSpeak(object sender, RoutedEventArgs e)
    {
        if (_speech is null) return;
        if (_speech.IsSpeaking) { _speech.Stop(); ResetSpeakButton(); return; }

        SpeakButton.Content = "";
        SpeakButton.Foreground = Theme.Brush("Accent");
        _speech.Finished += ResetSpeakButton;
        try { await _speech.SpeakAsync(OriginalBox.Text, _sourceLanguage.Split('-')[0]); }
        catch { ResetSpeakButton(); }
    }

    private void ResetSpeakButton()
    {
        if (_speech is not null) _speech.Finished -= ResetSpeakButton;
        Dispatcher.Invoke(() => { SpeakButton.Content = ""; SpeakButton.Foreground = Theme.Brush("Muted"); });
    }

    private static async void FlashCopied(System.Windows.Controls.Button b, string flash, string normal)
    {
        b.Content = flash;
        await Task.Delay(1100);
        b.Content = normal;
    }

    private static void CopyToClipboard(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        try { Clipboard.SetDataObject(text, true); } catch { /* clipboard busy */ }
    }

    protected override void OnClosed(EventArgs e)
    {
        _speech?.Stop();
        SizeSettled?.Invoke(SizeToContent == SizeToContent.Manual ? new Size(ActualWidth, ActualHeight) : null);
        base.OnClosed(e);
    }
}
