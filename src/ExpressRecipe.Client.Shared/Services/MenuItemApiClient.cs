using ExpressRecipe.Client.Shared.Models.MenuItem;

namespace ExpressRecipe.Client.Shared.Services;

public interface IMenuItemApiClient
{
    Task<MenuItemDto?> GetMenuItemAsync(Guid id);
    Task<MenuItemSearchResult?> SearchMenuItemsAsync(MenuItemSearchRequest request);
    Task<Guid?> CreateMenuItemAsync(CreateMenuItemRequest request);
    Task<bool> UpdateMenuItemAsync(Guid id, UpdateMenuItemRequest request);
    Task<bool> DeleteMenuItemAsync(Guid id);
    Task<List<MenuItemIngredientDto>?> GetIngredientsAsync(Guid id);
    Task<MenuItemNutritionDto?> GetNutritionAsync(Guid id);
}

public class MenuItemApiClient : ApiClientBase, IMenuItemApiClient
{
    public MenuItemApiClient(HttpClient httpClient, ITokenProvider tokenProvider)
        : base(httpClient, tokenProvider)
    {
    }

    public async Task<MenuItemDto?> GetMenuItemAsync(Guid id)
        => await GetAsync<MenuItemDto>($"/api/menuitems/{id}");

    public async Task<MenuItemSearchResult?> SearchMenuItemsAsync(MenuItemSearchRequest request)
    {
        var qs = new List<string>();
        if (request.RestaurantId.HasValue)
            qs.Add($"restaurantId={request.RestaurantId.Value}");
        if (!string.IsNullOrEmpty(request.SearchTerm))
            qs.Add($"searchTerm={Uri.EscapeDataString(request.SearchTerm)}");
        if (!string.IsNullOrEmpty(request.Category))
            qs.Add($"category={Uri.EscapeDataString(request.Category)}");
        if (request.MaxPrice.HasValue)
            qs.Add($"maxPrice={request.MaxPrice.Value}");
        if (request.OnlyAvailable.HasValue)
            qs.Add($"onlyAvailable={request.OnlyAvailable.Value.ToString().ToLower()}");
        if (request.OnlyApproved.HasValue)
            qs.Add($"onlyApproved={request.OnlyApproved.Value.ToString().ToLower()}");
        qs.Add($"pageNumber={request.PageNumber}");
        qs.Add($"pageSize={request.PageSize}");

        var items = await GetAsync<List<MenuItemDto>>($"/api/menuitems/search?{string.Join("&", qs)}");
        if (items == null) return null;
        return new MenuItemSearchResult { Items = items, TotalCount = items.Count, PageNumber = request.PageNumber, PageSize = request.PageSize };
    }

    public async Task<Guid?> CreateMenuItemAsync(CreateMenuItemRequest request)
    {
        var result = await PostAsync<CreateMenuItemRequest, MenuItemDto>("/api/menuitems", request);
        return result?.Id;
    }

    public async Task<bool> UpdateMenuItemAsync(Guid id, UpdateMenuItemRequest request)
        => await PutAsync(endpoint: $"/api/menuitems/{id}", data: request);

    public async Task<bool> DeleteMenuItemAsync(Guid id)
        => await DeleteAsync($"/api/menuitems/{id}");

    public async Task<List<MenuItemIngredientDto>?> GetIngredientsAsync(Guid id)
        => await GetAsync<List<MenuItemIngredientDto>>($"/api/menuitems/{id}/ingredients");

    public async Task<MenuItemNutritionDto?> GetNutritionAsync(Guid id)
        => await GetAsync<MenuItemNutritionDto>($"/api/menuitems/{id}/nutrition");
}
