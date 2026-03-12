using ExpressRecipe.AIService.Configuration;

namespace ExpressRecipe.AIService.Providers;

/// <summary>
/// Cloud AI provider stub. This provider requires cloud deployment configuration.
/// <para>
/// To enable: configure the provider credentials in appsettings.json under
/// <c>AI:Providers:Claude:ApiKey</c> and deploy to a cloud environment.
/// </para>
/// POST https://api.anthropic.com/v1/messages
/// Headers: x-api-key, anthropic-version: 2023-06-01
/// </summary>
public sealed class ClaudeProvider : IAIProvider
{
    public string ProviderName => "Claude";

    private readonly ILocalModeConfig _localMode;

    public ClaudeProvider(ILocalModeConfig localMode)
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
                Success      = true,
                Text         = "[Claude mock response — local mode]",
                ProviderName = ProviderName
            });
        }

        // Cloud deployment required: Implement HTTP POST to the Anthropic messages endpoint.
        // See: https://docs.anthropic.com/en/api/messages
        throw new NotImplementedException("Claude provider requires cloud deployment. Configure AI:Providers:Claude:ApiKey.");
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
