using ExpressRecipe.Shared.DTOs.Recipe;

namespace ExpressRecipe.RecipeService.Data;

/// <summary>
/// Interface for recipe data access
/// </summary>
public interface IRecipeRepository
{
    /// <summary>
    /// Create a new recipe with ingredients, nutrition, and allergen warnings
    /// </summary>
    Task<Guid> CreateRecipeAsync(CreateRecipeRequest request, Guid createdBy);

    /// <summary>
    /// Add ingredients to a recipe
    /// </summary>
    Task AddRecipeIngredientsAsync(Guid recipeId, List<RecipeIngredientDto> ingredients, Guid? createdBy = null);

    /// <summary>
    /// Add nutrition information to a recipe
    /// </summary>
    Task AddRecipeNutritionAsync(Guid recipeId, RecipeNutritionDto nutrition);

    /// <summary>
    /// Add allergen warnings to a recipe
    /// </summary>
    Task AddRecipeAllergensAsync(Guid recipeId, List<RecipeAllergenWarningDto> allergens);

    /// <summary>
    /// Add tags to a recipe
    /// </summary>
    Task AddRecipeTagsAsync(Guid recipeId, List<string> tagNames);

    /// <summary>
    /// Find potential duplicate recipes by name and author
    /// </summary>
    Task<RecipeDto?> FindDuplicateRecipeAsync(string name, Guid authorId);

    /// <summary>
    /// Get recipe by ID
    /// </summary>
    Task<RecipeDto?> GetRecipeByIdAsync(Guid id);

    /// <summary>
    /// Update recipe instructions (parsed step-by-step instructions)
    /// </summary>
    Task UpdateRecipeInstructionsAsync(Guid recipeId, string instructions);
}
