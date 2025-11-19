using ExpressRecipe.Shared.DTOs.Recipe;
using ExpressRecipe.RecipeService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpressRecipe.RecipeService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RecipeImportController : ControllerBase
{
    private readonly IRecipeImportRepository _importRepository;
    private readonly ILogger<RecipeImportController> _logger;

    public RecipeImportController(
        IRecipeImportRepository importRepository,
        ILogger<RecipeImportController> logger)
    {
        _importRepository = importRepository;
        _logger = logger;
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return null;
        }
        return userId;
    }

    /// <summary>
    /// Get import summary for user
    /// </summary>
    [HttpGet("summary")]
    public async Task<ActionResult<ImportSummaryDto>> GetSummary()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var summary = await _importRepository.GetImportSummaryAsync(userId.Value);
            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving import summary");
            return StatusCode(500, new { message = "An error occurred while retrieving import summary" });
        }
    }

    /// <summary>
    /// Get available import sources
    /// </summary>
    [HttpGet("sources")]
    [AllowAnonymous]
    public async Task<ActionResult<List<RecipeImportSourceDto>>> GetImportSources([FromQuery] bool activeOnly = true)
    {
        try
        {
            var sources = await _importRepository.GetImportSourcesAsync(activeOnly);
            return Ok(sources);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving import sources");
            return StatusCode(500, new { message = "An error occurred while retrieving import sources" });
        }
    }

    /// <summary>
    /// Get user's import jobs
    /// </summary>
    [HttpGet("jobs")]
    public async Task<ActionResult<List<RecipeImportJobDto>>> GetJobs([FromQuery] int limit = 50)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            if (limit <= 0 || limit > 200)
            {
                return BadRequest(new { message = "Limit must be between 1 and 200" });
            }

            var jobs = await _importRepository.GetUserImportJobsAsync(userId.Value, limit);
            return Ok(jobs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving import jobs");
            return StatusCode(500, new { message = "An error occurred while retrieving your import jobs" });
        }
    }

    /// <summary>
    /// Get import job by ID
    /// </summary>
    [HttpGet("jobs/{id:guid}")]
    public async Task<ActionResult<RecipeImportJobDto>> GetJob(Guid id)
    {
        try
        {
            var job = await _importRepository.GetImportJobByIdAsync(id);

            if (job == null)
            {
                return NotFound(new { message = "Import job not found" });
            }

            // Verify ownership
            var userId = GetCurrentUserId();
            if (job.UserId != userId)
            {
                return Forbid();
            }

            return Ok(job);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving import job {JobId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving the import job" });
        }
    }

    /// <summary>
    /// Start a new import job
    /// </summary>
    [HttpPost("jobs")]
    public async Task<ActionResult<Guid>> StartImportJob([FromBody] StartImportJobRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var jobId = await _importRepository.CreateImportJobAsync(userId.Value, request);

            _logger.LogInformation("Import job {JobId} created for user {UserId}", jobId, userId.Value);

            return CreatedAtAction(nameof(GetJob), new { id = jobId }, new { id = jobId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating import job");
            return StatusCode(500, new { message = "An error occurred while creating the import job" });
        }
    }

    /// <summary>
    /// Get recipe versions
    /// </summary>
    [HttpGet("versions/{recipeId:guid}")]
    public async Task<ActionResult<List<RecipeVersionDto>>> GetVersions(Guid recipeId)
    {
        try
        {
            var versions = await _importRepository.GetRecipeVersionsAsync(recipeId);
            return Ok(versions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recipe versions for {RecipeId}", recipeId);
            return StatusCode(500, new { message = "An error occurred while retrieving recipe versions" });
        }
    }

    /// <summary>
    /// Get recipe forks
    /// </summary>
    [HttpGet("forks/{recipeId:guid}")]
    public async Task<ActionResult<List<RecipeForkDto>>> GetForks(Guid recipeId)
    {
        try
        {
            var forks = await _importRepository.GetRecipeForksAsync(recipeId);
            return Ok(forks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recipe forks for {RecipeId}", recipeId);
            return StatusCode(500, new { message = "An error occurred while retrieving recipe forks" });
        }
    }

    /// <summary>
    /// Get export history
    /// </summary>
    [HttpGet("exports")]
    public async Task<ActionResult<List<RecipeExportHistoryDto>>> GetExportHistory([FromQuery] int limit = 50)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            if (limit <= 0 || limit > 200)
            {
                return BadRequest(new { message = "Limit must be between 1 and 200" });
            }

            var exports = await _importRepository.GetUserExportHistoryAsync(userId.Value, limit);
            return Ok(exports);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving export history");
            return StatusCode(500, new { message = "An error occurred while retrieving export history" });
        }
    }
}
