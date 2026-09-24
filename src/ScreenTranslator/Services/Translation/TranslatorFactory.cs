using ScreenTranslator.Models;

namespace ScreenTranslator.Services.Translation;

/// <summary>Builds the translation pipeline from settings: [AI engine →] Google fallback → cache.</summary>
public static class TranslatorFactory
{
    public const string EngineGoogle = "google";
    public const string EngineClaude = "claude";
    public const string EngineOpenAi = "openai";

    public static ITranslator Create(AppSettings s)
    {
        ITranslator google = new GoogleTranslator();
        ITranslator? primary = CreatePrimary(s);

        ITranslator pipeline = primary is null ? google : new FallbackTranslator(primary, google);
        return new CachedTranslator(pipeline);
    }

    /// <summary>The configured AI engine, or null when Google is selected / the engine isn't configured.</summary>
    public static ITranslator? CreatePrimary(AppSettings s)
    {
        switch (s.Engine)
        {
            case EngineClaude:
                var key = SecretStore.Unprotect(s.ClaudeApiKey);
                return string.IsNullOrWhiteSpace(key) ? null : new ClaudeTranslator(key, s.ClaudeModel);

            case EngineOpenAi:
                if (string.IsNullOrWhiteSpace(s.OpenAiBaseUrl) || string.IsNullOrWhiteSpace(s.OpenAiModel)) return null;
                return new OpenAiCompatibleTranslator(s.OpenAiBaseUrl, SecretStore.Unprotect(s.OpenAiApiKey), s.OpenAiModel);

            default:
                return null;
        }
    }
}

/// <summary>Tries the primary engine; on any failure silently falls back so the user always gets a translation.</summary>
public sealed class FallbackTranslator : ITranslator
{
    private readonly ITranslator _primary, _fallback;
    private DateTime _primaryDisabledUntil = DateTime.MinValue;

    public FallbackTranslator(ITranslator primary, ITranslator fallback)
    {
        _primary = primary;
        _fallback = fallback;
    }

    public string Name => _primary.Name;

    public event Action<Exception>? PrimaryFailed;

    public async Task<TranslationResult> TranslateAsync(string text, string target, CancellationToken ct = default)
    {
        if (DateTime.UtcNow >= _primaryDisabledUntil)
        {
            try { return await _primary.TranslateAsync(text, target, ct); }
            catch (Exception ex) when (ex is not OperationCanceledException) { Trip(ex); }
        }
        var r = await _fallback.TranslateAsync(text, target, ct);
        return r with { Engine = r.Engine + " (fallback)" };
    }

    public async Task<IReadOnlyList<TranslationResult>> TranslateManyAsync(IReadOnlyList<string> texts, string target, CancellationToken ct = default)
    {
        if (DateTime.UtcNow >= _primaryDisabledUntil)
        {
            try { return await _primary.TranslateManyAsync(texts, target, ct); }
            catch (Exception ex) when (ex is not OperationCanceledException) { Trip(ex); }
        }
        var rs = await _fallback.TranslateManyAsync(texts, target, ct);
        return rs.Select(r => r with { Engine = r.Engine + " (fallback)" }).ToList();
    }

    // After a failure, give the primary a short rest instead of paying its latency on every request.
    private void Trip(Exception ex)
    {
        _primaryDisabledUntil = DateTime.UtcNow.AddSeconds(30);
        PrimaryFailed?.Invoke(ex);
    }
}
