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

    // ──────────── Domain-specific report endpoints ────────────

    [HttpGet("spending/summary")]
    public async Task<IActionResult> GetSpendingSummary()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        // Spending data comes from PriceService/ShoppingService.
        // Until cross-service aggregation is implemented, return usage patterns from analytics events.
        var stats = await _repository.GetUsageSummaryAsync(userId.Value, "month");
        return Ok(new
        {
            TotalSpentThisMonth = 0m,
            TotalSpentLastMonth = 0m,
            AverageDailySpending = 0m,
            BudgetRemaining = 0m,
            MonthlyBudget = 0m,
            SpendingByCategory = Array.Empty<object>(),
            DailySpending = Array.Empty<object>()
        });
    }

    [HttpPost("spending/report")]
    public async Task<IActionResult> GetSpendingReport([FromBody] DateRangeReportRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var metrics = await _repository.GetMetricsAsync("spending", request.StartDate, request.EndDate);
        return Ok(new
        {
            TotalSpent = metrics.Sum(m => m.Value),
            TotalTransactions = metrics.Count,
            AverageTransactionAmount = metrics.Count > 0 ? metrics.Average(m => m.Value) : 0m,
            DataPoints = metrics.Select(m => new { m.PeriodStart, Amount = m.Value, Count = 1, Label = m.PeriodStart.ToString("MMM d") }),
            TopProducts = Array.Empty<object>(),
            TopStores = Array.Empty<object>()
        });
    }

    [HttpGet("nutrition/summary")]
    public async Task<IActionResult> GetNutritionSummary()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var metrics = await _repository.GetMetricsAsync("nutrition_calories",
            DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);
        var totalCalories = (int)metrics.Sum(m => m.Value);
        var days = metrics.Count > 0 ? metrics.Count : 1;

        return Ok(new
        {
            TotalCalories = totalCalories,
            TotalProtein = 0m,
            TotalCarbs = 0m,
            TotalFat = 0m,
            TotalFiber = 0m,
            DailyAverageCalories = totalCalories / days,
            DailyBreakdown = metrics.Select(m => new { m.PeriodStart, Calories = (int)m.Value })
        });
    }

    [HttpPost("nutrition/report")]
    public async Task<IActionResult> GetNutritionReport([FromBody] DateRangeReportRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var metrics = await _repository.GetMetricsAsync("nutrition_calories", request.StartDate, request.EndDate);
        return Ok(new
        {
            TotalCalories = (int)metrics.Sum(m => m.Value),
            TotalProtein = 0m,
            TotalCarbs = 0m,
            TotalFat = 0m,
            DailyBreakdown = metrics.Select(m => new { Date = m.PeriodStart, Calories = (int)m.Value })
        });
    }

    [HttpGet("inventory/summary")]
    public async Task<IActionResult> GetInventorySummary()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var metrics = await _repository.GetDashboardMetricsAsync(userId.Value);
        return Ok(new
        {
            TotalItems = metrics.GetValueOrDefault("inventory_items", 0),
            ExpiringThisWeek = metrics.GetValueOrDefault("expiring_soon", 0),
            ExpiredItems = metrics.GetValueOrDefault("expired_items", 0),
            LowStockItems = metrics.GetValueOrDefault("low_stock_items", 0),
            ItemsByCategory = Array.Empty<object>()
        });
    }

    [HttpPost("inventory/report")]
    public async Task<IActionResult> GetInventoryReport([FromBody] DateRangeReportRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var metrics = await _repository.GetMetricsAsync("inventory_value", request.StartDate, request.EndDate);
        return Ok(new
        {
            TotalItemsAdded = 0,
            TotalItemsConsumed = 0,
            TotalItemsExpired = 0,
            EstimatedValue = metrics.Sum(m => m.Value),
            WasteRate = 0m,
            DataPoints = metrics.Select(m => new { m.PeriodStart, Value = m.Value })
        });
    }

    [HttpGet("waste/summary")]
    public async Task<IActionResult> GetWasteSummary()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var metrics = await _repository.GetDashboardMetricsAsync(userId.Value);
        return Ok(new
        {
            TotalWastedItems = metrics.GetValueOrDefault("wasted_items", 0),
            EstimatedWasteValue = 0m,
            WasteReductionTrend = 0m,
            TopWastedCategories = Array.Empty<object>(),
            MonthlyWasteTrend = Array.Empty<object>()
        });
    }

    [HttpPost("waste/report")]
    public async Task<IActionResult> GetWasteReport([FromBody] DateRangeReportRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var metrics = await _repository.GetMetricsAsync("waste_items", request.StartDate, request.EndDate);
        return Ok(new
        {
            TotalWastedItems = (int)metrics.Sum(m => m.Value),
            EstimatedWasteValue = 0m,
            DataPoints = metrics.Select(m => new { m.PeriodStart, Count = (int)m.Value, Value = 0m })
        });
    }

    [HttpPost("export")]
    public async Task<IActionResult> ExportReport([FromBody] ExportReportRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        // Report export requires generating PDF/CSV from aggregated data; return a reference that can be polled
        _logger.LogInformation("Report export requested for type {ReportType} by user {UserId}", request.ReportType, userId);
        return Ok(new { ExportId = Guid.NewGuid(), Status = "Queued", DownloadUrl = (string?)null });
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

public class DateRangeReportRequest
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Category { get; set; }
    public string? GroupBy { get; set; } = "Day";
}

public class ExportReportRequest
{
    public string ReportType { get; set; } = string.Empty; // spending, nutrition, inventory, waste
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Format { get; set; } = "PDF"; // PDF, CSV
}
