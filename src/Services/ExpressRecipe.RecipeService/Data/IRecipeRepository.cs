using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shared = ExpressRecipe.Shared.DTOs.Recipe;
using CQ = ExpressRecipe.RecipeService.CQRS.Queries;

namespace ExpressRecipe.RecipeService.Data;

/// <summary>
/// Interface for recipe data access used by the RecipeService.
/// Uses explicit aliases to avoid ambiguous DTO type names between projects.
/// </summary>
public interface IRecipeRepository
{
    /// <summary>
    /// Create a new recipe with ingredients, nutrition, and allergen warnings
    /// </summary>
    Task<Guid> CreateRecipeAsync(ExpressRecipe.Shared.DTOs.Recipe.CreateRecipeRequest request, Guid createdBy);

    // Convenience overload used by CQRS handlers
    Task<Guid> CreateRecipeAsync(Guid userId, string name, string? description, int? prepTimeMinutes, int? cookTimeMinutes, int? totalTimeMinutes, int servings, string difficulty);

    /// <summary>
    /// Add ingredients to a recipe
    /// </summary>

    Task AddRecipeIngredientsAsync(Guid recipeId, List<ExpressRecipe.Shared.DTOs.Recipe.RecipeIngredientDto> ingredients, Guid? createdBy = null);

    /// <summary>
    /// Add nutrition information to a recipe
    /// </summary>
    Task AddRecipeNutritionAsync(Guid recipeId, ExpressRecipe.Shared.DTOs.Recipe.RecipeNutritionDto nutrition);

    /// <summary>
    /// Add allergen warnings to a recipe
    /// </summary>
    Task AddRecipeAllergensAsync(Guid recipeId, List<ExpressRecipe.Shared.DTOs.Recipe.RecipeAllergenWarningDto> allergens);

    /// <summary>
    /// Add tags to a recipe
    /// </summary>
    Task AddRecipeTagsAsync(Guid recipeId, List<string> tagNames);

    // Single-item helpers used by CQRS handlers
    Task AddRecipeCategoryAsync(Guid recipeId, string categoryName);
    Task AddRecipeTagAsync(Guid recipeId, string tagName);
    Task AddIngredientAsync(Guid recipeId, Guid? productId, string name, decimal quantity, string unit, string? notes, bool isOptional);
    Task AddInstructionAsync(Guid recipeId, int stepNumber, string instruction, int? timeMinutes);
    Task UpdateNutritionAsync(Guid recipeId, int? calories, decimal? protein, decimal? carbs, decimal? fat, decimal? fiber, decimal? sugar);

    /// <summary>
    /// Find potential duplicate recipes by name and author
    /// </summary>
    Task<ExpressRecipe.Shared.DTOs.Recipe.RecipeDto?> FindDuplicateRecipeAsync(string name, Guid authorId);

    /// <summary>
    /// Get recipe by ID
    /// </summary>
    Task<ExpressRecipe.Shared.DTOs.Recipe.RecipeDto?> GetRecipeByIdAsync(Guid id);

    Task<List<ExpressRecipe.Shared.DTOs.Recipe.RecipeDto>> SearchRecipesAsync(string searchTerm, int limit = 50, int offset = 0);
    Task<List<ExpressRecipe.Shared.DTOs.Recipe.RecipeDto>> GetAllRecipesAsync(int limit = 50, int offset = 0);
    Task<(decimal AverageRating, int RatingCount)> GetAverageRatingAsync(Guid recipeId);
    Task<List<string>> GetRecipeCategoriesAsync(Guid recipeId);
    Task<List<string>> GetRecipeTagsAsync(Guid recipeId);
    Task<List<ExpressRecipe.Shared.DTOs.Recipe.RecipeIngredientDto>> GetIngredientsAsync(Guid recipeId);
    Task<List<CQ.RecipeInstructionDto>> GetInstructionsAsync(Guid recipeId);
    Task<CQ.RecipeNutritionDto?> GetNutritionAsync(Guid recipeId);
    Task<CQ.RecipeDetailsDto?> GetRecipeDetailsAsync(Guid recipeId);
    Task IncrementViewCountAsync(Guid recipeId);

    Task UpdateRecipeInstructionsAsync(Guid recipeId, string instructions);
}
