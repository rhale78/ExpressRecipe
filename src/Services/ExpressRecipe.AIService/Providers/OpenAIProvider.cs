using ExpressRecipe.AIService.Configuration;

namespace ExpressRecipe.AIService.Providers;

/// <summary>
/// Cloud AI provider stub. This provider requires cloud deployment configuration.
/// <para>
/// To enable: configure the provider credentials in appsettings.json under
/// <c>AI:Providers:OpenAI:ApiKey</c> and deploy to a cloud environment.
/// </para>
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
    {
        if (_localMode.IsLocalMode)
        {
            return Task.FromResult(new AITextResult
            {
                Success = true, Text = "[OpenAI mock]", ProviderName = ProviderName
            });
        }

        // Cloud deployment required: Implement HTTP POST to the OpenAI chat completions endpoint.
        // See: https://platform.openai.com/docs/api-reference/chat
        throw new NotImplementedException("OpenAI provider requires cloud deployment. Configure AI:Providers:OpenAI:ApiKey.");
    }

    public Task<AIClassifyResult> ClassifyAsync(string prompt, string[] possibleClasses,
        AIRequestOptions? options = null, CancellationToken ct = default)
        => Task.FromResult(new AIClassifyResult { Success = false, ErrorMessage = "OpenAIProvider: ClassifyAsync not implemented" });

    public Task<AIApprovalResult> ScoreForApprovalAsync(string content, string entityType,
        AIRequestOptions? options = null, CancellationToken ct = default)
        => Task.FromResult(new AIApprovalResult { Success = false, KickToHuman = true, ErrorMessage = "OpenAIProvider: ScoreForApprovalAsync not implemented" });
}
