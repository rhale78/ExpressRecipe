using ExpressRecipe.ProductService.Data;
using ExpressRecipe.ProductService.Services;
using ExpressRecipe.Shared.DTOs.Product;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpressRecipe.ProductService.Controllers;

[ApiController]
[Route("api/catalog")]
public class CatalogController : ControllerBase
{
    private readonly IFoodCatalogRepository _catalog;
    private readonly IFoodSubstitutionService _substitutionService;
    private readonly ILogger<CatalogController> _logger;

    public CatalogController(
        IFoodCatalogRepository catalog,
        IFoodSubstitutionService substitutionService,
        ILogger<CatalogController> logger)
    {
        _catalog = catalog;
        _substitutionService = substitutionService;
        _logger = logger;
    }

    // -----------------------------------------------------------------------
    // Food Groups
    // -----------------------------------------------------------------------

    /// <summary>
    /// List all active food groups with optional search and functional-role filter.
    /// </summary>
    [HttpGet("food-groups")]
    [AllowAnonymous]
    public async Task<ActionResult<List<FoodGroupDto>>> GetFoodGroups(
        [FromQuery] string? search = null,
        [FromQuery] string? functionalRole = null,
        CancellationToken ct = default)
    {
        List<FoodGroupDto> groups = await _catalog.GetFoodGroupsAsync(search, functionalRole, ct);
        return Ok(groups);
    }

    /// <summary>
    /// Get a single food group with all its members.
    /// </summary>
    [HttpGet("food-groups/{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<FoodGroupDetailResponse>> GetFoodGroupById(
        Guid id,
        CancellationToken ct = default)
    {
        FoodGroupDto? group = await _catalog.GetFoodGroupByIdAsync(id, ct);
        if (group == null)
        {
            return NotFound();
        }

        List<FoodGroupMemberDto> members = await _catalog.GetFoodGroupMembersAsync(id, ct);

        return Ok(new FoodGroupDetailResponse { Group = group, Members = members });
    }

    /// <summary>
    /// Create a new food group (admin only).
    /// </summary>
    [HttpPost("food-groups")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<Guid>> CreateFoodGroup(
        [FromBody] FoodGroupRecord request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Name is required.");
        }

        Guid id = await _catalog.CreateFoodGroupAsync(request, ct);
        return CreatedAtAction(nameof(GetFoodGroupById), new { id }, id);
    }

    /// <summary>
    /// Add a member to an existing food group (admin only).
    /// </summary>
    [HttpPost("food-groups/{id:guid}/members")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<Guid>> AddFoodGroupMember(
        Guid id,
        [FromBody] FoodGroupMemberRecord request,
        CancellationToken ct = default)
    {
        FoodGroupDto? group = await _catalog.GetFoodGroupByIdAsync(id, ct);
        if (group == null)
        {
            return NotFound();
        }

        FoodGroupMemberRecord member = request with { FoodGroupId = id };
        Guid memberId = await _catalog.AddFoodGroupMemberAsync(member, ct);
        return CreatedAtAction(nameof(GetFoodGroupById), new { id }, memberId);
    }

    // -----------------------------------------------------------------------
    // Substitutes
    // -----------------------------------------------------------------------

    /// <summary>
    /// Get ranked substitute options for a specific ingredient.
    /// </summary>
    [HttpGet("substitutes/{ingredientId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<List<SubstituteOption>>> GetSubstitutes(
        Guid ingredientId,
        [FromQuery] Guid? userId = null,
        [FromQuery] Guid? householdId = null,
        [FromQuery] bool filterAllergens = false,
        CancellationToken ct = default)
    {
        Guid? currentUserId = userId ?? GetCurrentUserId();

        // Disable allergen filtering when no authenticated user is available —
        // calling SafeForkService with an empty userId would produce wrong results.
        bool effectiveFilterAllergens = filterAllergens && currentUserId.HasValue;
        Guid resolvedUserId = currentUserId ?? Guid.Empty;

        List<SubstituteOption> options = await _substitutionService.GetSubstitutesAsync(
            ingredientId, resolvedUserId, householdId, effectiveFilterAllergens, ct);

        return Ok(options);
    }

    // -----------------------------------------------------------------------
    // Substitution History
    // -----------------------------------------------------------------------

    /// <summary>
    /// Record that the authenticated user used a substitute for an ingredient.
    /// </summary>
    [HttpPost("substitution-history")]
    [Authorize]
    public async Task<ActionResult<Guid>> RecordSubstitution(
        [FromBody] SubstitutionHistoryRecord request,
        CancellationToken ct = default)
    {
        Guid? currentUserId = GetCurrentUserId();
        if (currentUserId == null)
        {
            return Unauthorized();
        }

        SubstitutionHistoryRecord record = request with { UserId = currentUserId.Value };
        Guid id = await _catalog.RecordSubstitutionAsync(record, ct);
        return CreatedAtAction(nameof(RecordSubstitution), new { id }, id);
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
}

/// <summary>Response wrapper for food group detail including members.</summary>
public sealed class FoodGroupDetailResponse
{
    public FoodGroupDto Group { get; init; } = null!;
    public List<FoodGroupMemberDto> Members { get; init; } = new();
}
