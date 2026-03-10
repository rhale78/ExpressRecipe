using ExpressRecipe.Shared.Services.FeatureGates;
using ExpressRecipe.UserService.Data;
using ExpressRecipe.UserService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;

namespace ExpressRecipe.UserService.Controllers;

/// <summary>
/// Admin API for managing feature flags and per-user overrides.
/// Also exposes an internal (unauthenticated) endpoint used by
/// <c>HttpFeatureFlagService</c> in other microservices.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FeatureFlagsController : ControllerBase
{
    private readonly IFeatureFlagRepository _repo;
    private readonly FeatureFlagService _service;
    private readonly ILogger<FeatureFlagsController> _logger;

    public FeatureFlagsController(
        IFeatureFlagRepository repo,
        FeatureFlagService service,
        ILogger<FeatureFlagsController> logger)
    {
        _repo    = repo;
        _service = service;
        _logger  = logger;
    }

    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    // ── Internal endpoints (called by HttpFeatureFlagService in other services) ──────

    /// <summary>
    /// Checks whether a feature is enabled for the given user/tier.
    /// Intentionally unauthenticated — called service-to-service.
    /// </summary>
    [HttpGet("check")]
    [AllowAnonymous]
    public async Task<IActionResult> CheckFeature(
        [FromQuery] string featureKey,
        [FromQuery] Guid userId,
        [FromQuery] string userTier = "Free",
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(featureKey))
            return BadRequest("featureKey is required");

        bool enabled = await _service.IsEnabledAsync(featureKey, userId, userTier, ct);
        return Ok(new { IsEnabled = enabled });
    }

    /// <summary>
    /// Returns whether the global admin toggle for a feature key is on.
    /// Intentionally unauthenticated — called service-to-service.
    /// </summary>
    [HttpGet("{featureKey}/isglobal")]
    [AllowAnonymous]
    public async Task<IActionResult> IsGlobal(string featureKey, CancellationToken ct)
    {
        bool enabled = await _service.IsGloballyEnabledAsync(featureKey, ct);
        return Ok(new { IsEnabled = enabled });
    }

    // ── Admin endpoints ──────────────────────────────────────────────────────

    /// <summary>Returns all feature flags (admin).</summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<List<FeatureFlagDto>>> GetAll(CancellationToken ct)
    {
        var flags = await _repo.GetAllFlagsAsync(ct);
        return Ok(flags);
    }

    /// <summary>Creates or updates a feature flag (admin).</summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Upsert([FromBody] FeatureFlagDto flag, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(flag.FeatureKey))
            return BadRequest("FeatureKey is required");

        await _repo.UpsertFlagAsync(flag, ct);
        _service.InvalidateCache(flag.FeatureKey);
        _logger.LogInformation("Feature flag {FeatureKey} upserted by admin", flag.FeatureKey);
        return Ok();
    }

    /// <summary>
    /// Partial update of a feature flag — supports inline toggle from the admin UI.
    /// Only fields present in the request body are updated.
    /// </summary>
    [HttpPatch("{featureKey}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Patch(string featureKey,
        [FromBody] PatchFeatureFlagRequest request, CancellationToken ct)
    {
        var existing = await _repo.GetFlagAsync(featureKey, ct);
        if (existing == null)
            return NotFound(new { message = $"Feature flag '{featureKey}' not found." });

        if (request.IsEnabled.HasValue)         existing.IsEnabled         = request.IsEnabled.Value;
        if (request.RolloutPercentage.HasValue)  existing.RolloutPercentage = request.RolloutPercentage.Value;
        if (request.RequiredTier   != null)      existing.RequiredTier      = request.RequiredTier == ""
                                                                                  ? null
                                                                                  : request.RequiredTier;
        if (request.Description    != null)      existing.Description       = request.Description;

        await _repo.UpsertFlagAsync(existing, ct);
        _service.InvalidateCache(featureKey);
        _logger.LogInformation("Feature flag {FeatureKey} patched by admin", featureKey);
        return Ok(existing);
    }

    // ── User-override endpoints ──────────────────────────────────────────────

    /// <summary>Returns all active overrides for a feature key (admin).</summary>
    [HttpGet("{featureKey}/overrides")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetOverrides(string featureKey, CancellationToken ct)
    {
        var overrides = await _repo.GetOverridesForFeatureAsync(featureKey, ct);
        return Ok(overrides);
    }

    /// <summary>Grants or revokes beta access for a specific user (admin).</summary>
    [HttpPost("{featureKey}/overrides")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SetOverride(string featureKey,
        [FromBody] SetUserOverrideRequest request, CancellationToken ct)
    {
        await _repo.SetUserOverrideAsync(
            request.UserId, featureKey, request.IsEnabled, request.ExpiresAt, ct);

        _logger.LogInformation(
            "Feature flag override set for user {UserId} on {FeatureKey} = {Value}",
            request.UserId, featureKey, request.IsEnabled);

        return Ok();
    }

    /// <summary>Removes the override for a specific user (admin).</summary>
    [HttpDelete("{featureKey}/overrides/{userId:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RemoveOverride(string featureKey, Guid userId,
        CancellationToken ct)
    {
        await _repo.RemoveUserOverrideAsync(userId, featureKey, ct);
        _logger.LogInformation(
            "Feature flag override removed for user {UserId} on {FeatureKey}", userId, featureKey);
        return NoContent();
    }
}

public sealed class PatchFeatureFlagRequest
{
    public bool? IsEnabled { get; init; }
    public int?  RolloutPercentage { get; init; }
    /// <summary>Empty string clears the tier requirement.</summary>
    public string? RequiredTier { get; init; }
    public string? Description  { get; init; }
}

public sealed class SetUserOverrideRequest
{
    public Guid     UserId    { get; init; }
    public bool     IsEnabled { get; init; } = true;
    public DateTime? ExpiresAt { get; init; }
}
