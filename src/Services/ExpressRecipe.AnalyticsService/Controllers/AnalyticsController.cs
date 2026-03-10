using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ExpressRecipe.AnalyticsService.Data;

namespace ExpressRecipe.AnalyticsService.Controllers;

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

    private Guid? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    [HttpPost("events")]
    public async Task<IActionResult> TrackEvent([FromBody] TrackEventRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var eventId = await _repository.TrackEventAsync(
            userId.Value, request.EventType, request.EventName, request.Properties, request.SessionId, request.DeviceId);
        return Ok(new { id = eventId });
    }

    [HttpGet("events")]
    public async Task<IActionResult> GetEvents([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, [FromQuery] int limit = 100)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var events = await _repository.GetUserEventsAsync(userId.Value, startDate, endDate, limit);
        return Ok(events);
    }

    [HttpGet("usage")]
    public async Task<IActionResult> GetUsageStats([FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var stats = await _repository.GetUserUsageStatsAsync(userId.Value, startDate, endDate);
        return Ok(stats);
    }

    [HttpGet("usage/summary")]
    public async Task<IActionResult> GetUsageSummary([FromQuery] string period = "week")
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var summary = await _repository.GetUsageSummaryAsync(userId.Value, period);
        return Ok(summary);
    }

    [HttpGet("patterns")]
    public async Task<IActionResult> GetPatterns([FromQuery] string? patternType)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var patterns = await _repository.GetUserPatternsAsync(userId.Value, patternType);
        return Ok(patterns);
    }

    [HttpGet("insights")]
    public async Task<IActionResult> GetInsights([FromQuery] bool unreadOnly = false)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var insights = await _repository.GetUserInsightsAsync(userId.Value, unreadOnly);
        return Ok(insights);
    }

    [HttpGet("insights/{id}")]
    public async Task<IActionResult> GetInsight(Guid id)
    {
        var insight = await _repository.GetInsightAsync(id);
        if (insight == null) return NotFound();
        return Ok(insight);
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
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var metrics = await _repository.GetDashboardMetricsAsync(userId.Value);
        return Ok(metrics);
    }

    [HttpGet("metrics")]
    public async Task<IActionResult> GetMetrics(
        [FromQuery] string metricName,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)
    {
        var metrics = await _repository.GetMetricsAsync(metricName, startDate, endDate);
        return Ok(metrics);
    }
}

public class TrackEventRequest
{
    public string EventType { get; set; } = string.Empty;
    public string EventName { get; set; } = string.Empty;
    public Dictionary<string, string> Properties { get; set; } = new();
    public string? SessionId { get; set; }
    public string? DeviceId { get; set; }
}
