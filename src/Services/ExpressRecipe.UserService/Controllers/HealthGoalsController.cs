using ExpressRecipe.Shared.DTOs.User;
using ExpressRecipe.UserService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpressRecipe.UserService.Controllers;

/// <summary>
/// Paginated result wrapper.
/// </summary>
public sealed record PagedResult<T>(
    List<T> Items,
    int Total,
    int Page,
    int PageSize)
{
    public int TotalPages => (int)Math.Ceiling(Total / (double)PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class HealthGoalsController : ControllerBase
{
    private readonly IHealthGoalRepository _repository;
    private readonly ILogger<HealthGoalsController> _logger;

    public HealthGoalsController(
        IHealthGoalRepository repository,
        ILogger<HealthGoalsController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Get all health goals
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<HealthGoalDto>>> GetAll()
    {
        var goals = await _repository.GetAllAsync();
        return Ok(goals);
    }

    /// <summary>
    /// Get health goals with pagination.
    /// <para>Prefer this endpoint for UI components; <see cref="GetAll"/> is retained for
    /// backward compatibility and offline/cache pre-warming scenarios.</para>
    /// </summary>
    [HttpGet("paged")]
    public async Task<ActionResult<PagedResult<HealthGoalDto>>> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? category = null)
    {
        if (page < 1) page = 1;
        pageSize = Math.Clamp(pageSize, 1, 200);
        var (items, total) = await _repository.GetPagedAsync(page, pageSize, category);
        return Ok(new PagedResult<HealthGoalDto>(items, total, page, pageSize));
    }

    /// <summary>
    /// Get health goal by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<HealthGoalDto>> GetById(Guid id)
    {
        var goal = await _repository.GetByIdAsync(id);

        if (goal == null)
        {
            return NotFound(new { message = "Health goal not found" });
        }

        return Ok(goal);
    }

    /// <summary>
    /// Get health goals by category
    /// </summary>
    [HttpGet("category/{category}")]
    public async Task<ActionResult<List<HealthGoalDto>>> GetByCategory(string category)
    {
        var goals = await _repository.GetByCategoryAsync(category);
        return Ok(goals);
    }

    /// <summary>
    /// Search health goals by name
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<List<HealthGoalDto>>> Search([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest(new { message = "Search term is required" });
        }

        var goals = await _repository.SearchByNameAsync(q);
        return Ok(goals);
    }
}
