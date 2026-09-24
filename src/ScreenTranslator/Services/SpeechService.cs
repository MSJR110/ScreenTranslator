using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;
using Windows.Storage.Streams;

namespace ScreenTranslator.Services;

/// <summary>
/// Reads text aloud. Short snippets (a word, a sentence) use Google's natural TTS voice when online;
/// everything else — and anything when offline — uses the built-in Windows voices.
/// </summary>
public sealed class SpeechService : IDisposable
{
    private static readonly HttpClient Http = CreateClient();
    private static readonly Dictionary<string, byte[]> OnlineCache = new();

    private readonly MediaPlayer _player = new();
    private SpeechSynthesizer? _synth;
    private int _generation;

    public bool IsSpeaking { get; private set; }

    public event Action? Finished;

    public SpeechService()
    {
        _player.MediaEnded += (_, _) => { IsSpeaking = false; Finished?.Invoke(); };
        _player.MediaFailed += (_, _) => { IsSpeaking = false; Finished?.Invoke(); };
    }

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0 Safari/537.36");
        return c;
    }

    public static bool HasVoiceFor(string languageTag)
        => SpeechSynthesizer.AllVoices.Any(v => v.Language.StartsWith(languageTag, StringComparison.OrdinalIgnoreCase));

    public async Task SpeakAsync(string text, string languageTag)
    {
        Stop();
        if (string.IsNullOrWhiteSpace(text)) return;
        int gen = ++_generation;

        // Keep it snappy: very long passages get trimmed to the first ~1200 chars.
        if (text.Length > 1200) text = text[..1200];

        IsSpeaking = true;
        if (text.Length <= 200)
        {
            var mp3 = await FetchOnlineAsync(text, languageTag);
            if (gen != _generation) return;
            if (mp3 is not null)
            {
                var ras = new InMemoryRandomAccessStream();
                await ras.WriteAsync(mp3.AsBuffer());
                ras.Seek(0);
                _player.Source = MediaSource.CreateFromStream(ras, "audio/mpeg");
                _player.Play();
                return;
            }
        }

        _synth ??= new SpeechSynthesizer();
        var voice = SpeechSynthesizer.AllVoices.FirstOrDefault(v => v.Language.StartsWith(languageTag, StringComparison.OrdinalIgnoreCase))
                    ?? SpeechSynthesizer.DefaultVoice;
        _synth.Voice = voice;

        var stream = await _synth.SynthesizeTextToStreamAsync(text);
        if (gen != _generation) return;
        _player.Source = MediaSource.CreateFromStream(stream, stream.ContentType);
        _player.Play();
    }

    private static async Task<byte[]?> FetchOnlineAsync(string text, string languageTag)
    {
        var key = languageTag + "|" + text;
        lock (OnlineCache)
            if (OnlineCache.TryGetValue(key, out var hit)) return hit;

        try
        {
            var url = $"https://translate.google.com/translate_tts?ie=UTF-8&client=tw-ob&tl={Uri.EscapeDataString(languageTag)}&q={Uri.EscapeDataString(text)}";
            using var resp = await Http.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return null;
            var bytes = await resp.Content.ReadAsByteArrayAsync();
            if (bytes.Length < 200) return null;

            lock (OnlineCache)
            {
                if (OnlineCache.Count > 200) OnlineCache.Clear();
                OnlineCache[key] = bytes;
            }
            return bytes;
        }
        catch { return null; }
    }

    public void Stop()
    {
        _generation++;
        if (!IsSpeaking) return;
        _player.Pause();
        _player.Source = null;
        IsSpeaking = false;
    }

    public void Dispose()
    {
        _player.Dispose();
        _synth?.Dispose();
    }
}
