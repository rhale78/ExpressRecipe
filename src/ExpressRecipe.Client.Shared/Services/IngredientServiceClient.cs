using Grpc.Net.Client;
using ExpressRecipe.IngredientService.Grpc;
using ExpressRecipe.Shared.DTOs.Product;
using System.Net.Http.Json;

namespace ExpressRecipe.Client.Shared.Services;

/// <summary>
/// High-performance client for the Ingredient microservice.
/// Uses REST API for all operations. gRPC support disabled until HTTP/2 issues resolved.
/// </summary>
public class IngredientServiceClient : ApiClientBase
{
    // Feature flag to enable gRPC in the future
    private const bool USE_GRPC = false;

    // Keep gRPC client for future use but make it nullable
    private readonly IngredientApi.IngredientApiClient? _grpcClient;

    public IngredientServiceClient(
        HttpClient httpClient, 
        ITokenProvider tokenProvider,
        IngredientApi.IngredientApiClient? grpcClient = null) 
        : base(httpClient, tokenProvider)
    {
        _grpcClient = grpcClient;
    }

    /// <summary>
    /// Bulk lookup of ingredient IDs by name via REST API.
    /// gRPC code commented out until HTTP/2 issues resolved.
    /// </summary>
    public async Task<Dictionary<string, Guid>> LookupIngredientIdsAsync(List<string> names)
    {
        if (names == null || !names.Any()) return new Dictionary<string, Guid>();

        // TODO: Re-enable gRPC when HTTP/2 issues are resolved
        // Set USE_GRPC = true and uncomment the gRPC code below
        /*
        if (USE_GRPC && _grpcClient != null)
        {
            try
            {
                var request = new BulkLookupRequest();
                request.Names.AddRange(names);

                var response = await _grpcClient.LookupIngredientIdsAsync(request);

                var result = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in response.Results)
                {
                    if (Guid.TryParse(kvp.Value, out var id)) result[kvp.Key] = id;
                }
                return result;
            }
            catch
            {
                // Fall through to REST API
            }
        }
        */

        // Use REST API
        var restResponse = await HttpClient.PostAsJsonAsync("api/ingredient/bulk/lookup", names);
        if (!restResponse.IsSuccessStatusCode) return new Dictionary<string, Guid>();
        return await restResponse.Content.ReadFromJsonAsync<Dictionary<string, Guid>>() ?? new Dictionary<string, Guid>();
    }

    public async Task<Guid?> GetIngredientIdByNameAsync(string name)
    {
        // TODO: Re-enable gRPC when HTTP/2 issues are resolved
        /*
        if (USE_GRPC && _grpcClient != null)
        {
            try
            {
                var response = await _grpcClient.GetIngredientIdAsync(new IngredientLookupRequest { Name = name });
                if (response.Found && Guid.TryParse(response.Id, out var id)) return id;
                return null;
            }
            catch
            {
                // Fall through to REST API
            }
        }
        */

        // Use REST API
        var ingredient = await GetAsync<IngredientDto>($"api/ingredient/name/{Uri.EscapeDataString(name)}");
        return ingredient?.Id;
    }

    // CRUD operations via REST
    public async Task<IngredientDto?> GetIngredientAsync(Guid id) => await GetAsync<IngredientDto>($"api/ingredient/{id}");
    public async Task<Guid?> CreateIngredientAsync(CreateIngredientRequest request) => await PostAsync<CreateIngredientRequest, Guid>("api/ingredient", request);
    public async Task<int> BulkCreateIngredientsAsync(List<string> names)
    {
        var response = await HttpClient.PostAsJsonAsync("api/ingredient/bulk/create", names);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<int>() : 0;
    }
}
