using ExpressRecipe.AIService.Configuration;

namespace ExpressRecipe.AIService.Providers;

/// <summary>
/// Cloud AI provider stub. This provider requires cloud deployment configuration.
/// <para>
/// To enable: configure the provider credentials in appsettings.json under
/// <c>AI:Providers:Gemini:ApiKey</c> and deploy to a cloud environment.
/// </para>
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
    {
        if (_localMode.IsLocalMode)
        {
            return Task.FromResult(new AITextResult
            {
                Success = true, Text = "[Gemini mock]", ProviderName = ProviderName
            });
        }

        // Cloud deployment required: Implement HTTP POST to the Gemini generateContent endpoint.
        // See: https://ai.google.dev/api/generate-content
        throw new NotImplementedException("Gemini provider requires cloud deployment. Configure AI:Providers:Gemini:ApiKey.");
    }

    public Task<AIClassifyResult> ClassifyAsync(string prompt, string[] possibleClasses,
        AIRequestOptions? options = null, CancellationToken ct = default)
        => Task.FromResult(new AIClassifyResult { Success = false, ErrorMessage = "GeminiProvider: ClassifyAsync not implemented" });

    public Task<AIApprovalResult> ScoreForApprovalAsync(string content, string entityType,
        AIRequestOptions? options = null, CancellationToken ct = default)
        => Task.FromResult(new AIApprovalResult { Success = false, KickToHuman = true, ErrorMessage = "GeminiProvider: ScoreForApprovalAsync not implemented" });
}
