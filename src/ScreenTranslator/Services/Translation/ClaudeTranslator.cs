using Anthropic;
using Anthropic.Models.Messages;

namespace ScreenTranslator.Services.Translation;

/// <summary>Claude via the official Anthropic SDK. Best quality for nuanced/literary text.</summary>
public sealed class ClaudeTranslator : LlmTranslatorBase
{
    public const string DefaultModel = "claude-opus-5";
    public static readonly string[] Models = { "claude-opus-5", "claude-sonnet-5", "claude-haiku-4-5" };

    private readonly AnthropicClient _client;
    private readonly string _model;

    public override string Name => "Claude";

    public ClaudeTranslator(string apiKey, string? model = null)
    {
        _client = new AnthropicClient { ApiKey = apiKey };
        _model = string.IsNullOrWhiteSpace(model) ? DefaultModel : model;
    }

    protected override async Task<string> CompleteAsync(string system, string user, CancellationToken ct)
    {
        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = _model,
            MaxTokens = 4096,
            System = system,
            // Translation is a quick, well-specified task: keep reasoning light so results feel instant.
            OutputConfig = new() { Effort = Effort.Low },
            Messages = [new() { Role = Role.User, Content = user }],
        }, ct);

        return string.Concat(response.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text));
    }
}
