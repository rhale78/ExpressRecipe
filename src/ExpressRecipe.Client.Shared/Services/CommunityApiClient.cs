using System.Net.Http.Json;
using ExpressRecipe.Client.Shared.Models.Community;

namespace ExpressRecipe.Client.Shared.Services;

public interface ICommunityApiClient
{
    // Ratings
    Task<RecipeRatingDto?> GetUserRatingAsync(Guid recipeId);
    Task<bool> CreateRatingAsync(CreateRecipeRatingRequest request);
    Task<bool> UpdateRatingAsync(UpdateRecipeRatingRequest request);
    Task<bool> DeleteRatingAsync(Guid ratingId);

    // Reviews
    Task<RecipeReviewDto?> GetReviewAsync(Guid reviewId);
    Task<RecipeReviewSearchResult?> SearchReviewsAsync(RecipeReviewSearchRequest request);
    Task<RecipeRatingSummaryDto?> GetRecipeRatingSummaryAsync(Guid recipeId);
    Task<Guid> CreateReviewAsync(CreateRecipeReviewRequest request);
    Task<bool> UpdateReviewAsync(UpdateRecipeReviewRequest request);
    Task<bool> DeleteReviewAsync(Guid reviewId);
    Task<bool> MarkReviewHelpfulAsync(MarkReviewHelpfulRequest request);

    // Favorites
    Task<List<RecipeFavoriteDto>> GetUserFavoritesAsync();
    Task<bool> IsFavoriteAsync(Guid recipeId);
    Task<bool> AddToFavoritesAsync(CreateRecipeFavoriteRequest request);
    Task<bool> RemoveFromFavoritesAsync(Guid recipeId);

    // Sharing
    Task<SharedRecipeDto?> ShareRecipeAsync(ShareRecipeRequest request);
    Task<SharedRecipeDto?> GetSharedRecipeAsync(string shareToken);
    Task<Guid?> CopySharedRecipeAsync(CopySharedRecipeRequest request);
    Task<List<SharedRecipeDto>> GetUserSharedRecipesAsync();

    // Comments
    Task<List<RecipeCommentDto>> GetRecipeCommentsAsync(Guid recipeId);
    Task<Guid> CreateCommentAsync(CreateRecipeCommentRequest request);
    Task<bool> DeleteCommentAsync(Guid commentId);
    Task<bool> LikeCommentAsync(LikeCommentRequest request);

    // Trending/Popular
    Task<List<TrendingRecipeDto>> GetPopularRecipesAsync(PopularRecipesRequest request);
    Task<List<TrendingRecipeDto>> GetTrendingRecipesAsync();

    // User Stats
    Task<UserRecipeStatsDto?> GetUserStatsAsync();
    Task<List<UserBadgeDto>> GetUserBadgesAsync();

    // Reporting
    Task<bool> ReportRecipeAsync(ReportRecipeRequest request);
}

public class CommunityApiClient : ICommunityApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ITokenProvider _tokenProvider;

    public CommunityApiClient(HttpClient httpClient, ITokenProvider tokenProvider)
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

    // Ratings
    public async Task<RecipeRatingDto?> GetUserRatingAsync(Guid recipeId)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            return await _httpClient.GetFromJsonAsync<RecipeRatingDto>($"/api/community/ratings/recipe/{recipeId}/user");
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> CreateRatingAsync(CreateRecipeRatingRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/community/ratings", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> UpdateRatingAsync(UpdateRecipeRatingRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PutAsJsonAsync($"/api/community/ratings/{request.RatingId}", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DeleteRatingAsync(Guid ratingId)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.DeleteAsync($"/api/community/ratings/{ratingId}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // Reviews
    public async Task<RecipeReviewDto?> GetReviewAsync(Guid reviewId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<RecipeReviewDto>($"/api/community/reviews/{reviewId}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<RecipeReviewSearchResult?> SearchReviewsAsync(RecipeReviewSearchRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/community/reviews/search", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<RecipeReviewSearchResult>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<RecipeRatingSummaryDto?> GetRecipeRatingSummaryAsync(Guid recipeId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<RecipeRatingSummaryDto>($"/api/community/ratings/recipe/{recipeId}/summary");
        }
        catch
        {
            return null;
        }
    }

    public async Task<Guid> CreateReviewAsync(CreateRecipeReviewRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return Guid.Empty;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/community/reviews", request);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<Guid>();
            return result;
        }
        catch
        {
            return Guid.Empty;
        }
    }

    public async Task<bool> UpdateReviewAsync(UpdateRecipeReviewRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PutAsJsonAsync($"/api/community/reviews/{request.ReviewId}", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DeleteReviewAsync(Guid reviewId)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.DeleteAsync($"/api/community/reviews/{reviewId}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> MarkReviewHelpfulAsync(MarkReviewHelpfulRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/community/reviews/helpful", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // Favorites
    public async Task<List<RecipeFavoriteDto>> GetUserFavoritesAsync()
    {
        if (!await EnsureAuthenticatedAsync())
            return new List<RecipeFavoriteDto>();

        try
        {
            var favorites = await _httpClient.GetFromJsonAsync<List<RecipeFavoriteDto>>("/api/community/favorites");
            return favorites ?? new List<RecipeFavoriteDto>();
        }
        catch
        {
            return new List<RecipeFavoriteDto>();
        }
    }

    public async Task<bool> IsFavoriteAsync(Guid recipeId)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var result = await _httpClient.GetFromJsonAsync<bool>($"/api/community/favorites/recipe/{recipeId}/check");
            return result;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> AddToFavoritesAsync(CreateRecipeFavoriteRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/community/favorites", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> RemoveFromFavoritesAsync(Guid recipeId)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.DeleteAsync($"/api/community/favorites/recipe/{recipeId}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // Sharing
    public async Task<SharedRecipeDto?> ShareRecipeAsync(ShareRecipeRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/community/sharing/share", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<SharedRecipeDto>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<SharedRecipeDto?> GetSharedRecipeAsync(string shareToken)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<SharedRecipeDto>($"/api/community/sharing/{shareToken}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<Guid?> CopySharedRecipeAsync(CopySharedRecipeRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/community/sharing/copy", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<Guid>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<SharedRecipeDto>> GetUserSharedRecipesAsync()
    {
        if (!await EnsureAuthenticatedAsync())
            return new List<SharedRecipeDto>();

        try
        {
            var shared = await _httpClient.GetFromJsonAsync<List<SharedRecipeDto>>("/api/community/sharing/user");
            return shared ?? new List<SharedRecipeDto>();
        }
        catch
        {
            return new List<SharedRecipeDto>();
        }
    }

    // Comments
    public async Task<List<RecipeCommentDto>> GetRecipeCommentsAsync(Guid recipeId)
    {
        try
        {
            var comments = await _httpClient.GetFromJsonAsync<List<RecipeCommentDto>>($"/api/community/comments/recipe/{recipeId}");
            return comments ?? new List<RecipeCommentDto>();
        }
        catch
        {
            return new List<RecipeCommentDto>();
        }
    }

    public async Task<Guid> CreateCommentAsync(CreateRecipeCommentRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return Guid.Empty;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/community/comments", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<Guid>();
        }
        catch
        {
            return Guid.Empty;
        }
    }

    public async Task<bool> DeleteCommentAsync(Guid commentId)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.DeleteAsync($"/api/community/comments/{commentId}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> LikeCommentAsync(LikeCommentRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/community/comments/like", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // Trending/Popular
    public async Task<List<TrendingRecipeDto>> GetPopularRecipesAsync(PopularRecipesRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/community/recipes/popular", request);
            response.EnsureSuccessStatusCode();
            var recipes = await response.Content.ReadFromJsonAsync<List<TrendingRecipeDto>>();
            return recipes ?? new List<TrendingRecipeDto>();
        }
        catch
        {
            return new List<TrendingRecipeDto>();
        }
    }

    public async Task<List<TrendingRecipeDto>> GetTrendingRecipesAsync()
    {
        try
        {
            var recipes = await _httpClient.GetFromJsonAsync<List<TrendingRecipeDto>>("/api/community/recipes/trending");
            return recipes ?? new List<TrendingRecipeDto>();
        }
        catch
        {
            return new List<TrendingRecipeDto>();
        }
    }

    // User Stats
    public async Task<UserRecipeStatsDto?> GetUserStatsAsync()
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            return await _httpClient.GetFromJsonAsync<UserRecipeStatsDto>("/api/community/users/stats");
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<UserBadgeDto>> GetUserBadgesAsync()
    {
        if (!await EnsureAuthenticatedAsync())
            return new List<UserBadgeDto>();

        try
        {
            var badges = await _httpClient.GetFromJsonAsync<List<UserBadgeDto>>("/api/community/users/badges");
            return badges ?? new List<UserBadgeDto>();
        }
        catch
        {
            return new List<UserBadgeDto>();
        }
    }

    // Reporting
    public async Task<bool> ReportRecipeAsync(ReportRecipeRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/community/reports", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
