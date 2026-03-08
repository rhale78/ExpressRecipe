using ExpressRecipe.IngredientService.Data;
using ExpressRecipe.IngredientService.Logging;
using ExpressRecipe.IngredientService.Services;
using ExpressRecipe.Shared.DTOs.Product;
using ExpressRecipe.Shared.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpressRecipe.IngredientService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IngredientController : ControllerBase
{
    private readonly IIngredientRepository _repository;
    private readonly HybridCacheService _cache;
    private readonly IIngredientEventPublisher _events;
    private readonly IIngredientBatchChannel _batchChannel;
    private readonly ILogger<IngredientController> _logger;

    public IngredientController(
        IIngredientRepository repository,
        HybridCacheService cache,
        IIngredientEventPublisher events,
        IIngredientBatchChannel batchChannel,
        ILogger<IngredientController> logger)
    {
        _repository   = repository;
        _cache        = cache;
        _events       = events;
        _batchChannel = batchChannel;
        _logger       = logger;
    }

    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<IngredientDto>> GetIngredient(Guid id)
    {
        var cacheKey = string.Format(CacheKeys.IngredientById, id);
        var result = await _cache.GetOrSetAsync<IngredientDto?>(cacheKey, ct => 
            new ValueTask<IngredientDto?>(_repository.GetIngredientByIdAsync(id)));

        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpGet("name/{name}")]
    public async Task<ActionResult<IngredientDto>> GetIngredientByName(string name)
    {
        var cacheKey = string.Format(CacheKeys.IngredientByName, name.ToLowerInvariant());
        var result = await _cache.GetOrSetAsync<IngredientDto?>(cacheKey, ct => 
            new ValueTask<IngredientDto?>(_repository.GetIngredientByNameAsync(name)));

        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost("bulk/lookup")]
    public async Task<ActionResult<Dictionary<string, Guid>>> LookupIngredientIds([FromBody] List<string> names)
    {
        if (names == null || !names.Any()) return BadRequest("No names provided");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await _repository.GetIngredientIdsByNamesAsync(names);
        sw.Stop();

        _logger.LogBulkLookup(names.Count, result.Count, sw.ElapsedMilliseconds);

        return Ok(result);
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<Guid>> CreateIngredient([FromBody] CreateIngredientRequest request)
    {
        var id = await _repository.CreateIngredientAsync(request);
        
        // Invalidate name cache
        await _cache.RemoveAsync(string.Format(CacheKeys.IngredientByName, request.Name.ToLowerInvariant()));

        await _events.PublishCreatedAsync(id, request.Name);
        
        return CreatedAtAction(nameof(GetIngredient), new { id }, id);
    }

    [HttpPost("bulk/create")]
    [Authorize]
    public async Task<ActionResult<int>> BulkCreateIngredients([FromBody] List<string> names)
    {
        if (names == null || !names.Any()) return BadRequest("No names provided");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var count = await _repository.BulkCreateIngredientsAsync(names);
        sw.Stop();

        _logger.LogBulkCreate(names.Count, count, sw.ElapsedMilliseconds);

        return Ok(count);
    }

    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> UpdateIngredient(Guid id, [FromBody] UpdateIngredientRequest request)
    {
        // Fetch old name for the update event before modifying
        var existing = await _repository.GetIngredientByIdAsync(id);
        var success = await _repository.UpdateIngredientAsync(id, request);
        if (!success) return NotFound();

        // Invalidate caches
        await _cache.RemoveAsync(string.Format(CacheKeys.IngredientById, id));
        await _cache.RemoveAsync(string.Format(CacheKeys.IngredientByName, request.Name.ToLowerInvariant()));

        await _events.PublishUpdatedAsync(id, request.Name, existing?.Name);

        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteIngredient(Guid id)
    {
        var ingredient = await _repository.GetIngredientByIdAsync(id);
        var success = await _repository.DeleteIngredientAsync(id);
        if (!success) return NotFound();

        // Invalidate caches
        await _cache.RemoveAsync(string.Format(CacheKeys.IngredientById, id));
        if (ingredient != null)
        {
            await _cache.RemoveAsync(string.Format(CacheKeys.IngredientByName, ingredient.Name.ToLowerInvariant()));
        }

        await _events.PublishDeletedAsync(id, ingredient?.Name);

        return NoContent();
    }

    /// <summary>
    /// Submit multiple ingredient names in one call – asynchronous channel path.
    /// Items are written to the <see cref="IIngredientBatchChannel"/> and processed by
    /// <see cref="IngredientBatchChannelWorker"/> in the background, which fires
    /// <see cref="IIngredientEventPublisher.PublishCreatedAsync"/> for each created ingredient.
    /// For creating a single ingredient use POST /api/ingredient (sync REST path).
    /// For purely synchronous bulk creation (no events) use POST /api/ingredient/bulk/create.
    /// </summary>
    [HttpPost("batch")]
    [Authorize]
    public async Task<IActionResult> BatchCreateIngredients([FromBody] List<string> names)
    {
        if (names == null || names.Count == 0)
            return BadRequest(new { message = "names list cannot be empty" });

        const int maxBatch = 2000;
        if (names.Count > maxBatch)
            return BadRequest(new { message = $"Batch exceeds maximum of {maxBatch} items" });

        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var sessionId = Guid.NewGuid().ToString("N");
        var accepted  = 0;

        foreach (var name in names.Where(n => !string.IsNullOrWhiteSpace(n)))
        {
            var item = new IngredientBatchItem
            {
                Name        = name.Trim(),
                SubmittedBy = userId.Value,
                SessionId   = sessionId
            };

            if (_batchChannel.TryWrite(item))
                accepted++;
            else
            {
                await _batchChannel.WriteAsync(item, HttpContext.RequestAborted);
                accepted++;
            }
        }

        _logger.LogInformation(
            "[IngredientController] Batch submitted: session={SessionId} accepted={Accepted} by user {UserId}",
            sessionId, accepted, userId.Value);

        return Accepted(new
        {
            sessionId,
            accepted,
            message = "Ingredient batch queued for async processing"
        });
    }
}
