using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Shared.Messages;
using ExpressRecipe.UserService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpressRecipe.UserService.Controllers;

/// <summary>
/// GDPR data-rights endpoints: Export, Delete, Forget (Right to be Forgotten).
/// </summary>
[Authorize]
[ApiController]
[Route("api/gdpr")]
public sealed class GdprController : ControllerBase
{
    private readonly IGdprRepository _gdpr;
    private readonly IMessageBus _bus;
    private readonly ILogger<GdprController> _logger;

    public GdprController(IGdprRepository gdpr, IMessageBus bus, ILogger<GdprController> logger)
    {
        _gdpr = gdpr;
        _bus = bus;
        _logger = logger;
    }

    private Guid? UserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    /// <summary>
    /// Queue a data-export request. The user will be notified (via NotificationService)
    /// with a download link when the export package is ready.
    /// </summary>
    [HttpPost("export")]
    public async Task<IActionResult> RequestExport(CancellationToken ct)
    {
        var userId = UserId();
        if (userId is null) return Unauthorized();

        var requestId = await _gdpr.CreateRequestAsync(userId.Value, "Export", ct);
        _logger.LogInformation("GDPR Export request {RequestId} queued for user {UserId}", requestId, userId);

        return Ok(new { requestId, message = "Export request queued. You'll be notified when ready." });
    }

    /// <summary>
    /// Initiate account deletion. Publishes <see cref="GdprDeleteEvent"/> so every service
    /// deletes its own data. UserService completes deletion after the 24 h confirmation window.
    /// </summary>
    [HttpPost("delete")]
    public async Task<IActionResult> RequestDelete(CancellationToken ct)
    {
        var userId = UserId();
        if (userId is null) return Unauthorized();

        var requestId = await _gdpr.CreateRequestAsync(userId.Value, "Delete", ct);

        await _bus.PublishAsync(
            new GdprDeleteEvent(userId.Value, requestId, DateTimeOffset.UtcNow),
            cancellationToken: ct);

        _logger.LogWarning("GDPR Delete initiated for user {UserId}, requestId {RequestId}", userId, requestId);

        return Ok(new { message = "Account deletion initiated. This may take up to 24 hours." });
    }

    /// <summary>
    /// Right to be Forgotten: anonymise PII while keeping contribution records for DB integrity.
    /// Email → anonymous_{guid}@deleted.local, DisplayName → "Deleted User".
    /// Allergen/dietary data is hard-deleted. Community records have UserId zeroed.
    /// </summary>
    [HttpPost("forget")]
    public async Task<IActionResult> RequestAnonymize(CancellationToken ct)
    {
        var userId = UserId();
        if (userId is null) return Unauthorized();

        var requestId = await _gdpr.CreateRequestAsync(userId.Value, "Forget", ct);

        await _gdpr.AnonymizeUserAsync(userId.Value, ct);
        await _gdpr.SetStatusAsync(requestId, "Completed", ct: ct);

        await _bus.PublishAsync(
            new GdprForgetEvent(userId.Value, requestId, DateTimeOffset.UtcNow),
            cancellationToken: ct);

        _logger.LogWarning("GDPR Forget (anonymise) completed for user {UserId}", userId);

        return Ok(new { message = "Personal data anonymized." });
    }

    /// <summary>
    /// List GDPR requests submitted by the current user.
    /// </summary>
    [HttpGet("requests")]
    public async Task<IActionResult> GetMyRequests(CancellationToken ct)
    {
        var userId = UserId();
        if (userId is null) return Unauthorized();

        var requests = await _gdpr.GetRequestsByUserAsync(userId.Value, ct);
        return Ok(requests);
    }
}
