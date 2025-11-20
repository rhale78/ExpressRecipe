using ExpressRecipe.Shared.DTOs.User;
using ExpressRecipe.UserService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpressRecipe.UserService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AllergyManagementController : ControllerBase
{
    private readonly IEnhancedAllergenRepository _allergenRepository;
    private readonly ILogger<AllergyManagementController> _logger;

    public AllergyManagementController(
        IEnhancedAllergenRepository allergenRepository,
        ILogger<AllergyManagementController> logger)
    {
        _allergenRepository = allergenRepository;
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
    /// Get comprehensive allergen summary for the user
    /// </summary>
    [HttpGet("summary")]
    public async Task<ActionResult<UserAllergenSummaryDto>> GetSummary()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var summary = await _allergenRepository.GetAllergenSummaryAsync(userId.Value);
            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving allergen summary");
            return StatusCode(500, new { message = "An error occurred while retrieving your allergen summary" });
        }
    }

    /// <summary>
    /// Get all reaction types
    /// </summary>
    [HttpGet("reaction-types")]
    [AllowAnonymous]
    public async Task<ActionResult<List<AllergenReactionTypeDto>>> GetReactionTypes([FromQuery] bool activeOnly = true)
    {
        try
        {
            var types = await _allergenRepository.GetReactionTypesAsync(activeOnly);
            return Ok(types);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving reaction types");
            return StatusCode(500, new { message = "An error occurred while retrieving reaction types" });
        }
    }

    // Ingredient Allergies

    /// <summary>
    /// Get user's ingredient-specific allergies
    /// </summary>
    [HttpGet("ingredient-allergies")]
    public async Task<ActionResult<List<UserIngredientAllergyDto>>> GetIngredientAllergies([FromQuery] bool includeReactions = true)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var allergies = await _allergenRepository.GetUserIngredientAllergiesAsync(userId.Value, includeReactions);
            return Ok(allergies);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ingredient allergies");
            return StatusCode(500, new { message = "An error occurred while retrieving your ingredient allergies" });
        }
    }

    /// <summary>
    /// Get specific ingredient allergy details
    /// </summary>
    [HttpGet("ingredient-allergies/{id:guid}")]
    public async Task<ActionResult<UserIngredientAllergyDto>> GetIngredientAllergy(Guid id, [FromQuery] bool includeReactions = true)
    {
        try
        {
            var allergy = await _allergenRepository.GetIngredientAllergyByIdAsync(id, includeReactions);

            if (allergy == null)
            {
                return NotFound(new { message = "Ingredient allergy not found" });
            }

            // Verify ownership
            var userId = GetCurrentUserId();
            if (allergy.UserId != userId)
            {
                return Forbid();
            }

            return Ok(allergy);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ingredient allergy {AllergyId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving the ingredient allergy" });
        }
    }

    /// <summary>
    /// Add a new ingredient-specific allergy
    /// </summary>
    [HttpPost("ingredient-allergies")]
    public async Task<ActionResult<Guid>> CreateIngredientAllergy([FromBody] CreateUserIngredientAllergyRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var allergyId = await _allergenRepository.CreateIngredientAllergyAsync(userId.Value, request);

            return CreatedAtAction(nameof(GetIngredientAllergy), new { id = allergyId }, new { id = allergyId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating ingredient allergy");
            return StatusCode(500, new { message = "An error occurred while creating the ingredient allergy" });
        }
    }

    /// <summary>
    /// Update an ingredient allergy
    /// </summary>
    [HttpPut("ingredient-allergies/{id:guid}")]
    public async Task<ActionResult> UpdateIngredientAllergy(Guid id, [FromBody] UpdateUserIngredientAllergyRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var success = await _allergenRepository.UpdateIngredientAllergyAsync(id, userId.Value, request);

            if (!success)
            {
                return NotFound(new { message = "Ingredient allergy not found or could not be updated" });
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating ingredient allergy {AllergyId}", id);
            return StatusCode(500, new { message = "An error occurred while updating the ingredient allergy" });
        }
    }

    /// <summary>
    /// Delete an ingredient allergy
    /// </summary>
    [HttpDelete("ingredient-allergies/{id:guid}")]
    public async Task<ActionResult> DeleteIngredientAllergy(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var success = await _allergenRepository.DeleteIngredientAllergyAsync(id, userId.Value);

            if (!success)
            {
                return NotFound(new { message = "Ingredient allergy not found" });
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting ingredient allergy {AllergyId}", id);
            return StatusCode(500, new { message = "An error occurred while deleting the ingredient allergy" });
        }
    }

    // Allergy Incidents

    /// <summary>
    /// Get user's allergy incident history
    /// </summary>
    [HttpGet("incidents")]
    public async Task<ActionResult<List<AllergyIncidentDto>>> GetIncidents([FromQuery] int limit = 50)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            if (limit <= 0 || limit > 200)
            {
                return BadRequest(new { message = "Limit must be between 1 and 200" });
            }

            var incidents = await _allergenRepository.GetUserIncidentsAsync(userId.Value, limit);
            return Ok(incidents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving allergy incidents");
            return StatusCode(500, new { message = "An error occurred while retrieving your allergy incidents" });
        }
    }

    /// <summary>
    /// Get specific incident details
    /// </summary>
    [HttpGet("incidents/{id:guid}")]
    public async Task<ActionResult<AllergyIncidentDto>> GetIncident(Guid id)
    {
        try
        {
            var incident = await _allergenRepository.GetIncidentByIdAsync(id);

            if (incident == null)
            {
                return NotFound(new { message = "Incident not found" });
            }

            // Verify ownership
            var userId = GetCurrentUserId();
            if (incident.UserId != userId)
            {
                return Forbid();
            }

            return Ok(incident);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving incident {IncidentId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving the incident" });
        }
    }

    /// <summary>
    /// Record a new allergy incident (IMPORTANT: Track reactions for safety)
    /// </summary>
    [HttpPost("incidents")]
    public async Task<ActionResult<Guid>> CreateIncident([FromBody] CreateAllergyIncidentRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var incidentId = await _allergenRepository.CreateIncidentAsync(userId.Value, request);

            _logger.LogWarning("Allergy incident recorded for user {UserId}: Severity={Severity}, EpiPenUsed={EpiPen}, Hospital={Hospital}",
                userId.Value, request.SeverityLevel, request.EpiPenUsed, request.HospitalVisit);

            return CreatedAtAction(nameof(GetIncident), new { id = incidentId }, new { id = incidentId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating allergy incident");
            return StatusCode(500, new { message = "An error occurred while creating the incident record" });
        }
    }
}
