using System.Net.Http.Json;
using ExpressRecipe.Client.Shared.Models.Shopping;

namespace ExpressRecipe.Client.Shared.Services;

public interface IShoppingListApiClient
{
    // Shopping List CRUD
    Task<ShoppingListDto?> GetShoppingListAsync(Guid id);
    Task<ShoppingListSearchResult?> SearchShoppingListsAsync(ShoppingListSearchRequest request);
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

    // Advanced operations
    Task<bool> AddItemsFromRecipeAsync(AddItemsFromRecipeRequest request);
    Task<bool> AddLowStockItemsAsync(AddLowStockItemsRequest request);
    Task<bool> CompleteShoppingListAsync(Guid id);
    Task<bool> ArchiveShoppingListAsync(Guid id);
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
        var token = await _tokenProvider.GetTokenAsync();
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
            return await _httpClient.GetFromJsonAsync<ShoppingListDto>($"/api/shopping/{id}");
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
            var response = await _httpClient.PostAsJsonAsync("/api/shopping/search", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ShoppingListSearchResult>();
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
            return await _httpClient.GetFromJsonAsync<ShoppingSummaryDto>("/api/shopping/summary");
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
            var response = await _httpClient.PostAsJsonAsync("/api/shopping", request);
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
            var response = await _httpClient.PutAsJsonAsync($"/api/shopping/{id}", request);
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
            var response = await _httpClient.DeleteAsync($"/api/shopping/{id}");
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
            var response = await _httpClient.PostAsJsonAsync("/api/shopping/items", request);
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
            var response = await _httpClient.PostAsync($"/api/shopping/{id}/complete", null);
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
            var response = await _httpClient.PostAsync($"/api/shopping/{id}/archive", null);
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
}
