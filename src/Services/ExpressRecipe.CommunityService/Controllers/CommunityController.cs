using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ExpressRecipe.CommunityService.Data;

namespace ExpressRecipe.CommunityService.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CommunityController : ControllerBase
{
    private readonly ILogger<CommunityController> _logger;
    private readonly ICommunityRepository _repository;

    public CommunityController(ILogger<CommunityController> logger, ICommunityRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    [HttpGet("contributions")]
    public async Task<IActionResult> GetContributions([FromQuery] int limit = 50)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var contributions = await _repository.GetUserContributionsAsync(userId.Value, limit);
        return Ok(contributions);
    }

    [HttpGet("leaderboard")]
    public async Task<IActionResult> GetLeaderboard([FromQuery] string period = "month", [FromQuery] int limit = 100)
    {
        var leaderboard = await _repository.GetLeaderboardAsync(period, limit);
        return Ok(leaderboard);
    }

    [HttpGet("points")]
    public async Task<IActionResult> GetPoints()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var points = await _repository.GetUserPointsAsync(userId.Value);
        return Ok(new { points });
    }

    [HttpPost("products")]
    public async Task<IActionResult> SubmitProduct([FromBody] SubmitProductRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var submissionId = await _repository.SubmitProductAsync(
            userId.Value, request.Name, request.Brand, request.Barcode, request.Category, request.Photo, request.IngredientsText);
        return Ok(new { id = submissionId });
    }

    [HttpGet("products/submissions")]
    public async Task<IActionResult> GetSubmissions()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var submissions = await _repository.GetUserSubmissionsAsync(userId.Value);
        return Ok(submissions);
    }

    [HttpPost("reports")]
    public async Task<IActionResult> CreateReport([FromBody] CreateReportRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var reportId = await _repository.CreateReportAsync(
            userId.Value, request.EntityType, request.EntityId, request.ReportType, request.Reason, request.Details);
        return Ok(new { id = reportId });
    }

    [HttpPost("reviews")]
    public async Task<IActionResult> CreateReview([FromBody] CreateReviewRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var reviewId = await _repository.CreateReviewAsync(
            userId.Value, request.EntityType, request.EntityId, request.Rating, request.Comment, request.IsVerifiedPurchase);
        return Ok(new { id = reviewId });
    }

    [HttpGet("{entityType}/{entityId}/reviews")]
    public async Task<IActionResult> GetEntityReviews(string entityType, Guid entityId, [FromQuery] int limit = 50)
    {
        var reviews = await _repository.GetEntityReviewsAsync(entityType, entityId, limit);
        return Ok(reviews);
    }

    [HttpGet("{entityType}/{entityId}/reviews/summary")]
    public async Task<IActionResult> GetReviewSummary(string entityType, Guid entityId)
    {
        var summary = await _repository.GetReviewSummaryAsync(entityType, entityId);
        return Ok(summary);
    }

    [HttpPost("reviews/{id}/vote")]
    public async Task<IActionResult> VoteReview(Guid id, [FromBody] VoteRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        await _repository.VoteReviewAsync(id, userId.Value, request.IsHelpful);
        return NoContent();
    }
}

public class SubmitProductRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Barcode { get; set; }
    public string? Category { get; set; }
    public byte[]? Photo { get; set; }
    public string? IngredientsText { get; set; }
}

public class CreateReportRequest
{
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string ReportType { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? Details { get; set; }
}

public class CreateReviewRequest
{
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public bool IsVerifiedPurchase { get; set; }
}

public class VoteRequest
{
    public bool IsHelpful { get; set; }
}
