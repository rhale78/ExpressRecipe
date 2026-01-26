using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ExpressRecipe.AnalyticsService.Data;

namespace ExpressRecipe.AnalyticsService.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class AnalyticsController : ControllerBase
    {
        private readonly ILogger<AnalyticsController> _logger;
        private readonly IAnalyticsRepository _repository;

        public AnalyticsController(ILogger<AnalyticsController> logger, IAnalyticsRepository repository)
        {
            _logger = logger;
            _repository = repository;
        }

        private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpPost("events")]
        public async Task<IActionResult> TrackEvent([FromBody] TrackEventRequest request)
        {
            Guid userId = GetUserId();
            Guid eventId = await _repository.TrackEventAsync(
                userId, request.EventType, request.EventName, request.Properties, request.SessionId, request.DeviceId);
            return Ok(new { id = eventId });
        }

        [HttpGet("events")]
        public async Task<IActionResult> GetEvents([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, [FromQuery] int limit = 100)
        {
            Guid userId = GetUserId();
            List<UserEventDto> events = await _repository.GetUserEventsAsync(userId, startDate, endDate, limit);
            return Ok(events);
        }

        [HttpGet("usage")]
        public async Task<IActionResult> GetUsageStats([FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
        {
            Guid userId = GetUserId();
            List<UsageStatisticsDto> stats = await _repository.GetUserUsageStatsAsync(userId, startDate, endDate);
            return Ok(stats);
        }

        [HttpGet("usage/summary")]
        public async Task<IActionResult> GetUsageSummary([FromQuery] string period = "week")
        {
            Guid userId = GetUserId();
            UsageSummaryDto summary = await _repository.GetUsageSummaryAsync(userId, period);
            return Ok(summary);
        }

        [HttpGet("patterns")]
        public async Task<IActionResult> GetPatterns([FromQuery] string? patternType)
        {
            Guid userId = GetUserId();
            List<PatternDetectionDto> patterns = await _repository.GetUserPatternsAsync(userId, patternType);
            return Ok(patterns);
        }

        [HttpGet("insights")]
        public async Task<IActionResult> GetInsights([FromQuery] bool unreadOnly = false)
        {
            Guid userId = GetUserId();
            List<InsightDto> insights = await _repository.GetUserInsightsAsync(userId, unreadOnly);
            return Ok(insights);
        }

        [HttpGet("insights/{id}")]
        public async Task<IActionResult> GetInsight(Guid id)
        {
            InsightDto? insight = await _repository.GetInsightAsync(id);
            return insight == null ? NotFound() : Ok(insight);
        }

        [HttpPut("insights/{id}/read")]
        public async Task<IActionResult> MarkInsightRead(Guid id)
        {
            await _repository.MarkInsightAsReadAsync(id);
            return NoContent();
        }

        [HttpPut("insights/{id}/dismiss")]
        public async Task<IActionResult> DismissInsight(Guid id)
        {
            await _repository.DismissInsightAsync(id);
            return NoContent();
        }

        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboardMetrics()
        {
            Guid userId = GetUserId();
            Dictionary<string, decimal> metrics = await _repository.GetDashboardMetricsAsync(userId);
            return Ok(metrics);
        }

        [HttpGet("metrics")]
        public async Task<IActionResult> GetMetrics(
            [FromQuery] string metricName,
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate)
        {
            List<AggregateMetricDto> metrics = await _repository.GetMetricsAsync(metricName, startDate, endDate);
            return Ok(metrics);
        }
    }

    public class TrackEventRequest
    {
        public string EventType { get; set; } = string.Empty;
        public string EventName { get; set; } = string.Empty;
        public Dictionary<string, string> Properties { get; set; } = [];
        public string? SessionId { get; set; }
        public string? DeviceId { get; set; }
    }
}
