using ExpressRecipe.Shared.DTOs.User;
using ExpressRecipe.UserService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ExpressRecipe.UserService.Controllers;

/// <summary>
/// Admin &amp; Customer-Service portal: privileged user management endpoints.
/// </summary>
[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "Admin,CS")]
public sealed class AdminUserController : ControllerBase
{
    private readonly IUserProfileRepository _users;
    private readonly ISubscriptionRepository _subs;
    private readonly IPointsRepository _points;
    private readonly IAuditRepository _audit;
    private readonly IConfiguration _config;
    private readonly ILogger<AdminUserController> _logger;

    public AdminUserController(
        IUserProfileRepository users,
        ISubscriptionRepository subs,
        IPointsRepository points,
        IAuditRepository audit,
        IConfiguration config,
        ILogger<AdminUserController> logger)
    {
        _users = users;
        _subs = subs;
        _points = points;
        _audit = audit;
        _config = config;
        _logger = logger;
    }

    private Guid? ActorId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    /// <summary>
    /// Full user detail: profile + active subscription + points balance.
    /// Available to Admin and CS roles.
    /// </summary>
    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> GetUserDetail(Guid userId, CancellationToken ct)
    {
        var profile = await _users.GetByUserIdAsync(userId);
        if (profile is null)
            return NotFound(new { message = "User not found." });

        var subscription = await _subs.GetUserSubscriptionAsync(userId);
        var pointsSummary = await _points.GetUserPointsSummaryAsync(userId);

        return Ok(new
        {
            profile,
            subscription,
            points = pointsSummary
        });
    }

    /// <summary>
    /// Issue a subscription tier credit (extend trial / grant free period).
    /// Admin only — CS cannot grant subscriptions.
    /// </summary>
    [HttpPost("{userId:guid}/subscription-credit")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GrantSubscriptionCredit(
        Guid userId,
        [FromBody] SubscriptionCreditRequest req,
        CancellationToken ct)
    {
        var actorId = ActorId();
        if (actorId is null) return Unauthorized();

        var creditId = await _subs.GrantCreditAsync(userId, req.Tier, req.DurationDays, req.Reason, actorId.Value, ct);
        await _audit.LogAsync(actorId.Value, "SubscriptionCredit", userId,
            $"Tier={req.Tier} Days={req.DurationDays} Reason={req.Reason}", ct);

        _logger.LogInformation("Admin {ActorId} granted {Tier}/{Days}d subscription credit to user {UserId}",
            actorId, req.Tier, req.DurationDays, userId);

        return NoContent();
    }

    /// <summary>
    /// Generate a short-lived read-only impersonation token.
    /// Admin only — CS cannot impersonate.
    /// </summary>
    [HttpPost("{userId:guid}/impersonate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Impersonate(Guid userId, CancellationToken ct)
    {
        var actorId = ActorId();
        if (actorId is null) return Unauthorized();

        var profile = await _users.GetByUserIdAsync(userId);
        if (profile is null) return NotFound(new { message = "User not found." });

        await _audit.LogAsync(actorId.Value, "ImpersonateStart", userId, "Admin view-as", ct);

        var token = GenerateImpersonationToken(actorId.Value, userId);

        _logger.LogWarning("Admin {ActorId} started impersonation of user {UserId}", actorId, userId);

        return Ok(new { token, expiresIn = 1800 });
    }

    /// <summary>
    /// Suspend a user account.
    /// Admin only.
    /// </summary>
    [HttpPost("{userId:guid}/suspend")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Suspend(Guid userId, [FromBody] SuspendRequest req, CancellationToken ct)
    {
        var actorId = ActorId();
        if (actorId is null) return Unauthorized();

        var success = await _users.SetSuspendedAsync(userId, true, actorId);
        if (!success) return NotFound(new { message = "User not found." });

        await _audit.LogAsync(actorId.Value, "UserSuspended", userId, req.Reason, ct);

        _logger.LogWarning("Admin {ActorId} suspended user {UserId}: {Reason}", actorId, userId, req.Reason);

        return NoContent();
    }

    /// <summary>
    /// Retrieve audit trail for a specific user.
    /// </summary>
    [HttpGet("{userId:guid}/audit")]
    public async Task<IActionResult> GetAuditHistory(Guid userId, CancellationToken ct)
    {
        var entries = await _audit.GetByTargetAsync(userId, 100, ct);
        return Ok(entries);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private string GenerateImpersonationToken(Guid adminId, Guid targetUserId)
    {
        var jwtKey = _config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        var issuer = _config["Jwt:Issuer"] ?? "ExpressRecipe";
        var audience = _config["Jwt:Audience"] ?? "ExpressRecipe";

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, targetUserId.ToString()),
            new Claim("impersonated_by", adminId.ToString()),
            new Claim("readonly", "true"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddSeconds(1800),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

// ──────────────────────────────────────────────────────────────────────────────
// Request DTOs (local to admin module)
// ──────────────────────────────────────────────────────────────────────────────

public sealed record SubscriptionCreditRequest
{
    public string Tier { get; init; } = "Plus";
    public int DurationDays { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public sealed record SuspendRequest
{
    public string Reason { get; init; } = string.Empty;
}
