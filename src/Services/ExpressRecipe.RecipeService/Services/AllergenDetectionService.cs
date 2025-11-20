using ExpressRecipe.RecipeService.Data;
using ExpressRecipe.RecipeService.Parsers;
using ExpressRecipe.Shared.DTOs.Recipe;
using Microsoft.Data.SqlClient;

namespace ExpressRecipe.RecipeService.Services;

/// <summary>
/// Service for detecting allergens in recipe ingredients
/// Cross-references ingredient names with known allergen database
/// </summary>
public class AllergenDetectionService
{
    private readonly string _connectionString;
    private readonly ILogger<AllergenDetectionService> _logger;

    // Common allergen keywords for fallback detection
    private static readonly Dictionary<string, List<string>> AllergenKeywords = new()
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

    public AllergenDetectionService(string connectionString, ILogger<AllergenDetectionService> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    /// <summary>
    /// Detect allergens in recipe ingredients by cross-referencing with database
    /// </summary>
    public async Task<List<RecipeAllergenWarningDto>> DetectAllergensAsync(ParsedRecipe recipe)
    {
        var allergenWarnings = new List<RecipeAllergenWarningDto>();
        var detectedAllergens = new HashSet<Guid>(); // Track to avoid duplicates

        foreach (var ingredient in recipe.Ingredients)
        {
            // Try database lookup first
            var dbAllergens = await FindAllergensInDatabaseAsync(ingredient.IngredientName);

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
                    // Create a pseudo-ID for keyword-based allergens
                    // In production, these should be matched to actual allergen IDs
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
    /// Find allergens by looking up BaseIngredient allergen mappings in database
    /// This queries the ProductService database for ingredient-allergen associations
    /// </summary>
    private async Task<List<(Guid AllergenId, string AllergenName)>> FindAllergensInDatabaseAsync(string ingredientName)
    {
        var allergens = new List<(Guid, string)>();

        // In a full implementation, this would:
        // 1. Query BaseIngredient table to find matching ingredient
        // 2. Join with BaseIngredientAllergen table
        // 3. Return all associated allergens

        // For now, we'll do a simple keyword-based lookup
        // TODO: Implement cross-service database query or API call to ProductService

        try
        {
            // This is a placeholder - in production, this should call ProductService API
            // or use a shared database connection if services share the same DB

            const string sql = @"
                SELECT DISTINCT a.Id, a.Name
                FROM Allergen a
                WHERE LOWER(a.Name) IN (
                    SELECT value
                    FROM STRING_SPLIT(LOWER(@IngredientName), ' ')
                )
                   OR LOWER(@IngredientName) LIKE '%' + LOWER(a.Name) + '%'";

            // Note: This assumes Allergen table exists in same database
            // In microservices, this should be a cross-service call

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@IngredientName", ingredientName);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                allergens.Add((
                    reader.GetGuid(0),
                    reader.GetString(1)
                ));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to query allergen database for ingredient: {Ingredient}",
                ingredientName);
        }

        return allergens;
    }

    /// <summary>
    /// Detect allergens using keyword matching (fallback method)
    /// </summary>
    private List<(string AllergenName, string Keyword)> DetectByKeywords(string ingredientName)
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
    /// Generate a consistent GUID for allergen names (for keyword-based detection)
    /// Uses deterministic GUID generation from allergen name
    /// </summary>
    private Guid GenerateAllergenId(string allergenName)
    {
        // Use a deterministic method to generate GUID from name
        // This ensures the same allergen name always gets the same ID
        using var md5 = System.Security.Cryptography.MD5.Create();
        var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(allergenName.ToLower()));
        return new Guid(hash);
    }

    /// <summary>
    /// Get all allergens that could potentially be in the recipe
    /// Used for displaying allergen information to users
    /// </summary>
    public async Task<List<(Guid Id, string Name)>> GetAllKnownAllergensAsync()
    {
        var allergens = new List<(Guid, string)>();

        try
        {
            const string sql = "SELECT Id, Name FROM Allergen WHERE IsDeleted = 0 ORDER BY Name";

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand(sql, connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                allergens.Add((reader.GetGuid(0), reader.GetString(1)));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve allergen list from database");

            // Fallback: return allergens from keyword dictionary
            foreach (var allergenName in AllergenKeywords.Keys)
            {
                allergens.Add((GenerateAllergenId(allergenName), allergenName));
            }
        }

        return allergens;
    }
}
