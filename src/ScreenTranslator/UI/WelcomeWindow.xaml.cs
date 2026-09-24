using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Shell;
using ScreenTranslator.Native;
using ScreenTranslator.Services;

namespace ScreenTranslator.UI;

/// <summary>First-run tour. The sample paragraph doubles as a live-mode demo target.</summary>
public partial class WelcomeWindow : Window
{
    private readonly AppSettings _settings;

    /// <summary>Raised with this window's handle when the user wants the live demo.</summary>
    public event Action<IntPtr>? TryLiveRequested;

    public WelcomeWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight = 0,
            ResizeBorderThickness = new Thickness(0),
            GlassFrameThickness = new Thickness(-1),
            UseAeroCaptionButtons = false,
        });

        SourceInitialized += (_, _) =>
        {
            if (!WindowEffects.TryApplyAcrylic(this, Theme.IsDark))
            {
                Shell.Background = Theme.Brush("Surface");
                WindowEffects.SetRoundedCorners(this);
            }
        };

        Logo.Source = AppIcon.Bitmap;
        StartupCheck.IsChecked = StartupManager.IsEnabled();

        FillKeys(KbdRegion, settings.HotkeyRegion);
        FillKeys(KbdSelection, settings.HotkeySelection);
        FillKeys(KbdLive, settings.HotkeyLiveRegion);
        FillKeys(KbdWord, settings.HotkeyWord);

        Loaded += (_, _) => AnimateIn();
    }

    private void FillKeys(StackPanel panel, string chord)
    {
        panel.Children.Clear();
        var hk = Hotkey.Parse(chord, Hotkey.None);
        if (hk.IsEmpty) return;
        foreach (var part in hk.ToString().Split('+'))
        {
            var b = new Border { Style = (Style)FindResource("Kbd") };
            b.Child = new TextBlock { Text = part, Style = (Style)FindResource("MutedLabel"), FontSize = 12 };
            panel.Children.Add(b);
        }
    }

    private void AnimateIn()
    {
        Root.Opacity = 0;
        Scale.ScaleX = Scale.ScaleY = 0.97;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var t = TimeSpan.FromMilliseconds(260);
        Root.BeginAnimation(OpacityProperty, new DoubleAnimation(1, t) { EasingFunction = ease });
        Scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, new DoubleAnimation(1, t) { EasingFunction = ease });
        Scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, new DoubleAnimation(1, t) { EasingFunction = ease });
    }

    private void OnTryLive(object sender, RoutedEventArgs e)
        => TryLiveRequested?.Invoke(new WindowInteropHelper(this).Handle);

    private void OnStart(object sender, RoutedEventArgs e)
    {
        try { StartupManager.SetEnabled(StartupCheck.IsChecked == true); } catch { }
        if (!_settings.WelcomeShown)
        {
            _settings.WelcomeShown = true;
            _settings.Save();
        }
        Close();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) OnStart(sender, e);
    }

    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }
}
