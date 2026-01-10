using ExpressRecipe.Shared.DTOs.User;
using ExpressRecipe.UserService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Net.Http;
using System.Text.Json;
using System.Text;

namespace ExpressRecipe.UserService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FamilyMembersController : ControllerBase
{
    private readonly IFamilyMemberRepository _repository;
    private readonly IFamilyRelationshipRepository _relationshipRepository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<FamilyMembersController> _logger;

    public FamilyMembersController(
        IFamilyMemberRepository repository,
        IFamilyRelationshipRepository relationshipRepository,
        IHttpClientFactory httpClientFactory,
        ILogger<FamilyMembersController> logger)
    {
        _repository = repository;
        _relationshipRepository = relationshipRepository;
        _httpClientFactory = httpClientFactory;
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

    /// <summary>
    /// Create a family member with a user account and send welcome email
    /// </summary>
    [HttpPost("create-with-account")]
    public async Task<ActionResult<FamilyMemberDto>> CreateWithAccount([FromBody] CreateFamilyMemberWithAccountRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            // Verify current user is admin
            var currentMember = await _repository.GetByUserIdAsync(userId);
            if (currentMember != null && currentMember.UserRole != "Admin")
            {
                return Forbid();
            }

            // Create user account in AuthService
            var authClient = _httpClientFactory.CreateClient("AuthService");
            var createUserRequest = new
            {
                email = request.Email,
                password = request.Password,
                firstName = request.Name.Split(' ').FirstOrDefault() ?? request.Name,
                lastName = request.Name.Split(' ').Skip(1).FirstOrDefault() ?? string.Empty
            };

            var authResponse = await authClient.PostAsJsonAsync("/api/auth/register-internal", createUserRequest);
            
            if (!authResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to create user account: {StatusCode}", authResponse.StatusCode);
                return StatusCode(500, new { message = "Failed to create user account" });
            }

            var authResult = await authResponse.Content.ReadFromJsonAsync<JsonElement>();
            var createdUserId = Guid.Parse(authResult.GetProperty("userId").GetString()!);

            // Create family member with account link
            var familyMemberId = await _repository.CreateWithAccountAsync(userId, request, createdUserId, userId);
            
            // Send welcome email if requested
            if (request.SendWelcomeEmail)
            {
                var notificationClient = _httpClientFactory.CreateClient("NotificationService");
                var emailRequest = new
                {
                    toEmail = request.Email,
                    toName = request.Name,
                    subject = "Welcome to ExpressRecipe!",
                    templateName = "FamilyMemberWelcome",
                    templateData = new
                    {
                        name = request.Name,
                        invitedBy = User.FindFirst(ClaimTypes.Name)?.Value ?? "A family member"
                    }
                };

                await notificationClient.PostAsJsonAsync("/api/notifications/send-email", emailRequest);
            }

            var familyMember = await _repository.GetByIdAsync(familyMemberId);
            _logger.LogInformation("Family member with account {FamilyMemberId} created for user {UserId}", familyMemberId, userId);

            return CreatedAtAction(nameof(GetById), new { id = familyMemberId }, familyMember);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating family member with account");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Dismiss a guest family member (admin only)
    /// </summary>
    [HttpPost("{id:guid}/dismiss")]
    public async Task<ActionResult> DismissGuest(Guid id, [FromBody] DismissGuestRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();

            // Verify current user is admin
            var currentMember = await _repository.GetByUserIdAsync(userId);
            if (currentMember != null && currentMember.UserRole != "Admin")
            {
                return Forbid();
            }

            // Verify the member to dismiss is a guest
            var guestMember = await _repository.GetByIdAsync(id);
            if (guestMember == null)
            {
                return NotFound(new { message = "Family member not found" });
            }

            if (!guestMember.IsGuest)
            {
                return BadRequest(new { message = "Can only dismiss guest members" });
            }

            if (guestMember.PrimaryUserId != userId)
            {
                return Forbid();
            }

            var success = await _repository.DismissGuestAsync(id, userId);

            if (!success)
            {
                return NotFound(new { message = "Family member not found" });
            }

            _logger.LogInformation("Guest family member {FamilyMemberId} dismissed by {UserId}", id, userId);

            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dismissing guest family member");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Get relationships for a family member
    /// </summary>
    [HttpGet("{id:guid}/relationships")]
    public async Task<ActionResult<List<FamilyRelationshipDto>>> GetRelationships(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();

            // Verify ownership
            var familyMember = await _repository.GetByIdAsync(id);
            if (familyMember == null)
            {
                return NotFound(new { message = "Family member not found" });
            }

            if (familyMember.PrimaryUserId != userId)
            {
                return Forbid();
            }

            var relationships = await _relationshipRepository.GetByFamilyMemberIdAsync(id);
            return Ok(relationships);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving relationships");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Create a relationship between family members
    /// </summary>
    [HttpPost("{id:guid}/relationships")]
    public async Task<ActionResult<FamilyRelationshipDto>> CreateRelationship(Guid id, [FromBody] CreateFamilyRelationshipRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();

            // Verify ownership of both family members
            var familyMember1 = await _repository.GetByIdAsync(id);
            var familyMember2 = await _repository.GetByIdAsync(request.FamilyMemberId2);

            if (familyMember1 == null || familyMember2 == null)
            {
                return NotFound(new { message = "Family member not found" });
            }

            if (familyMember1.PrimaryUserId != userId || familyMember2.PrimaryUserId != userId)
            {
                return Forbid();
            }

            var relationshipId = await _relationshipRepository.CreateAsync(id, request, userId);
            var relationship = await _relationshipRepository.GetByIdAsync(relationshipId);

            _logger.LogInformation("Relationship {RelationshipId} created between {Member1} and {Member2}", relationshipId, id, request.FamilyMemberId2);

            return CreatedAtAction(nameof(GetRelationships), new { id }, relationship);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating relationship");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Delete a relationship
    /// </summary>
    [HttpDelete("relationships/{relationshipId:guid}")]
    public async Task<ActionResult> DeleteRelationship(Guid relationshipId)
    {
        try
        {
            var userId = GetCurrentUserId();

            // Verify ownership
            var relationship = await _relationshipRepository.GetByIdAsync(relationshipId);
            if (relationship == null)
            {
                return NotFound(new { message = "Relationship not found" });
            }

            var familyMember = await _repository.GetByIdAsync(relationship.FamilyMemberId);
            if (familyMember == null || familyMember.PrimaryUserId != userId)
            {
                return Forbid();
            }

            var success = await _relationshipRepository.DeleteAsync(relationshipId, userId);

            if (!success)
            {
                return NotFound(new { message = "Relationship not found" });
            }

            _logger.LogInformation("Relationship {RelationshipId} deleted", relationshipId);

            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting relationship");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }
}
