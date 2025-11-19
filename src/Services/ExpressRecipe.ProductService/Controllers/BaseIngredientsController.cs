using ExpressRecipe.Shared.DTOs.Product;
using ExpressRecipe.ProductService.Data;
using ExpressRecipe.ProductService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpressRecipe.ProductService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BaseIngredientsController : ControllerBase
{
    private readonly IBaseIngredientRepository _repository;
    private readonly IIngredientParser _parser;
    private readonly ILogger<BaseIngredientsController> _logger;

    public BaseIngredientsController(
        IBaseIngredientRepository repository,
        IIngredientParser parser,
        ILogger<BaseIngredientsController> logger)
    {
        _repository = repository;
        _parser = parser;
        _logger = logger;
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
    /// Search for base ingredients
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<List<BaseIngredientDto>>> Search([FromQuery] BaseIngredientSearchRequest request)
    {
        try
        {
            var ingredients = await _repository.SearchAsync(request);
            return Ok(ingredients);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching base ingredients");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Get base ingredient by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BaseIngredientDto>> GetById(Guid id)
    {
        try
        {
            var ingredient = await _repository.GetByIdAsync(id);

            if (ingredient == null)
            {
                return NotFound(new { message = "Base ingredient not found" });
            }

            return Ok(ingredient);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving base ingredient {IngredientId}", id);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Get base ingredients by category
    /// </summary>
    [HttpGet("category/{category}")]
    public async Task<ActionResult<List<BaseIngredientDto>>> GetByCategory(string category)
    {
        try
        {
            var ingredients = await _repository.GetByCategoryAsync(category);
            return Ok(ingredients);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving base ingredients by category {Category}", category);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Get all allergen base ingredients
    /// </summary>
    [HttpGet("allergens")]
    public async Task<ActionResult<List<BaseIngredientDto>>> GetAllergens()
    {
        try
        {
            var allergens = await _repository.GetAllergensAsync();
            return Ok(allergens);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving allergen base ingredients");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Get all additive base ingredients
    /// </summary>
    [HttpGet("additives")]
    public async Task<ActionResult<List<BaseIngredientDto>>> GetAdditives()
    {
        try
        {
            var additives = await _repository.GetAdditivesAsync();
            return Ok(additives);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving additive base ingredients");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Create a new base ingredient
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<BaseIngredientDto>> Create([FromBody] CreateBaseIngredientRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var ingredientId = await _repository.CreateAsync(request, userId.Value);
            var ingredient = await _repository.GetByIdAsync(ingredientId);

            _logger.LogInformation("Base ingredient {IngredientId} created by user {UserId}", ingredientId, userId);

            return CreatedAtAction(nameof(GetById), new { id = ingredientId }, ingredient);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating base ingredient");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Update a base ingredient
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize]
    public async Task<ActionResult<BaseIngredientDto>> Update(Guid id, [FromBody] UpdateBaseIngredientRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var success = await _repository.UpdateAsync(id, request, userId.Value);

            if (!success)
            {
                return NotFound(new { message = "Base ingredient not found" });
            }

            var ingredient = await _repository.GetByIdAsync(id);

            _logger.LogInformation("Base ingredient {IngredientId} updated by user {UserId}", id, userId);

            return Ok(ingredient);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating base ingredient {IngredientId}", id);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Delete a base ingredient
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<ActionResult> Delete(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var success = await _repository.DeleteAsync(id, userId.Value);

            if (!success)
            {
                return NotFound(new { message = "Base ingredient not found" });
            }

            _logger.LogInformation("Base ingredient {IngredientId} deleted by user {UserId}", id, userId);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting base ingredient {IngredientId}", id);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Approve or reject a base ingredient (admin only)
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize] // TODO: Add admin role check
    public async Task<ActionResult> Approve(Guid id, [FromQuery] bool approve, [FromQuery] string? rejectionReason = null)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var success = await _repository.ApproveAsync(id, approve, userId.Value, rejectionReason);

            if (!success)
            {
                return NotFound(new { message = "Base ingredient not found" });
            }

            _logger.LogInformation(
                "Base ingredient {IngredientId} {Action} by user {UserId}",
                id,
                approve ? "approved" : "rejected",
                userId);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving base ingredient {IngredientId}", id);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Parse an ingredient string into base components
    /// </summary>
    [HttpPost("parse")]
    public async Task<ActionResult<ParsedIngredientResult>> ParseIngredientString([FromBody] string ingredientString)
    {
        try
        {
            var result = await _parser.ParseIngredientStringAsync(ingredientString);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing ingredient string");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }
}
