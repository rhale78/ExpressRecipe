using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ExpressRecipe.InventoryService.Data;

namespace ExpressRecipe.InventoryService.Controllers;

/// <summary>
/// Barcode scanning and lock mode operations
/// </summary>
[Authorize]
[ApiController]
[Route("api/inventory/[controller]")]
public class ScanController : ControllerBase
{
    private readonly ILogger<ScanController> _logger;
    private readonly IInventoryRepository _repository;

    public ScanController(ILogger<ScanController> logger, IInventoryRepository repository)
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
    /// Start a new scanning session (lock mode)
    /// </summary>
    [HttpPost("start")]
    public async Task<IActionResult> StartSession([FromBody] StartScanSessionRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            _logger.LogInformation("Starting scan session type {SessionType} for user {UserId}", request.SessionType, userId);
            var sessionId = await _repository.StartScanSessionAsync(
                userId.Value,
                request.HouseholdId,
                request.SessionType,
                request.StorageLocationId);
            var session = await _repository.GetScanSessionByIdAsync(sessionId);
            return CreatedAtAction(nameof(GetSession), new { id = sessionId }, session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting scan session");
            return StatusCode(500, new { message = "An error occurred while starting the scan session" });
        }
    }

    /// <summary>
    /// Get active scanning session for user
    /// </summary>
    [HttpGet("active")]
    public async Task<IActionResult> GetActiveSession()
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            var session = await _repository.GetActiveScanSessionAsync(userId.Value);
            if (session == null)
                return NotFound(new { message = "No active scan session" });
            return Ok(session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active scan session");
            return StatusCode(500, new { message = "An error occurred while retrieving the active scan session" });
        }
    }

    /// <summary>
    /// Get scanning session by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetSession(Guid id)
    {
        try
        {
            var session = await _repository.GetScanSessionByIdAsync(id);
            if (session == null)
                return NotFound();
            return Ok(session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving scan session {SessionId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving the scan session" });
        }
    }

    /// <summary>
    /// Scan barcode to add item to inventory
    /// </summary>
    [HttpPost("{sessionId}/add")]
    public async Task<IActionResult> ScanAdd(Guid sessionId, [FromBody] ScanAddRequest request)
    {
        _logger.LogInformation("Scanning barcode {Barcode} to add in session {SessionId}", request.Barcode, sessionId);
        try
        {
            var itemId = await _repository.ScanAddItemAsync(
                sessionId,
                request.Barcode,
                request.Quantity,
                request.StorageLocationId);
            return Ok(new { itemId, message = "Item added successfully" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning barcode {Barcode} to add in session {SessionId}", request.Barcode, sessionId);
            return StatusCode(500, new { message = "An error occurred while scanning the item" });
        }
    }

    /// <summary>
    /// Scan barcode to mark item as used
    /// </summary>
    [HttpPost("{sessionId}/use")]
    public async Task<IActionResult> ScanUse(Guid sessionId, [FromBody] ScanUseRequest request)
    {
        _logger.LogInformation("Scanning barcode {Barcode} to use in session {SessionId}", request.Barcode, sessionId);
        try
        {
            var itemId = await _repository.ScanUseItemAsync(
                sessionId,
                request.Barcode,
                request.Quantity);
            return Ok(new { itemId, message = "Item usage recorded" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning barcode {Barcode} to use in session {SessionId}", request.Barcode, sessionId);
            return StatusCode(500, new { message = "An error occurred while scanning the item" });
        }
    }

    /// <summary>
    /// Scan barcode to dispose/throw away item
    /// </summary>
    [HttpPost("{sessionId}/dispose")]
    public async Task<IActionResult> ScanDispose(Guid sessionId, [FromBody] ScanDisposeRequest request)
    {
        _logger.LogInformation("Scanning barcode {Barcode} to dispose in session {SessionId}", request.Barcode, sessionId);
        try
        {
            var itemId = await _repository.ScanDisposeItemAsync(
                sessionId,
                request.Barcode,
                request.DisposalReason,
                request.AllergenDetected);
            var message = string.IsNullOrEmpty(request.AllergenDetected)
                ? "Item disposed"
                : $"Item disposed - allergen '{request.AllergenDetected}' recorded";
            return Ok(new { itemId, message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning barcode {Barcode} to dispose in session {SessionId}", request.Barcode, sessionId);
            return StatusCode(500, new { message = "An error occurred while scanning the item" });
        }
    }

    /// <summary>
    /// End scanning session (unlock)
    /// </summary>
    [HttpPost("{sessionId}/end")]
    public async Task<IActionResult> EndSession(Guid sessionId)
    {
        try
        {
            _logger.LogInformation("Ending scan session {SessionId}", sessionId);
            await _repository.EndScanSessionAsync(sessionId);
            return Ok(new { message = "Scan session ended" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ending scan session {SessionId}", sessionId);
            return StatusCode(500, new { message = "An error occurred while ending the scan session" });
        }
    }

    /// <summary>
    /// Get allergen discoveries for user
    /// </summary>
    [HttpGet("allergens")]
    public async Task<IActionResult> GetAllergenDiscoveries()
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            var discoveries = await _repository.GetAllergenDiscoveriesAsync(userId.Value);
            return Ok(discoveries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving allergen discoveries");
            return StatusCode(500, new { message = "An error occurred while retrieving allergen discoveries" });
        }
    }

    /// <summary>
    /// Mark allergen as added to user profile
    /// </summary>
    [HttpPost("allergens/{discoveryId}/add-to-profile")]
    public async Task<IActionResult> AddAllergenToProfile(Guid discoveryId)
    {
        try
        {
            await _repository.MarkAllergenAddedToProfileAsync(discoveryId);
            return Ok(new { message = "Allergen added to profile" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking allergen discovery {DiscoveryId} as added to profile", discoveryId);
            return StatusCode(500, new { message = "An error occurred while updating the allergen profile" });
        }
    }
}

#region Request DTOs

public class StartScanSessionRequest
{
    public Guid? HouseholdId { get; set; }
    public string SessionType { get; set; } = "Adding"; // Adding, Using, Disposing, Purchasing
    public Guid? StorageLocationId { get; set; }
}

public class ScanAddRequest
{
    public string Barcode { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public Guid StorageLocationId { get; set; }
}

public class ScanUseRequest
{
    public string Barcode { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
}

public class ScanDisposeRequest
{
    public string Barcode { get; set; } = string.Empty;
    public string DisposalReason { get; set; } = "Other"; // Bad, CausedAllergy, Expired, Other
    public string? AllergenDetected { get; set; }
}

#endregion
