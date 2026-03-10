using ExpressRecipe.Client.Shared.Models.Recipe;

namespace ExpressRecipe.Client.Shared.Services;

/// <summary>
/// API client for recipe operations including CRUD and import functionality
/// </summary>
public interface IRecipeApiClient
{
    // Recipe CRUD
    Task<RecipeDto?> GetRecipeAsync(Guid id);
    Task<RecipeSearchResult?> SearchRecipesAsync(RecipeSearchRequest request);
    Task<List<RecipeDto>?> GetMyRecipesAsync(int page = 1, int pageSize = 20);
    Task<List<RecipeDto>?> GetFavoriteRecipesAsync(int page = 1, int pageSize = 20);
    Task<Guid?> CreateRecipeAsync(CreateRecipeRequest request);
    Task<bool> UpdateRecipeAsync(Guid id, UpdateRecipeRequest request);
    Task<bool> DeleteRecipeAsync(Guid id);

    // Recipe interactions
    Task<bool> FavoriteRecipeAsync(Guid recipeId);
    Task<bool> UnfavoriteRecipeAsync(Guid recipeId);
    Task<bool> IsFavoriteAsync(Guid recipeId);

    // Recipe import
    Task<ImportRecipeResponse?> ImportRecipeFromFileAsync(string fileContent, string contentType);
    Task<ImportRecipeResponse?> ImportRecipeFromPasteAsync(string pastedContent);
    Task<ImportRecipeResponse?> ImportRecipeFromUrlAsync(string url);
    Task<RecipeImportValidationResult?> ValidateRecipeImportAsync(ImportRecipeRequest request);

    // Share tokens & print
    Task<string?> GetShareTokenAsync(Guid recipeId, int expiryDays = 30);
    Task<RecipeShareTokenDto?> GetSharedRecipeAsync(string token);
    Task<bool> SetHouseholdShareAsync(Guid favoriteId, HouseholdShareRequest request);
    Task<List<RecipeDto>?> GetHouseholdSharedRecipesAsync(Guid householdId);
    Task<bool> ShareByEmailAsync(Guid recipeId, ShareRecipeEmailRequest request);

    // Recipe notes
    Task<List<RecipeNoteDto>?> GetRecipeNotesAsync(Guid recipeId);
    Task<bool> DismissRecipeNoteAsync(Guid recipeId, Guid noteId);
}

public class RecipeApiClient : ApiClientBase, IRecipeApiClient
{
    public RecipeApiClient(HttpClient httpClient, ITokenProvider tokenProvider)
        : base(httpClient, tokenProvider)
    {
    }

    // Recipe CRUD
    public async Task<RecipeDto?> GetRecipeAsync(Guid id)
    {
        return await GetAsync<RecipeDto>($"/api/recipes/{id}");
    }

    public async Task<RecipeSearchResult?> SearchRecipesAsync(RecipeSearchRequest request)
    {
        return await PostAsync<RecipeSearchRequest, RecipeSearchResult>("/api/recipes/search", request);
    }

    public async Task<List<RecipeDto>?> GetMyRecipesAsync(int page = 1, int pageSize = 20)
    {
        return await GetAsync<List<RecipeDto>>($"/api/recipes/my?page={page}&pageSize={pageSize}");
    }

    public async Task<List<RecipeDto>?> GetFavoriteRecipesAsync(int page = 1, int pageSize = 20)
    {
        return await GetAsync<List<RecipeDto>>($"/api/recipes/favorites?page={page}&pageSize={pageSize}");
    }

    public async Task<Guid?> CreateRecipeAsync(CreateRecipeRequest request)
    {
        var response = await PostAsync<CreateRecipeRequest, CreateRecipeResponse>("/api/recipes", request);
        return response?.RecipeId;
    }

    public async Task<bool> UpdateRecipeAsync(Guid id, UpdateRecipeRequest request)
    {
        return await PutAsync($"/api/recipes/{id}", request);
    }

    public async Task<bool> DeleteRecipeAsync(Guid id)
    {
        return await DeleteAsync($"/api/recipes/{id}");
    }

    // Recipe interactions
    public async Task<bool> FavoriteRecipeAsync(Guid recipeId)
    {
        return await PostAsync($"/api/recipes/{recipeId}/favorite", new { });
    }

    public async Task<bool> UnfavoriteRecipeAsync(Guid recipeId)
    {
        return await DeleteAsync($"/api/recipes/{recipeId}/favorite");
    }

    public async Task<bool> IsFavoriteAsync(Guid recipeId)
    {
        var result = await GetAsync<FavoriteStatusResponse>($"/api/recipes/{recipeId}/favorite");
        return result?.IsFavorite ?? false;
    }

    // Recipe import
    public async Task<ImportRecipeResponse?> ImportRecipeFromFileAsync(string fileContent, string contentType)
    {
        var request = new ImportRecipeRequest
        {
            Source = "File",
            Content = fileContent,
            ContentType = contentType
        };

        return await PostAsync<ImportRecipeRequest, ImportRecipeResponse>("/api/recipes/import", request);
    }

    public async Task<ImportRecipeResponse?> ImportRecipeFromPasteAsync(string pastedContent)
    {
        var request = new ImportRecipeRequest
        {
            Source = "Paste",
            Content = pastedContent,
            ContentType = "text"
        };

        return await PostAsync<ImportRecipeRequest, ImportRecipeResponse>("/api/recipes/import", request);
    }

    public async Task<ImportRecipeResponse?> ImportRecipeFromUrlAsync(string url)
    {
        var request = new ImportRecipeRequest
        {
            Source = "Url",
            SourceUrl = url,
            ContentType = "html"
        };

        return await PostAsync<ImportRecipeRequest, ImportRecipeResponse>("/api/recipes/import", request);
    }

    public async Task<RecipeImportValidationResult?> ValidateRecipeImportAsync(ImportRecipeRequest request)
    {
        return await PostAsync<ImportRecipeRequest, RecipeImportValidationResult>("/api/recipes/import/validate", request);
    }

    // Helper response classes
    private class CreateRecipeResponse
    {
        public Guid RecipeId { get; set; }
    }

    private class FavoriteStatusResponse
    {
        public bool IsFavorite { get; set; }
    }

    public async Task<string?> GetShareTokenAsync(Guid recipeId, int expiryDays = 30)
    {
        return await GetAsync<string>($"/api/recipes/{recipeId}/share-token?expiryDays={expiryDays}");
    }

    public async Task<RecipeShareTokenDto?> GetSharedRecipeAsync(string token)
    {
        return await GetAsync<RecipeShareTokenDto>($"/api/recipes/shared/{token}");
    }

    public async Task<bool> SetHouseholdShareAsync(Guid favoriteId, HouseholdShareRequest request)
    {
        return await PutAsync($"/api/recipes/favorites/{favoriteId}/household-share", request);
    }

    public async Task<List<RecipeDto>?> GetHouseholdSharedRecipesAsync(Guid householdId)
    {
        return await GetAsync<List<RecipeDto>>($"/api/recipes/household-shared?householdId={householdId}");
    }

    public async Task<bool> ShareByEmailAsync(Guid recipeId, ShareRecipeEmailRequest request)
    {
        return await PostAsync($"/api/recipes/{recipeId}/share", request);
    }

    public async Task<List<RecipeNoteDto>?> GetRecipeNotesAsync(Guid recipeId)
    {
        return await GetAsync<List<RecipeNoteDto>>($"/api/recipes/{recipeId}/notes");
    }

    public async Task<bool> DismissRecipeNoteAsync(Guid recipeId, Guid noteId)
    {
        return await PatchAsync($"/api/recipes/{recipeId}/notes/{noteId}/dismiss");
    }
}
