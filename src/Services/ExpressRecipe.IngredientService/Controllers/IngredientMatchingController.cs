using ExpressRecipe.IngredientService.Contracts;
using ExpressRecipe.IngredientService.Data;
using ExpressRecipe.IngredientService.Services.Matching;
using ExpressRecipe.Shared.Matching;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpressRecipe.IngredientService.Controllers;

[ApiController]
[Route("api/ingredients")]
public class IngredientMatchingController : ControllerBase
{
    private readonly IIngredientMatchingService _matching;

    public IngredientMatchingController(IIngredientMatchingService matching)
    {
        _matching = matching;
    }

    /// <summary>
    /// Match a single ingredient text to a known ingredient.
    /// </summary>
    [HttpPost("match")]
    public async Task<IActionResult> Match([FromBody] IngredientMatchRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Text)) { return BadRequest(); }
        return Ok(await _matching.MatchAsync(request.Text, request.SourceService ?? "API", null, ct));
    }

    /// <summary>
    /// Match multiple ingredient texts in a single request.
    /// </summary>
    [HttpPost("match/bulk")]
    public async Task<IActionResult> MatchBulk([FromBody] IngredientBulkMatchRequest request, CancellationToken ct)
    {
        if (request.Texts is null || request.Texts.Count == 0) { return BadRequest(); }
        return Ok(await _matching.MatchBulkAsync(request.Texts, request.SourceService ?? "API", request.SourceEntityId, ct));
    }
}

[ApiController]
[Route("api/admin/ingredients")]
[Authorize(Roles = "Admin")]
public class AdminIngredientController : ControllerBase
{
    private readonly IIngredientMatchingService _matching;
    private readonly IIngredientMatchingRepository _repo;

    public AdminIngredientController(IIngredientMatchingService matching, IIngredientMatchingRepository repo)
    {
        _matching = matching;
        _repo = repo;
    }

    /// <summary>
    /// List unresolved ingredient queue items.
    /// </summary>
    [HttpGet("unresolved")]
    public async Task<IActionResult> GetUnresolved(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] int minOccurrences = 1,
        CancellationToken ct = default)
    {
        if (page < 1 || pageSize < 1 || pageSize > 200 || minOccurrences < 1)
        {
            return BadRequest("page and minOccurrences must be >= 1; pageSize must be between 1 and 200.");
        }
        List<UnresolvedQueueItem> items = await _repo.GetUnresolvedQueueAsync(page, pageSize, minOccurrences, ct);
        return Ok(items);
    }

    /// <summary>
    /// Confirm a queue item as a match for the given ingredient, optionally creating an alias.
    /// </summary>
    [HttpPut("unresolved/{id}/confirm")]
    public async Task<IActionResult> Confirm(Guid id, [FromBody] ConfirmMatchRequest request, CancellationToken ct)
    {
        await _matching.ConfirmMatchAsync(id, request.IngredientId, request.CreateAlias, request.ResolvedBy, ct);
        return NoContent();
    }

    /// <summary>
    /// Create a new ingredient and resolve the queue item to it.
    /// </summary>
    [HttpPut("unresolved/{id}/create")]
    public async Task<IActionResult> Create(Guid id, [FromBody] CreateAndResolveRequest request, CancellationToken ct)
    {
        await _matching.CreateAndResolveAsync(id, request.NewIngredientName, request.Category, ct);
        return NoContent();
    }

    /// <summary>
    /// Reject / dismiss a queue item.
    /// </summary>
    [HttpPost("unresolved/{id}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectQueueItemRequest request, CancellationToken ct)
    {
        await _matching.RejectAsync(id, request.Reason, ct);
        return NoContent();
    }
}
