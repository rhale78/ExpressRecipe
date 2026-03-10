using ExpressRecipe.AIService.Configuration;

namespace ExpressRecipe.AIService.Providers;

/// <summary>
/// Stub: returns mock result when APP_LOCAL_MODE=true; throws NotImplementedException in cloud.
/// Fill in HTTP call when deploying to Azure/cloud.
/// POST https://api.anthropic.com/v1/messages
/// Headers: x-api-key, anthropic-version: 2023-06-01
/// </summary>
public sealed class ClaudeProvider : IAIProvider
{
    public string ProviderName => "Claude";

    private readonly ILocalModeConfig _localMode;
    private readonly IHttpClientFactory _http;
    private readonly string _apiKey;

    public ClaudeProvider(ILocalModeConfig localMode, IHttpClientFactory http,
        IConfiguration config)
    {
        _localMode = localMode;
        _http      = http;
        _apiKey    = config["AI:Claude:ApiKey"] ?? string.Empty;
    }

    public Task<AITextResult> GenerateAsync(string prompt,
        AIRequestOptions? options = null, CancellationToken ct = default)
    {
        if (_localMode.IsLocalMode)
        {
            return Task.FromResult(new AITextResult
            {
                Success      = true,
                Text         = "[Claude mock response — local mode]",
                ProviderName = ProviderName
            });
        }

        // TODO: Implement Anthropic /v1/messages call when cloud is live
        throw new NotImplementedException(
            "ClaudeProvider not yet implemented for cloud deployment.");
    }

    public Task<AIClassifyResult> ClassifyAsync(string prompt, string[] possibleClasses,
        AIRequestOptions? options = null, CancellationToken ct = default)
        => Task.FromResult(new AIClassifyResult
        {
            Success      = false,
            ErrorMessage = "ClaudeProvider: ClassifyAsync not implemented"
        });

    public Task<AIApprovalResult> ScoreForApprovalAsync(string content, string entityType,
        AIRequestOptions? options = null, CancellationToken ct = default)
        => Task.FromResult(new AIApprovalResult
        {
            Success      = false,
            KickToHuman  = true,
            ErrorMessage = "ClaudeProvider: ScoreForApprovalAsync not implemented"
        });
}
