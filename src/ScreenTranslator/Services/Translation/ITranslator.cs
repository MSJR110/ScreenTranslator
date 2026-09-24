using ScreenTranslator.Models;

namespace ScreenTranslator.Services.Translation;

public interface ITranslator
{
    string Name { get; }

    Task<TranslationResult> TranslateAsync(string text, string targetLanguage, CancellationToken ct = default);

    /// <summary>
    /// Translate several independent snippets in as few requests as possible.
    /// Default: join with newlines (Google preserves them 1:1); fall back to one request per item if the count drifts.
    /// </summary>
    async Task<IReadOnlyList<TranslationResult>> TranslateManyAsync(IReadOnlyList<string> texts, string targetLanguage, CancellationToken ct = default)
    {
        if (texts.Count == 0) return Array.Empty<TranslationResult>();
        if (texts.Count == 1) return new[] { await TranslateAsync(texts[0], targetLanguage, ct) };

        var flat = texts.Select(t => t.Replace('\n', ' ').Replace('\r', ' ')).ToList();
        var joined = await TranslateAsync(string.Join("\n", flat), targetLanguage, ct);
        var parts = joined.Text.Split('\n');

        if (parts.Length == texts.Count)
            return parts.Select(p => joined with { Text = p.Trim() }).ToList();

        var results = new TranslationResult[texts.Count];
        for (int i = 0; i < texts.Count; i++)
            results[i] = await TranslateAsync(flat[i], targetLanguage, ct);
        return results;
    }
}
