using ExpressRecipe.AIService.Configuration;

namespace ExpressRecipe.AIService.Providers;

/// <summary>
/// Stub: returns mock result when APP_LOCAL_MODE=true.
/// TODO: Implement when cloud deployment is live.
/// POST https://api.openai.com/v1/chat/completions
/// Config key: AI:OpenAI:ApiKey
/// </summary>
public sealed class OpenAIProvider : IAIProvider
{
    public string ProviderName => "OpenAI";

    private readonly ILocalModeConfig _localMode;

    public OpenAIProvider(ILocalModeConfig localMode)
    {
        _localMode = localMode;
    }

    public Task<AITextResult> GenerateAsync(string prompt,
        AIRequestOptions? options = null, CancellationToken ct = default)
        => Task.FromResult(_localMode.IsLocalMode
            ? new AITextResult { Success = true, Text = "[OpenAI mock]", ProviderName = ProviderName }
            : new AITextResult { Success = false, ErrorMessage = "OpenAIProvider: not implemented", ProviderName = ProviderName });

    public Task<AIClassifyResult> ClassifyAsync(string prompt, string[] possibleClasses,
        AIRequestOptions? options = null, CancellationToken ct = default)
        => Task.FromResult(new AIClassifyResult { Success = false, ErrorMessage = "OpenAIProvider: ClassifyAsync not implemented" });

    public Task<AIApprovalResult> ScoreForApprovalAsync(string content, string entityType,
        AIRequestOptions? options = null, CancellationToken ct = default)
        => Task.FromResult(new AIApprovalResult { Success = false, KickToHuman = true, ErrorMessage = "OpenAIProvider: ScoreForApprovalAsync not implemented" });
}
