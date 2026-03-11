using ExpressRecipe.Shared.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Hybrid;
using System.Security.Claims;

namespace ExpressRecipe.UserService.Controllers;

/// <summary>
/// Admin-only endpoints for managing feature flags and per-user overrides.
/// </summary>
[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/features")]
public sealed class FeatureFlagController : ControllerBase
{
    private readonly IFeatureFlagRepository _repo;
    private readonly HybridCache _cache;

    public FeatureFlagController(IFeatureFlagRepository repo, HybridCache cache)
    {
        _repo = repo;
        _cache = cache;
    }

    private Guid? AdminId()
    {
        string? value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(value) || !Guid.TryParse(value, out Guid id))
        {
            return null;
        }
        return id;
    }

    /// <summary>
    /// Get all feature flags.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await _repo.GetAllFlagsAsync(ct));

    /// <summary>
    /// Create or update a global feature flag.
    /// </summary>
    [HttpPut("{key}")]
    public async Task<IActionResult> UpsertFlag(string key,
        [FromBody] UpsertFlagRequest req, CancellationToken ct)
    {
        Guid? adminId = AdminId();
        if (adminId is null) { return Unauthorized(); }
        if (req.RolloutPercent is < 0 or > 100)
        {
            return BadRequest(new { message = "RolloutPercent must be between 0 and 100." });
        }
        await _repo.UpsertFlagAsync(key, req.IsEnabled, req.RolloutPercent,
            req.RequiresTier, req.Description, adminId.Value, ct);
        await _cache.RemoveAsync($"feat-flag:{key}", ct);   // invalidate immediately
        return NoContent();
    }

    /// <summary>
    /// Create or update a per-user feature flag override.
    /// </summary>
    [HttpPut("users/{userId}/{key}")]
    public async Task<IActionResult> UpsertUserOverride(Guid userId, string key,
        [FromBody] UpsertOverrideRequest req, CancellationToken ct)
    {
        Guid? adminId = AdminId();
        if (adminId is null) { return Unauthorized(); }
        await _repo.UpsertUserOverrideAsync(userId, key, req.IsEnabled,
            req.Reason, adminId.Value, req.ExpiresAt, ct);
        await _cache.RemoveAsync($"feat-override:{userId}:{key}", ct);
        return NoContent();
    }

    /// <summary>
    /// Remove a per-user feature flag override.
    /// </summary>
    [HttpDelete("users/{userId}/{key}")]
    public async Task<IActionResult> RemoveUserOverride(Guid userId, string key,
        CancellationToken ct)
    {
        Guid? adminId = AdminId();
        if (adminId is null) { return Unauthorized(); }
        await _repo.DeleteUserOverrideAsync(userId, key, ct);
        await _cache.RemoveAsync($"feat-override:{userId}:{key}", ct);
        return NoContent();
    }
}

/// <summary>
/// Request body for creating or updating a global feature flag.
/// </summary>
public sealed record UpsertFlagRequest
{
    public bool IsEnabled { get; init; }
    public int RolloutPercent { get; init; } = 100;
    public string? RequiresTier { get; init; }
    public string? Description { get; init; }
}

/// <summary>
/// Request body for creating or updating a per-user feature flag override.
/// </summary>
public sealed record UpsertOverrideRequest
{
    public bool IsEnabled { get; init; }
    public string? Reason { get; init; }
    public DateTime? ExpiresAt { get; init; }
}
