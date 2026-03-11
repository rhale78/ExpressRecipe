using ExpressRecipe.CommunityService.Data;
using ExpressRecipe.CommunityService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpressRecipe.CommunityService.Controllers;

[ApiController]
[Route("api/community")]
public class RecipeGalleryController : ControllerBase
{
    private const string InternalKeyHeader = "X-Internal-Api-Key";

    private readonly ICommunityRecipeRepository _galleryRepository;
    private readonly IApprovalQueueService _approvalQueue;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RecipeGalleryController> _logger;

    public RecipeGalleryController(
        ICommunityRecipeRepository galleryRepository,
        IApprovalQueueService approvalQueue,
        ILogger<RecipeGalleryController> logger,
        IConfiguration configuration)
    {
        _galleryRepository = galleryRepository;
        _approvalQueue = approvalQueue;
        _configuration = configuration;
        _logger = logger;
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    /// <summary>
    /// GET /api/community/recipes — public recipe gallery (approved only).
    /// Filters: cuisine, diet, minRating, search
    /// Sort: cursor-based (afterId)
    /// </summary>
    [HttpGet("recipes")]
    [AllowAnonymous]
    public async Task<IActionResult> GetGallery(
        [FromQuery] string? cuisine,
        [FromQuery] string? diet,
        [FromQuery] string? search,
        [FromQuery] Guid? afterId,
        [FromQuery] decimal minRating = 0,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);

        var page = await _galleryRepository.GetGalleryPageAsync(
            cuisine, diet, minRating, search, afterId, pageSize, ct);

        return Ok(page);
    }

    /// <summary>
    /// GET /api/community/recipes/{id} — get a single gallery item.
    /// Anonymous users can only see Approved recipes.
    /// Authenticated submitters and admins can also see non-approved items.
    /// </summary>
    [HttpGet("recipes/{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetRecipe(Guid id, CancellationToken ct)
    {
        var recipe = await _galleryRepository.GetByIdAsync(id, ct);
        if (recipe == null) return NotFound();

        // Visibility: non-approved items are only visible to the submitter or admins
        var isApproved = string.Equals(recipe.Status, "Approved", StringComparison.OrdinalIgnoreCase);
        if (!isApproved)
        {
            var isAuthenticated = User.Identity?.IsAuthenticated == true;
            if (!isAuthenticated)
            {
                return NotFound();
            }

            var userId = GetUserId();
            var isAdmin = User.IsInRole("Admin");
            var isOwner = userId.HasValue && recipe.SubmittedBy == userId.Value;

            if (!isAdmin && !isOwner)
            {
                return NotFound();
            }
        }

        await _galleryRepository.IncrementViewCountAsync(id, ct);
        return Ok(recipe);
    }

    /// <summary>
    /// POST /api/community/recipes — submit a recipe for gallery approval
    /// </summary>
    [HttpPost("recipes")]
    [Authorize]
    public async Task<IActionResult> SubmitRecipe([FromBody] SubmitRecipeRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var existing = await _galleryRepository.GetByRecipeIdAsync(request.RecipeId, ct);
        if (existing != null)
        {
            return Conflict(new { message = "Recipe already submitted." });
        }

        var communityRecipeId = await _galleryRepository.SubmitRecipeAsync(request.RecipeId, userId.Value, ct);

        await _approvalQueue.SubmitForApprovalAsync(
            request.RecipeId, "Recipe", request.ContentSummary, ct);

        _logger.LogInformation("User {UserId} submitted recipe {RecipeId} for gallery approval",
            userId.Value, request.RecipeId);

        return CreatedAtAction(nameof(GetRecipe), new { id = communityRecipeId }, new { id = communityRecipeId });
    }

    /// <summary>
    /// POST /api/community/recipes/{id}/approve — approve or reject (admin)
    /// </summary>
    [HttpPost("recipes/{id:guid}/approve")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveRecipeRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var recipe = await _galleryRepository.GetByIdAsync(id, ct);
        if (recipe == null) return NotFound();

        if (request.Approve)
        {
            await _approvalQueue.ApproveAsync(recipe.RecipeId, "Recipe", userId.Value.ToString(), ct);
        }
        else
        {
            await _approvalQueue.RejectAsync(recipe.RecipeId, "Recipe", userId.Value.ToString(),
                request.RejectionReason ?? "Rejected by moderator", ct);
        }

        return NoContent();
    }

    /// <summary>
    /// POST /api/community/recipes/ai-score — AI scoring callback (internal service-to-service).
    /// Requires X-Internal-Api-Key header matching Internal:ApiKey configuration.
    /// </summary>
    [HttpPost("recipes/ai-score")]
    [AllowAnonymous]
    public async Task<IActionResult> ProcessAIScore([FromBody] AIScoreRequest request, CancellationToken ct)
    {
        var configuredKey = _configuration["Internal:ApiKey"];
        if (!string.IsNullOrWhiteSpace(configuredKey))
        {
            Request.Headers.TryGetValue(InternalKeyHeader, out var providedKey);
            if (string.IsNullOrWhiteSpace(providedKey) ||
                !string.Equals(configuredKey, providedKey.ToString(), StringComparison.Ordinal))
            {
                return Unauthorized(new { message = "Invalid or missing internal API key." });
            }
        }

        await _approvalQueue.ProcessAIApprovalAsync(request.QueueItemId, request.Score, ct);
        return NoContent();
    }
}

public sealed class SubmitRecipeRequest
{
    public Guid RecipeId { get; set; }
    public string? ContentSummary { get; set; }
}

public sealed class ApproveRecipeRequest
{
    public bool Approve { get; set; }
    public string? RejectionReason { get; set; }
}

public sealed class AIScoreRequest
{
    public Guid QueueItemId { get; set; }
    public decimal Score { get; set; }
}
