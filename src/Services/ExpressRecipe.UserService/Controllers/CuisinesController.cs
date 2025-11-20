using ExpressRecipe.Shared.DTOs.User;
using ExpressRecipe.UserService.Data;
using Microsoft.AspNetCore.Mvc;

namespace ExpressRecipe.UserService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CuisinesController : ControllerBase
{
    private readonly ICuisineRepository _repository;
    private readonly ILogger<CuisinesController> _logger;

    public CuisinesController(
        ICuisineRepository repository,
        ILogger<CuisinesController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Get all cuisines
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<CuisineDto>>> GetAll()
    {
        try
        {
            var cuisines = await _repository.GetAllAsync();
            return Ok(cuisines);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cuisines");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Get cuisine by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CuisineDto>> GetById(Guid id)
    {
        try
        {
            var cuisine = await _repository.GetByIdAsync(id);

            if (cuisine == null)
            {
                return NotFound(new { message = "Cuisine not found" });
            }

            return Ok(cuisine);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cuisine {CuisineId}", id);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Search cuisines by name
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<List<CuisineDto>>> Search([FromQuery] string q)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return BadRequest(new { message = "Search term is required" });
            }

            var cuisines = await _repository.SearchByNameAsync(q);
            return Ok(cuisines);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching cuisines");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }
}
