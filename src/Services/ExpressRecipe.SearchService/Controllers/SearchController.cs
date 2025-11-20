using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ExpressRecipe.SearchService.Data;

namespace ExpressRecipe.SearchService.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private readonly ILogger<SearchController> _logger;
    private readonly ISearchRepository _repository;

    public SearchController(ILogger<SearchController> logger, ISearchRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string query,
        [FromQuery] string? entityType,
        [FromQuery] string? category,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0)
    {
        var userId = GetUserId();
        var results = await _repository.SearchAsync(query, entityType, category, null, limit, offset);

        // Record search
        await _repository.RecordSearchAsync(userId, query, entityType, results.TotalResults, results.TotalResults > 0);

        return Ok(results);
    }

    [HttpGet("suggest")]
    public async Task<IActionResult> GetSuggestions([FromQuery] string q, [FromQuery] int limit = 10)
    {
        var suggestions = await _repository.GetSuggestionsAsync(q, limit);
        return Ok(suggestions);
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory([FromQuery] int limit = 20)
    {
        var userId = GetUserId();
        var history = await _repository.GetUserSearchHistoryAsync(userId, limit);
        return Ok(history);
    }

    [HttpDelete("history")]
    public async Task<IActionResult> ClearHistory()
    {
        var userId = GetUserId();
        await _repository.ClearUserSearchHistoryAsync(userId);
        return NoContent();
    }

    [HttpGet("popular")]
    public async Task<IActionResult> GetPopularSearches([FromQuery] string? entityType, [FromQuery] int daysBack = 30, [FromQuery] int limit = 20)
    {
        var popular = await _repository.GetPopularSearchesAsync(entityType, daysBack, limit);
        return Ok(popular);
    }

    [HttpGet("recommendations")]
    public async Task<IActionResult> GetRecommendations([FromQuery] string? entityType, [FromQuery] int limit = 20)
    {
        var userId = GetUserId();
        var recommendations = await _repository.GetUserRecommendationsAsync(userId, entityType, limit);
        return Ok(recommendations);
    }

    [HttpPost("recommendations/refresh")]
    public async Task<IActionResult> RefreshRecommendations()
    {
        var userId = GetUserId();
        await _repository.RefreshRecommendationsAsync(userId);
        return NoContent();
    }

    [HttpPost("preferences")]
    public async Task<IActionResult> SavePreference([FromBody] SavePreferenceRequest request)
    {
        var userId = GetUserId();
        await _repository.SaveSearchPreferenceAsync(userId, request.PreferenceKey, request.PreferenceValue);
        return NoContent();
    }

    [HttpGet("preferences")]
    public async Task<IActionResult> GetPreferences()
    {
        var userId = GetUserId();
        var prefs = await _repository.GetUserPreferencesAsync(userId);
        return Ok(prefs);
    }
}

public class SavePreferenceRequest
{
    public string PreferenceKey { get; set; } = string.Empty;
    public string PreferenceValue { get; set; } = string.Empty;
}
