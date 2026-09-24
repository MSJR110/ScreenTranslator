using System.Collections.Concurrent;
using ScreenTranslator.Models;

namespace ScreenTranslator.Services.Translation;

/// <summary>Memoizes translations so identical text never hits the network twice (crucial for live mode).</summary>
public sealed class CachedTranslator : ITranslator
{
    private readonly ITranslator _inner;
    private readonly ConcurrentDictionary<string, TranslationResult> _cache = new();
    private readonly ConcurrentQueue<string> _order = new();
    private readonly int _capacity;

    public CachedTranslator(ITranslator inner, int capacity = 4000)
    {
        _inner = inner;
        _capacity = capacity;
    }

    public string Name => _inner.Name;

    public async Task<TranslationResult> TranslateAsync(string text, string targetLanguage, CancellationToken ct = default)
    {
        if (TryGet(text, targetLanguage, out var hit)) return hit;

        var result = await _inner.TranslateAsync(text, targetLanguage, ct);
        Put(text, targetLanguage, result);
        return result;
    }

    public async Task<IReadOnlyList<TranslationResult>> TranslateManyAsync(IReadOnlyList<string> texts, string targetLanguage, CancellationToken ct = default)
    {
        var results = new TranslationResult?[texts.Count];
        var missing = new List<int>();

        for (int i = 0; i < texts.Count; i++)
        {
            if (TryGet(texts[i], targetLanguage, out var hit)) results[i] = hit;
            else missing.Add(i);
        }

        if (missing.Count > 0)
        {
            var fetched = await _inner.TranslateManyAsync(missing.Select(i => texts[i]).ToList(), targetLanguage, ct);
            for (int k = 0; k < missing.Count; k++)
            {
                results[missing[k]] = fetched[k];
                Put(texts[missing[k]], targetLanguage, fetched[k]);
            }
        }

        return results!;
    }

    private bool TryGet(string text, string target, out TranslationResult result)
    {
        if (_cache.TryGetValue(target + "|" + text, out var hit))
        {
            result = hit with { FromCache = true, Elapsed = TimeSpan.Zero };
            return true;
        }
        result = null!;
        return false;
    }

    private void Put(string text, string target, TranslationResult result)
    {
        var key = target + "|" + text;
        if (!_cache.TryAdd(key, result)) return;

        _order.Enqueue(key);
        while (_order.Count > _capacity && _order.TryDequeue(out var old))
            _cache.TryRemove(old, out _);
    }
}
