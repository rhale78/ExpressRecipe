using ExpressRecipe.Shared.DTOs.User;
using ExpressRecipe.UserService.Data;
using Microsoft.AspNetCore.Mvc;

namespace ExpressRecipe.UserService.Controllers;

[ApiController]
[Route("api/[controller]")]
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
        try
        {
            var goals = await _repository.GetAllAsync();
            return Ok(goals);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving health goals");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Get health goal by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<HealthGoalDto>> GetById(Guid id)
    {
        try
        {
            var goal = await _repository.GetByIdAsync(id);

            if (goal == null)
            {
                return NotFound(new { message = "Health goal not found" });
            }

            return Ok(goal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving health goal {GoalId}", id);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Get health goals by category
    /// </summary>
    [HttpGet("category/{category}")]
    public async Task<ActionResult<List<HealthGoalDto>>> GetByCategory(string category)
    {
        try
        {
            var goals = await _repository.GetByCategoryAsync(category);
            return Ok(goals);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving health goals by category {Category}", category);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Search health goals by name
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<List<HealthGoalDto>>> Search([FromQuery] string q)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return BadRequest(new { message = "Search term is required" });
            }

            var goals = await _repository.SearchByNameAsync(q);
            return Ok(goals);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching health goals");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }
}
