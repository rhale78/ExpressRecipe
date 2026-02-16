using ExpressRecipe.Client.Shared.Models.Recipe;
using ExpressRecipe.Client.Shared.Services;

namespace ExpressRecipe.BlazorWeb.Services;

/// <summary>
/// Service for synchronizing recipes between database and disk
/// Handles auto-save when recipes are created/updated/deleted
/// </summary>
public class RecipeSyncService
{
    private readonly RecipeFileService _fileService;
    private readonly IRecipeApiClient _apiClient;
    private readonly ILogger<RecipeSyncService> _logger;
    private string? _currentUserIdentifier;

    public RecipeSyncService(
        RecipeFileService fileService,
        IRecipeApiClient apiClient,
        ILogger<RecipeSyncService> logger)
    {
        _fileService = fileService;
        _apiClient = apiClient;
        _logger = logger;
    }

    /// <summary>
    /// Set the current user identifier for sync operations
    /// </summary>
    public void SetCurrentUser(string userIdentifier)
    {
        _currentUserIdentifier = userIdentifier;
        _logger.LogInformation("RecipeSyncService user set to: {User}", userIdentifier);
    }

    /// <summary>
    /// Auto-save a recipe to disk after creation or update
    /// </summary>
    public async Task<string?> AutoSaveRecipeAsync(RecipeDto recipe)
    {
        if (string.IsNullOrEmpty(_currentUserIdentifier))
        {
            _logger.LogWarning("Cannot auto-save: No user identifier set");
            return null;
        }

        try
        {
            var filePath = await _fileService.ExportRecipeAsync(recipe, _currentUserIdentifier);
            _logger.LogInformation("Auto-saved recipe {RecipeId} to disk", recipe.Id);
            return filePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error auto-saving recipe {RecipeId}", recipe.Id);
            return null;
        }
    }

    /// <summary>
    /// Auto-delete a recipe file when recipe is deleted from database
    /// </summary>
    public async Task AutoDeleteRecipeAsync(Guid recipeId, string recipeTitle)
    {
        if (string.IsNullOrEmpty(_currentUserIdentifier))
        {
            _logger.LogWarning("Cannot auto-delete: No user identifier set");
            return;
        }

        try
        {
            // Find the file
            var userDir = _fileService.GetUserExportPath(_currentUserIdentifier);
            var pattern = $"*_{recipeId}.md";
            var files = Directory.GetFiles(userDir, pattern, SearchOption.TopDirectoryOnly);
            
            if (files.Length > 0)
            {
                await _fileService.DeleteRecipeFileAsync(files[0]);
                _logger.LogInformation("Auto-deleted recipe file for {RecipeId}", recipeId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error auto-deleting recipe {RecipeId}", recipeId);
        }
    }

    /// <summary>
    /// Scan disk and load recipes that don't exist in database
    /// </summary>
    public async Task<List<RecipeDto>> ScanAndLoadNewRecipesAsync()
    {
        if (string.IsNullOrEmpty(_currentUserIdentifier))
        {
            _logger.LogWarning("Cannot scan: No user identifier set");
            return new List<RecipeDto>();
        }

        var newRecipes = new List<RecipeDto>();

        try
        {
            // Get all recipe files from disk
            var diskFiles = await _fileService.ScanUserRecipesAsync(_currentUserIdentifier);
            
            // Get all recipes from database
            var dbRecipes = await _apiClient.GetMyRecipesAsync(1, 1000);
            var dbRecipeIds = dbRecipes?.Select(r => r.Id).ToHashSet() ?? new HashSet<Guid>();

            // Check each disk file
            foreach (var filePath in diskFiles)
            {
                try
                {
                    var recipe = await _fileService.ImportRecipeFromDiskAsync(filePath);
                    if (recipe != null && !dbRecipeIds.Contains(recipe.Id))
                    {
                        newRecipes.Add(recipe);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading recipe from {FilePath}", filePath);
                }
            }

            if (newRecipes.Any())
            {
                _logger.LogInformation("Found {Count} new recipes on disk", newRecipes.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning for new recipes");
        }

        return newRecipes;
    }

    /// <summary>
    /// Export all user recipes to disk
    /// </summary>
    public async Task<int> ExportAllRecipesAsync()
    {
        if (string.IsNullOrEmpty(_currentUserIdentifier))
        {
            _logger.LogWarning("Cannot export all: No user identifier set");
            return 0;
        }

        try
        {
            var recipes = await _apiClient.GetMyRecipesAsync(1, 1000);
            if (recipes == null || !recipes.Any())
            {
                return 0;
            }

            return await _fileService.ExportAllRecipesAsync(recipes, _currentUserIdentifier);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting all recipes");
            return 0;
        }
    }

    /// <summary>
    /// Get export path for current user
    /// </summary>
    public string? GetExportPath()
    {
        if (string.IsNullOrEmpty(_currentUserIdentifier))
        {
            return null;
        }

        return _fileService.GetUserExportPath(_currentUserIdentifier);
    }
}
