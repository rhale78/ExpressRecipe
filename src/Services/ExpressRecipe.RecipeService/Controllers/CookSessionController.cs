using ExpressRecipe.RecipeService.Data;
using ExpressRecipe.RecipeService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpressRecipe.RecipeService.Controllers;

[Authorize]
[ApiController]
[Route("api/cook-sessions")]
public sealed class CookSessionController : ControllerBase
{
    private const int MaxSessionLimit = 100;

    private readonly ICookSessionRepository _repo;
    private readonly IRecipeEventPublisher _publisher;

    public CookSessionController(ICookSessionRepository repo, IRecipeEventPublisher publisher)
    {
        _repo      = repo;
        _publisher = publisher;
    }

    private Guid? GetCurrentUserId()
    {
        string? value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(value, out Guid userId)) { return null; }
        return userId;
    }

    [HttpPost]
    public async Task<IActionResult> LogSession(
        [FromBody] LogCookSessionRequest req, CancellationToken ct)
    {
        Guid? userId = GetCurrentUserId();
        if (userId is null) { return Unauthorized(); }

        DateTimeOffset cookedAt = req.CookedAt ?? DateTimeOffset.UtcNow;

        Guid id = await _repo.LogSessionAsync(userId.Value, req, cookedAt, ct);

        await _publisher.PublishCookedSessionAsync(
            sessionId:   id,
            userId:      userId.Value,
            householdId: req.HouseholdId,
            recipeId:    req.RecipeId,
            cookedAt:    cookedAt,
            hasRating:   req.Rating.HasValue,
            ct:          ct);

        return Ok(new { id });
    }

    [HttpGet]
    public async Task<IActionResult> GetSessions(
        [FromQuery] Guid? recipeId, [FromQuery] int limit = 20, CancellationToken ct = default)
    {
        if (limit < 1 || limit > MaxSessionLimit)
        {
            return BadRequest(new { message = $"limit must be between 1 and {MaxSessionLimit}." });
        }

        Guid? userId = GetCurrentUserId();
        if (userId is null) { return Unauthorized(); }

        List<CookSessionDto> sessions =
            await _repo.GetSessionsAsync(userId.Value, recipeId, limit, ct);
        return Ok(sessions);
    }
}
