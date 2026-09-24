using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using ScreenTranslator.Models;

namespace ScreenTranslator.Services.Translation;

/// <summary>Google Translate via its public web endpoints (auto-detects source language).</summary>
public sealed class GoogleTranslator : ITranslator
{
    private static readonly HttpClient Http = CreateClient();

    // Both return [["translated text","detected-source"]] and preserve newlines.
    // The "single?client=gtx" endpoint is deliberately not used: it rate-limits aggressively (HTTP 429).
    private static readonly string[] Endpoints =
    {
        "https://translate.googleapis.com/translate_a/t?client=gtx&sl=auto&tl={0}",
        "https://clients5.google.com/translate_a/t?client=dict-chrome-ex&sl=auto&tl={0}",
    };

    public string Name => "Google";

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0 Safari/537.36");
        return c;
    }

    public async Task<TranslationResult> TranslateAsync(string text, string targetLanguage, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        Exception? last = null;

        foreach (var template in Endpoints)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var (translated, src) = await CallAsync(string.Format(template, targetLanguage), text, ct);
                return new TranslationResult(translated, src, Name, false, sw.Elapsed);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                last = ex;
            }
        }

        throw last ?? new HttpRequestException("Translation failed.");
    }

    private static async Task<(string text, string src)> CallAsync(string url, string text, CancellationToken ct)
    {
        using var content = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("q", text) });
        using var resp = await Http.PostAsync(url, content, ct);
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadAsStringAsync(ct);
        var parsed = Parse(body);
        if (string.IsNullOrWhiteSpace(parsed.text))
            throw new HttpRequestException("Empty translation. Response: " + body[..Math.Min(200, body.Length)]);
        return parsed;
    }

    // Shapes seen in the wild: [["text","src"]], ["text"], or "text".
    private static (string text, string src) Parse(string body)
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        if (root.ValueKind == JsonValueKind.String)
            return (root.GetString()!.Trim(), "auto");

        var first = root[0];
        if (first.ValueKind == JsonValueKind.Array)
        {
            var src = first.GetArrayLength() > 1 ? first[1].GetString() ?? "auto" : "auto";
            return ((first[0].GetString() ?? "").Trim(), src);
        }

        return ((first.GetString() ?? "").Trim(), "auto");
    }
}
