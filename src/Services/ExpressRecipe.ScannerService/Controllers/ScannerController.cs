using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ExpressRecipe.ScannerService.Data;

namespace ExpressRecipe.ScannerService.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ScannerController : ControllerBase
{
    private readonly ILogger<ScannerController> _logger;
    private readonly IScannerRepository _repository;

    public ScannerController(ILogger<ScannerController> logger, IScannerRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Scan a barcode and check for allergens
    /// </summary>
    [HttpPost("scan")]
    public async Task<IActionResult> ScanBarcode([FromBody] ScanBarcodeRequest request)
    {
        var userId = GetUserId();
        _logger.LogInformation("User {UserId} scanning barcode {Barcode}", userId, request.Barcode);

        var scanId = await _repository.CreateScanAsync(userId, request.Barcode, request.ProductId, request.WasRecognized, request.ScanType);

        // Check for allergens if product was recognized
        List<ScanAlertDto> alerts = new();
        if (request.WasRecognized && !string.IsNullOrEmpty(request.ProductId))
        {
            alerts = await _repository.CheckAllergensAsync(userId, Guid.Parse(request.ProductId));
        }

        return Ok(new { scanId, alerts });
    }

    /// <summary>
    /// Get scan history
    /// </summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetScanHistory([FromQuery] int limit = 50)
    {
        var userId = GetUserId();
        var scans = await _repository.GetUserScansAsync(userId, limit);
        return Ok(scans);
    }

    /// <summary>
    /// Get allergen alerts
    /// </summary>
    [HttpGet("alerts")]
    public async Task<IActionResult> GetAlerts([FromQuery] bool unreadOnly = true)
    {
        var userId = GetUserId();
        var alerts = await _repository.GetUserAlertsAsync(userId, unreadOnly);
        return Ok(alerts);
    }

    /// <summary>
    /// Mark alert as read
    /// </summary>
    [HttpPut("alerts/{id}/read")]
    public async Task<IActionResult> MarkAlertRead(Guid id)
    {
        await _repository.MarkAlertAsReadAsync(id);
        return NoContent();
    }

    /// <summary>
    /// Report unknown product
    /// </summary>
    [HttpPost("unknown")]
    public async Task<IActionResult> ReportUnknownProduct([FromBody] ReportUnknownProductRequest request)
    {
        var userId = GetUserId();
        _logger.LogInformation("User {UserId} reporting unknown product {Barcode}", userId, request.Barcode);

        var unknownProductId = await _repository.ReportUnknownProductAsync(
            userId, request.Barcode, request.ProductName, request.Brand, request.Photo);

        return Ok(new { id = unknownProductId });
    }

    /// <summary>
    /// Get unknown products (admin/community)
    /// </summary>
    [HttpGet("unknown")]
    public async Task<IActionResult> GetUnknownProducts([FromQuery] int limit = 100)
    {
        var products = await _repository.GetUnknownProductsAsync(limit);
        return Ok(products);
    }

    /// <summary>
    /// Save OCR result
    /// </summary>
    [HttpPost("ocr")]
    public async Task<IActionResult> SaveOCRResult([FromBody] SaveOCRRequest request)
    {
        var userId = GetUserId();
        var ocrId = await _repository.SaveOCRResultAsync(
            userId, request.Image, request.ExtractedText, request.Confidence, request.ProductMatch);

        return Ok(new { id = ocrId });
    }

    /// <summary>
    /// Get OCR history
    /// </summary>
    [HttpGet("ocr")]
    public async Task<IActionResult> GetOCRHistory([FromQuery] int limit = 50)
    {
        var userId = GetUserId();
        var results = await _repository.GetUserOCRResultsAsync(userId, limit);
        return Ok(results);
    }
}

public class ScanBarcodeRequest
{
    public string Barcode { get; set; } = string.Empty;
    public string? ProductId { get; set; }
    public bool WasRecognized { get; set; }
    public string ScanType { get; set; } = "Barcode";
}

public class ReportUnknownProductRequest
{
    public string Barcode { get; set; } = string.Empty;
    public string? ProductName { get; set; }
    public string? Brand { get; set; }
    public byte[]? Photo { get; set; }
}

public class SaveOCRRequest
{
    public byte[] Image { get; set; } = Array.Empty<byte>();
    public string ExtractedText { get; set; } = string.Empty;
    public decimal Confidence { get; set; }
    public string? ProductMatch { get; set; }
}
