using ExpressRecipe.Shared.DTOs.Recipe;
using ExpressRecipe.RecipeService.Data;
using ExpressRecipe.RecipeService.CQRS.Commands;
using ExpressRecipe.RecipeService.CQRS.Queries;
using ExpressRecipe.RecipeService.Services;
using ExpressRecipe.Shared.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using SharedRecipe = ExpressRecipe.Shared.DTOs.Recipe;
using CQRSQueries = ExpressRecipe.RecipeService.CQRS.Queries;

namespace ExpressRecipe.RecipeService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RecipesController : ControllerBase
{
    private readonly IRecipeRepository _recipeRepository;
    private readonly ServingSizeService _servingSizeService;
    private readonly ShoppingListIntegrationService _shoppingListService;
    private readonly IRecipeEventPublisher _events;
    private readonly IRecipeBatchChannel _batchChannel;
    private readonly ILogger<RecipesController> _logger;
    private readonly HybridCacheService? _cache;

    public RecipesController(
        IRecipeRepository recipeRepository,
        ServingSizeService servingSizeService,
        ShoppingListIntegrationService shoppingListService,
        IRecipeEventPublisher events,
        IRecipeBatchChannel batchChannel,
        ILogger<RecipesController> logger,
        HybridCacheService? cache = null)
    {
        _recipeRepository    = recipeRepository;
        _servingSizeService  = servingSizeService;
        _shoppingListService = shoppingListService;
        _events              = events;
        _batchChannel        = batchChannel;
        _logger              = logger;
        _cache               = cache;
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return null;
        }
        return userId;
    }

    /// <summary>
    /// Search and list recipes with filtering
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<RecipeSearchResult>> SearchRecipes(
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? category = null,
        [FromQuery] string? cuisine = null,
        [FromQuery] string? difficulty = null,
        [FromQuery] int? maxPrepTime = null,
        [FromQuery] int? maxCookTime = null,
        [FromQuery] decimal? maxCostPerServing = null,
        [FromQuery] string? sortBy = "CreatedAt",
        [FromQuery] bool sortDescending = true,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;

            var offset = (page - 1) * pageSize;

            // Build search query
            List<RecipeDto> recipes;
            
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                recipes = await _recipeRepository.SearchRecipesAsync(searchTerm, pageSize, offset);
            }
            else
            {
                recipes = await _recipeRepository.GetAllRecipesAsync(pageSize, offset);
            }

            // Note: category/cuisine/difficulty/time filters are applied in-memory after DB retrieval.
            // Future: push these filters into the SQL query in IRecipeRepository.GetAllRecipesAsync()/SearchRecipesAsync().
            if (!string.IsNullOrWhiteSpace(category))
            {
                recipes = recipes.Where(r => r.Category?.Equals(category, StringComparison.OrdinalIgnoreCase) == true).ToList();
            }

            if (!string.IsNullOrWhiteSpace(cuisine))
            {
                recipes = recipes.Where(r => r.Cuisine?.Equals(cuisine, StringComparison.OrdinalIgnoreCase) == true).ToList();
            }

            if (!string.IsNullOrWhiteSpace(difficulty))
            {
                recipes = recipes.Where(r => r.DifficultyLevel?.Equals(difficulty, StringComparison.OrdinalIgnoreCase) == true).ToList();
            }

            if (maxPrepTime.HasValue)
            {
                recipes = recipes.Where(r => r.PrepTimeMinutes <= maxPrepTime.Value).ToList();
            }

            if (maxCookTime.HasValue)
            {
                recipes = recipes.Where(r => r.CookTimeMinutes <= maxCookTime.Value).ToList();
            }

            if (maxCostPerServing.HasValue)
            {
                recipes = recipes.Where(r => r.EstimatedCostPerServing.HasValue && r.EstimatedCostPerServing.Value <= maxCostPerServing.Value).ToList();
            }

            // Apply sorting
            recipes = sortBy?.ToLower() switch
            {
                "name" => sortDescending ? recipes.OrderByDescending(r => r.Name).ToList() : recipes.OrderBy(r => r.Name).ToList(),
                "preptime" => sortDescending ? recipes.OrderByDescending(r => r.PrepTimeMinutes).ToList() : recipes.OrderBy(r => r.PrepTimeMinutes).ToList(),
                "cooktime" => sortDescending ? recipes.OrderByDescending(r => r.CookTimeMinutes).ToList() : recipes.OrderBy(r => r.CookTimeMinutes).ToList(),
                "difficulty" => sortDescending ? recipes.OrderByDescending(r => r.DifficultyLevel).ToList() : recipes.OrderBy(r => r.DifficultyLevel).ToList(),
                "cost" => recipes.OrderBy(r => r.EstimatedCostPerServing.HasValue ? 0 : 1).ThenBy(r => r.EstimatedCostPerServing).ToList(),
                _ => sortDescending ? recipes.OrderByDescending(r => r.CreatedAt).ToList() : recipes.OrderBy(r => r.CreatedAt).ToList()
            };

            return Ok(new RecipeSearchResult
            {
                Recipes = recipes,
                TotalCount = recipes.Count,
                Page = page,
                PageSize = pageSize
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching recipes");
            return StatusCode(500, new { message = "An error occurred while searching recipes" });
        }
    }

    /// <summary>
    /// Get recipe by ID with full details
    /// </summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<RecipeDto>> GetRecipe(Guid id)
    {
        try
        {
            var recipe = await _recipeRepository.GetRecipeByIdAsync(id);

            if (recipe == null)
            {
                return NotFound(new { message = "Recipe not found" });
            }

            // Load all related data in parallel – each is an independent DB query
            var ingredientsTask  = _recipeRepository.GetRecipeIngredientsAsync(id);
            var nutritionTask    = _recipeRepository.GetRecipeNutritionAsync(id);
            var tagsTask         = _recipeRepository.GetRecipeTagsAsync(id);
            var allergensTask    = _recipeRepository.GetRecipeAllergensAsync(id);
            var ratingTask       = _recipeRepository.GetAverageRatingAsync(id);

            await Task.WhenAll(ingredientsTask, nutritionTask, tagsTask, allergensTask, ratingTask);

            recipe.Ingredients      = await ingredientsTask;
            recipe.Nutrition        = await nutritionTask;
            recipe.Tags             = (await tagsTask).Select(name => new RecipeTagDto { Name = name }).ToList();
            recipe.AllergenWarnings = await allergensTask;

            var (avgRating, ratingCount) = await ratingTask;
            recipe.AverageRating = avgRating;
            recipe.RatingCount   = ratingCount;

            _logger.LogInformation("[RecipeService] Recipe {RecipeId} '{Name}' loaded: {IngCount} ingredients, {TagCount} tags, {AllergenCount} allergen warnings",
                id, recipe.Name,
                recipe.Ingredients?.Count ?? 0,
                recipe.Tags?.Count ?? 0,
                recipe.AllergenWarnings?.Count ?? 0);

            return Ok(recipe);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recipe {RecipeId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving the recipe" });
        }
    }

    /// <summary>
    /// Create a new recipe
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Guid>> CreateRecipe([FromBody] CreateRecipeRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            request.CreatedBy = userId.Value;

            _logger.LogInformation("[RecipeService] Creating recipe '{Name}' for user {UserId}: {IngCount} ingredients, {StepCount} steps, {TagCount} tags",
                request.Name, userId.Value,
                request.Ingredients?.Count ?? 0,
                request.Steps?.Count ?? 0,
                request.Tags?.Count ?? 0);

            var recipeId = await _recipeRepository.CreateRecipeAsync(request, userId.Value);

            // Add ingredients if provided
            if (request.Ingredients != null && request.Ingredients.Any())
            {
                var ingredients = request.Ingredients.Select(i => new SharedRecipe.RecipeIngredientDto
                {
                    IngredientName = i.Name,
                    Quantity = i.Quantity,
                    Unit = i.Unit,
                    OrderIndex = i.OrderIndex,
                    PreparationNote = i.Notes,
                    IsOptional = i.IsOptional
                }).ToList();
                await _recipeRepository.AddRecipeIngredientsAsync(recipeId, ingredients, userId.Value);
            }

            // Add steps if provided
            if (request.Steps != null && request.Steps.Any())
            {
                foreach (var step in request.Steps)
                {
                    await _recipeRepository.AddInstructionAsync(recipeId, step.OrderIndex, step.Instruction, step.DurationMinutes);
                }
            }

            // Add tags if provided
            if (request.Tags != null && request.Tags.Any())
            {
                await _recipeRepository.AddRecipeTagsAsync(recipeId, request.Tags);
            }

            _logger.LogInformation("Recipe {RecipeId} created with all components by user {UserId}", recipeId, userId.Value);

            await _events.PublishCreatedAsync(recipeId, request.Name ?? string.Empty,
                request.Category, request.Cuisine, userId.Value);

            return CreatedAtAction(nameof(GetRecipe), new { id = recipeId }, new { id = recipeId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating recipe");
            return StatusCode(500, new { message = "An error occurred while creating the recipe" });
        }
    }

    /// <summary>
    /// Update an existing recipe
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult> UpdateRecipe(Guid id, [FromBody] UpdateRecipeRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            // Check if recipe exists and user has permission
            var existingRecipe = await _recipeRepository.GetRecipeByIdAsync(id);
            if (existingRecipe == null)
            {
                return NotFound(new { message = "Recipe not found" });
            }

            if (existingRecipe.AuthorId != userId.Value)
            {
                return Forbid();
            }

            _logger.LogInformation("[RecipeService] Updating recipe {RecipeId} '{Name}' for user {UserId}: {IngCount} ingredients, {StepCount} steps, {TagCount} tags",
                id, existingRecipe.Name, userId.Value,
                request.Ingredients?.Count ?? 0,
                request.Steps?.Count ?? 0,
                request.Tags?.Count ?? 0);

            await _recipeRepository.UpdateRecipeAsync(id, request, userId.Value);

            // Update ingredients if provided
            if (request.Ingredients != null)
            {
                await _recipeRepository.ClearRecipeIngredientsAsync(id);
                if (request.Ingredients.Any())
                {
                    var ingredients = request.Ingredients.Select(i => new SharedRecipe.RecipeIngredientDto
                    {
                        IngredientName = i.Name,
                        Quantity = i.Quantity,
                        Unit = i.Unit,
                        OrderIndex = i.OrderIndex,
                        PreparationNote = i.Notes,
                        IsOptional = i.IsOptional
                    }).ToList();
                    await _recipeRepository.AddRecipeIngredientsAsync(id, ingredients, userId.Value);
                }
            }

            // Update steps if provided
            if (request.Steps != null)
            {
                await _recipeRepository.ClearRecipeInstructionsAsync(id);
                if (request.Steps.Any())
                {
                    foreach (var step in request.Steps)
                    {
                        await _recipeRepository.AddInstructionAsync(id, step.OrderIndex, step.Instruction, step.DurationMinutes);
                    }
                }
            }

            // Update tags if provided
            if (request.Tags != null)
            {
                await _recipeRepository.ClearRecipeTagsAsync(id);
                if (request.Tags.Any())
                {
                    await _recipeRepository.AddRecipeTagsAsync(id, request.Tags);
                }
            }

            _logger.LogInformation("Recipe {RecipeId} fully updated by user {UserId}", id, userId.Value);

            var changedFields = new List<string> { nameof(request.Name) };
            if (request.Category != null) changedFields.Add(nameof(request.Category));
            if (request.Cuisine != null) changedFields.Add(nameof(request.Cuisine));
            if (request.Ingredients != null) changedFields.Add("Ingredients");
            if (request.Steps != null) changedFields.Add("Steps");
            if (request.Tags != null) changedFields.Add("Tags");

            await _events.PublishUpdatedAsync(id, request.Name, request.Category, request.Cuisine,
                userId.Value, changedFields);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating recipe {RecipeId}", id);
            return StatusCode(500, new { message = "An error occurred while updating the recipe" });
        }
    }

    /// <summary>
    /// Delete a recipe
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteRecipe(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            // Check if recipe exists and user has permission
            var existingRecipe = await _recipeRepository.GetRecipeByIdAsync(id);
            if (existingRecipe == null)
            {
                return NotFound(new { message = "Recipe not found" });
            }

            if (existingRecipe.AuthorId != userId.Value)
            {
                return Forbid();
            }

            await _recipeRepository.DeleteRecipeAsync(id);

            _logger.LogInformation("Recipe {RecipeId} deleted by user {UserId}", id, userId.Value);

            await _events.PublishDeletedAsync(id, userId.Value);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting recipe {RecipeId}", id);
            return StatusCode(500, new { message = "An error occurred while deleting the recipe" });
        }
    }

    /// <summary>
    /// Get user's recipes
    /// </summary>
    [HttpGet("my-recipes")]
    public async Task<ActionResult<List<RecipeDto>>> GetMyRecipes([FromQuery] int limit = 50)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var recipes = await _recipeRepository.GetUserRecipesAsync(userId.Value, limit);
            return Ok(recipes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user recipes");
            return StatusCode(500, new { message = "An error occurred while retrieving your recipes" });
        }
    }

    /// <summary>
    /// Get recipes by category
    /// </summary>
    [HttpGet("by-category/{category}")]
    [AllowAnonymous]
    public async Task<ActionResult<List<RecipeDto>>> GetRecipesByCategory(string category, [FromQuery] int limit = 50)
    {
        try
        {
            var recipes = await _recipeRepository.GetRecipesByCategoryAsync(category, limit);
            return Ok(recipes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recipes by category {Category}", category);
            return StatusCode(500, new { message = "An error occurred while retrieving recipes" });
        }
    }

    /// <summary>
    /// Get recipes by cuisine
    /// </summary>
    [HttpGet("by-cuisine/{cuisine}")]
    [AllowAnonymous]
    public async Task<ActionResult<List<RecipeDto>>> GetRecipesByCuisine(string cuisine, [FromQuery] int limit = 50)
    {
        try
        {
            var recipes = await _recipeRepository.GetRecipesByCuisineAsync(cuisine, limit);
            return Ok(recipes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recipes by cuisine {Cuisine}", cuisine);
            return StatusCode(500, new { message = "An error occurred while retrieving recipes" });
        }
    }

    /// <summary>
    /// Get recipes by meal type (uses tags)
    /// </summary>
    [HttpGet("by-meal-type/{mealType}")]
    [AllowAnonymous]
    public async Task<ActionResult<List<RecipeDto>>> GetRecipesByMealType(string mealType, [FromQuery] int limit = 50)
    {
        try
        {
            var recipes = await _recipeRepository.GetRecipesByTagAsync(mealType, limit);
            return Ok(recipes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recipes by meal type {MealType}", mealType);
            return StatusCode(500, new { message = "An error occurred while retrieving recipes" });
        }
    }

    /// <summary>
    /// Search recipes by ingredient
    /// </summary>
    [HttpGet("by-ingredient")]
    [AllowAnonymous]
    public async Task<ActionResult<List<RecipeDto>>> GetRecipesByIngredient([FromQuery] string ingredient, [FromQuery] int limit = 50)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ingredient))
            {
                return BadRequest(new { message = "Ingredient parameter is required" });
            }

            var recipes = await _recipeRepository.GetRecipesByIngredientAsync(ingredient, limit);
            return Ok(recipes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recipes by ingredient {Ingredient}", ingredient);
            return StatusCode(500, new { message = "An error occurred while retrieving recipes" });
        }
    }

    /// <summary>
    /// Returns recipes that can be made with the supplied ingredients (pantry discovery).
    /// Used by PantryDiscovery to answer "what can I cook with what I have?".
    /// Pass ingredient names as repeated <c>ingredients</c> query params,
    /// e.g. <c>GET /api/recipes/with-ingredients?ingredients=chicken&amp;ingredients=garlic</c>
    /// </summary>
    [HttpGet("with-ingredients")]
    [AllowAnonymous]
    public async Task<ActionResult<List<RecipeDto>>> GetRecipesWithIngredients(
        [FromQuery] List<string> ingredients,
        [FromQuery] int limit = 20)
    {
        try
        {
            var filtered = ingredients
                .Where(i => !string.IsNullOrWhiteSpace(i))
                .Select(i => i.Trim())
                .ToList();

            if (filtered.Count == 0)
                return BadRequest(new { message = "At least one ingredient is required" });

            var recipes = await _recipeRepository.GetRecipesWithIngredientsAsync(filtered, limit);
            return Ok(recipes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recipes with ingredients");
            return StatusCode(500, new { message = "An error occurred while retrieving recipes" });
        }
    }

    /// <summary>
    /// Get available categories
    /// </summary>
    [HttpGet("categories")]
    [AllowAnonymous]
    public async Task<ActionResult<List<string>>> GetCategories()
    {
        try
        {
            var categories = await _recipeRepository.GetAllCategoriesAsync();
            return Ok(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving categories");
            return StatusCode(500, new { message = "An error occurred while retrieving categories" });
        }
    }

    /// <summary>
    /// Get available cuisines
    /// </summary>
    [HttpGet("cuisines")]
    [AllowAnonymous]
    public async Task<ActionResult<List<string>>> GetCuisines()
    {
        try
        {
            var cuisines = await _recipeRepository.GetAllCuisinesAsync();
            return Ok(cuisines);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cuisines");
            return StatusCode(500, new { message = "An error occurred while retrieving cuisines" });
        }
    }

    /// <summary>
    /// Scale recipe to a different serving size
    /// </summary>
    [HttpGet("{id:guid}/scale")]
    [AllowAnonymous]
    public async Task<ActionResult<ScaledRecipeDto>> ScaleRecipe(Guid id, [FromQuery] int servings)
    {
        try
        {
            if (servings <= 0)
            {
                return BadRequest(new { message = "Servings must be greater than zero" });
            }

            // Get the recipe with ingredients
            var recipe = await _recipeRepository.GetRecipeByIdAsync(id);
            if (recipe == null)
            {
                return NotFound(new { message = "Recipe not found" });
            }

            recipe.Ingredients = await _recipeRepository.GetRecipeIngredientsAsync(id);

            // Scale the recipe
            var scaledRecipe = _servingSizeService.ScaleRecipe(recipe, servings);

            // Add time adjustments
            scaledRecipe.TimeAdjustment = _servingSizeService.AdjustTimings(
                recipe.Servings ?? 1,
                servings,
                recipe.PrepTimeMinutes,
                recipe.CookTimeMinutes
            );

            return Ok(scaledRecipe);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scaling recipe {RecipeId}", id);
            return StatusCode(500, new { message = "An error occurred while scaling the recipe" });
        }
    }

    /// <summary>
    /// Get serving size suggestions for a recipe
    /// </summary>
    [HttpGet("{id:guid}/serving-suggestions")]
    [AllowAnonymous]
    public async Task<ActionResult<List<int>>> GetServingSuggestions(Guid id)
    {
        try
        {
            var recipe = await _recipeRepository.GetRecipeByIdAsync(id);
            if (recipe == null)
            {
                return NotFound(new { message = "Recipe not found" });
            }

            var suggestions = _servingSizeService.GetServingSizeSuggestions(recipe.Servings ?? 4);
            return Ok(suggestions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting serving suggestions for recipe {RecipeId}", id);
            return StatusCode(500, new { message = "An error occurred while getting serving suggestions" });
        }
    }

    /// <summary>
    /// Prepare recipe ingredients for shopping list
    /// Returns ingredient data optimized for adding to shopping list
    /// </summary>
    [HttpPost("{id:guid}/prepare-shopping-list")]
    public async Task<ActionResult<ShoppingListPreparationDto>> PrepareShoppingList(
        Guid id,
        [FromQuery] int? servings = null)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            // Get the recipe with ingredients
            var recipe = await _recipeRepository.GetRecipeByIdAsync(id);
            if (recipe == null)
            {
                return NotFound(new { message = "Recipe not found" });
            }

            var ingredients = await _recipeRepository.GetRecipeIngredientsAsync(id);

            // Prepare shopping list items
            var shoppingList = _shoppingListService.PrepareRecipeForShopping(recipe, ingredients, servings);

            _logger.LogInformation("Prepared shopping list for recipe {RecipeId} for user {UserId}", id, userId.Value);

            return Ok(shoppingList);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error preparing shopping list for recipe {RecipeId}", id);
            return StatusCode(500, new { message = "An error occurred while preparing the shopping list" });
        }
    }

    /// <summary>
    /// Internal service-to-service endpoint: returns shopping ingredients for a recipe without requiring user auth.
    /// Used by ShoppingService to add recipe ingredients to a shopping list.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("{id:guid}/internal/shopping-ingredients")]
    public async Task<ActionResult<ShoppingListPreparationDto>> GetShoppingIngredientsInternal(
        Guid id,
        [FromQuery] int? servings = null)
    {
        try
        {
            var recipe = await _recipeRepository.GetRecipeByIdAsync(id);
            if (recipe == null)
                return NotFound(new { message = "Recipe not found" });

            var ingredients = await _recipeRepository.GetRecipeIngredientsAsync(id);
            var shoppingList = _shoppingListService.PrepareRecipeForShopping(recipe, ingredients, servings);

            return Ok(shoppingList);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error preparing internal shopping ingredients for recipe {RecipeId}", id);
            return StatusCode(500, new { message = "An error occurred while preparing the shopping ingredients" });
        }
    }

    /// <summary>
    /// Submit multiple recipes in one call – asynchronous channel path.
    /// Items are written to the <see cref="IRecipeBatchChannel"/> and processed by
    /// <see cref="RecipeBatchChannelWorker"/> in the background, which also fires
    /// <see cref="IRecipeEventPublisher.PublishCreatedAsync"/> for each created recipe.
    /// For creating a single recipe use POST /api/recipes (sync REST path).
    /// </summary>
    [HttpPost("batch-import")]
    public async Task<IActionResult> BatchImport([FromBody] BatchImportRecipesRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized(new { message = "User not authenticated" });

            if (request.Recipes == null || request.Recipes.Count == 0)
                return BadRequest(new { message = "recipes list cannot be empty" });

            const int maxBatch = 200;
            if (request.Recipes.Count > maxBatch)
                return BadRequest(new { message = $"Batch exceeds maximum of {maxBatch} recipes" });

            var sessionId = Guid.NewGuid().ToString("N");
            var accepted  = 0;

            foreach (var recipeRequest in request.Recipes)
            {
                var item = new RecipeBatchItem
                {
                    Request     = recipeRequest,
                    SubmittedBy = userId.Value,
                    SessionId   = sessionId
                };

                if (_batchChannel.TryWrite(item))
                    accepted++;
                else
                {
                    await _batchChannel.WriteAsync(item, HttpContext.RequestAborted);
                    accepted++;
                }
            }

            _logger.LogInformation(
                "[RecipesController] Batch import submitted: session={SessionId} count={Count} by user {UserId}",
                sessionId, accepted, userId.Value);

            return Accepted(new
            {
                sessionId,
                accepted,
                message = "Recipe batch queued for async processing"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting recipe batch import");
            return StatusCode(500, new { message = "An error occurred while submitting the recipe batch" });
        }
    }
    /// <summary>
    /// Returns recipes with their full ingredient list — used by PantryDiscoveryService.
    /// Results are cached for 4 hours (the recipe catalog changes infrequently).
    /// </summary>
    [HttpGet("with-ingredient-summary")]
    [AllowAnonymous]
    public async Task<ActionResult<List<RecipeIngredientSummary>>> GetWithIngredients(
        [FromQuery] int limit = 500, CancellationToken ct = default)
    {
        if (limit < 1)   { limit = 1; }
        if (limit > 1000) { limit = 1000; }

        try
        {
            string cacheKey = $"recipes:with-ingredients:{limit}";

            List<RecipeIngredientSummary>? result = null;
            if (_cache is not null)
            {
                result = await _cache.GetOrSetAsync<List<RecipeIngredientSummary>>(
                    cacheKey,
                    async innerCt => await _recipeRepository.GetRecipesWithIngredientSummaryAsync(limit, innerCt),
                    TimeSpan.FromHours(4),
                    cancellationToken: ct);
            }
            else
            {
                result = await _recipeRepository.GetRecipesWithIngredientSummaryAsync(limit, ct);
            }

            return Ok(result ?? new List<RecipeIngredientSummary>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recipes with ingredients");
            return StatusCode(500, new { message = "An error occurred while retrieving recipes" });
        }
    }
}

public class RecipeSearchResult
{
    public List<RecipeDto> Recipes { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>
/// Request body for <c>POST /api/recipes/batch-import</c> (async channel path).
/// </summary>
public class BatchImportRecipesRequest
{
    public List<CreateRecipeRequest> Recipes { get; set; } = new();
}
