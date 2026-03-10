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

        Guid id = await _repo.LogSessionAsync(userId.Value, req, ct);

        await _publisher.PublishCookedSessionAsync(
            sessionId:   id,
            userId:      userId.Value,
            householdId: req.HouseholdId,
            recipeId:    req.RecipeId,
            cookedAt:    req.CookedAt ?? DateTime.UtcNow,
            hasRating:   req.Rating.HasValue,
            ct:          ct);

        return Ok(new { id });
    }

    [HttpGet]
    public async Task<IActionResult> GetSessions(
        [FromQuery] Guid? recipeId, [FromQuery] int limit = 20, CancellationToken ct = default)
    {
        Guid? userId = GetCurrentUserId();
        if (userId is null) { return Unauthorized(); }

        List<CookSessionDto> sessions =
            await _repo.GetSessionsAsync(userId.Value, recipeId, limit, ct);
        return Ok(sessions);
    }
}

[Authorize]
[ApiController]
[Route("api/recipes/{recipeId:guid}/notes")]
public sealed class RecipeNoteController : ControllerBase
{
    private readonly ICookSessionRepository _repo;

    public RecipeNoteController(ICookSessionRepository repo)
    {
        _repo = repo;
    }

    private Guid? GetCurrentUserId()
    {
        string? value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(value, out Guid userId)) { return null; }
        return userId;
    }

    [HttpGet]
    public async Task<IActionResult> GetNotes(Guid recipeId, CancellationToken ct)
    {
        Guid? userId = GetCurrentUserId();
        if (userId is null) { return Unauthorized(); }
        return Ok(await _repo.GetNotesAsync(userId.Value, recipeId, ct));
    }

    [HttpPost]
    public async Task<IActionResult> SaveNote(Guid recipeId,
        [FromBody] SaveRecipeNoteRequest req, CancellationToken ct)
    {
        Guid? userId = GetCurrentUserId();
        if (userId is null) { return Unauthorized(); }

        SaveRecipeNoteRequest reqWithId = req with { RecipeId = recipeId };
        Guid id = await _repo.SaveNoteAsync(userId.Value, reqWithId, ct);
        return Ok(new { id });
    }

    [HttpPatch("{noteId:guid}/dismiss")]
    public async Task<IActionResult> DismissNote(Guid recipeId, Guid noteId, CancellationToken ct)
    {
        Guid? userId = GetCurrentUserId();
        if (userId is null) { return Unauthorized(); }
        await _repo.DismissNoteAsync(noteId, userId.Value, ct);
        return NoContent();
    }

    [HttpDelete("{noteId:guid}")]
    public async Task<IActionResult> DeleteNote(Guid recipeId, Guid noteId, CancellationToken ct)
    {
        Guid? userId = GetCurrentUserId();
        if (userId is null) { return Unauthorized(); }
        await _repo.DeleteNoteAsync(noteId, userId.Value, ct);
        return NoContent();
    }
}
