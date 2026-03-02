using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ExpressRecipe.Shared.DTOs.Recipe;
using CQ = ExpressRecipe.RecipeService.CQRS.Queries;

namespace ExpressRecipe.RecipeService.Data;

/// <summary>
/// Interface for recipe data access used by the RecipeService.
/// </summary>
public interface IRecipeRepository
{
    Task<Guid> CreateRecipeAsync(CreateRecipeRequest request, Guid createdBy);
    Task<Guid> CreateRecipeAsync(Guid userId, string name, string? description, int? prepTimeMinutes, int? cookTimeMinutes, int? totalTimeMinutes, int servings, string difficulty);
    Task AddRecipeIngredientsAsync(Guid recipeId, List<RecipeIngredientDto> ingredients, Guid? createdBy = null);
    Task AddRecipeNutritionAsync(Guid recipeId, RecipeNutritionDto nutrition);
    Task AddRecipeAllergensAsync(Guid recipeId, List<RecipeAllergenWarningDto> allergens);
    Task AddRecipeTagsAsync(Guid recipeId, List<string> tagNames);
    Task AddRecipeCategoryAsync(Guid recipeId, string categoryName);
    Task AddRecipeTagAsync(Guid recipeId, string tagName);
    Task AddIngredientAsync(Guid recipeId, Guid? productId, string name, decimal quantity, string unit, string? notes, bool isOptional);
    Task AddInstructionAsync(Guid recipeId, int stepNumber, string instruction, int? timeMinutes);
    Task UpdateNutritionAsync(Guid recipeId, int? calories, decimal? protein, decimal? carbs, decimal? fat, decimal? fiber, decimal? sugar);
    Task<RecipeDto?> FindDuplicateRecipeAsync(string name, Guid authorId);
    Task<RecipeDto?> GetRecipeByIdAsync(Guid id);
    Task<List<RecipeDto>> SearchRecipesAsync(string searchTerm, int limit = 50, int offset = 0);
    Task<List<RecipeDto>> GetAllRecipesAsync(int limit = 50, int offset = 0);
    Task<HashSet<string>> GetAllRecipeTitlesAsync();
    Task<Dictionary<string, bool>> GetAllRecipeTitlesCompletenessAsync();
    Task<(decimal AverageRating, int RatingCount)> GetAverageRatingAsync(Guid recipeId);
    Task<List<string>> GetRecipeCategoriesAsync(Guid recipeId);
    Task<List<string>> GetRecipeTagsAsync(Guid recipeId);
    Task<List<RecipeIngredientDto>> GetIngredientsAsync(Guid recipeId);
    Task<List<CQ.RecipeInstructionDto>> GetInstructionsAsync(Guid recipeId);
    Task<CQ.RecipeNutritionDto?> GetNutritionAsync(Guid recipeId);
    Task<CQ.RecipeDetailsDto?> GetRecipeDetailsAsync(Guid recipeId);
    Task IncrementViewCountAsync(Guid recipeId);
    Task UpdateRecipeInstructionsAsync(Guid recipeId, string instructions);
    Task ClearRecipeIngredientsAsync(Guid recipeId);
    Task ClearRecipeInstructionsAsync(Guid recipeId);
    Task ClearRecipeTagsAsync(Guid recipeId);
    Task AddRecipeImagesAsync(Guid recipeId, List<RecipeImageDto> images);
    Task<int> BulkCreateFullRecipesAsync(List<FullRecipeImportDto> recipes);
    Task<int> BulkCreateFullRecipesHighSpeedAsync(List<FullRecipeImportDto> recipes);
    Task<List<RecipeIngredientDto>> GetRecipeIngredientsAsync(Guid recipeId);
    Task<RecipeNutritionDto?> GetRecipeNutritionAsync(Guid recipeId);
    Task<List<RecipeAllergenWarningDto>> GetRecipeAllergensAsync(Guid recipeId);
    Task UpdateRecipeAsync(Guid id, UpdateRecipeRequest request, Guid userId);
    Task DeleteRecipeAsync(Guid id);
    Task<List<RecipeDto>> GetUserRecipesAsync(Guid userId, int limit = 50);
    Task<List<RecipeDto>> GetRecipesByCategoryAsync(string category, int limit = 50);
    Task<List<RecipeDto>> GetRecipesByCuisineAsync(string cuisine, int limit = 50);
    Task<List<RecipeDto>> GetRecipesByTagAsync(string tag, int limit = 50);
    Task<List<RecipeDto>> GetRecipesByIngredientAsync(string ingredient, int limit = 50);
    Task<List<string>> GetAllCategoriesAsync();
    Task<List<string>> GetAllCuisinesAsync();
    Task<object?> GetByExactTitleAsync(string title);
}
