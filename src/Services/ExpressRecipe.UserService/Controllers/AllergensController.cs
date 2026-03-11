using ExpressRecipe.Shared.DTOs.User;
using ExpressRecipe.UserService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace ExpressRecipe.UserService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AllergensController : ControllerBase
{
    private readonly IAllergenRepository _repository;
    private readonly IEnhancedAllergenRepository _enhancedRepository;
    private readonly IFamilyMemberRepository _familyMemberRepository;
    private readonly IConfiguration? _configuration;
    private readonly ILogger<AllergensController> _logger;

    public AllergensController(
        IAllergenRepository repository,
        IEnhancedAllergenRepository enhancedRepository,
        IFamilyMemberRepository familyMemberRepository,
        ILogger<AllergensController> logger,
        IConfiguration? configuration = null)
    {
        _repository = repository;
        _enhancedRepository = enhancedRepository;
        _familyMemberRepository = familyMemberRepository;
        _configuration = configuration;
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

    /// <summary>
    /// Internal service-to-service endpoint: returns all allergen names for all family members
    /// belonging to the household identified by <paramref name="householdId"/>.
    /// Used by PantryDiscovery to filter unsafe recipes without authenticating on behalf of a user.
    /// <c>userId</c> is the primary account holder for the household.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("/api/users/{userId:guid}/household/{householdId:guid}/allergen-names")]
    public async Task<ActionResult> GetHouseholdAllergenNames(Guid userId, Guid householdId)
    {
        string? configuredKey = _configuration?["InternalApi:Key"];
        if (!string.IsNullOrEmpty(configuredKey))
        {
            string? providedKey = Request.Headers["X-Internal-Api-Key"].FirstOrDefault();
            if (!IsValidApiKey(providedKey, configuredKey))
                return Unauthorized(new { error = "Invalid or missing X-Internal-Api-Key header" });
        }

        try
        {
            // Collect allergens from the primary user
            var primaryAllergens = await _enhancedRepository.GetUserAllergensAsync(userId, includeReactions: false);
            var allergenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var a in primaryAllergens)
            {
                if (!string.IsNullOrWhiteSpace(a.AllergenName))
                    allergenNames.Add(a.AllergenName.Trim());
            }

            // Collect allergens from all linked family members
            var familyMembers = await _familyMemberRepository.GetByPrimaryUserIdAsync(userId);
            foreach (var member in familyMembers)
            {
                foreach (var name in member.Allergens)
                {
                    if (!string.IsNullOrWhiteSpace(name))
                        allergenNames.Add(name.Trim());
                }

                // If the family member has a linked user account, fetch their stored allergens too
                if (member.UserId.HasValue)
                {
                    var memberAllergens = await _enhancedRepository.GetUserAllergensAsync(member.UserId.Value, includeReactions: false);
                    foreach (var a in memberAllergens)
                    {
                        if (!string.IsNullOrWhiteSpace(a.AllergenName))
                            allergenNames.Add(a.AllergenName.Trim());
                    }
                }
            }

            return Ok(new { UserId = userId, AllergenNames = allergenNames.OrderBy(n => n).ToList() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving household allergen names for user {UserId} household {HouseholdId}", userId, householdId);
            return StatusCode(500, new { message = "An error occurred while retrieving household allergen names" });
        }
    }

    private static bool IsValidApiKey(string? provided, string configured)
    {
        if (provided is null) return false;
        byte[] a = Encoding.UTF8.GetBytes(provided);
        byte[] b = Encoding.UTF8.GetBytes(configured);
        if (a.Length != b.Length)
        {
            byte[] padded = new byte[Math.Max(a.Length, b.Length)];
            Buffer.BlockCopy(a.Length < b.Length ? a : b, 0, padded, 0, Math.Min(a.Length, b.Length));
            if (a.Length < b.Length) { a = padded; } else { b = padded; }
        }
        return CryptographicOperations.FixedTimeEquals(a, b);
    }
}
