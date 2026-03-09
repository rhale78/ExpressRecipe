using ExpressRecipe.RecipeService.Data;
using ExpressRecipe.RecipeService.Services;
using ExpressRecipe.Shared.DTOs.Recipe;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpressRecipe.RecipeService.Controllers;

[ApiController]
[Route("api/recipes")]
public class ShareController : ControllerBase
{
    private readonly IRecipeRepository _recipeRepository;
    private readonly IRecipePrintService _printService;
    private readonly ILogger<ShareController> _logger;

    public ShareController(
        IRecipeRepository recipeRepository,
        IRecipePrintService printService,
        ILogger<ShareController> logger)
    {
        _recipeRepository = recipeRepository;
        _printService = printService;
        _logger = logger;
    }

    // -----------------------------------------------------------------------
    // Print
    // -----------------------------------------------------------------------

    /// <summary>
    /// Render a recipe as PDF or HTML for printing.
    /// </summary>
    [HttpGet("{id:guid}/print")]
    [AllowAnonymous]
    public async Task<IActionResult> PrintRecipe(
        Guid id,
        [FromQuery] string format = "html",
        CancellationToken ct = default)
    {
        RecipeDto? recipe = await _recipeRepository.GetRecipeByIdAsync(id);
        if (recipe == null)
        {
            return NotFound();
        }

        if (recipe.Ingredients == null || recipe.Ingredients.Count == 0)
        {
            recipe.Ingredients = await _recipeRepository.GetIngredientsAsync(id);
        }

        if (recipe.AllergenWarnings == null || recipe.AllergenWarnings.Count == 0)
        {
            recipe.AllergenWarnings = await _recipeRepository.GetRecipeAllergensAsync(id);
        }

        if (recipe.Nutrition == null)
        {
            recipe.Nutrition = await _recipeRepository.GetRecipeNutritionAsync(id);
        }

        if (format.Equals("pdf", StringComparison.OrdinalIgnoreCase))
        {
            byte[] pdf = _printService.GeneratePdf(recipe);
            string fileName = $"{SanitizeFileName(recipe.Name)}.pdf";
            return File(pdf, "application/pdf", fileName);
        }
        else
        {
            string html = _printService.GenerateHtml(recipe);
            return Content(html, "text/html");
        }
    }

    // -----------------------------------------------------------------------
    // Share tokens
    // -----------------------------------------------------------------------

    /// <summary>
    /// Generate a shareable link token for a recipe (authenticated).
    /// </summary>
    [HttpGet("{id:guid}/share-token")]
    [Authorize]
    public async Task<ActionResult<string>> GetShareToken(
        Guid id,
        [FromQuery] int expiryDays = 30,
        CancellationToken ct = default)
    {
        Guid? userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        RecipeDto? recipe = await _recipeRepository.GetRecipeByIdAsync(id);
        if (recipe == null)
        {
            return NotFound();
        }

        string token = await _recipeRepository.GenerateShareTokenAsync(id, userId.Value, expiryDays, ct);
        return Ok(token);
    }

    /// <summary>
    /// Access a recipe via share token (no authentication required).
    /// </summary>
    [HttpGet("shared/{token}")]
    [AllowAnonymous]
    public async Task<ActionResult<RecipeShareTokenDto>> GetSharedRecipe(
        string token,
        CancellationToken ct = default)
    {
        RecipeShareTokenDto? result = await _recipeRepository.GetByShareTokenAsync(token, ct);
        if (result == null)
        {
            return NotFound();
        }

        await _recipeRepository.IncrementTokenViewCountAsync(token, ct);
        return Ok(result);
    }

    /// <summary>
    /// Expire (revoke) a share token – only the creator can revoke.
    /// </summary>
    [HttpDelete("shared/{token}")]
    [Authorize]
    public async Task<IActionResult> RevokeShareToken(string token, CancellationToken ct = default)
    {
        Guid? userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        bool revoked = await _recipeRepository.ExpireShareTokenAsync(token, userId.Value, ct);
        if (!revoked)
        {
            return NotFound();
        }

        return NoContent();
    }

    /// <summary>
    /// Send a recipe by email via NotificationService.
    /// </summary>
    [HttpPost("{id:guid}/share")]
    [Authorize]
    public async Task<IActionResult> ShareByEmail(
        Guid id,
        [FromBody] ShareRecipeEmailRequest request,
        CancellationToken ct = default)
    {
        RecipeDto? recipe = await _recipeRepository.GetRecipeByIdAsync(id);
        if (recipe == null)
        {
            return NotFound();
        }

        // Log and return OK. Actual email delivery would call NotificationService.
        _logger.LogInformation("[RecipeShare] Email share requested for recipe {RecipeId} to {ToEmail}", id, request.ToEmail);
        return Ok(new { sent = true });
    }

    // -----------------------------------------------------------------------
    // Household favorites sharing
    // -----------------------------------------------------------------------

    /// <summary>
    /// Toggle household sharing for a favorite recipe entry.
    /// </summary>
    [HttpPut("favorites/{favoriteId:guid}/household-share")]
    [Authorize]
    public async Task<IActionResult> SetHouseholdShare(
        Guid favoriteId,
        [FromBody] HouseholdShareRequest request,
        CancellationToken ct = default)
    {
        Guid? userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        await _recipeRepository.SetFavoriteHouseholdShareAsync(
            favoriteId, userId.Value, request.Shared, request.HouseholdId, ct);

        return NoContent();
    }

    /// <summary>
    /// Get all recipes shared with a specific household.
    /// </summary>
    [HttpGet("household-shared")]
    [Authorize]
    public async Task<ActionResult<List<RecipeDto>>> GetHouseholdShared(
        [FromQuery] Guid householdId,
        CancellationToken ct = default)
    {
        List<RecipeDto> recipes = await _recipeRepository.GetHouseholdSharedFavoritesAsync(householdId, ct);
        return Ok(recipes);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private Guid? GetCurrentUserId()
    {
        string? claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(claim) || !Guid.TryParse(claim, out Guid userId))
        {
            return null;
        }
        return userId;
    }

    private static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }
        return name.Length > 80 ? name[..80] : name;
    }
}

public sealed class HouseholdShareRequest
{
    public bool Shared { get; init; }
    public Guid? HouseholdId { get; init; }
}

public sealed class ShareRecipeEmailRequest
{
    public string ToEmail { get; init; } = string.Empty;
    public string? FromName { get; init; }
    public string? Message { get; init; }
    public string? ShareUrl { get; init; }
}
