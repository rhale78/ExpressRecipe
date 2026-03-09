using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ExpressRecipe.ShoppingService.Data;
using ExpressRecipe.ShoppingService.Services;

namespace ExpressRecipe.ShoppingService.Controllers;

[Authorize]
[ApiController]
[Route("api/shopping/[controller]")]
public class ScanController : ControllerBase
{
    private readonly ILogger<ScanController> _logger;
    private readonly IShoppingRepository _repository;
    private readonly IShoppingSessionService _sessionService;

    public ScanController(
        ILogger<ScanController> logger,
        IShoppingRepository repository,
        IShoppingSessionService sessionService)
    {
        _logger = logger;
        _repository = repository;
        _sessionService = sessionService;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Start shopping scan session (lock mode for continuous scanning)
    /// </summary>
    [HttpPost("start")]
    public async Task<IActionResult> StartSession([FromBody] StartScanSessionRequest request)
    {
        var userId = GetUserId();
        var sessionId = await _repository.StartShoppingScanSessionAsync(userId, request.ShoppingListId, request.StoreId);

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
        var session = await _repository.GetActiveShoppingScanSessionAsync(userId);

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
    /// Complete a shopping scan session and notify inventory service for checked items.
    /// </summary>
    [HttpPost("{sessionId}/complete")]
    public async Task<IActionResult> CompleteSession(Guid sessionId, CancellationToken ct = default)
    {
        try
        {
            Guid userId = GetUserId();
            ShoppingSessionSummaryDto summary = await _sessionService.CompleteSessionAsync(sessionId, userId, ct);
            _logger.LogInformation("User {UserId} completed shopping session {SessionId}", userId, sessionId);
            return Ok(summary);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Session {SessionId} not found or invalid: {Message}", sessionId, ex.Message);
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Unauthorized attempt to complete session {SessionId}: {Message}", sessionId, ex.Message);
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing session {SessionId}", sessionId);
            return StatusCode(500, new { message = "An error occurred while completing the session" });
        }
    }

    /// <summary>
    /// Get report for completed shopping session
    /// </summary>
    [HttpGet("{sessionId}/report")]
    public async Task<IActionResult> GetSessionReport(Guid sessionId)
    {
        var userId = GetUserId();
        var session = await _repository.GetActiveShoppingScanSessionAsync(userId);
        
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
