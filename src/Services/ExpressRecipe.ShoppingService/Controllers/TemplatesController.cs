using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ExpressRecipe.ShoppingService.Data;

namespace ExpressRecipe.ShoppingService.Controllers;

[Authorize]
[ApiController]
[Route("api/shopping/[controller]")]
public class TemplatesController : ControllerBase
{
    private readonly ILogger<TemplatesController> _logger;
    private readonly IShoppingRepository _repository;

    public TemplatesController(ILogger<TemplatesController> logger, IShoppingRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    /// <summary>
    /// Get user's templates
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetTemplates()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var templates = await _repository.GetUserTemplatesAsync(userId.Value);
        return Ok(templates);
    }

    /// <summary>
    /// Get household's templates
    /// </summary>
    [HttpGet("household/{householdId}")]
    public async Task<IActionResult> GetHouseholdTemplates(Guid householdId)
    {
        var templates = await _repository.GetHouseholdTemplatesAsync(householdId);
        return Ok(templates);
    }

    /// <summary>
    /// Get template by ID with items
    /// </summary>
    [HttpGet("{templateId}")]
    public async Task<IActionResult> GetTemplate(Guid templateId)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var templates = await _repository.GetUserTemplatesAsync(userId.Value);
        var template = templates.FirstOrDefault(t => t.Id == templateId);
        
        if (template == null)
            return NotFound();

        return Ok(template);
    }

    /// <summary>
    /// Create new template
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateTemplate([FromBody] CreateTemplateRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var templateId = await _repository.CreateTemplateAsync(
            userId.Value,
            request.HouseholdId,
            request.Name,
            request.Description,
            request.Category
        );

        _logger.LogInformation("User {UserId} created template {TemplateId}", userId, templateId);
        return CreatedAtAction(nameof(GetTemplate), new { templateId }, new { id = templateId });
    }

    /// <summary>
    /// Get template items
    /// </summary>
    [HttpGet("{templateId}/items")]
    public async Task<IActionResult> GetTemplateItems(Guid templateId)
    {
        var items = await _repository.GetTemplateItemsAsync(templateId);
        return Ok(items);
    }

    /// <summary>
    /// Add item to template
    /// </summary>
    [HttpPost("{templateId}/items")]
    public async Task<IActionResult> AddItemToTemplate(Guid templateId, [FromBody] AddTemplateItemRequest request)
    {
        var itemId = await _repository.AddItemToTemplateAsync(
            templateId,
            request.ProductId,
            request.CustomName,
            request.Quantity,
            request.Unit,
            request.Category
        );

        _logger.LogInformation("Added item {ItemId} to template {TemplateId}", itemId, templateId);
        return Ok(new { id = itemId });
    }

    /// <summary>
    /// Create shopping list from template
    /// </summary>
    [HttpPost("{templateId}/create-list")]
    public async Task<IActionResult> CreateListFromTemplate(Guid templateId, [FromBody] CreateListFromTemplateRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var listId = await _repository.CreateListFromTemplateAsync(templateId, userId.Value, request.ListName);

        _logger.LogInformation("User {UserId} created list {ListId} from template {TemplateId}", 
            userId, listId, templateId);
        
        return CreatedAtAction("GetList", "Shopping", new { id = listId }, new { id = listId });
    }

    /// <summary>
    /// Delete template
    /// </summary>
    [HttpDelete("{templateId}")]
    public async Task<IActionResult> DeleteTemplate(Guid templateId)
    {
        var userId = GetUserId();
        await _repository.DeleteTemplateAsync(templateId);
        _logger.LogInformation("User {UserId} deleted template {TemplateId}", userId, templateId);
        return NoContent();
    }
}

public record CreateTemplateRequest(
    Guid? HouseholdId,
    string Name,
    string? Description,
    string? Category
);

public record AddTemplateItemRequest(
    Guid? ProductId,
    string? CustomName,
    decimal Quantity,
    string? Unit,
    string? Category
);

public record CreateListFromTemplateRequest(
    string ListName
);
