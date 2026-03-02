using ExpressRecipe.IngredientService.Data;
using ExpressRecipe.IngredientService.Logging;
using ExpressRecipe.Shared.DTOs.Product;
using ExpressRecipe.Shared.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpressRecipe.IngredientService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IngredientController : ControllerBase
{
    private readonly IIngredientRepository _repository;
    private readonly HybridCacheService _cache;
    private readonly ILogger<IngredientController> _logger;

    public IngredientController(
        IIngredientRepository repository,
        HybridCacheService cache,
        ILogger<IngredientController> logger)
    {
        _repository = repository;
        _cache = cache;
        _logger = logger;
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
        var success = await _repository.UpdateIngredientAsync(id, request);
        if (!success) return NotFound();

        // Invalidate caches
        await _cache.RemoveAsync(string.Format(CacheKeys.IngredientById, id));
        await _cache.RemoveAsync(string.Format(CacheKeys.IngredientByName, request.Name.ToLowerInvariant()));

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

        return NoContent();
    }
}
