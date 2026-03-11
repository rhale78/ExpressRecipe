using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;

namespace ExpressRecipe.AIService.Services;

/// <summary>
/// Factory that resolves the correct <see cref="IAIProvider"/> for a given use-case
/// by looking up the <c>AIProviderConfig</c> table. Falls back to the default Ollama
/// model when the DB is unavailable or the use-case has no configured entry.
/// Results are cached in-memory for 1 hour because use-case→model mappings are static.
/// </summary>
public sealed class AIProviderFactory : IAIProviderFactory
{
    private readonly IOllamaService _ollamaService;
    private readonly string? _connectionString;
    private readonly string _defaultModel;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AIProviderFactory> _logger;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    public AIProviderFactory(IOllamaService ollamaService, IConfiguration configuration,
        IMemoryCache cache, ILogger<AIProviderFactory> logger)
    {
        _ollamaService = ollamaService;
        _connectionString = configuration.GetConnectionString("aidb");
        _defaultModel = configuration["AI:DefaultModel"]
            ?? configuration["Ollama:DefaultModel"]
            ?? "llama3.2";
        _cache = cache;
        _logger = logger;
    }

    public async Task<IAIProvider> GetProviderForUseCaseAsync(string useCase,
        CancellationToken ct = default)
    {
        string model = _defaultModel;

        if (!string.IsNullOrEmpty(_connectionString))
        {
            string cacheKey = $"aimodel:{useCase}";
            if (!_cache.TryGetValue(cacheKey, out string? cachedModel))
            {
                cachedModel = await LookupModelForUseCaseAsync(useCase, ct);
                if (cachedModel is not null)
                {
                    _cache.Set(cacheKey, cachedModel, CacheTtl);
                }
            }
            model = cachedModel ?? _defaultModel;
        }

        return new OllamaAIProvider(_ollamaService, model, _logger);
    }

    private async Task<string?> LookupModelForUseCaseAsync(string useCase, CancellationToken ct)
    {
        try
        {
            await using SqlConnection conn = new(_connectionString!);
            await conn.OpenAsync(ct);
            await using SqlCommand cmd = new(
                "SELECT TOP 1 ModelName FROM AIProviderConfig WHERE UseCase = @UseCase AND IsActive = 1",
                conn);
            cmd.Parameters.AddWithValue("@UseCase", useCase);
            object? result = await cmd.ExecuteScalarAsync(ct);
            return result as string;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not resolve AI provider config for use case '{UseCase}'; using default model", useCase);
            return null;
        }
    }
}

/// <summary>
/// Ollama-backed <see cref="IAIProvider"/> that delegates to the shared
/// <see cref="IOllamaService"/> with a specific model name.
/// </summary>
internal sealed class OllamaAIProvider : IAIProvider
{
    private readonly IOllamaService _service;
    private readonly string _modelName;
    private readonly ILogger _logger;

    internal OllamaAIProvider(IOllamaService service, string modelName, ILogger logger)
    {
        _service = service;
        _modelName = modelName;
        _logger = logger;
    }

    public async Task<AITextResult> GenerateAsync(string prompt, AIRequestOptions options,
        CancellationToken ct = default)
    {
        try
        {
            ct.ThrowIfCancellationRequested();

            string text = await _service.GenerateCompletionAsync(
                prompt, _modelName, (double)options.Temperature, options.MaxTokens, ct);

            return new AITextResult { Success = true, Text = text };
        }
        catch (OperationCanceledException)
        {
            // Propagate cancellation so upstream callers can observe it.
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI generation failed for model '{Model}'", _modelName);
            return new AITextResult { Success = false, ErrorMessage = ex.Message };
        }
    }
}
