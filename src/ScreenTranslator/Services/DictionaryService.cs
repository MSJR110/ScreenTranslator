using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ScreenTranslator.Services;

public sealed record DictionarySense(string PartOfSpeech, IReadOnlyList<string> Definitions, string? Example);

/// <param name="Lemma">When the looked-up word is an inflected form ("running"), the base form it was resolved to ("run").</param>
public sealed record DictionaryEntry(string Word, string? Lemma, string? Ipa, IReadOnlyList<DictionarySense> Senses);

/// <summary>English definitions and IPA from Wiktionary (free, no key, reachable where Google's dictionary endpoint is not).</summary>
public static partial class DictionaryService
{
    private static readonly HttpClient Http = CreateClient();
    private static readonly Dictionary<string, DictionaryEntry?> Cache = new(StringComparer.OrdinalIgnoreCase);

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        // Wikimedia asks for an identifying UA and throttles anonymous browser strings.
        c.DefaultRequestHeaders.UserAgent.ParseAdd("ScreenTranslator/0.4 (Windows desktop; personal use)");
        c.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        return c;
    }

    /// <summary>Strip surrounding punctuation/quotes that OCR attaches to words ("running," → running).</summary>
    public static string CleanWord(string raw)
    {
        var s = raw.Trim().Trim('"', '“', '”', '‘', '’', '\'', '(', ')', '[', ']', '{', '}', '«', '»',
            ',', '.', ';', ':', '!', '?', '؟', '،', '…', '*', '_', '|', '/', '\\', '<', '>');
        // Possessive / contraction tails: "word's" → word
        s = Regex.Replace(s, @"['’](s|re|ve|ll|d|m|t)$", "", RegexOptions.IgnoreCase);
        return s;
    }

    public static bool LooksEnglish(string word) => word.Length > 0 && word.All(ch => ch < 'ɐ' && (char.IsLetter(ch) || ch is '-' or '\''));

    public static async Task<DictionaryEntry?> LookupAsync(string word, CancellationToken ct = default)
    {
        lock (Cache)
            if (Cache.TryGetValue(word, out var hit)) return hit;

        DictionaryEntry? entry = null;
        try
        {
            entry = await LookupCoreAsync(word, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { /* offline or blocked: caller shows translation only */ }

        lock (Cache)
        {
            if (Cache.Count > 500) Cache.Clear();
            Cache[word] = entry;
        }
        return entry;
    }

    private static async Task<DictionaryEntry?> LookupCoreAsync(string word, CancellationToken ct)
    {
        // Wiktionary titles are case-sensitive; try as typed, then lowercase (most common nouns/verbs).
        var candidates = new List<string> { word };
        if (word != word.ToLowerInvariant()) candidates.Add(word.ToLowerInvariant());

        foreach (var title in candidates)
        {
            var senses = await DefinitionsAsync(title, ct);
            if (senses is null) continue;

            string? lemma = null;
            // "running" → "present participle of run": follow once so the user sees the real meaning too.
            if (FormOf(senses) is { } baseForm && !string.Equals(baseForm, title, StringComparison.OrdinalIgnoreCase))
            {
                var baseSenses = await DefinitionsAsync(baseForm, ct);
                if (baseSenses is not null)
                {
                    lemma = baseForm;
                    senses = baseSenses;
                }
            }

            var ipa = await IpaAsync(title, ct);
            return new DictionaryEntry(title, lemma, ipa, senses);
        }
        return null;
    }

    /// <summary>If the headline definition is "past participle of go" etc., return "go"; otherwise null.</summary>
    private static string? FormOf(IReadOnlyList<DictionarySense> senses)
    {
        var first = senses[0].Definitions[0];
        var m = FormOfRegex().Match(first);
        return m.Success && first.Length < 80 ? m.Groups[1].Value : null;
    }

    private static readonly string[] SkipPos = { "Proper noun", "Letter", "Symbol", "Punctuation mark" };

    private static async Task<List<DictionarySense>?> DefinitionsAsync(string title, CancellationToken ct)
    {
        var url = "https://en.wiktionary.org/api/rest_v1/page/definition/" + Uri.EscapeDataString(title.Replace(' ', '_'));
        using var resp = await Http.GetAsync(url, ct);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        if (!doc.RootElement.TryGetProperty("en", out var en)) return null;

        var senses = new List<DictionarySense>();
        foreach (var block in en.EnumerateArray())
        {
            var pos = block.TryGetProperty("partOfSpeech", out var p) ? p.GetString() ?? "" : "";
            if (SkipPos.Contains(pos) && en.GetArrayLength() > 1) continue;

            var defs = new List<string>();
            string? example = null;
            if (block.TryGetProperty("definitions", out var arr))
            {
                foreach (var d in arr.EnumerateArray())
                {
                    var text = Clean(d.TryGetProperty("definition", out var dt) ? dt.GetString() : null);
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    defs.Add(text);

                    if (example is null && d.TryGetProperty("examples", out var ex) && ex.ValueKind == JsonValueKind.Array)
                        foreach (var e in ex.EnumerateArray())
                        {
                            var et = Clean(e.GetString());
                            if (!string.IsNullOrWhiteSpace(et) && et.Length < 160) { example = et; break; }
                        }
                    if (defs.Count == 4) break;
                }
            }
            if (defs.Count > 0) senses.Add(new DictionarySense(pos, defs, example));
        }
        return senses.Count == 0 ? null : senses;
    }

    private static async Task<string?> IpaAsync(string title, CancellationToken ct)
    {
        try
        {
            var url = "https://en.wiktionary.org/w/api.php?action=parse&prop=wikitext&format=json&formatversion=2&page="
                      + Uri.EscapeDataString(title.Replace(' ', '_'));
            using var resp = await Http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return null;

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var wikitext = doc.RootElement.GetProperty("parse").GetProperty("wikitext").GetString() ?? "";

            // Only the ==English== section; other languages have their own IPA lines.
            int start = wikitext.IndexOf("==English==", StringComparison.Ordinal);
            if (start >= 0)
            {
                var next = NextLanguageHeading().Match(wikitext, start + 11);   // next level-2 heading = next language
                wikitext = next.Success ? wikitext[start..next.Index] : wikitext[start..];
            }

            // Prefer a General American line, else the first one.
            var matches = IpaRegex().Matches(wikitext);
            if (matches.Count == 0) return null;
            foreach (Match m in matches)
                if (m.Value.Contains("GA") || m.Value.Contains("US")) return m.Groups[1].Value;
            return matches[0].Groups[1].Value;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { return null; }
    }

    private static string? Clean(string? html)
    {
        if (html is null) return null;
        var s = TagRegex().Replace(html, "");
        s = WebUtility.HtmlDecode(s);
        return Regex.Replace(s, @"\s+", " ").Trim();
    }

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\n==[^=\n][^\n]*==[ \t]*\n")]
    private static partial Regex NextLanguageHeading();

    // {{IPA|en|/ˌsɛɹ.ənˈdɪp.ɪ.ti/|a=GA}} — capture the first slashed or bracketed transcription.
    [GeneratedRegex(@"\{\{IPA\|en\|[^}]*?([/\[][^/\]|}]+[/\]])[^}]*\}\}")]
    private static partial Regex IpaRegex();

    // Matches the cleaned text of Wiktionary "form-of" definitions.
    [GeneratedRegex(@"\b(?:plural|present participle(?: and gerund)?|gerund|past participle|simple past(?: tense)?(?: and past participle)?|third-person singular simple present(?: indicative)?(?: form)?|comparative form|superlative form|alternative (?:form|spelling)|obsolete (?:form|spelling)|inflection) of ([A-Za-z][A-Za-z'\- ]*?)(?:\.|$| \()", RegexOptions.IgnoreCase)]
    private static partial Regex FormOfRegex();
}
