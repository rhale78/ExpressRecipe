using ExpressRecipe.Client.Shared.Models.Restaurant;

namespace ExpressRecipe.Client.Shared.Services;

public interface IRestaurantApiClient
{
    Task<RestaurantDto?> GetRestaurantAsync(Guid id);
    Task<RestaurantSearchResult?> SearchRestaurantsAsync(RestaurantSearchRequest request);
    Task<Guid?> CreateRestaurantAsync(CreateRestaurantRequest request);
    Task<bool> UpdateRestaurantAsync(Guid id, UpdateRestaurantRequest request);
    Task<bool> DeleteRestaurantAsync(Guid id);
    Task<bool> ApproveRestaurantAsync(Guid id, bool approve, string? rejectionReason = null);
    Task<List<UserRestaurantRatingDto>?> GetRatingsAsync(Guid id);
    Task<UserRestaurantRatingDto?> GetMyRatingAsync(Guid id);
    Task<bool> RateRestaurantAsync(Guid id, RateRestaurantRequest request);
    Task<bool> DeleteRatingAsync(Guid id);
}

public class RestaurantApiClient : ApiClientBase, IRestaurantApiClient
{
    public RestaurantApiClient(HttpClient httpClient, ITokenProvider tokenProvider)
        : base(httpClient, tokenProvider)
    {
    }

    public async Task<RestaurantDto?> GetRestaurantAsync(Guid id)
        => await GetAsync<RestaurantDto>($"/api/restaurants/{id}");

    public async Task<RestaurantSearchResult?> SearchRestaurantsAsync(RestaurantSearchRequest request)
    {
        var qs = new List<string>();
        if (!string.IsNullOrEmpty(request.SearchTerm))
            qs.Add($"searchTerm={Uri.EscapeDataString(request.SearchTerm)}");
        if (!string.IsNullOrEmpty(request.CuisineType))
            qs.Add($"cuisineType={Uri.EscapeDataString(request.CuisineType)}");
        if (!string.IsNullOrEmpty(request.City))
            qs.Add($"city={Uri.EscapeDataString(request.City)}");
        if (!string.IsNullOrEmpty(request.State))
            qs.Add($"state={Uri.EscapeDataString(request.State)}");
        if (request.Latitude.HasValue)
            qs.Add($"latitude={request.Latitude}");
        if (request.Longitude.HasValue)
            qs.Add($"longitude={request.Longitude}");
        if (request.RadiusMiles.HasValue)
            qs.Add($"radiusMiles={request.RadiusMiles}");
        if (request.OnlyApproved.HasValue)
            qs.Add($"onlyApproved={request.OnlyApproved.Value.ToString().ToLower()}");
        qs.Add($"pageNumber={request.PageNumber}");
        qs.Add($"pageSize={request.PageSize}");

        var items = await GetAsync<List<RestaurantDto>>($"/api/restaurants/search?{string.Join("&", qs)}");
        if (items == null) return null;
        return new RestaurantSearchResult { Items = items, TotalCount = items.Count, PageNumber = request.PageNumber, PageSize = request.PageSize };
    }

    public async Task<Guid?> CreateRestaurantAsync(CreateRestaurantRequest request)
    {
        var result = await PostAsync<CreateRestaurantRequest, RestaurantDto>("/api/restaurants", request);
        return result?.Id;
    }

    public async Task<bool> UpdateRestaurantAsync(Guid id, UpdateRestaurantRequest request)
        => await PutAsync(endpoint: $"/api/restaurants/{id}", data: request);

    public async Task<bool> DeleteRestaurantAsync(Guid id)
        => await DeleteAsync($"/api/restaurants/{id}");

    public async Task<bool> ApproveRestaurantAsync(Guid id, bool approve, string? rejectionReason = null)
    {
        var qs = $"approve={approve.ToString().ToLower()}";
        if (!string.IsNullOrEmpty(rejectionReason))
            qs += $"&rejectionReason={Uri.EscapeDataString(rejectionReason)}";
        return await PostAsync<object>($"/api/restaurants/{id}/approve?{qs}", new { });
    }

    public async Task<List<UserRestaurantRatingDto>?> GetRatingsAsync(Guid id)
        => await GetAsync<List<UserRestaurantRatingDto>>($"/api/restaurants/{id}/ratings");

    public async Task<UserRestaurantRatingDto?> GetMyRatingAsync(Guid id)
        => await GetAsync<UserRestaurantRatingDto>($"/api/restaurants/{id}/ratings/me");

    public async Task<bool> RateRestaurantAsync(Guid id, RateRestaurantRequest request)
        => await PostAsync($"/api/restaurants/{id}/ratings", request);

    public async Task<bool> DeleteRatingAsync(Guid id)
        => await DeleteAsync($"/api/restaurants/{id}/ratings");
}
