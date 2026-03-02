using ExpressRecipe.Client.Shared.Models.User;

namespace ExpressRecipe.Client.Shared.Services;

/// <summary>
/// API client for user profile and family member management
/// </summary>
public interface IUserProfileApiClient
{
    // User profile
    Task<UserProfileDto?> GetProfileAsync();
    Task<bool> UpdateProfileAsync(UpdateUserProfileRequest request);

    // Family members
    Task<List<FamilyMemberDto>?> GetFamilyMembersAsync();
    Task<FamilyMemberDto?> GetFamilyMemberAsync(Guid id);
    Task<Guid?> CreateFamilyMemberAsync(CreateFamilyMemberRequest request);
    Task<Guid?> CreateFamilyMemberWithAccountAsync(CreateFamilyMemberWithAccountRequest request);
    Task<bool> UpdateFamilyMemberAsync(Guid id, UpdateFamilyMemberRequest request);
    Task<bool> DeleteFamilyMemberAsync(Guid id);
    Task<bool> DismissGuestAsync(Guid id, string? reason = null);

    // Family relationships
    Task<List<FamilyRelationshipDto>?> GetFamilyRelationshipsAsync(Guid familyMemberId);
    Task<Guid?> CreateFamilyRelationshipAsync(Guid familyMemberId, CreateFamilyRelationshipRequest request);
    Task<bool> DeleteFamilyRelationshipAsync(Guid relationshipId);

    // Favorites - Recipes
    Task<List<UserFavoriteRecipeDto>?> GetFavoriteRecipesAsync();
    Task<bool> AddFavoriteRecipeAsync(Guid recipeId, string? notes = null);
    Task<bool> RemoveFavoriteRecipeAsync(Guid recipeId);

    // Favorites - Products
    Task<List<UserFavoriteProductDto>?> GetFavoriteProductsAsync();
    Task<bool> AddFavoriteProductAsync(Guid productId, string? notes = null);
    Task<bool> RemoveFavoriteProductAsync(Guid productId);

    // Product Ratings
    Task<List<UserProductRatingDto>?> GetMyProductRatingsAsync();
    Task<UserProductRatingDto?> GetProductRatingAsync(Guid productId);
    Task<bool> RateProductAsync(Guid productId, int rating, string? reviewText = null);
    Task<bool> DeleteProductRatingAsync(Guid productId);
    Task<ProductRatingStatsDto?> GetProductRatingStatsAsync(Guid productId);

    // Allergens and restrictions
    Task<AllergensAndRestrictionsDto?> GetAllergensAndRestrictionsAsync();
}

public class UserProfileApiClient : ApiClientBase, IUserProfileApiClient
{
    public UserProfileApiClient(HttpClient httpClient, ITokenProvider tokenProvider)
        : base(httpClient, tokenProvider)
    {
    }

    // User profile
    public async Task<UserProfileDto?> GetProfileAsync()
    {
        return await GetAsync<UserProfileDto>("/api/userprofile/me");
    }

    public async Task<bool> UpdateProfileAsync(UpdateUserProfileRequest request)
    {
        return await PutAsync("/api/userprofile/me", request);
    }

    // Family members
    public async Task<List<FamilyMemberDto>?> GetFamilyMembersAsync()
    {
        return await GetAsync<List<FamilyMemberDto>>("/api/familymembers");
    }

    public async Task<FamilyMemberDto?> GetFamilyMemberAsync(Guid id)
    {
        return await GetAsync<FamilyMemberDto>($"/api/familymembers/{id}");
    }

    public async Task<Guid?> CreateFamilyMemberAsync(CreateFamilyMemberRequest request)
    {
        var response = await PostAsync<CreateFamilyMemberRequest, FamilyMemberDto>("/api/familymembers", request);
        return response?.Id;
    }

    public async Task<Guid?> CreateFamilyMemberWithAccountAsync(CreateFamilyMemberWithAccountRequest request)
    {
        var response = await PostAsync<CreateFamilyMemberWithAccountRequest, FamilyMemberDto>("/api/familymembers/create-with-account", request);
        return response?.Id;
    }

    public async Task<bool> UpdateFamilyMemberAsync(Guid id, UpdateFamilyMemberRequest request)
    {
        return await PutAsync($"/api/familymembers/{id}", request);
    }

    public async Task<bool> DeleteFamilyMemberAsync(Guid id)
    {
        return await DeleteAsync($"/api/familymembers/{id}");
    }

    public async Task<bool> DismissGuestAsync(Guid id, string? reason = null)
    {
        var request = new { reason };
        return await PostAsync($"/api/familymembers/{id}/dismiss", request);
    }

    // Family relationships
    public async Task<List<FamilyRelationshipDto>?> GetFamilyRelationshipsAsync(Guid familyMemberId)
    {
        return await GetAsync<List<FamilyRelationshipDto>>($"/api/familymembers/{familyMemberId}/relationships");
    }

    public async Task<Guid?> CreateFamilyRelationshipAsync(Guid familyMemberId, CreateFamilyRelationshipRequest request)
    {
        var response = await PostAsync<CreateFamilyRelationshipRequest, FamilyRelationshipDto>($"/api/familymembers/{familyMemberId}/relationships", request);
        return response?.Id;
    }

    public async Task<bool> DeleteFamilyRelationshipAsync(Guid relationshipId)
    {
        return await DeleteAsync($"/api/familymembers/relationships/{relationshipId}");
    }

    // Favorites - Recipes
    public async Task<List<UserFavoriteRecipeDto>?> GetFavoriteRecipesAsync()
    {
        return await GetAsync<List<UserFavoriteRecipeDto>>("/api/userfavorites/recipes");
    }

    public async Task<bool> AddFavoriteRecipeAsync(Guid recipeId, string? notes = null)
    {
        var request = new { notes };
        return await PostAsync($"/api/userfavorites/recipes/{recipeId}", request);
    }

    public async Task<bool> RemoveFavoriteRecipeAsync(Guid recipeId)
    {
        return await DeleteAsync($"/api/userfavorites/recipes/{recipeId}");
    }

    // Favorites - Products
    public async Task<List<UserFavoriteProductDto>?> GetFavoriteProductsAsync()
    {
        return await GetAsync<List<UserFavoriteProductDto>>("/api/userfavorites/products");
    }

    public async Task<bool> AddFavoriteProductAsync(Guid productId, string? notes = null)
    {
        var request = new { notes };
        return await PostAsync($"/api/userfavorites/products/{productId}", request);
    }

    public async Task<bool> RemoveFavoriteProductAsync(Guid productId)
    {
        return await DeleteAsync($"/api/userfavorites/products/{productId}");
    }

    // Product Ratings
    public async Task<List<UserProductRatingDto>?> GetMyProductRatingsAsync()
    {
        return await GetAsync<List<UserProductRatingDto>>("/api/userfavorites/ratings");
    }

    public async Task<UserProductRatingDto?> GetProductRatingAsync(Guid productId)
    {
        return await GetAsync<UserProductRatingDto>($"/api/userfavorites/ratings/products/{productId}");
    }

    public async Task<bool> RateProductAsync(Guid productId, int rating, string? reviewText = null)
    {
        var request = new { productId, rating, reviewText };
        return await PostAsync($"/api/userfavorites/ratings/products/{productId}", request);
    }

    public async Task<bool> DeleteProductRatingAsync(Guid productId)
    {
        return await DeleteAsync($"/api/userfavorites/ratings/products/{productId}");
    }

    public async Task<ProductRatingStatsDto?> GetProductRatingStatsAsync(Guid productId)
    {
        return await GetAsync<ProductRatingStatsDto>($"/api/userfavorites/ratings/products/{productId}/stats");
    }

    // Allergens and restrictions
    public async Task<AllergensAndRestrictionsDto?> GetAllergensAndRestrictionsAsync()
    {
        return await GetAsync<AllergensAndRestrictionsDto>("/api/allergymgmt");
    }
}
