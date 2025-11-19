using System.Net.Http.Json;
using ExpressRecipe.Client.Shared.Models.AI;

namespace ExpressRecipe.Client.Shared.Services;

public interface IAIApiClient
{
    // Recipe Suggestions
    Task<List<RecipeSuggestionDto>> GetRecipeSuggestionsAsync(RecipeSuggestionRequest request);

    // Ingredient Substitutions
    Task<IngredientSubstitutionDto?> GetIngredientSubstitutionsAsync(IngredientSubstitutionRequest request);

    // Recipe Extraction
    Task<ExtractedRecipeDto?> ExtractRecipeAsync(RecipeExtractionRequest request);

    // Meal Planning
    Task<MealPlanSuggestionDto?> GetMealPlanSuggestionsAsync(MealPlanSuggestionRequest request);

    // Allergen Detection
    Task<AllergenDetectionResult?> DetectAllergensAsync(AllergenDetectionRequest request);

    // Shopping Optimization
    Task<ShoppingOptimizationResult?> OptimizeShoppingListAsync(ShoppingOptimizationRequest request);

    // Dietary Analysis
    Task<DietaryAnalysisResult?> AnalyzeDietAsync(DietaryAnalysisRequest request);

    // AI Chat Assistant
    Task<AIChatResponse?> ChatAsync(AIChatRequest request);
}

public class AIApiClient : IAIApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ITokenProvider _tokenProvider;

    public AIApiClient(HttpClient httpClient, ITokenProvider tokenProvider)
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

    public async Task<List<RecipeSuggestionDto>> GetRecipeSuggestionsAsync(RecipeSuggestionRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return new List<RecipeSuggestionDto>();

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/ai/recipes/suggest", request);
            response.EnsureSuccessStatusCode();
            var suggestions = await response.Content.ReadFromJsonAsync<List<RecipeSuggestionDto>>();
            return suggestions ?? new List<RecipeSuggestionDto>();
        }
        catch
        {
            return new List<RecipeSuggestionDto>();
        }
    }

    public async Task<IngredientSubstitutionDto?> GetIngredientSubstitutionsAsync(IngredientSubstitutionRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/ai/ingredients/substitute", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<IngredientSubstitutionDto>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<ExtractedRecipeDto?> ExtractRecipeAsync(RecipeExtractionRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/ai/recipes/extract", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ExtractedRecipeDto>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<MealPlanSuggestionDto?> GetMealPlanSuggestionsAsync(MealPlanSuggestionRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/ai/meal-plans/suggest", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<MealPlanSuggestionDto>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<AllergenDetectionResult?> DetectAllergensAsync(AllergenDetectionRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/ai/allergens/detect", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<AllergenDetectionResult>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<ShoppingOptimizationResult?> OptimizeShoppingListAsync(ShoppingOptimizationRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/ai/shopping/optimize", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ShoppingOptimizationResult>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<DietaryAnalysisResult?> AnalyzeDietAsync(DietaryAnalysisRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/ai/dietary/analyze", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<DietaryAnalysisResult>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<AIChatResponse?> ChatAsync(AIChatRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/ai/chat", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<AIChatResponse>();
        }
        catch
        {
            return null;
        }
    }
}
