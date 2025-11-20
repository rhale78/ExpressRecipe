using ExpressRecipe.Shared.DTOs.User;
using ExpressRecipe.UserService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpressRecipe.UserService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FamilyMembersController : ControllerBase
{
    private readonly IFamilyMemberRepository _repository;
    private readonly ILogger<FamilyMembersController> _logger;

    public FamilyMembersController(
        IFamilyMemberRepository repository,
        ILogger<FamilyMembersController> logger)
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
    /// Get all family members for current user
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<FamilyMemberDto>>> GetMyFamilyMembers()
    {
        try
        {
            var userId = GetCurrentUserId();
            var familyMembers = await _repository.GetByPrimaryUserIdAsync(userId);
            return Ok(familyMembers);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving family members");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Get family member by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FamilyMemberDto>> GetById(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var familyMember = await _repository.GetByIdAsync(id);

            if (familyMember == null)
            {
                return NotFound(new { message = "Family member not found" });
            }

            // Ensure the family member belongs to the current user
            if (familyMember.PrimaryUserId != userId)
            {
                return Forbid();
            }

            return Ok(familyMember);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving family member {FamilyMemberId}", id);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Create a new family member
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<FamilyMemberDto>> Create([FromBody] CreateFamilyMemberRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var familyMemberId = await _repository.CreateAsync(userId, request, userId);
            var familyMember = await _repository.GetByIdAsync(familyMemberId);

            _logger.LogInformation("Family member {FamilyMemberId} created for user {UserId}", familyMemberId, userId);

            return CreatedAtAction(nameof(GetById), new { id = familyMemberId }, familyMember);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating family member");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Update a family member
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<FamilyMemberDto>> Update(Guid id, [FromBody] UpdateFamilyMemberRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();

            // Verify ownership
            var existingMember = await _repository.GetByIdAsync(id);
            if (existingMember == null)
            {
                return NotFound(new { message = "Family member not found" });
            }

            if (existingMember.PrimaryUserId != userId)
            {
                return Forbid();
            }

            var success = await _repository.UpdateAsync(id, request, userId);

            if (!success)
            {
                return NotFound(new { message = "Family member not found" });
            }

            var familyMember = await _repository.GetByIdAsync(id);
            _logger.LogInformation("Family member {FamilyMemberId} updated", id);

            return Ok(familyMember);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating family member");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Delete a family member
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();

            // Verify ownership
            var existingMember = await _repository.GetByIdAsync(id);
            if (existingMember == null)
            {
                return NotFound(new { message = "Family member not found" });
            }

            if (existingMember.PrimaryUserId != userId)
            {
                return Forbid();
            }

            var success = await _repository.DeleteAsync(id, userId);

            if (!success)
            {
                return NotFound(new { message = "Family member not found" });
            }

            _logger.LogInformation("Family member {FamilyMemberId} deleted", id);

            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting family member");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }
}
