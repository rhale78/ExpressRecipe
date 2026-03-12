using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace ExpressRecipe.RecipeService.Data;

/// <summary>
/// HTTP-backed implementation of IAllergenRepository.
/// Delegates allergen lookups to the UserService, which owns the Allergen master table.
/// Falls back to an empty result (triggering keyword-based detection in AllergenDetectionService)
/// when UserService is unavailable.
/// </summary>
public sealed class HttpAllergenRepository : IAllergenRepository
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HttpAllergenRepository> _logger;

    public HttpAllergenRepository(IHttpClientFactory httpClientFactory, ILogger<HttpAllergenRepository> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<(Guid AllergenId, string AllergenName)>> FindAllergensByIngredientNameAsync(string ingredientName)
    {
        if (string.IsNullOrWhiteSpace(ingredientName))
            return [];

        try
        {
            var client = _httpClientFactory.CreateClient("UserService");
            var response = await client.GetAsync(
                $"/api/allergens/search?q={Uri.EscapeDataString(ingredientName)}");

            if (!response.IsSuccessStatusCode)
                return [];

            var allergens = await response.Content.ReadFromJsonAsync<List<AllergenSearchResult>>();
            if (allergens is null)
                return [];

            return allergens
                .Where(a => a.Id != Guid.Empty && !string.IsNullOrWhiteSpace(a.Name))
                .Select(a => (a.Id, a.Name!))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "UserService allergen lookup unavailable for ingredient '{Ingredient}'; keyword fallback will apply",
                ingredientName);
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<List<(Guid Id, string Name)>> GetAllKnownAllergensAsync()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("UserService");
            var response = await client.GetAsync("/api/allergens");

            if (!response.IsSuccessStatusCode)
                return [];

            var allergens = await response.Content.ReadFromJsonAsync<List<AllergenSearchResult>>();
            if (allergens is null)
                return [];

            return allergens
                .Where(a => a.Id != Guid.Empty && !string.IsNullOrWhiteSpace(a.Name))
                .Select(a => (a.Id, a.Name!))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "UserService unavailable for allergen list; keyword fallback will apply");
            return [];
        }
    }

    // Minimal DTO matching the shape returned by GET /api/allergens and GET /api/allergens/search
    private sealed class AllergenSearchResult
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}
