using ExpressRecipe.CookbookService.Data;
using ExpressRecipe.CookbookService.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpressRecipe.CookbookService.Controllers;

[ApiController]
[Route("api/cookbooks/{cookbookId:guid}")]
[Authorize]
public class CookbookSectionsController : ControllerBase
{
    private readonly ICookbookRepository _repository;
    private readonly ILogger<CookbookSectionsController> _logger;

    public CookbookSectionsController(ICookbookRepository repository, ILogger<CookbookSectionsController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(claim) || !Guid.TryParse(claim, out var id)) return null;
        return id;
    }

    [HttpPost("sections")]
    public async Task<ActionResult<Guid>> CreateSection(Guid cookbookId, [FromBody] CreateCookbookSectionRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized(new { message = "User not authenticated" });

            var sectionId = await _repository.CreateSectionAsync(cookbookId, userId.Value, request);
            return Ok(new { id = sectionId });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating section for cookbook {CookbookId}", cookbookId);
            return StatusCode(500, new { message = "An error occurred while creating the section" });
        }
    }

    [HttpPut("sections/{sectionId:guid}")]
    public async Task<ActionResult> UpdateSection(Guid cookbookId, Guid sectionId, [FromBody] UpdateCookbookSectionRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized(new { message = "User not authenticated" });

            var success = await _repository.UpdateSectionAsync(sectionId, userId.Value, request);
            if (!success) return NotFound(new { message = "Section not found" });
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating section {SectionId}", sectionId);
            return StatusCode(500, new { message = "An error occurred while updating the section" });
        }
    }

    [HttpDelete("sections/{sectionId:guid}")]
    public async Task<ActionResult> DeleteSection(Guid cookbookId, Guid sectionId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized(new { message = "User not authenticated" });

            var success = await _repository.DeleteSectionAsync(sectionId, userId.Value);
            if (!success) return NotFound(new { message = "Section not found" });
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting section {SectionId}", sectionId);
            return StatusCode(500, new { message = "An error occurred while deleting the section" });
        }
    }

    [HttpPut("sections/reorder")]
    public async Task<ActionResult> ReorderSections(Guid cookbookId, [FromBody] ReorderRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized(new { message = "User not authenticated" });

            var success = await _repository.ReorderSectionsAsync(cookbookId, userId.Value, request.Ids);
            if (!success) return Forbid();
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reordering sections for cookbook {CookbookId}", cookbookId);
            return StatusCode(500, new { message = "An error occurred while reordering sections" });
        }
    }

    [HttpPost("sections/{sectionId:guid}/recipes")]
    public async Task<ActionResult<Guid>> AddRecipeToSection(Guid cookbookId, Guid sectionId, [FromBody] AddCookbookRecipeRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized(new { message = "User not authenticated" });

            request.SectionId = sectionId;
            var id = await _repository.AddRecipeToCookbookAsync(cookbookId, userId.Value, request);
            return Ok(new { id });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding recipe to section {SectionId}", sectionId);
            return StatusCode(500, new { message = "An error occurred while adding the recipe" });
        }
    }

    [HttpPost("sections/{sectionId:guid}/recipes/batch")]
    public async Task<ActionResult> AddRecipesBatch(Guid cookbookId, Guid sectionId, [FromBody] List<AddCookbookRecipeRequest> recipes)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized(new { message = "User not authenticated" });

            var success = await _repository.AddRecipesBatchAsync(cookbookId, userId.Value, sectionId, recipes);
            if (!success) return Forbid();
            return Ok(new { message = $"{recipes.Count} recipes added" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error batch adding recipes to section {SectionId}", sectionId);
            return StatusCode(500, new { message = "An error occurred while adding recipes" });
        }
    }

    [HttpDelete("recipes/{cookbookRecipeId:guid}")]
    public async Task<ActionResult> RemoveRecipe(Guid cookbookId, Guid cookbookRecipeId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized(new { message = "User not authenticated" });

            var success = await _repository.RemoveRecipeFromCookbookAsync(cookbookRecipeId, userId.Value);
            if (!success) return NotFound(new { message = "Recipe not found in cookbook" });
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing recipe {RecipeId} from cookbook {CookbookId}", cookbookRecipeId, cookbookId);
            return StatusCode(500, new { message = "An error occurred while removing the recipe" });
        }
    }

    [HttpPut("recipes/{cookbookRecipeId:guid}/move")]
    public async Task<ActionResult> MoveRecipe(Guid cookbookId, Guid cookbookRecipeId, [FromBody] MoveRecipeRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized(new { message = "User not authenticated" });

            var success = await _repository.MoveRecipeToSectionAsync(cookbookRecipeId, userId.Value, request.NewSectionId);
            if (!success) return NotFound(new { message = "Recipe not found" });
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error moving recipe {RecipeId}", cookbookRecipeId);
            return StatusCode(500, new { message = "An error occurred while moving the recipe" });
        }
    }

    [HttpPut("sections/{sectionId:guid}/recipes/reorder")]
    public async Task<ActionResult> ReorderRecipes(Guid cookbookId, Guid sectionId, [FromBody] ReorderRequest request)
    {
        try
        {
            var success = await _repository.ReorderRecipesAsync(cookbookId, sectionId, request.Ids);
            if (!success) return StatusCode(500, new { message = "Reorder failed" });
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reordering recipes in section {SectionId}", sectionId);
            return StatusCode(500, new { message = "An error occurred while reordering recipes" });
        }
    }
}
