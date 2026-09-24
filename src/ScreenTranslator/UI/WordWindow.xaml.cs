using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shell;
using ScreenTranslator.Models;
using ScreenTranslator.Native;
using ScreenTranslator.Services;

namespace ScreenTranslator.UI;

/// <summary>Compact dictionary card for one word: pronunciation, Persian meaning, English senses.</summary>
public partial class WordWindow : Window
{
    private readonly SpeechService? _speech;
    private readonly string _word;
    private string _language = "en";
    private bool _pinned;

    /// <summary>The user wants the whole line translated; the host opens the regular popup.</summary>
    public event Action? TranslateLineRequested;

    public WordWindow(string word, SpeechService? speech, bool hasLine)
    {
        InitializeComponent();
        _word = word;
        _speech = speech;
        WordText.Text = word;
        SpeakButton.Visibility = speech is null ? Visibility.Collapsed : Visibility.Visible;
        LineButton.Visibility = hasLine ? Visibility.Visible : Visibility.Collapsed;

        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight = 0,
            ResizeBorderThickness = new Thickness(0),
            GlassFrameThickness = new Thickness(-1),
            UseAeroCaptionButtons = false,
        });

        SourceInitialized += (_, _) =>
        {
            WindowEffects.HideFromAltTab(this);
            if (!WindowEffects.TryApplyAcrylic(this, Theme.IsDark))
            {
                Shell.Background = Theme.Brush("Surface");
                WindowEffects.SetRoundedCorners(this);
            }
        };

        Loaded += (_, _) => AnimateIn();
        StartShimmer(TranslationLoading);
        StartShimmer(SensesLoading);
    }

    // ---- Content -------------------------------------------------------

    public void SetTranslation(TranslationResult result)
    {
        _language = result.SourceLanguage.Split('-')[0];
        TranslationLoading.Visibility = Visibility.Collapsed;
        TranslationBox.Text = result.Text;
        FadeIn(TranslationBox);
    }

    public void SetTranslationError(string message)
    {
        TranslationLoading.Visibility = Visibility.Collapsed;
        TranslationBox.Text = message;
        TranslationBox.Foreground = Theme.Brush("Error");
        TranslationBox.FontSize = 13;
    }

    public void SetDictionary(DictionaryEntry? entry)
    {
        SensesLoading.Visibility = Visibility.Collapsed;
        if (entry is null)
        {
            NoDefs.Visibility = Visibility.Visible;
            return;
        }

        if (!string.IsNullOrEmpty(entry.Ipa)) IpaText.Text = entry.Ipa;
        if (entry.Lemma is not null)
        {
            LemmaText.Text = $"شکل صرفی «{entry.Lemma}»";
            LemmaText.Visibility = Visibility.Visible;
        }
        SourceText.Text = "Wiktionary"; SourceChip.Visibility = Visibility.Visible;

        var accent = Theme.Brush("Accent");
        var muted = Theme.Brush("Muted");
        var text = Theme.Brush("Text");

        foreach (var sense in entry.Senses.Take(3))
        {
            var chip = new Border
            {
                Background = Theme.Brush("AccentGradientSoft"),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(7, 1, 7, 2),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 8, 0, 5),
                Child = new TextBlock
                {
                    Text = sense.PartOfSpeech.ToUpperInvariant(),
                    FontFamily = Theme.AppFont,
                    FontSize = 10.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = accent,
                },
            };
            Senses.Children.Add(chip);

            int n = 1;
            foreach (var def in sense.Definitions.Take(3))
            {
                var row = new Grid { Margin = new Thickness(2, 0, 0, 5) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
                row.ColumnDefinitions.Add(new ColumnDefinition());
                var num = new TextBlock { Text = n++ + ".", FontFamily = Theme.AppFont, FontSize = 12.5, Foreground = muted };
                var body = new TextBlock
                {
                    Text = def,
                    FontFamily = Theme.AppFont,
                    FontSize = 12.5,
                    Foreground = text,
                    Opacity = 0.9,
                    TextWrapping = TextWrapping.Wrap,
                    LineHeight = 19,
                };
                Grid.SetColumn(body, 1);
                row.Children.Add(num);
                row.Children.Add(body);
                Senses.Children.Add(row);
            }

            if (sense.Example is { } ex)
            {
                Senses.Children.Add(new TextBlock
                {
                    Text = "“" + ex + "”",
                    FontFamily = Theme.AppFont,
                    FontSize = 12,
                    FontStyle = FontStyles.Italic,
                    Foreground = muted,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(20, 0, 0, 4),
                });
            }
        }

        Senses.Opacity = 0;
        FadeIn(Senses);
    }

    private static void FadeIn(UIElement e)
    {
        e.Opacity = 0;
        e.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(220)));
    }

    // ---- Placement -----------------------------------------------------

    private System.Drawing.Rectangle _anchorPx;
    private double _scale = 1.0;
    private bool _userMoved;

    /// <summary>Place the card just below (or above) the word's on-screen rectangle, clamped to that monitor's work area.</summary>
    public void ShowNear(System.Drawing.Rectangle anchorPx)
    {
        _anchorPx = anchorPx;
        _scale = Dpi.ScaleFor(anchorPx);

        var dip = Dpi.ToDip(anchorPx);
        Left = dip.Left - 12;
        Top = dip.Bottom + 8;
        Show();
        UpdateLayout();
        Reposition();
        Activate();
        SizeChanged += (_, _) => { if (!_userMoved) Reposition(); };
    }

    private void Reposition()
    {
        var area = System.Windows.Forms.Screen.FromRectangle(_anchorPx).WorkingArea;
        int w = (int)Math.Round(ActualWidth * _scale);
        int h = (int)Math.Round(ActualHeight * _scale);
        int gap = (int)(8 * _scale);

        int x = _anchorPx.Left - (int)(14 * _scale);
        int y = _anchorPx.Bottom + gap;
        if (y + h > area.Bottom) y = _anchorPx.Top - h - gap;
        if (y < area.Top) y = Math.Max(area.Top, area.Bottom - h);
        x = Math.Clamp(x, area.Left, Math.Max(area.Left, area.Right - w));
        Dpi.SetPixelPosition(this, x, y);
    }

    // ---- Motion ----------------------------------------------------------

    private void AnimateIn()
    {
        Root.Opacity = 0;
        Slide.Y = -8;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        Root.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(160)) { EasingFunction = ease });
        Slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(200)) { EasingFunction = ease });
    }

    private static void StartShimmer(UIElement e)
    {
        e.BeginAnimation(OpacityProperty, new DoubleAnimation(0.35, 1.0, TimeSpan.FromMilliseconds(700))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase(),
        });
    }

    // ---- Interaction ---------------------------------------------------

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
        else if (e.Key == Key.Space && _speech is not null) { OnSpeak(sender, e); e.Handled = true; }
    }

    private void OnDeactivated(object? sender, EventArgs e) { if (!_pinned) Close(); }

    private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;
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

    private void OnCopy(object sender, RoutedEventArgs e)
    {
        var meaning = TranslationBox.Text;
        if (string.IsNullOrWhiteSpace(meaning)) return;
        try { Clipboard.SetDataObject($"{_word} — {meaning}", true); } catch { }
        CopyButton.Content = "کپی شد ✓";
        _ = Task.Delay(1100).ContinueWith(_ => Dispatcher.Invoke(() => CopyButton.Content = "کپی معنی"));
    }

    private void OnTranslateLine(object sender, RoutedEventArgs e) => TranslateLineRequested?.Invoke();

    private async void OnSpeak(object sender, RoutedEventArgs e)
    {
        if (_speech is null) return;
        if (_speech.IsSpeaking) { _speech.Stop(); SpeakButton.Content = ""; return; }

        SpeakButton.Content = "";
        SpeakButton.Foreground = Theme.Brush("Accent");
        _speech.Finished += ResetSpeakButton;
        try { await _speech.SpeakAsync(_word, _language); }
        catch { ResetSpeakButton(); }
    }

    private void ResetSpeakButton()
    {
        if (_speech is not null) _speech.Finished -= ResetSpeakButton;
        Dispatcher.Invoke(() => { SpeakButton.Content = ""; SpeakButton.Foreground = Theme.Brush("Muted"); });
    }

    protected override void OnClosed(EventArgs e)
    {
        _speech?.Stop();
        base.OnClosed(e);
    }
}
