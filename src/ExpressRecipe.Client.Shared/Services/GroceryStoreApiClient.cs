using System.Net.Http.Json;
using ExpressRecipe.Client.Shared.Models.GroceryStore;

namespace ExpressRecipe.Client.Shared.Services;

public interface IGroceryStoreApiClient
{
    Task<GroceryStoreSearchResponse> SearchStoresAsync(GroceryStoreSearchRequest request);
    Task<GroceryStoreDto?> GetStoreByIdAsync(Guid id);
    Task<List<GroceryStoreDto>> GetNearbyStoresAsync(double latitude, double longitude, double radiusMiles = 10, int limit = 50);
}

public class GroceryStoreApiClient : IGroceryStoreApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ITokenProvider _tokenProvider;

    public GroceryStoreApiClient(HttpClient httpClient, ITokenProvider tokenProvider)
    {
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
    }

    public async Task<GroceryStoreSearchResponse> SearchStoresAsync(GroceryStoreSearchRequest request)
    {
        try
        {
            var queryParams = new List<string>();
            if (!string.IsNullOrEmpty(request.Name)) queryParams.Add($"name={Uri.EscapeDataString(request.Name)}");
            if (!string.IsNullOrEmpty(request.Chain)) queryParams.Add($"chain={Uri.EscapeDataString(request.Chain)}");
            if (!string.IsNullOrEmpty(request.City)) queryParams.Add($"city={Uri.EscapeDataString(request.City)}");
            if (!string.IsNullOrEmpty(request.State)) queryParams.Add($"state={Uri.EscapeDataString(request.State)}");
            if (!string.IsNullOrEmpty(request.ZipCode)) queryParams.Add($"zipCode={Uri.EscapeDataString(request.ZipCode)}");
            if (!string.IsNullOrEmpty(request.StoreType)) queryParams.Add($"storeType={Uri.EscapeDataString(request.StoreType)}");
            if (request.AcceptsSnap.HasValue) queryParams.Add($"acceptsSnap={request.AcceptsSnap}");
            queryParams.Add($"page={request.Page}");
            queryParams.Add($"pageSize={request.PageSize}");

            var url = $"/api/grocerystores?{string.Join("&", queryParams)}";
            var result = await _httpClient.GetFromJsonAsync<GroceryStoreSearchResponse>(url);
            return result ?? new GroceryStoreSearchResponse();
        }
        catch
        {
            return new GroceryStoreSearchResponse();
        }
    }

    public async Task<GroceryStoreDto?> GetStoreByIdAsync(Guid id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<GroceryStoreDto>($"/api/grocerystores/{id}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<GroceryStoreDto>> GetNearbyStoresAsync(double latitude, double longitude, double radiusMiles = 10, int limit = 50)
    {
        try
        {
            var url = $"/api/grocerystores/nearby?lat={latitude}&lon={longitude}&radiusMiles={radiusMiles}&limit={limit}";
            var result = await _httpClient.GetFromJsonAsync<List<GroceryStoreDto>>(url);
            return result ?? new List<GroceryStoreDto>();
        }
        catch
        {
            return new List<GroceryStoreDto>();
        }
    }
}
