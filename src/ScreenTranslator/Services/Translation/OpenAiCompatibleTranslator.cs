using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ScreenTranslator.Services.Translation;

/// <summary>
/// Any provider that speaks the OpenAI chat-completions wire format: OpenAI, OpenRouter, Groq, DeepSeek,
/// a local Ollama/LM Studio server, or a regional proxy. Base URL is whatever comes before "/chat/completions".
/// </summary>
public sealed class OpenAiCompatibleTranslator : LlmTranslatorBase
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };

    private readonly string _endpoint;
    private readonly string _apiKey;
    private readonly string _model;

    public override string Name => "AI";

    public OpenAiCompatibleTranslator(string baseUrl, string apiKey, string model)
    {
        var b = baseUrl.Trim().TrimEnd('/');
        _endpoint = b.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase) ? b : b + "/chat/completions";
        _apiKey = apiKey;
        _model = model;
    }

    protected override async Task<string> CompleteAsync(string system, string user, CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(new
        {
            model = _model,
            temperature = 0.2,
            messages = new object[]
            {
                new { role = "system", content = system },
                new { role = "user", content = user },
            },
        });

        using var req = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        if (!string.IsNullOrEmpty(_apiKey))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        using var resp = await Http.SendAsync(req, ct);
        var text = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"{(int)resp.StatusCode} {resp.ReasonPhrase}: {Trim(text)}");

        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
    }

    private static string Trim(string s) => s.Length > 200 ? s[..200] + "…" : s;
}
