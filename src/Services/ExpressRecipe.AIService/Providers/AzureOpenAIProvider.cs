using ExpressRecipe.AIService.Configuration;

namespace ExpressRecipe.AIService.Providers;

/// <summary>
/// Stub: returns mock result when APP_LOCAL_MODE=true.
/// TODO: Implement when cloud deployment is live.
/// POST https://{endpoint}/openai/deployments/{deployment}/chat/completions?api-version=...
/// Config keys: AI:AzureOpenAI:Endpoint, AI:AzureOpenAI:ApiKey
/// </summary>
public sealed class AzureOpenAIProvider : IAIProvider
{
    public string ProviderName => "AzureOpenAI";

    private readonly ILocalModeConfig _localMode;

    public AzureOpenAIProvider(ILocalModeConfig localMode)
    {
        _localMode = localMode;
    }

    public Task<AITextResult> GenerateAsync(string prompt,
        AIRequestOptions? options = null, CancellationToken ct = default)
    {
        if (_localMode.IsLocalMode)
        {
            return Task.FromResult(new AITextResult
            {
                Success = true, Text = "[AzureOpenAI mock]", ProviderName = ProviderName
            });
        }

        // TODO: Implement Azure OpenAI chat/completions call when cloud is live
        throw new NotImplementedException(
            "AzureOpenAIProvider not yet implemented for cloud deployment.");
    }

    public Task<AIClassifyResult> ClassifyAsync(string prompt, string[] possibleClasses,
        AIRequestOptions? options = null, CancellationToken ct = default)
        => Task.FromResult(new AIClassifyResult { Success = false, ErrorMessage = "AzureOpenAIProvider: ClassifyAsync not implemented" });

    public Task<AIApprovalResult> ScoreForApprovalAsync(string content, string entityType,
        AIRequestOptions? options = null, CancellationToken ct = default)
        => Task.FromResult(new AIApprovalResult { Success = false, KickToHuman = true, ErrorMessage = "AzureOpenAIProvider: ScoreForApprovalAsync not implemented" });
}
