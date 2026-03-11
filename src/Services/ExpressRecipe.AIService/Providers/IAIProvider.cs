using System.Text.Json.Serialization;

namespace ExpressRecipe.AIService.Providers;

public interface IAIProvider
{
    string ProviderName { get; }

    Task<AITextResult> GenerateAsync(string prompt, AIRequestOptions? options = null,
        CancellationToken ct = default);

    Task<AIClassifyResult> ClassifyAsync(string prompt, string[] possibleClasses,
        AIRequestOptions? options = null, CancellationToken ct = default);

    Task<AIApprovalResult> ScoreForApprovalAsync(string content, string entityType,
        AIRequestOptions? options = null, CancellationToken ct = default);
}

public sealed record AIRequestOptions
{
    public int MaxTokens { get; init; } = 512;
    public decimal Temperature { get; init; } = 0.3m;
    public string? SystemPrompt { get; init; }
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
}

public sealed record AITextResult
{
    public bool Success { get; init; }
    public string Text { get; init; } = string.Empty;
    public string? ErrorMessage { get; init; }
    public string ProviderName { get; init; } = string.Empty;
    public int TokensUsed { get; init; }
}

public sealed record AIClassifyResult
{
    public bool Success { get; init; }
    public string ChosenClass { get; init; } = string.Empty;
    public decimal Confidence { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed record AIApprovalResult
{
    public bool Success { get; init; }
    public decimal Score { get; init; }
    public string Reasoning { get; init; } = string.Empty;
    public bool KickToHuman { get; init; }
    public string? ErrorMessage { get; init; }
}
