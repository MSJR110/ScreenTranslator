using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScreenTranslator.Services;

public sealed class AppSettings
{
    // Translation
    public string TargetLanguage { get; set; } = "fa";

    /// <summary>"google" | "claude" | "openai" — AI engines fall back to Google automatically on failure.</summary>
    public string Engine { get; set; } = "google";
    public string ClaudeApiKey { get; set; } = "";          // DPAPI-protected, see SecretStore
    public string ClaudeModel { get; set; } = "claude-opus-5";
    public string OpenAiBaseUrl { get; set; } = "https://api.openai.com/v1";
    public string OpenAiApiKey { get; set; } = "";          // DPAPI-protected
    public string OpenAiModel { get; set; } = "gpt-4o-mini";

    /// <summary>OCR language tag (e.g. "en-US"). Null = use the Windows user-profile language.</summary>
    public string? OcrLanguage { get; set; }

    // Hotkeys (serialized chords such as "Ctrl+Alt+T")
    public string HotkeyRegion { get; set; } = "Ctrl+Alt+T";
    public string HotkeySelection { get; set; } = "Ctrl+Alt+S";
    public string HotkeyLiveRegion { get; set; } = "Ctrl+Alt+L";
    public string HotkeyLiveWindow { get; set; } = "Ctrl+Alt+W";
    public string HotkeyWord { get; set; } = "Ctrl+Alt+D";
    public string HotkeyCopyText { get; set; } = "Ctrl+Alt+C";

    // Appearance: "system" | "dark" | "light"
    public string Theme { get; set; } = "system";

    /// <summary>Interface language: "auto" (follow Windows) | "fa" | "en".</summary>
    public string UiLanguage { get; set; } = "auto";

    /// <summary>Popup translation font size in DIPs.</summary>
    public double PopupFontSize { get; set; } = 16;

    /// <summary>Popup size the user last dragged it to (DIPs); 0 = size to content.</summary>
    public double PopupWidth { get; set; }
    public double PopupHeight { get; set; }

    // Live mode
    public int LiveIntervalMs { get; set; } = 700;

    /// <summary>0..1 — how opaque the boxes painted over the original text are.</summary>
    public double OverlayOpacity { get; set; } = 1.0;

    public double OverlayFontScale { get; set; } = 1.0;

    // Behaviour
    public bool SpeakEnabled { get; set; } = true;
    public bool HistoryEnabled { get; set; } = true;
    public bool WelcomeShown { get; set; }

    [JsonIgnore]
    public static string Folder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScreenTranslator");

    [JsonIgnore]
    public static string FilePath => Path.Combine(Folder, "settings.json");

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public event Action? Saved;

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Options) ?? new AppSettings();
        }
        catch { /* corrupt settings: start fresh */ }
        return new AppSettings();
    }

    public void Save()
    {
        SaveQuiet();
        Saved?.Invoke();
    }

    /// <summary>Persist without raising <see cref="Saved"/> — for incidental state (window sizes) that needs no re-wiring.</summary>
    public void SaveQuiet()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Options));
        }
        catch { /* read-only profile or locked file: settings just stay in memory */ }
    }
}
