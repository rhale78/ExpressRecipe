using ExpressRecipe.RecipeService.Data;
using ExpressRecipe.RecipeService.Parsers;
using ExpressRecipe.Shared.DTOs.Recipe;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("ExpressRecipe.RecipeService.Tests")]

namespace ExpressRecipe.RecipeService.Services;

/// <summary>
/// Service for detecting allergens in recipe ingredients.
/// Uses an injected <see cref="IAllergenRepository"/> for database lookups,
/// falling back to keyword matching when no database results are found.
/// </summary>
public class AllergenDetectionService
{
    private readonly IAllergenRepository _allergenRepository;
    private readonly ILogger<AllergenDetectionService> _logger;

    // Common allergen keywords for fallback detection
    internal static readonly Dictionary<string, List<string>> AllergenKeywords = new()
    {
        ["Milk"] = new() { "milk", "cream", "butter", "cheese", "yogurt", "whey", "casein", "lactose" },
        ["Eggs"] = new() { "egg", "eggs", "mayonnaise", "mayo" },
        ["Fish"] = new() { "fish", "salmon", "tuna", "cod", "halibut", "anchovy", "anchovies" },
        ["Shellfish"] = new() { "shrimp", "crab", "lobster", "shellfish", "clam", "oyster", "mussel", "scallop" },
        ["Tree Nuts"] = new() { "almond", "walnut", "pecan", "cashew", "pistachio", "hazelnut", "macadamia", "nut" },
        ["Peanuts"] = new() { "peanut", "peanuts", "peanut butter" },
        ["Wheat"] = new() { "wheat", "flour", "bread", "pasta", "noodle", "semolina", "farina", "bulgur", "couscous" },
        ["Soybeans"] = new() { "soy", "tofu", "tempeh", "edamame", "miso", "soy sauce", "tamari" },
        ["Sesame"] = new() { "sesame", "tahini" },
        ["Gluten"] = new() { "wheat", "barley", "rye", "flour", "bread", "pasta", "seitan", "malt" }
    };

    public AllergenDetectionService(IAllergenRepository allergenRepository, ILogger<AllergenDetectionService> logger)
    {
        _allergenRepository = allergenRepository;
        _logger = logger;
    }

    /// <summary>
    /// Detect allergens in recipe ingredients by cross-referencing with database,
    /// falling back to keyword matching when the repository returns no results.
    /// </summary>
    public async Task<List<RecipeAllergenWarningDto>> DetectAllergensAsync(ParsedRecipe recipe)
    {
        var allergenWarnings = new List<RecipeAllergenWarningDto>();
        var detectedAllergens = new HashSet<Guid>(); // Track to avoid duplicates

        foreach (var ingredient in recipe.Ingredients)
        {
            // Try database lookup first
            var dbAllergens = await _allergenRepository.FindAllergensByIngredientNameAsync(ingredient.IngredientName);

            foreach (var allergen in dbAllergens)
            {
                if (detectedAllergens.Add(allergen.AllergenId))
                {
                    allergenWarnings.Add(new RecipeAllergenWarningDto
                    {
                        AllergenId = allergen.AllergenId,
                        AllergenName = allergen.AllergenName,
                        SourceIngredientId = null // Will be set when ingredient is saved
                    });
                }
            }

            // Fallback to keyword matching if no database match
            if (!dbAllergens.Any())
            {
                var keywordAllergens = DetectByKeywords(ingredient.IngredientName);
                foreach (var (allergenName, _) in keywordAllergens)
                {
                    var pseudoId = GenerateAllergenId(allergenName);

                    if (detectedAllergens.Add(pseudoId))
                    {
                        allergenWarnings.Add(new RecipeAllergenWarningDto
                        {
                            AllergenId = pseudoId,
                            AllergenName = allergenName,
                            SourceIngredientId = null
                        });

                        _logger.LogInformation(
                            "Detected allergen '{Allergen}' in ingredient '{Ingredient}' using keyword matching",
                            allergenName, ingredient.IngredientName);
                    }
                }
            }
        }

        return allergenWarnings;
    }

    /// <summary>
    /// Detect allergens using keyword matching (fallback method).
    /// Internal so it can be tested directly via <see cref="InternalsVisibleToAttribute"/>.
    /// </summary>
    internal static List<(string AllergenName, string Keyword)> DetectByKeywords(string ingredientName)
    {
        var detected = new List<(string, string)>();
        var lowerIngredient = ingredientName.ToLower();

        foreach (var (allergenName, keywords) in AllergenKeywords)
        {
            foreach (var keyword in keywords)
            {
                if (lowerIngredient.Contains(keyword))
                {
                    detected.Add((allergenName, keyword));
                    break; // Only add each allergen once
                }
            }
        }

        return detected;
    }

    /// <summary>
    /// Generate a consistent GUID for allergen names (for keyword-based detection).
    /// Uses deterministic GUID generation from allergen name.
    /// </summary>
    private static Guid GenerateAllergenId(string allergenName)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(allergenName.ToLower()));
        // Use first 16 bytes of SHA-256 hash as GUID
        return new Guid(hash[..16]);
    }

    /// <summary>
    /// Get all allergens that could potentially be in the recipe.
    /// Used for displaying allergen information to users.
    /// Falls back to keyword dictionary if database is unavailable.
    /// </summary>
    public async Task<List<(Guid Id, string Name)>> GetAllKnownAllergensAsync()
    {
        var allergens = await _allergenRepository.GetAllKnownAllergensAsync();

        if (allergens.Count == 0)
        {
            _logger.LogWarning("No allergens found in database; using keyword dictionary as fallback");
            // Fallback: return allergens from keyword dictionary
            foreach (var allergenName in AllergenKeywords.Keys)
            {
                allergens.Add((GenerateAllergenId(allergenName), allergenName));
            }
        }

        return allergens;
    }
}
