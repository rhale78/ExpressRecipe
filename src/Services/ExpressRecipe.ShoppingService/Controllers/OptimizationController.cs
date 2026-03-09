using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ExpressRecipe.ShoppingService.Data;
using ExpressRecipe.ShoppingService.Services;

namespace ExpressRecipe.ShoppingService.Controllers;

/// <summary>
/// Endpoints for shopping list optimization, preferences, and recipe integration.
/// </summary>
[Authorize]
[ApiController]
[Route("api/shopping")]
public class OptimizationController : ControllerBase
{
    private readonly ILogger<OptimizationController> _logger;
    private readonly IShoppingRepository _repository;
    private readonly IShoppingOptimizationService _optimizationService;
    private readonly IShoppingSessionService _sessionService;

    public OptimizationController(
        ILogger<OptimizationController> logger,
        IShoppingRepository repository,
        IShoppingOptimizationService optimizationService,
        IShoppingSessionService sessionService)
    {
        _logger = logger;
        _repository = repository;
        _optimizationService = optimizationService;
        _sessionService = sessionService;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ── Optimization ──────────────────────────────────────────────────────────

    /// <summary>
    /// Optimize the shopping list using the specified strategy.
    /// Strategies: SingleStore | CheapestOverall | MinimizeStores | PreferredStorePerCategory | Hybrid
    /// </summary>
    [HttpPost("{listId}/optimize")]
    public async Task<IActionResult> Optimize(
        Guid listId,
        [FromQuery] string strategy = "SingleStore",
        CancellationToken ct = default)
    {
        try
        {
            Guid userId = GetUserId();
            ShoppingListDto? list = await _repository.GetShoppingListAsync(listId, userId);
            if (list == null) return NotFound();

            OptimizedShoppingPlan plan = await _optimizationService.OptimizeAsync(listId, userId, strategy, ct);
            return Ok(plan);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error optimizing list {ListId}", listId);
            return StatusCode(500, new { message = "An error occurred during optimization" });
        }
    }

    /// <summary>
    /// Get the stored optimization result for a list.
    /// </summary>
    [HttpGet("{listId}/optimization")]
    public async Task<IActionResult> GetOptimization(Guid listId, CancellationToken ct = default)
    {
        try
        {
            Guid userId = GetUserId();
            ShoppingListDto? list = await _repository.GetShoppingListAsync(listId, userId);
            if (list == null) return NotFound();

            ShoppingListOptimizationDto? result = await _repository.GetOptimizationResultAsync(listId, ct);
            if (result == null) return NotFound(new { message = "No optimization result found. Run POST /{listId}/optimize first." });
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving optimization for list {ListId}", listId);
            return StatusCode(500, new { message = "An error occurred while retrieving optimization" });
        }
    }

    // ── Recipe integration ────────────────────────────────────────────────────

    /// <summary>
    /// Add ingredients from a recipe to the shopping list (net of on-hand inventory).
    /// </summary>
    [HttpPost("{listId}/add-from-recipe")]
    public async Task<IActionResult> AddFromRecipe(
        Guid listId,
        [FromBody] AddFromRecipeRequest request,
        CancellationToken ct = default)
    {
        try
        {
            Guid userId = GetUserId();
            ShoppingListDto? list = await _repository.GetShoppingListAsync(listId, userId);
            if (list == null) return NotFound();

            await _sessionService.AddItemsFromRecipeAsync(listId, userId, request.RecipeId, request.Servings, ct);
            return Ok(new { message = "Ingredients added to list." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding recipe {RecipeId} items to list {ListId}", request?.RecipeId, listId);
            return StatusCode(500, new { message = "An error occurred while adding recipe items" });
        }
    }

    // ── Aisle-sorted items ────────────────────────────────────────────────────

    /// <summary>
    /// Get list items sorted by aisle for in-store navigation.
    /// sortMode: Aisle | ColdLast | BackToFront | Category
    /// </summary>
    [HttpGet("{listId}/items/sorted")]
    public async Task<IActionResult> GetSortedItems(
        Guid listId,
        [FromQuery] Guid storeId,
        [FromQuery] string mode = "Aisle",
        CancellationToken ct = default)
    {
        try
        {
            Guid userId = GetUserId();
            ShoppingListDto? list = await _repository.GetShoppingListAsync(listId, userId);
            if (list == null) return NotFound();

            List<OptimizedShoppingItem> items = await _repository.GetItemsSortedByAisleAsync(listId, storeId, mode, ct);
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sorted items for list {ListId}", listId);
            return StatusCode(500, new { message = "An error occurred while retrieving sorted items" });
        }
    }

    // ── Category preferences ──────────────────────────────────────────────────

    /// <summary>
    /// Get user's per-category store preferences.
    /// </summary>
    [HttpGet("preferences/categories")]
    public async Task<IActionResult> GetCategoryPreferences(CancellationToken ct = default)
    {
        try
        {
            Guid userId = GetUserId();
            List<UserStoreCategoryPreferenceDto> prefs = await _repository.GetUserCategoryPreferencesAsync(userId, ct);
            return Ok(prefs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving category preferences");
            return StatusCode(500, new { message = "An error occurred while retrieving preferences" });
        }
    }

    /// <summary>
    /// Upsert (bulk) per-category store preferences.
    /// </summary>
    [HttpPut("preferences/categories")]
    public async Task<IActionResult> UpsertCategoryPreferences(
        [FromBody] List<UserStoreCategoryPreferenceRecord> preferences,
        CancellationToken ct = default)
    {
        try
        {
            Guid userId = GetUserId();
            foreach (UserStoreCategoryPreferenceRecord pref in preferences)
            {
                pref.UserId = userId;
                await _repository.UpsertStoreCategoryPreferenceAsync(pref, ct);
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error upserting category preferences");
            return StatusCode(500, new { message = "An error occurred while saving preferences" });
        }
    }

    /// <summary>
    /// Delete a per-category store preference.
    /// </summary>
    [HttpDelete("preferences/categories/{preferenceId}")]
    public async Task<IActionResult> DeleteCategoryPreference(Guid preferenceId, CancellationToken ct = default)
    {
        try
        {
            Guid userId = GetUserId();
            await _repository.DeleteStoreCategoryPreferenceAsync(preferenceId, userId, ct);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting category preference {PreferenceId}", preferenceId);
            return StatusCode(500, new { message = "An error occurred while deleting the preference" });
        }
    }

    // ── Price search profile ──────────────────────────────────────────────────

    /// <summary>
    /// Get user's price search profile.
    /// </summary>
    [HttpGet("preferences/price-profile")]
    public async Task<IActionResult> GetPriceProfile(CancellationToken ct = default)
    {
        try
        {
            Guid userId = GetUserId();
            UserPriceSearchProfileDto? profile = await _repository.GetPriceSearchProfileAsync(userId, ct);
            if (profile == null) return NotFound(new { message = "No price search profile found." });
            return Ok(profile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving price search profile");
            return StatusCode(500, new { message = "An error occurred while retrieving the price search profile" });
        }
    }

    /// <summary>
    /// Create or update user's price search profile.
    /// </summary>
    [HttpPut("preferences/price-profile")]
    public async Task<IActionResult> UpsertPriceProfile(
        [FromBody] UserPriceSearchProfileRecord profile,
        CancellationToken ct = default)
    {
        try
        {
            Guid userId = GetUserId();
            profile.UserId = userId;
            await _repository.UpsertPriceSearchProfileAsync(profile, ct);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error upserting price search profile");
            return StatusCode(500, new { message = "An error occurred while saving the price search profile" });
        }
    }
}

// ── Request DTOs ──────────────────────────────────────────────────────────────

public record AddFromRecipeRequest(Guid RecipeId, int Servings);
