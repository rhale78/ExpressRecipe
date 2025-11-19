using ExpressRecipe.Shared.DTOs.User;
using ExpressRecipe.UserService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpressRecipe.UserService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AllergensController : ControllerBase
{
    private readonly IAllergenRepository _repository;
    private readonly ILogger<AllergensController> _logger;

    public AllergensController(
        IAllergenRepository repository,
        ILogger<AllergensController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid user ID in token");
        }
        return userId;
    }

    /// <summary>
    /// Get all available allergens
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<AllergenDto>>> GetAll()
    {
        try
        {
            var allergens = await _repository.GetAllAllergensAsync();
            return Ok(allergens);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving allergens");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Get allergen by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AllergenDto>> GetById(Guid id)
    {
        try
        {
            var allergen = await _repository.GetByIdAsync(id);

            if (allergen == null)
            {
                return NotFound(new { message = "Allergen not found" });
            }

            return Ok(allergen);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving allergen {AllergenId}", id);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Search allergens by name
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<List<AllergenDto>>> Search([FromQuery] string q)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return BadRequest(new { message = "Search term is required" });
            }

            var allergens = await _repository.SearchByNameAsync(q);
            return Ok(allergens);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching allergens");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Get current user's allergens
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<List<UserAllergenDto>>> GetMyAllergens()
    {
        try
        {
            var userId = GetCurrentUserId();
            var allergens = await _repository.GetUserAllergensAsync(userId);
            return Ok(allergens);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user allergens");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Add allergen to current user
    /// </summary>
    [HttpPost("me")]
    [Authorize]
    public async Task<ActionResult<UserAllergenDto>> AddMyAllergen([FromBody] AddUserAllergenRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();

            // Verify allergen exists
            var allergen = await _repository.GetByIdAsync(request.AllergenId);
            if (allergen == null)
            {
                return NotFound(new { message = "Allergen not found" });
            }

            var userAllergenId = await _repository.AddUserAllergenAsync(userId, request, userId);
            var userAllergens = await _repository.GetUserAllergensAsync(userId);
            var addedAllergen = userAllergens.FirstOrDefault(ua => ua.Id == userAllergenId);

            _logger.LogInformation("Allergen {AllergenId} added to user {UserId}", request.AllergenId, userId);

            return CreatedAtAction(nameof(GetMyAllergens), addedAllergen);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding user allergen");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Update user allergen
    /// </summary>
    [HttpPut("me/{userAllergenId:guid}")]
    [Authorize]
    public async Task<ActionResult> UpdateMyAllergen(Guid userAllergenId, [FromBody] UpdateUserAllergenRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var success = await _repository.UpdateUserAllergenAsync(userAllergenId, request, userId);

            if (!success)
            {
                return NotFound(new { message = "User allergen not found" });
            }

            _logger.LogInformation("User allergen {UserAllergenId} updated", userAllergenId);

            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user allergen");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Remove allergen from current user
    /// </summary>
    [HttpDelete("me/{userAllergenId:guid}")]
    [Authorize]
    public async Task<ActionResult> RemoveMyAllergen(Guid userAllergenId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var success = await _repository.RemoveUserAllergenAsync(userAllergenId, userId);

            if (!success)
            {
                return NotFound(new { message = "User allergen not found" });
            }

            _logger.LogInformation("User allergen {UserAllergenId} removed", userAllergenId);

            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing user allergen");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }
}
