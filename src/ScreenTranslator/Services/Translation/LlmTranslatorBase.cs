using System.Diagnostics;
using System.Text.Json;
using ScreenTranslator.Models;

namespace ScreenTranslator.Services.Translation;

/// <summary>
/// Shared prompt/parse logic for LLM-backed engines. The model always answers with a small JSON object
/// so we get the detected source language and exactly one translation per input snippet.
/// </summary>
public abstract class LlmTranslatorBase : ITranslator
{
    public abstract string Name { get; }

    protected abstract Task<string> CompleteAsync(string system, string user, CancellationToken ct);

    public async Task<TranslationResult> TranslateAsync(string text, string targetLanguage, CancellationToken ct = default)
    {
        var results = await TranslateManyAsync(new[] { text }, targetLanguage, ct);
        return results[0];
    }

    public async Task<IReadOnlyList<TranslationResult>> TranslateManyAsync(IReadOnlyList<string> texts, string targetLanguage, CancellationToken ct = default)
    {
        if (texts.Count == 0) return Array.Empty<TranslationResult>();

        var sw = Stopwatch.StartNew();
        var payload = JsonSerializer.Serialize(new { target = LanguageName(targetLanguage), segments = texts });
        var raw = await CompleteAsync(SystemPrompt, payload, ct);
        var (lang, translations) = Parse(raw, texts.Count);
        sw.Stop();

        return translations.Select(t => new TranslationResult(t, lang, Name, false, sw.Elapsed)).ToList();
    }

    private const string SystemPrompt =
        "You are a professional translator embedded in a screen-translation tool. " +
        "The user message is a JSON object: {\"target\": <language>, \"segments\": [<text>, ...]}. " +
        "Segments come from OCR of a screen and may contain UI labels, code, or fragments; translate them faithfully and naturally, " +
        "keep the register, keep product names, code identifiers, numbers and symbols unchanged, and never add commentary. " +
        "Reply with ONLY a JSON object: {\"lang\": <ISO 639-1 code of the source language>, \"translations\": [<one translation per segment, same order, same count>]}.";

    private static (string lang, List<string> translations) Parse(string raw, int expected)
    {
        var json = ExtractJson(raw);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var lang = root.TryGetProperty("lang", out var l) && l.ValueKind == JsonValueKind.String ? l.GetString() ?? "auto" : "auto";
        var list = new List<string>();
        if (root.TryGetProperty("translations", out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var item in arr.EnumerateArray())
                list.Add(item.ValueKind == JsonValueKind.String ? item.GetString() ?? "" : item.ToString());

        if (list.Count != expected)
            throw new InvalidOperationException($"Model returned {list.Count} translations for {expected} segments.");
        return (lang, list);
    }

    private static string ExtractJson(string raw)
    {
        int start = raw.IndexOf('{');
        int end = raw.LastIndexOf('}');
        if (start < 0 || end <= start) throw new InvalidOperationException("Model did not return JSON.");
        return raw[start..(end + 1)];
    }

    private static string LanguageName(string tag) => tag switch
    {
        "fa" => "Persian (Farsi)", "en" => "English", "ar" => "Arabic", "tr" => "Turkish", "de" => "German",
        "fr" => "French", "es" => "Spanish", "ru" => "Russian", "it" => "Italian", "ja" => "Japanese", "zh-CN" => "Simplified Chinese",
        _ => tag,
    };
}
