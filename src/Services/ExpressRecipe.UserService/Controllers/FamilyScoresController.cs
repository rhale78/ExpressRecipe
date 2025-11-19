using ExpressRecipe.Shared.DTOs.User;
using ExpressRecipe.UserService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpressRecipe.UserService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FamilyScoresController : ControllerBase
{
    private readonly IFamilyScoreRepository _familyScoreRepository;
    private readonly ILogger<FamilyScoresController> _logger;

    public FamilyScoresController(
        IFamilyScoreRepository familyScoreRepository,
        ILogger<FamilyScoresController> logger)
    {
        _familyScoreRepository = familyScoreRepository;
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
    /// Get user's family scores
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<FamilyScoreDto>>> GetScores(
        [FromQuery] string? entityType = null,
        [FromQuery] bool? favoritesOnly = null)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var scores = await _familyScoreRepository.GetUserFamilyScoresAsync(userId.Value, entityType, favoritesOnly);
            return Ok(scores);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving family scores");
            return StatusCode(500, new { message = "An error occurred while retrieving family scores" });
        }
    }

    /// <summary>
    /// Get family score for a specific entity
    /// </summary>
    [HttpGet("{entityType}/{entityId:guid}")]
    public async Task<ActionResult<FamilyScoreDto>> GetScore(string entityType, Guid entityId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var score = await _familyScoreRepository.GetFamilyScoreAsync(userId.Value, entityType, entityId);

            if (score == null)
            {
                return NotFound(new { message = "Family score not found" });
            }

            return Ok(score);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving family score for {EntityType} {EntityId}", entityType, entityId);
            return StatusCode(500, new { message = "An error occurred while retrieving the family score" });
        }
    }

    /// <summary>
    /// Get user's favorites
    /// </summary>
    [HttpGet("favorites")]
    public async Task<ActionResult<List<FamilyScoreDto>>> GetFavorites([FromQuery] string? entityType = null)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var favorites = await _familyScoreRepository.GetFavoritesAsync(userId.Value, entityType);
            return Ok(favorites);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving favorites");
            return StatusCode(500, new { message = "An error occurred while retrieving your favorites" });
        }
    }

    /// <summary>
    /// Get user's blacklisted items
    /// </summary>
    [HttpGet("blacklisted")]
    public async Task<ActionResult<List<FamilyScoreDto>>> GetBlacklisted([FromQuery] string? entityType = null)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var blacklisted = await _familyScoreRepository.GetBlacklistedAsync(userId.Value, entityType);
            return Ok(blacklisted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving blacklisted items");
            return StatusCode(500, new { message = "An error occurred while retrieving your blacklisted items" });
        }
    }

    /// <summary>
    /// Create a family score
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Guid>> CreateScore([FromBody] CreateFamilyScoreRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var scoreId = await _familyScoreRepository.CreateFamilyScoreAsync(userId.Value, request);

            return CreatedAtAction(
                nameof(GetScore),
                new { entityType = request.EntityType, entityId = request.EntityId },
                new { id = scoreId });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to create family score");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating family score");
            return StatusCode(500, new { message = "An error occurred while creating the family score" });
        }
    }

    /// <summary>
    /// Update a family score
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult> UpdateScore(Guid id, [FromBody] UpdateFamilyScoreRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var success = await _familyScoreRepository.UpdateFamilyScoreAsync(id, userId.Value, request);

            if (!success)
            {
                return NotFound(new { message = "Family score not found or could not be updated" });
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating family score {FamilyScoreId}", id);
            return StatusCode(500, new { message = "An error occurred while updating the family score" });
        }
    }

    /// <summary>
    /// Delete a family score
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteScore(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var success = await _familyScoreRepository.DeleteFamilyScoreAsync(id, userId.Value);

            if (!success)
            {
                return NotFound(new { message = "Family score not found" });
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting family score {FamilyScoreId}", id);
            return StatusCode(500, new { message = "An error occurred while deleting the family score" });
        }
    }

    /// <summary>
    /// Add a member score
    /// </summary>
    [HttpPost("{familyScoreId:guid}/members")]
    public async Task<ActionResult<Guid>> AddMemberScore(
        Guid familyScoreId,
        [FromBody] CreateFamilyMemberScoreRequest request)
    {
        try
        {
            var memberScoreId = await _familyScoreRepository.AddMemberScoreAsync(
                familyScoreId,
                request.FamilyMemberId,
                request.IndividualScore,
                request.Notes);

            return Ok(new { id = memberScoreId, message = "Member score added successfully" });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid member score value");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding member score");
            return StatusCode(500, new { message = "An error occurred while adding the member score" });
        }
    }

    /// <summary>
    /// Update a member score
    /// </summary>
    [HttpPut("members/{memberScoreId:guid}")]
    public async Task<ActionResult> UpdateMemberScore(
        Guid memberScoreId,
        [FromBody] UpdateFamilyMemberScoreRequest request)
    {
        try
        {
            var success = await _familyScoreRepository.UpdateMemberScoreAsync(memberScoreId, request);

            if (!success)
            {
                return NotFound(new { message = "Member score not found" });
            }

            return NoContent();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid member score value");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating member score {MemberScoreId}", memberScoreId);
            return StatusCode(500, new { message = "An error occurred while updating the member score" });
        }
    }

    /// <summary>
    /// Delete a member score
    /// </summary>
    [HttpDelete("members/{memberScoreId:guid}")]
    public async Task<ActionResult> DeleteMemberScore(Guid memberScoreId)
    {
        try
        {
            var success = await _familyScoreRepository.DeleteMemberScoreAsync(memberScoreId);

            if (!success)
            {
                return NotFound(new { message = "Member score not found" });
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting member score {MemberScoreId}", memberScoreId);
            return StatusCode(500, new { message = "An error occurred while deleting the member score" });
        }
    }

    /// <summary>
    /// Get member scores for a family score
    /// </summary>
    [HttpGet("{familyScoreId:guid}/members")]
    public async Task<ActionResult<List<FamilyMemberScoreDto>>> GetMemberScores(Guid familyScoreId)
    {
        try
        {
            var memberScores = await _familyScoreRepository.GetMemberScoresAsync(familyScoreId);
            return Ok(memberScores);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving member scores for {FamilyScoreId}", familyScoreId);
            return StatusCode(500, new { message = "An error occurred while retrieving member scores" });
        }
    }
}
