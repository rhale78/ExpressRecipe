using ExpressRecipe.Client.Shared.Models.Cookbook;

namespace ExpressRecipe.Client.Shared.Services;

/// <summary>
/// API client for cookbook operations including CRUD, sections, recipes, and social features
/// </summary>
public interface ICookbookApiClient
{
    // Cookbook CRUD
    Task<CookbookDto?> GetCookbookAsync(Guid id);
    Task<CookbookDto?> GetCookbookBySlugAsync(string slug);
    Task<CookbookSearchResult?> SearchCookbooksAsync(CookbookSearchRequest request);
    Task<List<CookbookSummaryDto>?> GetMyCookbooksAsync(int page = 1, int pageSize = 20);
    Task<List<CookbookSummaryDto>?> GetFavoriteCookbooksAsync(int page = 1, int pageSize = 20);
    // Note: use SearchCookbooksAsync for public search (GET /api/cookbooks with visibility filter)
    Task<Guid?> CreateCookbookAsync(CreateCookbookRequest request);
    Task<bool> UpdateCookbookAsync(Guid id, UpdateCookbookRequest request);
    Task<bool> DeleteCookbookAsync(Guid id);

    // Favorites
    Task<bool> FavoriteCookbookAsync(Guid id);
    Task<bool> UnfavoriteCookbookAsync(Guid id);

    // Ratings
    Task<bool> RateCookbookAsync(Guid id, int rating);

    // Comments
    Task<List<CookbookCommentDto>?> GetCommentsAsync(Guid id, int page = 1, int pageSize = 20);
    Task<Guid?> AddCommentAsync(Guid id, string content);
    Task<bool> DeleteCommentAsync(Guid cookbookId, Guid commentId);

    // Sections
    Task<Guid?> CreateSectionAsync(Guid cookbookId, CreateCookbookSectionRequest request);
    Task<bool> UpdateSectionAsync(Guid cookbookId, Guid sectionId, UpdateCookbookSectionRequest request);
    Task<bool> DeleteSectionAsync(Guid cookbookId, Guid sectionId);
    Task<bool> ReorderSectionsAsync(Guid cookbookId, List<Guid> sectionIds);

    // Recipes in cookbook
    Task<Guid?> AddRecipeToCookbookAsync(Guid cookbookId, Guid sectionId, AddCookbookRecipeRequest request);
    Task<bool> AddRecipesBatchAsync(Guid cookbookId, Guid sectionId, List<AddCookbookRecipeRequest> recipes);
    Task<bool> RemoveRecipeFromCookbookAsync(Guid cookbookId, Guid cookbookRecipeId);
    Task<bool> MoveRecipeAsync(Guid cookbookId, Guid cookbookRecipeId, Guid? newSectionId);
    Task<bool> ReorderRecipesAsync(Guid cookbookId, Guid sectionId, List<Guid> recipeIds);

    // Advanced operations
    Task<Guid?> MergeCookbooksAsync(MergeCookbooksRequest request);
    Task<List<Guid>?> SplitCookbookAsync(Guid id, SplitCookbookRequest request);
    Task<List<Guid>?> ExtractRecipesAsync(Guid id);

    // Sharing
    Task<bool> ShareCookbookAsync(Guid id, Guid targetUserId, bool canEdit);
    Task<bool> RevokeShareAsync(Guid id, Guid targetUserId);

    // Export / Web view
    Task<string?> GetPrintPreviewAsync(Guid id);
    Task<byte[]?> ExportPdfAsync(Guid id);
    Task<byte[]?> ExportWordAsync(Guid id);
}

public class CookbookApiClient : ApiClientBase, ICookbookApiClient
{
    public CookbookApiClient(HttpClient httpClient, ITokenProvider tokenProvider)
        : base(httpClient, tokenProvider)
    {
    }

    // Cookbook CRUD
    public async Task<CookbookDto?> GetCookbookAsync(Guid id)
        => await GetAsync<CookbookDto>($"/api/cookbooks/{id}");

    public async Task<CookbookDto?> GetCookbookBySlugAsync(string slug)
        => await GetAsync<CookbookDto>($"/api/cookbooks/slug/{slug}");

    public async Task<CookbookSearchResult?> SearchCookbooksAsync(CookbookSearchRequest request)
    {
        var query = BuildSearchQuery(request.SearchTerm, request.Visibility, request.Page, request.PageSize);
        var result = await GetAsync<CookbookSearchResult>($"/api/cookbooks{query}");
        return result;
    }

    public async Task<List<CookbookSummaryDto>?> GetMyCookbooksAsync(int page = 1, int pageSize = 20)
        => (await GetAsync<CookbookSearchResult>($"/api/cookbooks?page={page}&pageSize={pageSize}"))?.Items;

    public async Task<List<CookbookSummaryDto>?> GetFavoriteCookbooksAsync(int page = 1, int pageSize = 20)
        => await GetAsync<List<CookbookSummaryDto>>($"/api/cookbooks/favorites?page={page}&pageSize={pageSize}");

    public async Task<Guid?> CreateCookbookAsync(CreateCookbookRequest request)
    {
        var response = await PostAsync<CreateCookbookRequest, CreateCookbookResponse>("/api/cookbooks", request);
        return response?.Id;
    }

    public async Task<bool> UpdateCookbookAsync(Guid id, UpdateCookbookRequest request)
        => await PutAsync($"/api/cookbooks/{id}", request);

    public async Task<bool> DeleteCookbookAsync(Guid id)
        => await DeleteAsync($"/api/cookbooks/{id}");

    // Favorites
    public async Task<bool> FavoriteCookbookAsync(Guid id)
        => await PostAsync($"/api/cookbooks/{id}/favorite", new { });

    public async Task<bool> UnfavoriteCookbookAsync(Guid id)
        => await DeleteAsync($"/api/cookbooks/{id}/favorite");

    // Ratings
    public async Task<bool> RateCookbookAsync(Guid id, int rating)
        => await PostAsync($"/api/cookbooks/{id}/rate", new RateCookbookRequest { Rating = rating });

    // Comments
    public async Task<List<CookbookCommentDto>?> GetCommentsAsync(Guid id, int page = 1, int pageSize = 20)
        => await GetAsync<List<CookbookCommentDto>>($"/api/cookbooks/{id}/comments?page={page}&pageSize={pageSize}");

    public async Task<Guid?> AddCommentAsync(Guid id, string content)
    {
        var response = await PostAsync<AddCommentRequest, CreateCommentResponse>(
            $"/api/cookbooks/{id}/comments",
            new AddCommentRequest { Content = content });
        return response?.Id;
    }

    public async Task<bool> DeleteCommentAsync(Guid cookbookId, Guid commentId)
        => await DeleteAsync($"/api/cookbooks/{cookbookId}/comments/{commentId}");

    // Sections
    public async Task<Guid?> CreateSectionAsync(Guid cookbookId, CreateCookbookSectionRequest request)
    {
        var response = await PostAsync<CreateCookbookSectionRequest, CreateSectionResponse>(
            $"/api/cookbooks/{cookbookId}/sections", request);
        return response?.Id;
    }

    public async Task<bool> UpdateSectionAsync(Guid cookbookId, Guid sectionId, UpdateCookbookSectionRequest request)
        => await PutAsync($"/api/cookbooks/{cookbookId}/sections/{sectionId}", request);

    public async Task<bool> DeleteSectionAsync(Guid cookbookId, Guid sectionId)
        => await DeleteAsync($"/api/cookbooks/{cookbookId}/sections/{sectionId}");

    public async Task<bool> ReorderSectionsAsync(Guid cookbookId, List<Guid> sectionIds)
        => await PutAsync($"/api/cookbooks/{cookbookId}/sections/reorder", new ReorderRequest { Ids = sectionIds });

    // Recipes in cookbook
    public async Task<Guid?> AddRecipeToCookbookAsync(Guid cookbookId, Guid sectionId, AddCookbookRecipeRequest request)
    {
        var response = await PostAsync<AddCookbookRecipeRequest, CreateCookbookRecipeResponse>(
            $"/api/cookbooks/{cookbookId}/sections/{sectionId}/recipes", request);
        return response?.Id;
    }

    public async Task<bool> AddRecipesBatchAsync(Guid cookbookId, Guid sectionId, List<AddCookbookRecipeRequest> recipes)
        => await PostAsync($"/api/cookbooks/{cookbookId}/sections/{sectionId}/recipes/batch", recipes);

    public async Task<bool> RemoveRecipeFromCookbookAsync(Guid cookbookId, Guid cookbookRecipeId)
        => await DeleteAsync($"/api/cookbooks/{cookbookId}/recipes/{cookbookRecipeId}");

    public async Task<bool> MoveRecipeAsync(Guid cookbookId, Guid cookbookRecipeId, Guid? newSectionId)
        => await PutAsync($"/api/cookbooks/{cookbookId}/recipes/{cookbookRecipeId}/move",
            new MoveRecipeRequest { NewSectionId = newSectionId });

    public async Task<bool> ReorderRecipesAsync(Guid cookbookId, Guid sectionId, List<Guid> recipeIds)
        => await PutAsync($"/api/cookbooks/{cookbookId}/sections/{sectionId}/recipes/reorder",
            new ReorderRequest { Ids = recipeIds });

    // Advanced operations
    public async Task<Guid?> MergeCookbooksAsync(MergeCookbooksRequest request)
    {
        var response = await PostAsync<MergeCookbooksRequest, CreateCookbookResponse>("/api/cookbooks/merge", request);
        return response?.Id;
    }

    public async Task<List<Guid>?> SplitCookbookAsync(Guid id, SplitCookbookRequest request)
        => await PostAsync<SplitCookbookRequest, List<Guid>>($"/api/cookbooks/{id}/split", request);

    public async Task<List<Guid>?> ExtractRecipesAsync(Guid id)
        => await PostAsync<object, List<Guid>>($"/api/cookbooks/{id}/extract-recipes", new { });

    // Sharing
    public async Task<bool> ShareCookbookAsync(Guid id, Guid targetUserId, bool canEdit)
        => await PostAsync($"/api/cookbooks/{id}/share",
            new ShareCookbookRequest { TargetUserId = targetUserId, CanEdit = canEdit });

    public async Task<bool> RevokeShareAsync(Guid id, Guid targetUserId)
        => await DeleteAsync($"/api/cookbooks/{id}/share/{targetUserId}");

    // Export / Web view
    public async Task<string?> GetPrintPreviewAsync(Guid id)
        => await GetAsync<string>($"/api/cookbooks/{id}/print-preview");

    // TODO: implement when PDF library is added
    public Task<byte[]?> ExportPdfAsync(Guid id) => Task.FromResult<byte[]?>(null);

    // TODO: implement when Word export library is added
    public Task<byte[]?> ExportWordAsync(Guid id) => Task.FromResult<byte[]?>(null);

    // Helper response records
    private record CreateCookbookResponse(Guid Id);
    private record CreateCommentResponse(Guid Id);
    private record CreateSectionResponse(Guid Id);
    private record CreateCookbookRecipeResponse(Guid Id);

    private static string BuildSearchQuery(string? searchTerm, string? visibility, int page, int pageSize)
    {
        var parts = new List<string>
        {
            $"page={page}",
            $"pageSize={pageSize}"
        };
        if (!string.IsNullOrEmpty(searchTerm))
            parts.Add($"searchTerm={Uri.EscapeDataString(searchTerm)}");
        if (!string.IsNullOrEmpty(visibility))
            parts.Add($"visibility={Uri.EscapeDataString(visibility)}");
        return "?" + string.Join("&", parts);
    }
}
