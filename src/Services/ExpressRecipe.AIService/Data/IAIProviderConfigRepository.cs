namespace ExpressRecipe.AIService.Data;

public sealed record AIProviderConfigDto
{
    public string UseCase { get; init; } = string.Empty;
    public string Provider { get; init; } = "Ollama";
}

public interface IAIProviderConfigRepository
{
    Task<AIProviderConfigDto?> GetConfigAsync(string useCase,
        CancellationToken ct = default);
}
