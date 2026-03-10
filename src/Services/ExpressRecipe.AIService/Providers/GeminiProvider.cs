using ExpressRecipe.AIService.Configuration;

namespace ExpressRecipe.AIService.Providers;

/// <summary>
/// Stub: returns mock result when APP_LOCAL_MODE=true.
/// TODO: Implement when cloud deployment is live.
/// POST https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent
/// Config key: AI:Gemini:ApiKey
/// </summary>
public sealed class GeminiProvider : IAIProvider
{
    public string ProviderName => "Gemini";

    private readonly ILocalModeConfig _localMode;

    public GeminiProvider(ILocalModeConfig localMode)
    {
        _localMode = localMode;
    }

    public Task<AITextResult> GenerateAsync(string prompt,
        AIRequestOptions? options = null, CancellationToken ct = default)
        => Task.FromResult(_localMode.IsLocalMode
            ? new AITextResult { Success = true, Text = "[Gemini mock]", ProviderName = ProviderName }
            : new AITextResult { Success = false, ErrorMessage = "GeminiProvider: not implemented", ProviderName = ProviderName });

    public Task<AIClassifyResult> ClassifyAsync(string prompt, string[] possibleClasses,
        AIRequestOptions? options = null, CancellationToken ct = default)
        => Task.FromResult(new AIClassifyResult { Success = false, ErrorMessage = "GeminiProvider: ClassifyAsync not implemented" });

    public Task<AIApprovalResult> ScoreForApprovalAsync(string content, string entityType,
        AIRequestOptions? options = null, CancellationToken ct = default)
        => Task.FromResult(new AIApprovalResult { Success = false, KickToHuman = true, ErrorMessage = "GeminiProvider: ScoreForApprovalAsync not implemented" });
}
