using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Shell;
using ScreenTranslator.Native;
using ScreenTranslator.Services;
using ScreenTranslator.Services.Translation;

namespace ScreenTranslator.UI;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private bool _loaded;

    public event Action? WelcomeRequested;

    private static readonly (string tag, string name)[] TargetLanguages =
    {
        ("fa", "فارسی"), ("en", "English"), ("ar", "العربية"), ("tr", "Türkçe"), ("de", "Deutsch"),
        ("fr", "Français"), ("es", "Español"), ("ru", "Русский"), ("it", "Italiano"), ("ja", "日本語"), ("zh-CN", "中文"),
    };

    public SettingsWindow(AppSettings settings)
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

        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.3";
        VersionText.Text = Loc.T("settings.version", version);
        AboutVersion.Text = VersionText.Text;
        LogoImage.Source = AppIcon.Bitmap;
        AboutLogo.Source = AppIcon.Bitmap;

        Populate();
        _loaded = true;
        RefreshValueLabels();
    }

    private void Populate()
    {
        foreach (ComboBoxItem item in ThemeCombo.Items)
            if ((string)item.Tag == _settings.Theme) ThemeCombo.SelectedItem = item;
        if (ThemeCombo.SelectedItem is null) ThemeCombo.SelectedIndex = 0;

        PopupFontSlider.Value = _settings.PopupFontSize;
        IntervalSlider.Value = _settings.LiveIntervalMs;
        OpacitySlider.Value = Math.Round(_settings.OverlayOpacity * 100);
        FontScaleSlider.Value = Math.Round(_settings.OverlayFontScale * 100);

        foreach (var (tag, name) in TargetLanguages)
            TargetCombo.Items.Add(new ComboBoxItem { Content = name, Tag = tag });
        TargetCombo.SelectedIndex = Math.Max(0, Array.FindIndex(TargetLanguages, l => l.tag == _settings.TargetLanguage));

        foreach (ComboBoxItem item in UiLangCombo.Items)
            if ((string)item.Tag == _settings.UiLanguage) UiLangCombo.SelectedItem = item;
        if (UiLangCombo.SelectedItem is null) UiLangCombo.SelectedIndex = 0;

        OcrCombo.Items.Add(new ComboBoxItem { Content = Loc.T("settings.ocr.auto"), Tag = "" });
        foreach (var lang in OcrService.AvailableLanguages)
            OcrCombo.Items.Add(new ComboBoxItem { Content = lang, Tag = lang });
        OcrCombo.SelectedIndex = 0;
        for (int i = 1; i < OcrCombo.Items.Count; i++)
            if ((string)((ComboBoxItem)OcrCombo.Items[i]).Tag == _settings.OcrLanguage) OcrCombo.SelectedIndex = i;

        StartupCheck.IsChecked = StartupManager.IsEnabled();
        SpeakCheck.IsChecked = _settings.SpeakEnabled;
        HistoryCheck.IsChecked = _settings.HistoryEnabled;

        HkRegion.Value = Hotkey.Parse(_settings.HotkeyRegion, Hotkey.None);
        HkSelection.Value = Hotkey.Parse(_settings.HotkeySelection, Hotkey.None);
        HkLiveRegion.Value = Hotkey.Parse(_settings.HotkeyLiveRegion, Hotkey.None);
        HkLiveWindow.Value = Hotkey.Parse(_settings.HotkeyLiveWindow, Hotkey.None);
        HkWord.Value = Hotkey.Parse(_settings.HotkeyWord, Hotkey.None);
        HkCopy.Value = Hotkey.Parse(_settings.HotkeyCopyText, Hotkey.None);

        foreach (var m in ClaudeTranslator.Models)
            ClaudeModel.Items.Add(new ComboBoxItem { Content = m, Tag = m });
        ClaudeModel.SelectedIndex = Math.Max(0, Array.IndexOf(ClaudeTranslator.Models, _settings.ClaudeModel));
        ClaudeKey.Password = SecretStore.Unprotect(_settings.ClaudeApiKey);

        OpenAiUrl.Text = _settings.OpenAiBaseUrl;
        OpenAiKey.Password = SecretStore.Unprotect(_settings.OpenAiApiKey);
        OpenAiModel.Text = _settings.OpenAiModel;

        (_settings.Engine switch
        {
            TranslatorFactory.EngineClaude => EngineClaude,
            TranslatorFactory.EngineOpenAi => EngineOpenAi,
            _ => EngineGoogle,
        }).IsChecked = true;
    }

    // ---- Tabs ----------------------------------------------------------

    /// <summary>Open on a given tab ("hotkeys", "live", "engine", "about"); used by the demo flags.</summary>
    public void ShowTab(string tab)
    {
        switch (tab)
        {
            case "hotkeys": TabHotkeys.IsChecked = true; break;
            case "live": TabLive.IsChecked = true; break;
            case "engine": TabEngine.IsChecked = true; EngineClaude.IsChecked = true; break;
            case "openai": TabEngine.IsChecked = true; EngineOpenAi.IsChecked = true; break;
            case "about": TabAbout.IsChecked = true; break;
        }
    }

    private void OnTab(object sender, RoutedEventArgs e)
    {
        if (PageGeneral is null) return;
        PageGeneral.Visibility = TabGeneral.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PageHotkeys.Visibility = TabHotkeys.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PageLive.Visibility = TabLive.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PageEngine.Visibility = TabEngine.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PageAbout.Visibility = TabAbout.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnEngineChanged(object sender, RoutedEventArgs e)
    {
        if (ClaudePanel is null) return;
        bool claude = EngineClaude.IsChecked == true, openai = EngineOpenAi.IsChecked == true;
        ClaudePanel.Visibility = claude ? Visibility.Visible : Visibility.Collapsed;
        OpenAiPanel.Visibility = openai ? Visibility.Visible : Visibility.Collapsed;
        TestPanel.Visibility = claude || openai ? Visibility.Visible : Visibility.Collapsed;
        EngineHint.Text = Loc.T(claude ? "settings.engine.hint.claude"
                              : openai ? "settings.engine.hint.openai"
                              : "settings.engine.hint.google");
        TestResult.Text = "";
    }

    private async void OnTestEngine(object sender, RoutedEventArgs e)
    {
        var temp = SnapshotToSettings(new AppSettings());
        var engine = TranslatorFactory.CreatePrimary(temp);
        if (engine is null)
        {
            TestResult.Text = Loc.T("settings.test.missing");
            return;
        }

        TestButton.IsEnabled = false;
        TestResult.Text = Loc.T("settings.test.running");
        try
        {
            var r = await engine.TranslateAsync("Hello! This is a quick connection test.", temp.TargetLanguage);
            TestResult.Text = $"✓ {r.Text}  ({r.Elapsed.TotalMilliseconds:0}ms)";
        }
        catch (Exception ex)
        {
            TestResult.Text = "✗ " + Shorten(ex.Message);
        }
        finally
        {
            TestButton.IsEnabled = true;
        }
    }

    private static string Shorten(string s) => s.Length > 160 ? s[..157] + "…" : s;

    // ---- Values --------------------------------------------------------

    private void OnSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loaded) RefreshValueLabels();
    }

    private void RefreshValueLabels()
    {
        PopupFontValue.Text = $"{PopupFontSlider.Value:0}";
        IntervalValue.Text = $"{IntervalSlider.Value:0}ms";
        OpacityValue.Text = $"{OpacitySlider.Value:0}%";
        FontScaleValue.Text = $"{FontScaleSlider.Value:0}%";
    }

    private AppSettings SnapshotToSettings(AppSettings s)
    {
        s.Theme = (string)((ComboBoxItem)ThemeCombo.SelectedItem).Tag;
        s.UiLanguage = (string)((ComboBoxItem)UiLangCombo.SelectedItem).Tag;
        s.PopupFontSize = Math.Round(PopupFontSlider.Value);
        s.LiveIntervalMs = (int)IntervalSlider.Value;
        s.OverlayOpacity = OpacitySlider.Value / 100.0;
        s.OverlayFontScale = FontScaleSlider.Value / 100.0;
        s.TargetLanguage = (string)((ComboBoxItem)TargetCombo.SelectedItem).Tag;
        var ocr = (string)((ComboBoxItem)OcrCombo.SelectedItem).Tag;
        s.OcrLanguage = string.IsNullOrEmpty(ocr) ? null : ocr;
        s.SpeakEnabled = SpeakCheck.IsChecked == true;
        s.HistoryEnabled = HistoryCheck.IsChecked == true;

        s.HotkeyRegion = HkRegion.Value.ToString();
        s.HotkeySelection = HkSelection.Value.ToString();
        s.HotkeyLiveRegion = HkLiveRegion.Value.ToString();
        s.HotkeyLiveWindow = HkLiveWindow.Value.ToString();
        s.HotkeyWord = HkWord.Value.ToString();
        s.HotkeyCopyText = HkCopy.Value.ToString();

        s.Engine = EngineClaude.IsChecked == true ? TranslatorFactory.EngineClaude
                 : EngineOpenAi.IsChecked == true ? TranslatorFactory.EngineOpenAi
                 : TranslatorFactory.EngineGoogle;
        s.ClaudeApiKey = SecretStore.Protect(ClaudeKey.Password.Trim());
        s.ClaudeModel = (string)((ComboBoxItem)ClaudeModel.SelectedItem).Tag;
        s.OpenAiBaseUrl = OpenAiUrl.Text.Trim();
        s.OpenAiApiKey = SecretStore.Protect(OpenAiKey.Password.Trim());
        s.OpenAiModel = OpenAiModel.Text.Trim();
        return s;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        // Duplicate chords would silently shadow each other.
        var chords = new[] { HkRegion.Value, HkSelection.Value, HkLiveRegion.Value, HkLiveWindow.Value, HkWord.Value, HkCopy.Value }.Where(h => !h.IsEmpty).ToList();
        if (chords.Distinct().Count() != chords.Count)
        {
            TabHotkeys.IsChecked = true;
            MessageBox.Show(this, Loc.T("settings.hotkey.duplicate"), "ScreenTranslator", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SnapshotToSettings(_settings);

        try { StartupManager.SetEnabled(StartupCheck.IsChecked == true); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "ScreenTranslator", MessageBoxButton.OK, MessageBoxImage.Warning); }

        _settings.Save();
        Theme.Apply(_settings.Theme);
        Loc.Use(_settings.UiLanguage);        // windows opened from now on use the new language

        SavedHint.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(1600)) { BeginTime = TimeSpan.FromMilliseconds(300) });
    }

    private void OnShowWelcome(object sender, RoutedEventArgs e) => WelcomeRequested?.Invoke();

    private void OnOpenLink(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); } catch { }
        e.Handled = true;
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && !(Keyboard.FocusedElement is HotkeyBox)) Close();
    }

    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }
}
