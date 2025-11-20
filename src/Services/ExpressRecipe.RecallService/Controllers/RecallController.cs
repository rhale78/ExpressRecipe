using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ExpressRecipe.RecallService.Data;

namespace ExpressRecipe.RecallService.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class RecallController : ControllerBase
{
    private readonly ILogger<RecallController> _logger;
    private readonly IRecallRepository _repository;

    public RecallController(ILogger<RecallController> logger, IRecallRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetRecentRecalls([FromQuery] int limit = 100)
    {
        var recalls = await _repository.GetRecentRecallsAsync(limit);
        return Ok(recalls);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetRecall(Guid id)
    {
        var recall = await _repository.GetRecallAsync(id);
        if (recall == null) return NotFound();
        return Ok(recall);
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchRecalls(
        [FromQuery] string? searchTerm,
        [FromQuery] string? severity,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate)
    {
        var recalls = await _repository.SearchRecallsAsync(searchTerm ?? "", severity, startDate, endDate);
        return Ok(recalls);
    }

    [HttpGet("{id}/products")]
    public async Task<IActionResult> GetRecallProducts(Guid id)
    {
        var products = await _repository.GetRecallProductsAsync(id);
        return Ok(products);
    }

    [HttpGet("alerts")]
    public async Task<IActionResult> GetUserAlerts([FromQuery] bool unacknowledgedOnly = true)
    {
        var userId = GetUserId();
        var alerts = await _repository.GetUserAlertsAsync(userId, unacknowledgedOnly);
        return Ok(alerts);
    }

    [HttpPut("alerts/{id}/acknowledge")]
    public async Task<IActionResult> AcknowledgeAlert(Guid id)
    {
        await _repository.AcknowledgeAlertAsync(id);
        return NoContent();
    }

    [HttpGet("alerts/count")]
    public async Task<IActionResult> GetUnacknowledgedCount()
    {
        var userId = GetUserId();
        var count = await _repository.GetUnacknowledgedCountAsync(userId);
        return Ok(new { count });
    }

    [HttpPost("subscriptions")]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest request)
    {
        var userId = GetUserId();
        var subId = await _repository.SubscribeToRecallsAsync(userId, request.Category, request.Brand, request.Keyword);
        return Ok(new { id = subId });
    }

    [HttpGet("subscriptions")]
    public async Task<IActionResult> GetSubscriptions()
    {
        var userId = GetUserId();
        var subs = await _repository.GetUserSubscriptionsAsync(userId);
        return Ok(subs);
    }

    [HttpDelete("subscriptions/{id}")]
    public async Task<IActionResult> Unsubscribe(Guid id)
    {
        await _repository.UnsubscribeAsync(id);
        return NoContent();
    }
}

public class SubscribeRequest
{
    public string? Category { get; set; }
    public string? Brand { get; set; }
    public string? Keyword { get; set; }
}
