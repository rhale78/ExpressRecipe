namespace ExpressRecipe.Shared.Services;

/// <summary>
/// Helper for creating valid HybridCache keys.
/// HybridCache restricts certain characters in keys.
/// </summary>
public static class CacheKeyHelper
{
    private static readonly char[] InvalidChars = { '{', '}', '(', ')', '[', ']', ',', ';', '=', ' ', '\t', '\r', '\n', '\\', '/' };

    /// <summary>
    /// Creates a safe cache key by replacing invalid characters.
    /// HybridCache requires keys without special characters.
    /// </summary>
    public static string CreateKey(string prefix, params object[] parts)
    {
        var key = $"{prefix}:{string.Join(":", parts)}";
        
        // Replace invalid characters with underscores
        foreach (var c in InvalidChars)
        {
            key = key.Replace(c, '_');
        }

        // Ensure key length is reasonable (max 512 chars for most cache systems)
        if (key.Length > 512)
        {
            key = key.Substring(0, 512);
        }

        return key;
    }

    /// <summary>
    /// Creates a cache key for a recipe detail by ID
    /// </summary>
    public static string RecipeDetails(Guid recipeId) => CreateKey("recipe_details", recipeId);

    /// <summary>
    /// Creates a cache key for recipe search results
    /// </summary>
    public static string RecipeSearch(string searchTerm, int page) => CreateKey("recipe_search", searchTerm, page);

    /// <summary>
    /// Creates a cache key for product by ID
    /// </summary>
    public static string ProductDetails(Guid productId) => CreateKey("product_details", productId);

    /// <summary>
    /// Creates a cache key for ingredient by name
    /// </summary>
    public static string IngredientByName(string name) => CreateKey("ingredient_name", name);
}
