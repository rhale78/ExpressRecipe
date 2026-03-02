using ExpressRecipe.CookbookService.Data;
using ExpressRecipe.CookbookService.Models;
using ExpressRecipe.Shared.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpressRecipe.CookbookService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CookbooksController : ControllerBase
{
    private readonly ICookbookRepository _repository;
    private readonly HybridCacheService _cache;
    private readonly ILogger<CookbooksController> _logger;

    public CookbooksController(ICookbookRepository repository, HybridCacheService cache, ILogger<CookbooksController> logger)
    {
        _repository = repository;
        _cache = cache;
        _logger = logger;
    }

    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(claim) || !Guid.TryParse(claim, out var id)) return null;
        return id;
    }

    [HttpGet]
    public async Task<ActionResult<CookbookSearchResult>> GetCookbooks(
        [FromQuery] string? searchTerm,
        [FromQuery] string? visibility,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var userId = GetCurrentUserId();
            List<CookbookSummaryDto> items;
            int totalCount;
            if (userId.HasValue && string.IsNullOrEmpty(visibility))
            {
                items = await _repository.GetUserCookbooksAsync(userId.Value, page, pageSize);
                totalCount = await _repository.GetUserCookbookCountAsync(userId.Value);
            }
            else
            {
                items = await _repository.SearchCookbooksAsync(searchTerm, visibility ?? "Public", page, pageSize);
                totalCount = await _repository.SearchCookbooksCountAsync(searchTerm, visibility ?? "Public");
            }
            return Ok(new CookbookSearchResult { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cookbooks");
            return StatusCode(500, new { message = "An error occurred while retrieving cookbooks" });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CookbookDto>> GetCookbook(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var cacheKey = string.Format(CacheKeys.CookbookById, id);
            var cookbook = await _cache.GetOrSetAsync<CookbookDto?>(cacheKey,
                async _ => await _repository.GetCookbookByIdAsync(id),
                expiration: TimeSpan.FromMinutes(30));
            if (cookbook == null) return NotFound(new { message = "Cookbook not found" });

            if (!userId.HasValue || !await _repository.CanViewAsync(id, userId.Value))
                return Forbid();

            await _repository.IncrementViewCountAsync(id);
            return Ok(cookbook);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cookbook {Id}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving the cookbook" });
        }
    }

    [HttpGet("slug/{slug}")]
    [AllowAnonymous]
    public async Task<ActionResult<CookbookDto>> GetCookbookBySlug(string slug)
    {
        try
        {
            var cacheKey = string.Format(CacheKeys.CookbookBySlug, slug);
            var cookbook = await _cache.GetOrSetAsync<CookbookDto?>(cacheKey,
                async _ => await _repository.GetCookbookBySlugAsync(slug),
                expiration: TimeSpan.FromMinutes(30));
            if (cookbook == null) return NotFound(new { message = "Cookbook not found" });
            if (cookbook.Visibility != "Public") return Forbid();

            await _repository.IncrementViewCountAsync(cookbook.Id);
            return Ok(cookbook);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cookbook by slug {Slug}", slug);
            return StatusCode(500, new { message = "An error occurred while retrieving the cookbook" });
        }
    }

    [HttpPost]
    public async Task<ActionResult<Guid>> CreateCookbook([FromBody] CreateCookbookRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized(new { message = "User not authenticated" });

            var id = await _repository.CreateCookbookAsync(request, userId.Value);
            return CreatedAtAction(nameof(GetCookbook), new { id }, new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating cookbook");
            return StatusCode(500, new { message = "An error occurred while creating the cookbook" });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> UpdateCookbook(Guid id, [FromBody] UpdateCookbookRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized(new { message = "User not authenticated" });

            var success = await _repository.UpdateCookbookAsync(id, userId.Value, request);
            if (!success) return NotFound(new { message = "Cookbook not found or not authorized" });

            // Invalidate cache — evict both id-based and slug-based entries
            // Read the old slug before the cache entry is gone
            var existing = await _repository.GetCookbookByIdAsync(id, includeSections: false);
            await _cache.RemoveAsync(string.Format(CacheKeys.CookbookById, id));
            if (existing?.WebSlug is { Length: > 0 } oldSlug)
                await _cache.RemoveAsync(string.Format(CacheKeys.CookbookBySlug, oldSlug));
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating cookbook {Id}", id);
            return StatusCode(500, new { message = "An error occurred while updating the cookbook" });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteCookbook(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized(new { message = "User not authenticated" });

            var success = await _repository.DeleteCookbookAsync(id, userId.Value);
            if (!success) return NotFound(new { message = "Cookbook not found" });

            // Invalidate cache — evict both id-based and slug-based entries
            var existing = await _repository.GetCookbookByIdAsync(id, includeSections: false);
            await _cache.RemoveAsync(string.Format(CacheKeys.CookbookById, id));
            if (existing?.WebSlug is { Length: > 0 } oldSlug)
                await _cache.RemoveAsync(string.Format(CacheKeys.CookbookBySlug, oldSlug));
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting cookbook {Id}", id);
            return StatusCode(500, new { message = "An error occurred while deleting the cookbook" });
        }
    }

    [HttpPost("{id:guid}/favorite")]
    public async Task<ActionResult> FavoriteCookbook(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized(new { message = "User not authenticated" });

            await _repository.FavoriteCookbookAsync(id, userId.Value);
            return Ok(new { message = "Cookbook favorited" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error favoriting cookbook {Id}", id);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    [HttpDelete("{id:guid}/favorite")]
    public async Task<ActionResult> UnfavoriteCookbook(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized(new { message = "User not authenticated" });

            await _repository.UnfavoriteCookbookAsync(id, userId.Value);
            return Ok(new { message = "Cookbook unfavorited" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unfavoriting cookbook {Id}", id);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    [HttpGet("favorites")]
    public async Task<ActionResult<List<CookbookSummaryDto>>> GetMyFavorites([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized(new { message = "User not authenticated" });

            var favorites = await _repository.GetFavoriteCookbooksAsync(userId.Value, page, pageSize);
            return Ok(favorites);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting favorites");
            return StatusCode(500, new { message = "An error occurred while retrieving favorites" });
        }
    }

    [HttpPost("{id:guid}/rate")]
    public async Task<ActionResult> RateCookbook(Guid id, [FromBody] RateCookbookRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized(new { message = "User not authenticated" });

            if (request.Rating < 1 || request.Rating > 5)
                return BadRequest(new { message = "Rating must be between 1 and 5" });

            await _repository.RateCookbookAsync(id, userId.Value, request.Rating);
            var (avg, count) = await _repository.GetRatingsAsync(id);
            return Ok(new { averageRating = avg, ratingCount = count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rating cookbook {Id}", id);
            return StatusCode(500, new { message = "An error occurred while rating the cookbook" });
        }
    }

    [HttpGet("{id:guid}/comments")]
    public async Task<ActionResult<List<CookbookCommentDto>>> GetComments(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var comments = await _repository.GetCommentsAsync(id, page, pageSize);
            return Ok(comments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting comments for cookbook {Id}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving comments" });
        }
    }

    [HttpPost("{id:guid}/comments")]
    public async Task<ActionResult<Guid>> AddComment(Guid id, [FromBody] AddCommentRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized(new { message = "User not authenticated" });

            var commentId = await _repository.AddCommentAsync(id, userId.Value, request.Content);
            return Ok(new { id = commentId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding comment to cookbook {Id}", id);
            return StatusCode(500, new { message = "An error occurred while adding the comment" });
        }
    }

    [HttpDelete("{cookbookId:guid}/comments/{commentId:guid}")]
    public async Task<ActionResult> DeleteComment(Guid cookbookId, Guid commentId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized(new { message = "User not authenticated" });

            var success = await _repository.DeleteCommentAsync(commentId, userId.Value);
            if (!success) return NotFound(new { message = "Comment not found" });
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting comment {CommentId}", commentId);
            return StatusCode(500, new { message = "An error occurred while deleting the comment" });
        }
    }

    [HttpPost("{id:guid}/share")]
    public async Task<ActionResult> ShareCookbook(Guid id, [FromBody] ShareCookbookRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized(new { message = "User not authenticated" });

            var success = await _repository.ShareCookbookAsync(id, userId.Value, request.TargetUserId, request.CanEdit);
            if (!success) return NotFound(new { message = "Cookbook not found or not authorized" });
            return Ok(new { message = "Cookbook shared" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sharing cookbook {Id}", id);
            return StatusCode(500, new { message = "An error occurred while sharing the cookbook" });
        }
    }

    [HttpDelete("{id:guid}/share/{targetUserId:guid}")]
    public async Task<ActionResult> RevokeShare(Guid id, Guid targetUserId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized(new { message = "User not authenticated" });

            var success = await _repository.RevokeCookbookShareAsync(id, userId.Value, targetUserId);
            if (!success) return NotFound(new { message = "Share not found" });
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking share for cookbook {Id}", id);
            return StatusCode(500, new { message = "An error occurred while revoking the share" });
        }
    }

    [HttpPost("merge")]
    public async Task<ActionResult<Guid>> MergeCookbooks([FromBody] MergeCookbooksRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized(new { message = "User not authenticated" });

            var newId = await _repository.MergeCookbooksAsync(userId.Value, request);
            return CreatedAtAction(nameof(GetCookbook), new { id = newId }, new { id = newId });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error merging cookbooks");
            return StatusCode(500, new { message = "An error occurred while merging cookbooks" });
        }
    }

    [HttpPost("{id:guid}/split")]
    public async Task<ActionResult<List<Guid>>> SplitCookbook(Guid id, [FromBody] SplitCookbookRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized(new { message = "User not authenticated" });

            var newIds = await _repository.SplitCookbookAsync(id, userId.Value, request);
            return Ok(newIds);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error splitting cookbook {Id}", id);
            return StatusCode(500, new { message = "An error occurred while splitting the cookbook" });
        }
    }

    [HttpPost("{id:guid}/extract-recipes")]
    public async Task<ActionResult<List<Guid>>> ExtractRecipes(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized(new { message = "User not authenticated" });

            var recipeIds = await _repository.ExtractRecipesFromCookbookAsync(id, userId.Value);
            return Ok(recipeIds);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting recipes from cookbook {Id}", id);
            return StatusCode(500, new { message = "An error occurred while extracting recipes" });
        }
    }
}
