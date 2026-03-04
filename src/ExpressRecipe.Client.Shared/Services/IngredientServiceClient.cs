using Grpc.Net.Client;
using ExpressRecipe.IngredientService.Grpc;
using ExpressRecipe.Shared.DTOs.Product;
using ExpressRecipe.Shared.Services;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace ExpressRecipe.Client.Shared.Services;

/// <summary>
/// High-performance client for the Ingredient microservice.
/// Integrates .NET 9 HybridCache with Redis for ultra-fast local lookups.
/// Uses gRPC for high-speed inter-service communication where possible.
/// </summary>
public class IngredientServiceClient : ApiClientBase, IIngredientServiceClient
{
    private readonly IngredientApi.IngredientApiClient? _grpcClient;
    private readonly HybridCacheService? _cache;
    private readonly ILogger<IngredientServiceClient>? _logger;

    // Feature flag for gRPC - can be toggled via config in production if needed
    // Currently disabled due to HTTP/2 vs 1.1 issues in local development environment
    private const bool USE_GRPC = false;

    public IngredientServiceClient(
        HttpClient httpClient, 
        ITokenProvider tokenProvider,
        HybridCacheService? cache = null,
        ILogger<IngredientServiceClient>? logger = null,
        IngredientApi.IngredientApiClient? grpcClient = null) 
        : base(httpClient, tokenProvider)
    {
        _cache = cache;
        _logger = logger;
        _grpcClient = grpcClient;
    }

    /// <summary>
    /// Bulk lookup of ingredient IDs by name.
    /// Uses HybridCache for local hits and gRPC for microservice communication.
    /// </summary>
    public async Task<Dictionary<string, Guid>> LookupIngredientIdsAsync(List<string> names)
    {
        if (names == null || !names.Any()) return new Dictionary<string, Guid>();

        var result = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var uncachedNames = new List<string>();

        // 1. Check local HybridCache first (L1/L2)
        if (_cache != null)
        {
            foreach (var name in names.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var cacheKey = string.Format(CacheKeys.IngredientByName, name.ToLowerInvariant());
                // We use GetAsync here because we don't want to trigger the factory for EACH individual name
                // in a bulk lookup, as that would be inefficient.
                var cachedId = await _cache.GetAsync<Guid?>(cacheKey);
                if (cachedId.HasValue)
                {
                    result[name] = cachedId.Value;
                }
                else
                {
                    uncachedNames.Add(name);
                }
            }

            if (!uncachedNames.Any()) return result; // All found in local cache!
        }
        else
        {
            uncachedNames = names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        // 2. Fetch missing names from microservice via gRPC or REST
        Dictionary<string, Guid> serviceResults = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        if (USE_GRPC && _grpcClient != null)
        {
            try
            {
                var request = new BulkLookupRequest();
                request.Names.AddRange(uncachedNames);

                var response = await _grpcClient.LookupIngredientIdsAsync(request);

                foreach (var kvp in response.Results)
                {
                    if (Guid.TryParse(kvp.Value, out var id)) 
                    {
                        serviceResults[kvp.Key] = id;
                        result[kvp.Key] = id;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "gRPC BulkLookup failed, falling back to REST");
                serviceResults = await LookupIngredientIdsRestAsync(uncachedNames);
            }
        }
        else
        {
            serviceResults = await LookupIngredientIdsRestAsync(uncachedNames);
        }

        // 3. Cache the results back into HybridCache
        if (_cache != null && serviceResults.Any())
        {
            foreach (var kvp in serviceResults)
            {
                var cacheKey = string.Format(CacheKeys.IngredientByName, kvp.Key.ToLowerInvariant());
                await _cache.SetAsync(cacheKey, (Guid?)kvp.Value, expiration: TimeSpan.FromHours(24));
                result[kvp.Key] = kvp.Value;
            }
        }

        return result;
    }

    private async Task<Dictionary<string, Guid>> LookupIngredientIdsRestAsync(List<string> names)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var restResponse = await HttpClient.PostAsJsonAsync("api/ingredient/bulk/lookup", names);

        if (!restResponse.IsSuccessStatusCode)
        {
            _logger?.LogWarning("[IngredientService] Bulk lookup failed: {Count} names -> {StatusCode}",
                names.Count, restResponse.StatusCode);
            return new Dictionary<string, Guid>();
        }

        var result = await restResponse.Content.ReadFromJsonAsync<Dictionary<string, Guid>>() ?? new Dictionary<string, Guid>();

        sw.Stop();
        _logger?.LogInformation("[IngredientService] Bulk lookup: {Requested} names -> {Found} ingredients in {Ms}ms",
            names.Count, result.Count, sw.ElapsedMilliseconds);

        return result;
    }

    public async Task<Guid?> GetIngredientIdByNameAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        var cacheKey = string.Format(CacheKeys.IngredientByName, name.ToLowerInvariant());

        if (_cache != null)
        {
            return await _cache.GetOrSetAsync<Guid?>(cacheKey, async ct => 
            {
                return await GetIngredientIdByNameInternalAsync(name);
            }, expiration: TimeSpan.FromHours(24));
        }

        return await GetIngredientIdByNameInternalAsync(name);
    }

    private async Task<Guid?> GetIngredientIdByNameInternalAsync(string name)
    {
        if (USE_GRPC && _grpcClient != null)
        {
            try
            {
                var response = await _grpcClient.GetIngredientIdAsync(new IngredientLookupRequest { Name = name });
                if (response.Found && Guid.TryParse(response.Id, out var id)) return id;
                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "gRPC GetIngredientId failed, falling back to REST");
            }
        }

        // Fallback to REST
        var ingredient = await GetAsync<IngredientDto>($"api/ingredient/name/{Uri.EscapeDataString(name)}");
        return ingredient?.Id;
    }

    public async Task<IngredientDto?> GetIngredientAsync(Guid id)
    {
        var cacheKey = string.Format(CacheKeys.IngredientById, id);

        if (_cache != null)
        {
            return await _cache.GetOrSetAsync<IngredientDto?>(cacheKey, async ct => 
            {
                return await GetAsync<IngredientDto>($"api/ingredient/{id}");
            }, expiration: TimeSpan.FromHours(4));
        }

        return await GetAsync<IngredientDto>($"api/ingredient/{id}");
    }

    public async Task<List<IngredientDto>> GetAllIngredientsAsync()
    {
        // Cache this for a short time as it might be large
        if (_cache != null)
        {
            return await _cache.GetOrSetAsync("ingredients:all", async ct => 
            {
                return await GetAsync<List<IngredientDto>>("api/ingredient") ?? new List<IngredientDto>();
            }, expiration: TimeSpan.FromMinutes(15));
        }

        return await GetAsync<List<IngredientDto>>("api/ingredient") ?? new List<IngredientDto>();
    }

    public async Task<Guid?> CreateIngredientAsync(CreateIngredientRequest request)
    {
        var id = await PostAsync<CreateIngredientRequest, Guid>("api/ingredient", request);
        
        // Invalidate name cache if successfully created
        if (id != Guid.Empty && _cache != null)
        {
            var cacheKey = string.Format(CacheKeys.IngredientByName, request.Name.ToLowerInvariant());
            await _cache.RemoveAsync(cacheKey);
        }

        return id == Guid.Empty ? null : id;
    }

    public async Task<int> BulkCreateIngredientsAsync(List<string> names)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var response = await HttpClient.PostAsJsonAsync("api/ingredient/bulk/create", names);

        if (response.IsSuccessStatusCode)
        {
            var count = await response.Content.ReadFromJsonAsync<int>();

            sw.Stop();
            _logger?.LogInformation("[IngredientService] Bulk create: {Requested} names -> {Created} created in {Ms}ms",
                names.Count, count, sw.ElapsedMilliseconds);

            return count;
        }

        sw.Stop();
        _logger?.LogWarning("[IngredientService] Bulk create failed: {Count} names -> {StatusCode} in {Ms}ms",
            names.Count, response.StatusCode, sw.ElapsedMilliseconds);

        return 0;
    }

    public async Task<List<string>> ParseIngredientListAsync(string ingredientsText)
    {
        var response = await HttpClient.PostAsJsonAsync("api/ingredient/parse/list", ingredientsText);
        return response.IsSuccessStatusCode 
            ? await response.Content.ReadFromJsonAsync<List<string>>() ?? new List<string>()
            : new List<string>();
    }

    public async Task<Dictionary<string, List<string>>> BulkParseIngredientListsAsync(List<string> texts)
    {
        var response = await HttpClient.PostAsJsonAsync("api/ingredient/parse/list/bulk", texts);
        return response.IsSuccessStatusCode 
            ? await response.Content.ReadFromJsonAsync<Dictionary<string, List<string>>>() ?? new Dictionary<string, List<string>>()
            : new Dictionary<string, List<string>>();
    }

    public async Task<ParsedIngredientResult?> ParseIngredientStringAsync(string text)
    {
        var response = await HttpClient.PostAsJsonAsync("api/ingredient/parse/string", text);
        return response.IsSuccessStatusCode 
            ? await response.Content.ReadFromJsonAsync<ParsedIngredientResult>()
            : null;
    }

    public async Task<Dictionary<string, ParsedIngredientResult>> BulkParseIngredientStringsAsync(List<string> texts)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var response = await HttpClient.PostAsJsonAsync("api/ingredient/parse/string/bulk", texts);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<Dictionary<string, ParsedIngredientResult>>() 
                ?? new Dictionary<string, ParsedIngredientResult>();

            sw.Stop();
            _logger?.LogInformation("[IngredientService] Bulk parse: {Requested} strings -> {Parsed} parsed in {Ms}ms",
                texts.Count, result.Count, sw.ElapsedMilliseconds);

            return result;
        }

        sw.Stop();
        _logger?.LogWarning("[IngredientService] Bulk parse failed: {Count} strings -> {StatusCode} in {Ms}ms",
            texts.Count, response.StatusCode, sw.ElapsedMilliseconds);

        return new Dictionary<string, ParsedIngredientResult>();
    }

    public async Task<IngredientValidationResult?> ValidateIngredientAsync(string ingredient)
    {
        var response = await HttpClient.GetAsync($"api/ingredient/parse/validate?ingredient={Uri.EscapeDataString(ingredient)}");
        return response.IsSuccessStatusCode 
            ? await response.Content.ReadFromJsonAsync<IngredientValidationResult>()
            : null;
    }
}
