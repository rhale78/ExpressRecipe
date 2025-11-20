using ExpressRecipe.Shared.DTOs.Product;
using ExpressRecipe.ProductService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpressRecipe.ProductService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IngredientsController : ControllerBase
{
    private readonly IIngredientRepository _repository;
    private readonly ILogger<IngredientsController> _logger;

    public IngredientsController(
        IIngredientRepository repository,
        ILogger<IngredientsController> logger)
    {
        _repository = repository;
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
    /// Get all ingredients
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<IngredientDto>>> GetAll()
    {
        try
        {
            var ingredients = await _repository.GetAllAsync();
            return Ok(ingredients);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ingredients");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Get ingredient by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<IngredientDto>> GetById(Guid id)
    {
        try
        {
            var ingredient = await _repository.GetByIdAsync(id);

            if (ingredient == null)
            {
                return NotFound(new { message = "Ingredient not found" });
            }

            return Ok(ingredient);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ingredient {IngredientId}", id);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Search ingredients by name
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<List<IngredientDto>>> Search([FromQuery] string q)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return BadRequest(new { message = "Search term is required" });
            }

            var ingredients = await _repository.SearchByNameAsync(q);
            return Ok(ingredients);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching ingredients");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Get ingredients by category
    /// </summary>
    [HttpGet("category/{category}")]
    public async Task<ActionResult<List<IngredientDto>>> GetByCategory(string category)
    {
        try
        {
            var ingredients = await _repository.GetByCategoryAsync(category);
            return Ok(ingredients);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ingredients by category {Category}", category);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Create a new ingredient
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<IngredientDto>> Create([FromBody] CreateIngredientRequest request)
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

            _logger.LogInformation("Ingredient {IngredientId} created by user {UserId}", ingredientId, userId);

            return CreatedAtAction(nameof(GetById), new { id = ingredientId }, ingredient);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating ingredient");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Update an ingredient
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize]
    public async Task<ActionResult<IngredientDto>> Update(Guid id, [FromBody] UpdateIngredientRequest request)
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
                return NotFound(new { message = "Ingredient not found" });
            }

            var ingredient = await _repository.GetByIdAsync(id);

            _logger.LogInformation("Ingredient {IngredientId} updated by user {UserId}", id, userId);

            return Ok(ingredient);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating ingredient {IngredientId}", id);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Delete an ingredient
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
                return NotFound(new { message = "Ingredient not found" });
            }

            _logger.LogInformation("Ingredient {IngredientId} deleted by user {UserId}", id, userId);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting ingredient {IngredientId}", id);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }
}
