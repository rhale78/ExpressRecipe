namespace ExpressRecipe.AIService.Services;

public interface IAIProvider
{
    Task<AITextResult> GenerateAsync(string prompt, AIRequestOptions options,
        CancellationToken ct = default);
}

public interface IAIProviderFactory
{
    Task<IAIProvider> GetProviderForUseCaseAsync(string useCase,
        CancellationToken ct = default);
}

public sealed record AITextResult
{
    public bool Success { get; init; }
    public string Text { get; init; } = string.Empty;
    public string? ErrorMessage { get; init; }
}

public sealed record AIRequestOptions
{
    public int MaxTokens { get; init; } = 2000;
    public decimal Temperature { get; init; } = 0.7m;
}
