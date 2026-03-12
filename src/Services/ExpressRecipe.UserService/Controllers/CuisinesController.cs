using ExpressRecipe.Shared.DTOs.User;
using ExpressRecipe.Shared.Models;
using ExpressRecipe.UserService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpressRecipe.UserService.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
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
        var cuisines = await _repository.GetAllAsync();
        return Ok(cuisines);
    }

    /// <summary>
    /// Get cuisines with pagination.
    /// <para>Prefer this endpoint for UI components; <see cref="GetAll"/> is retained for
    /// backward compatibility and offline/cache pre-warming scenarios.</para>
    /// </summary>
    [HttpGet("paged")]
    public async Task<ActionResult<PagedResult<CuisineDto>>> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (page < 1) page = 1;
        pageSize = Math.Clamp(pageSize, 1, 200);
        var (items, total) = await _repository.GetPagedAsync(page, pageSize);
        return Ok(new PagedResult<CuisineDto>(items, total, page, pageSize));
    }

    /// <summary>
    /// Get cuisine by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CuisineDto>> GetById(Guid id)
    {
        var cuisine = await _repository.GetByIdAsync(id);

        if (cuisine == null)
        {
            return NotFound(new { message = "Cuisine not found" });
        }

        return Ok(cuisine);
    }

    /// <summary>
    /// Search cuisines by name
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<List<CuisineDto>>> Search([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest(new { message = "Search term is required" });
        }

        var cuisines = await _repository.SearchByNameAsync(q);
        return Ok(cuisines);
    }
}
