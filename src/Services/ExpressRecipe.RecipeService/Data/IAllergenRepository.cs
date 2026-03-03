namespace ExpressRecipe.RecipeService.Data;

/// <summary>
/// Repository interface for allergen data access.
/// </summary>
public interface IAllergenRepository
{
    /// <summary>
    /// Find allergens associated with a given ingredient name.
    /// Returns an empty list if no database match is found (keyword fallback will apply).
    /// </summary>
    Task<List<(Guid AllergenId, string AllergenName)>> FindAllergensByIngredientNameAsync(string ingredientName);

    /// <summary>
    /// Get all known allergens from the database.
    /// </summary>
    Task<List<(Guid Id, string Name)>> GetAllKnownAllergensAsync();
}
