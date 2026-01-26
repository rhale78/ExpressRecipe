using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ExpressRecipe.CommunityService.Data;

namespace ExpressRecipe.CommunityService.Controllers
{
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

        private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet("contributions")]
        public async Task<IActionResult> GetContributions([FromQuery] int limit = 50)
        {
            Guid userId = GetUserId();
            List<UserContributionDto> contributions = await _repository.GetUserContributionsAsync(userId, limit);
            return Ok(contributions);
        }

        [HttpGet("leaderboard")]
        public async Task<IActionResult> GetLeaderboard([FromQuery] string period = "month", [FromQuery] int limit = 100)
        {
            LeaderboardDto leaderboard = await _repository.GetLeaderboardAsync(period, limit);
            return Ok(leaderboard);
        }

        [HttpGet("points")]
        public async Task<IActionResult> GetPoints()
        {
            Guid userId = GetUserId();
            var points = await _repository.GetUserPointsAsync(userId);
            return Ok(new { points });
        }

        [HttpPost("products")]
        public async Task<IActionResult> SubmitProduct([FromBody] SubmitProductRequest request)
        {
            Guid userId = GetUserId();
            Guid submissionId = await _repository.SubmitProductAsync(
                userId, request.Name, request.Brand, request.Barcode, request.Category, request.Photo, request.IngredientsText);
            return Ok(new { id = submissionId });
        }

        [HttpGet("products/submissions")]
        public async Task<IActionResult> GetSubmissions()
        {
            Guid userId = GetUserId();
            List<ProductSubmissionDto> submissions = await _repository.GetUserSubmissionsAsync(userId);
            return Ok(submissions);
        }

        [HttpPost("reports")]
        public async Task<IActionResult> CreateReport([FromBody] CreateReportRequest request)
        {
            Guid userId = GetUserId();
            Guid reportId = await _repository.CreateReportAsync(
                userId, request.EntityType, request.EntityId, request.ReportType, request.Reason, request.Details);
            return Ok(new { id = reportId });
        }

        [HttpPost("reviews")]
        public async Task<IActionResult> CreateReview([FromBody] CreateReviewRequest request)
        {
            Guid userId = GetUserId();
            Guid reviewId = await _repository.CreateReviewAsync(
                userId, request.EntityType, request.EntityId, request.Rating, request.Comment, request.IsVerifiedPurchase);
            return Ok(new { id = reviewId });
        }

        [HttpGet("{entityType}/{entityId}/reviews")]
        public async Task<IActionResult> GetEntityReviews(string entityType, Guid entityId, [FromQuery] int limit = 50)
        {
            List<CommunityReviewDto> reviews = await _repository.GetEntityReviewsAsync(entityType, entityId, limit);
            return Ok(reviews);
        }

        [HttpGet("{entityType}/{entityId}/reviews/summary")]
        public async Task<IActionResult> GetReviewSummary(string entityType, Guid entityId)
        {
            ReviewSummaryDto summary = await _repository.GetReviewSummaryAsync(entityType, entityId);
            return Ok(summary);
        }

        [HttpPost("reviews/{id}/vote")]
        public async Task<IActionResult> VoteReview(Guid id, [FromBody] VoteRequest request)
        {
            Guid userId = GetUserId();
            await _repository.VoteReviewAsync(id, userId, request.IsHelpful);
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
}
