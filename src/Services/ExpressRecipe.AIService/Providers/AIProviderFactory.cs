using ExpressRecipe.AIService.Data;
using Microsoft.Extensions.Caching.Hybrid;

namespace ExpressRecipe.AIService.Providers;

public interface IAIProviderFactory
{
    Task<IAIProvider> GetProviderForUseCaseAsync(string useCase,
        CancellationToken ct = default);
}

public sealed class AIProviderFactory : IAIProviderFactory
{
    private readonly IReadOnlyDictionary<string, IAIProvider> _providers;
    private readonly HybridCache _cache;
    private readonly IAIProviderConfigRepository _config;

    public AIProviderFactory(IEnumerable<IAIProvider> providers,
        HybridCache cache, IAIProviderConfigRepository config)
    {
        _providers = providers.ToDictionary(
            p => p.ProviderName, StringComparer.OrdinalIgnoreCase);
        _cache  = cache;
        _config = config;
    }

    public async Task<IAIProvider> GetProviderForUseCaseAsync(string useCase,
        CancellationToken ct = default)
    {
        string providerName = await _cache.GetOrCreateAsync(
            $"ai-provider:{useCase}",
            async innerCt =>
            {
                AIProviderConfigDto? cfg =
                    await _config.GetConfigAsync(useCase, innerCt)
                    ?? await _config.GetConfigAsync("global", innerCt);
                return cfg?.Provider ?? "Ollama";
            },
            new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(10) },
            cancellationToken: ct);

        return _providers.TryGetValue(providerName, out IAIProvider? provider)
            ? provider
            : _providers["Ollama"];
    }
}
