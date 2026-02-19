using System.Net.Http.Json;
using ExpressRecipe.Client.Shared.Models.Shopping;

namespace ExpressRecipe.Client.Shared.Services;

public interface IShoppingListApiClient
{
    // Shopping List CRUD
    Task<ShoppingListDto?> GetShoppingListAsync(Guid id);
    Task<ShoppingListSearchResult?> SearchShoppingListsAsync(ShoppingListSearchRequest request);
    Task<List<ShoppingListDto>?> GetHouseholdListsAsync(Guid householdId);
    Task<ShoppingSummaryDto?> GetShoppingSummaryAsync();
    Task<Guid?> CreateShoppingListAsync(CreateShoppingListRequest request);
    Task<bool> UpdateShoppingListAsync(Guid id, UpdateShoppingListRequest request);
    Task<bool> DeleteShoppingListAsync(Guid id);

    // Shopping List Items
    Task<Guid?> AddItemAsync(AddShoppingListItemRequest request);
    Task<bool> UpdateItemAsync(Guid itemId, UpdateShoppingListItemRequest request);
    Task<bool> DeleteItemAsync(Guid itemId);
    Task<bool> MarkItemPurchasedAsync(MarkItemPurchasedRequest request);
    Task<bool> ReorderItemsAsync(ReorderItemsRequest request);
    Task<bool> MoveItemToListAsync(Guid itemId, MoveItemRequest request);

    // Advanced operations
    Task<bool> AddItemsFromRecipeAsync(AddItemsFromRecipeRequest request);
    Task<bool> AddLowStockItemsAsync(AddLowStockItemsRequest request);
    Task<bool> CompleteShoppingListAsync(Guid id);
    Task<bool> ArchiveShoppingListAsync(Guid id);

    // Favorites
    Task<List<FavoriteItemDto>?> GetFavoritesAsync();
    Task<List<FavoriteItemDto>?> GetHouseholdFavoritesAsync(Guid householdId);
    Task<Guid?> AddFavoriteAsync(AddFavoriteItemRequest request);
    Task<bool> RemoveFavoriteAsync(Guid favoriteId);
    Task<bool> AddFavoriteToListAsync(Guid favoriteId, Guid listId);
    Task<bool> UpdateFavoriteUsageAsync(Guid favoriteId);

    // Stores
    Task<List<StoreDto>?> GetNearbyStoresAsync(double latitude, double longitude, double? radiusKm = null);
    Task<StoreDto?> GetStoreAsync(Guid storeId);
    Task<Guid?> CreateStoreAsync(CreateStoreRequest request);
    Task<bool> UpdateStoreAsync(Guid storeId, UpdateStoreRequest request);
    Task<bool> SetPreferredStoreAsync(Guid storeId);
    Task<List<PriceComparisonDto>?> GetBestPricesAsync(Guid productId);
    Task<bool> RecordPriceComparisonAsync(RecordPriceRequest request);
    Task<StoreLayoutDto?> GetStoreLayoutAsync(Guid storeId);
    Task<bool> UpdateStoreLayoutAsync(Guid storeId, UpdateStoreLayoutRequest request);

    // Templates
    Task<List<ShoppingListTemplateDto>?> GetTemplatesAsync();
    Task<List<ShoppingListTemplateDto>?> GetHouseholdTemplatesAsync(Guid householdId);
    Task<ShoppingListTemplateDto?> GetTemplateAsync(Guid templateId);
    Task<Guid?> CreateTemplateAsync(CreateTemplateRequest request);
    Task<bool> AddItemToTemplateAsync(Guid templateId, AddTemplateItemRequest request);
    Task<List<ShoppingListTemplateItemDto>?> GetTemplateItemsAsync(Guid templateId);
    Task<Guid?> CreateListFromTemplateAsync(Guid templateId, CreateListFromTemplateRequest request);
    Task<bool> DeleteTemplateAsync(Guid templateId);

    // Shopping Scan Sessions
    Task<Guid?> StartShoppingScanSessionAsync(StartShoppingScanRequest request);
    Task<ShoppingScanSessionDto?> GetActiveShoppingScanSessionAsync();
    Task<bool> ScanPurchaseItemAsync(Guid sessionId, ScanPurchaseRequest request);
    Task<ShoppingScanSessionResultDto?> EndShoppingScanSessionAsync(Guid sessionId);
    Task<bool> AddPurchasedToInventoryAsync(AddPurchasedToInventoryRequest request);
}

public class ShoppingListApiClient : IShoppingListApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ITokenProvider _tokenProvider;

    public ShoppingListApiClient(HttpClient httpClient, ITokenProvider tokenProvider)
    {
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
    }

    private async Task<bool> EnsureAuthenticatedAsync()
    {
        var token = await _tokenProvider.GetAccessTokenAsync();
        if (string.IsNullOrEmpty(token))
            return false;

        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return true;
    }

    public async Task<ShoppingListDto?> GetShoppingListAsync(Guid id)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            return await _httpClient.GetFromJsonAsync<ShoppingListDto>($"/api/shopping/lists/{id}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<ShoppingListSearchResult?> SearchShoppingListsAsync(ShoppingListSearchRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            // Return empty results for now since endpoint doesn't exist
            return new ShoppingListSearchResult
            {
                Lists = new List<ShoppingListDto>(),
                TotalCount = 0,
                Page = request.Page,
                PageSize = request.PageSize
            };
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<ShoppingListDto>?> GetHouseholdListsAsync(Guid householdId)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            return await _httpClient.GetFromJsonAsync<List<ShoppingListDto>>($"/api/shopping/lists/household/{householdId}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<ShoppingSummaryDto?> GetShoppingSummaryAsync()
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            // Return empty summary for now since endpoint doesn't exist yet
            return new ShoppingSummaryDto
            {
                TotalActiveLists = 0,
                TotalActiveItems = 0,
                CompletedItemsThisWeek = 0,
                EstimatedTotal = 0,
                CategoriesSummary = new List<ShoppingCategorySummaryDto>()
            };
        }
        catch
        {
            return null;
        }
    }

    public async Task<Guid?> CreateShoppingListAsync(CreateShoppingListRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/shopping/lists", request);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<CreateShoppingListResponse>();
            return result?.Id;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> UpdateShoppingListAsync(Guid id, UpdateShoppingListRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PutAsJsonAsync($"/api/shopping/lists/{id}", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DeleteShoppingListAsync(Guid id)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.DeleteAsync($"/api/shopping/lists/{id}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<Guid?> AddItemAsync(AddShoppingListItemRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/shopping/lists/{request.ShoppingListId}/items", request);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<AddItemResponse>();
            return result?.ItemId;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> UpdateItemAsync(Guid itemId, UpdateShoppingListItemRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PutAsJsonAsync($"/api/shopping/items/{itemId}", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DeleteItemAsync(Guid itemId)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.DeleteAsync($"/api/shopping/items/{itemId}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> MarkItemPurchasedAsync(MarkItemPurchasedRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/shopping/items/mark-purchased", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> ReorderItemsAsync(ReorderItemsRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/shopping/reorder", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> MoveItemToListAsync(Guid itemId, MoveItemRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/shopping/items/{itemId}/move", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> AddItemsFromRecipeAsync(AddItemsFromRecipeRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/shopping/add-from-recipe", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> AddLowStockItemsAsync(AddLowStockItemsRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/shopping/add-low-stock", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> CompleteShoppingListAsync(Guid id)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PostAsync($"/api/shopping/lists/{id}/complete", null);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> ArchiveShoppingListAsync(Guid id)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PostAsync($"/api/shopping/lists/{id}/archive", null);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // Favorites
    public async Task<List<FavoriteItemDto>?> GetFavoritesAsync()
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            return await _httpClient.GetFromJsonAsync<List<FavoriteItemDto>>("/api/shopping/favorites");
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<FavoriteItemDto>?> GetHouseholdFavoritesAsync(Guid householdId)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            return await _httpClient.GetFromJsonAsync<List<FavoriteItemDto>>($"/api/shopping/favorites/household/{householdId}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<Guid?> AddFavoriteAsync(AddFavoriteItemRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/shopping/favorites", request);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<GuidResponse>();
            return result?.Id;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> RemoveFavoriteAsync(Guid favoriteId)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.DeleteAsync($"/api/shopping/favorites/{favoriteId}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> AddFavoriteToListAsync(Guid favoriteId, Guid listId)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PostAsync($"/api/shopping/favorites/{favoriteId}/add-to-list/{listId}", null);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> UpdateFavoriteUsageAsync(Guid favoriteId)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PutAsync($"/api/shopping/favorites/{favoriteId}/use", null);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // Stores
    public async Task<List<StoreDto>?> GetNearbyStoresAsync(double latitude, double longitude, double? radiusKm = null)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var radius = radiusKm ?? 10.0;
            return await _httpClient.GetFromJsonAsync<List<StoreDto>>(
                $"/api/shopping/stores/nearby?latitude={latitude}&longitude={longitude}&radiusKm={radius}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<StoreDto?> GetStoreAsync(Guid storeId)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            return await _httpClient.GetFromJsonAsync<StoreDto>($"/api/shopping/stores/{storeId}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<Guid?> CreateStoreAsync(CreateStoreRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/shopping/stores", request);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<GuidResponse>();
            return result?.Id;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> UpdateStoreAsync(Guid storeId, UpdateStoreRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PutAsJsonAsync($"/api/shopping/stores/{storeId}", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> SetPreferredStoreAsync(Guid storeId)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PutAsync($"/api/shopping/stores/{storeId}/preferred", null);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<PriceComparisonDto>?> GetBestPricesAsync(Guid productId)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            return await _httpClient.GetFromJsonAsync<List<PriceComparisonDto>>($"/api/shopping/stores/products/{productId}/best-prices");
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> RecordPriceComparisonAsync(RecordPriceRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/shopping/stores/items/{request.ProductId}/prices", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<StoreLayoutDto?> GetStoreLayoutAsync(Guid storeId)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            return await _httpClient.GetFromJsonAsync<StoreLayoutDto>($"/api/shopping/stores/{storeId}/layout");
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> UpdateStoreLayoutAsync(Guid storeId, UpdateStoreLayoutRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PutAsJsonAsync($"/api/shopping/stores/{storeId}/layout", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // Templates
    public async Task<List<ShoppingListTemplateDto>?> GetTemplatesAsync()
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            return await _httpClient.GetFromJsonAsync<List<ShoppingListTemplateDto>>("/api/shopping/templates");
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<ShoppingListTemplateDto>?> GetHouseholdTemplatesAsync(Guid householdId)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            return await _httpClient.GetFromJsonAsync<List<ShoppingListTemplateDto>>($"/api/shopping/templates/household/{householdId}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<ShoppingListTemplateDto?> GetTemplateAsync(Guid templateId)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            return await _httpClient.GetFromJsonAsync<ShoppingListTemplateDto>($"/api/shopping/templates/{templateId}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<Guid?> CreateTemplateAsync(CreateTemplateRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/shopping/templates", request);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<GuidResponse>();
            return result?.Id;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> AddItemToTemplateAsync(Guid templateId, AddTemplateItemRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/shopping/templates/{templateId}/items", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<ShoppingListTemplateItemDto>?> GetTemplateItemsAsync(Guid templateId)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            return await _httpClient.GetFromJsonAsync<List<ShoppingListTemplateItemDto>>($"/api/shopping/templates/{templateId}/items");
        }
        catch
        {
            return null;
        }
    }

    public async Task<Guid?> CreateListFromTemplateAsync(Guid templateId, CreateListFromTemplateRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/shopping/templates/{templateId}/create-list", request);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<GuidResponse>();
            return result?.Id;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> DeleteTemplateAsync(Guid templateId)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.DeleteAsync($"/api/shopping/templates/{templateId}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // Shopping Scan Sessions
    public async Task<Guid?> StartShoppingScanSessionAsync(StartShoppingScanRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/shopping/scan/start", request);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<GuidResponse>();
            return result?.Id;
        }
        catch
        {
            return null;
        }
    }

    public async Task<ShoppingScanSessionDto?> GetActiveShoppingScanSessionAsync()
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            return await _httpClient.GetFromJsonAsync<ShoppingScanSessionDto>("/api/shopping/scan/active");
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> ScanPurchaseItemAsync(Guid sessionId, ScanPurchaseRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/shopping/scan/{sessionId}/purchase", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<ShoppingScanSessionResultDto?> EndShoppingScanSessionAsync(Guid sessionId)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var response = await _httpClient.PostAsync($"/api/shopping/scan/{sessionId}/end", null);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ShoppingScanSessionResultDto>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> AddPurchasedToInventoryAsync(AddPurchasedToInventoryRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/shopping/scan/add-to-inventory", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private class CreateShoppingListResponse
    {
        public Guid Id { get; set; }
    }

    private class AddItemResponse
    {
        public Guid ItemId { get; set; }
    }

    private class GuidResponse
    {
        public Guid Id { get; set; }
    }
}
