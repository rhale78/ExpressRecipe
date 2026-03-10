using ExpressRecipe.UserService.Data;
using ExpressRecipe.UserService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpressRecipe.UserService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReferralController : ControllerBase
{
    private readonly IReferralService _referralService;
    private readonly IReferralRepository _referralRepository;
    private readonly ILogger<ReferralController> _logger;

    public ReferralController(
        IReferralService referralService,
        IReferralRepository referralRepository,
        ILogger<ReferralController> logger)
    {
        _referralService = referralService;
        _referralRepository = referralRepository;
        _logger = logger;
    }

    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    /// <summary>GET /api/referral/code — get or create the caller's referral code</summary>
    [HttpGet("code")]
    public async Task<IActionResult> GetOrCreateCode(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        try
        {
            var code = await _referralService.GetOrCreateReferralCodeAsync(userId.Value, ct);
            return Ok(new { code });
        }
        catch (ReferralException ex)
        {
            _logger.LogWarning(ex, "Referral cap: {Code}", ex.Code);
            return UnprocessableEntity(new { code = ex.Code, message = ex.Message });
        }
    }

    /// <summary>POST /api/referral/apply — apply a referral code to the current user</summary>
    [HttpPost("apply")]
    public async Task<IActionResult> Apply([FromBody] ApplyReferralCodeRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var applied = await _referralService.ApplyReferralCodeAsync(userId.Value, request.Code, ct);
        if (!applied)
        {
            return BadRequest(new { message = "Referral code is invalid, inactive, or cannot be self-applied." });
        }

        return NoContent();
    }

    /// <summary>POST /api/referral/convert — record a referral conversion (webhook trigger)</summary>
    [HttpPost("convert")]
    [AllowAnonymous]
    public async Task<IActionResult> Convert([FromBody] ReferralConvertRequest request, CancellationToken ct)
    {
        await _referralService.RecordConversionAsync(request.UserId, ct);
        return NoContent();
    }

    /// <summary>POST /api/referral/share-link — create a share link</summary>
    [HttpPost("share-link")]
    public async Task<IActionResult> CreateShareLink([FromBody] CreateShareLinkRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        try
        {
            var token = await _referralService.CreateShareLinkAsync(userId.Value, request.EntityType, request.EntityId, ct);
            return Ok(new { token });
        }
        catch (ReferralException ex)
        {
            _logger.LogWarning(ex, "Share link cap: {Code}", ex.Code);
            return UnprocessableEntity(new { code = ex.Code, message = ex.Message });
        }
    }

    /// <summary>GET /api/referral/share/{token} — resolve a share link</summary>
    [HttpGet("share/{token}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetShareLink(string token, CancellationToken ct)
    {
        var link = await _referralRepository.GetShareLinkByTokenAsync(token, ct);
        if (link == null)
        {
            return NotFound(new { message = "Share link not found." });
        }

        if (link.ExpiresAt < DateTime.UtcNow)
        {
            return NotFound(new { message = "Share link has expired." });
        }

        await _referralRepository.IncrementShareLinkViewCountAsync(link.Id, ct);
        return Ok(link);
    }
}

public sealed class ApplyReferralCodeRequest
{
    public string Code { get; set; } = string.Empty;
}

public sealed class ReferralConvertRequest
{
    public Guid UserId { get; set; }
}

public sealed class CreateShareLinkRequest
{
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
}
