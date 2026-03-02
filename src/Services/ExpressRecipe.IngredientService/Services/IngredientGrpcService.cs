using Grpc.Core;
using ExpressRecipe.IngredientService.Grpc;
using ExpressRecipe.IngredientService.Data;
using ExpressRecipe.Shared.Services;

namespace ExpressRecipe.IngredientService.Services;

public class IngredientGrpcService : IngredientApi.IngredientApiBase
{
    private readonly IIngredientRepository _repository;
    private readonly HybridCacheService _cache;
    private readonly ILogger<IngredientGrpcService> _logger;

    public IngredientGrpcService(
        IIngredientRepository repository,
        HybridCacheService cache,
        ILogger<IngredientGrpcService> logger)
    {
        _repository = repository;
        _cache = cache;
        _logger = logger;
    }

    public override async Task<IngredientLookupResponse> GetIngredientId(IngredientLookupRequest request, ServerCallContext context)
    {
        var name = request.Name.Trim().ToLowerInvariant();
        var cacheKey = string.Format(CacheKeys.IngredientByName, name);

        var id = await _cache.GetOrSetAsync<Guid?>(cacheKey, async ct => 
        {
            var ing = await _repository.GetIngredientByNameAsync(request.Name);
            return ing?.Id;
        });

        if (id.HasValue)
        {
            return new IngredientLookupResponse { Id = id.Value.ToString(), Found = true };
        }

        return new IngredientLookupResponse { Found = false };
    }

    public override async Task<BulkLookupResponse> LookupIngredientIds(BulkLookupRequest request, ServerCallContext context)
    {
        var response = new BulkLookupResponse();
        var namesToLookup = request.Names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        
        if (!namesToLookup.Any()) return response;

        // Use high-speed bulk repository method
        var resultIds = await _repository.GetIngredientIdsByNamesAsync(namesToLookup);

        foreach (var kvp in resultIds)
        {
            response.Results.Add(kvp.Key, kvp.Value.ToString());
            
            // Proactively cache these results individually if not already there
            var cacheKey = string.Format(CacheKeys.IngredientByName, kvp.Key.ToLowerInvariant());
            await _cache.SetAsync(cacheKey, kvp.Value);
        }

        return response;
    }
}
