using ExpressRecipe.AIService.Configuration;

namespace ExpressRecipe.AIService.Providers;

/// <summary>
/// Cloud AI provider stub. This provider requires cloud deployment configuration.
/// <para>
/// To enable: configure the provider credentials in appsettings.json under
/// <c>AI:Providers:AzureOpenAI:ApiKey</c> and deploy to a cloud environment.
/// </para>
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

        // Cloud deployment required: Implement HTTP POST to the Azure OpenAI completions endpoint.
        // See: https://learn.microsoft.com/azure/cognitive-services/openai/reference
        throw new NotImplementedException("AzureOpenAI provider requires cloud deployment. Configure AI:Providers:AzureOpenAI:ApiKey and endpoint.");
    }

    public Task<AIClassifyResult> ClassifyAsync(string prompt, string[] possibleClasses,
        AIRequestOptions? options = null, CancellationToken ct = default)
        => Task.FromResult(new AIClassifyResult { Success = false, ErrorMessage = "AzureOpenAIProvider: ClassifyAsync not implemented" });

    public Task<AIApprovalResult> ScoreForApprovalAsync(string content, string entityType,
        AIRequestOptions? options = null, CancellationToken ct = default)
        => Task.FromResult(new AIApprovalResult { Success = false, KickToHuman = true, ErrorMessage = "AzureOpenAIProvider: ScoreForApprovalAsync not implemented" });
}
