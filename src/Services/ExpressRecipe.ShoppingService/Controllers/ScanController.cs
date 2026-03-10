using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ExpressRecipe.ShoppingService.Data;

namespace ExpressRecipe.ShoppingService.Controllers;

[Authorize]
[ApiController]
[Route("api/shopping/[controller]")]
public class ScanController : ControllerBase
{
    private readonly ILogger<ScanController> _logger;
    private readonly IShoppingRepository _repository;

    public ScanController(ILogger<ScanController> logger, IShoppingRepository repository)
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
    /// Start shopping scan session (lock mode for continuous scanning)
    /// </summary>
    [HttpPost("start")]
    public async Task<IActionResult> StartSession([FromBody] StartScanSessionRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var sessionId = await _repository.StartShoppingScanSessionAsync(userId.Value, request.ShoppingListId, request.StoreId);

        _logger.LogInformation("User {UserId} started shopping scan session {SessionId} for list {ListId}", 
            userId, sessionId, request.ShoppingListId);
        
        return Ok(new { sessionId });
    }

    /// <summary>
    /// Get active shopping scan session
    /// </summary>
    [HttpGet("active")]
    public async Task<IActionResult> GetActiveSession()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var session = await _repository.GetActiveShoppingScanSessionAsync(userId.Value);

        if (session == null)
            return NotFound("No active scan session");

        return Ok(session);
    }

    /// <summary>
    /// Scan item for purchase (marks as checked and records price)
    /// </summary>
    [HttpPost("{sessionId}/purchase")]
    public async Task<IActionResult> ScanPurchaseItem(Guid sessionId, [FromBody] ScanPurchaseRequest request)
    {
        try
        {
            var itemId = await _repository.ScanPurchaseItemAsync(
                sessionId,
                request.Barcode,
                request.Quantity,
                request.Price
            );

            _logger.LogInformation("Scanned item {ItemId} in session {SessionId}", itemId, sessionId);
            return Ok(new { itemId });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Scan error in session {SessionId}: {Message}", sessionId, ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// End shopping scan session
    /// </summary>
    [HttpPost("{sessionId}/end")]
    public async Task<IActionResult> EndSession(Guid sessionId, [FromBody] EndScanSessionRequest? request = null)
    {
        await _repository.EndShoppingScanSessionAsync(sessionId, request?.TotalSpent);
        _logger.LogInformation("Ended shopping scan session {SessionId}", sessionId);
        return NoContent();
    }

    /// <summary>
    /// Add purchased items to inventory (requires InventoryService integration)
    /// </summary>
    [HttpPost("add-to-inventory")]
    public async Task<IActionResult> AddPurchasedItemsToInventory([FromBody] AddToInventoryRequest request)
    {
        var userId = GetUserId();
        
        try
        {
            await _repository.AddPurchasedItemsToInventoryAsync(request.ListId);
            
            _logger.LogInformation("User {UserId} added purchased items from list {ListId} to inventory", 
                userId, request.ListId);
            
            return Ok(new { message = "Items added to inventory (integration pending)" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding purchased items to inventory for list {ListId}", request.ListId);
            return StatusCode(500, new { error = "Failed to add items to inventory", details = ex.Message });
        }
    }

    /// <summary>
    /// Get report for completed shopping session
    /// </summary>
    [HttpGet("{sessionId}/report")]
    public async Task<IActionResult> GetSessionReport(Guid sessionId)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var session = await _repository.GetActiveShoppingScanSessionAsync(userId.Value);
        
        if (session == null || session.Id != sessionId)
            return NotFound("Session not found");

        // TODO: Add more detailed report with items scanned, savings, etc.
        return Ok(session);
    }
}

public record StartScanSessionRequest(
    Guid ShoppingListId,
    Guid? StoreId
);

public record ScanPurchaseRequest(
    string Barcode,
    decimal Quantity,
    decimal Price
);

public record EndScanSessionRequest(
    decimal? TotalSpent
);

public record AddToInventoryRequest(
    Guid ListId
);
