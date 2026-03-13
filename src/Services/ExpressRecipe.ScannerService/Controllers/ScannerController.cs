using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
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

    private Guid? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    /// <summary>
    /// Scan a barcode and check for allergens
    /// </summary>
    [HttpPost("scan")]
    public async Task<IActionResult> ScanBarcode([FromBody] ScanBarcodeRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        _logger.LogInformation("User {UserId} scanning barcode {Barcode}", userId, request.Barcode);

        var scanId = await _repository.CreateScanAsync(userId.Value, request.Barcode, request.ProductId, request.WasRecognized, request.ScanType);

        // Check for allergens if product was recognized
        List<ScanAlertDto> alerts = new();
        if (request.WasRecognized && !string.IsNullOrEmpty(request.ProductId))
        {
            if (!Guid.TryParse(request.ProductId, out var productGuid))
                return BadRequest(new { message = "Invalid product ID format." });
            alerts = await _repository.CheckAllergensAsync(userId.Value, productGuid);
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
        if (userId == null) return Unauthorized();
        var scans = await _repository.GetUserScansAsync(userId.Value, limit);
        return Ok(scans);
    }

    /// <summary>
    /// Get allergen alerts
    /// </summary>
    [HttpGet("alerts")]
    public async Task<IActionResult> GetAlerts([FromQuery] bool unreadOnly = true)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var alerts = await _repository.GetUserAlertsAsync(userId.Value, unreadOnly);
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
        if (userId == null) return Unauthorized();
        _logger.LogInformation("User {UserId} reporting unknown product {Barcode}", userId, request.Barcode);

        var unknownProductId = await _repository.ReportUnknownProductAsync(
            userId.Value, request.Barcode, request.ProductName, request.Brand, request.Photo);

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
        if (userId == null) return Unauthorized();
        var ocrId = await _repository.SaveOCRResultAsync(
            userId.Value, request.Image, request.ExtractedText, request.Confidence, request.ProductMatch);

        return Ok(new { id = ocrId });
    }

    /// <summary>
    /// Get OCR history
    /// </summary>
    [HttpGet("ocr")]
    public async Task<IActionResult> GetOCRHistory([FromQuery] int limit = 50)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var results = await _repository.GetUserOCRResultsAsync(userId.Value, limit);
        return Ok(results);
    }

    /// <summary>
    /// Save vision capture result
    /// </summary>
    [HttpPost("vision/capture")]
    public async Task<IActionResult> SaveVisionCapture([FromBody] SaveVisionCaptureRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        _logger.LogInformation("User {UserId} saving vision capture", userId);

        VisionCaptureRecord capture = new VisionCaptureRecord
        {
            UserId = userId.Value,
            ScanHistoryId = request.ScanHistoryId,
            CaptureImageJpeg = request.CaptureImageJpeg,
            DetectedBarcode = request.DetectedBarcode,
            DetectedProductName = request.DetectedProductName,
            DetectedBrand = request.DetectedBrand,
            ProviderUsed = request.ProviderUsed,
            Confidence = request.Confidence,
            IsTrainingData = request.IsTrainingData
        };

        Guid captureId = await _repository.SaveVisionCaptureAsync(capture, ct);
        return Ok(new { id = captureId });
    }

    /// <summary>
    /// Submit a correction report for a vision capture
    /// </summary>
    [HttpPost("correction")]
    public async Task<IActionResult> CreateCorrectionReport([FromBody] CreateCorrectionReportRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        _logger.LogInformation("User {UserId} creating correction report for capture {CaptureId}", userId, request.VisionCaptureId);

        CorrectionReportRecord report = new CorrectionReportRecord
        {
            VisionCaptureId = request.VisionCaptureId,
            UserId = userId.Value,
            AiGuess = request.AiGuess,
            UserCorrection = request.UserCorrection,
            UserNote = request.UserNote
        };

        Guid reportId = await _repository.CreateCorrectionReportAsync(report, ct);
        return Ok(new { id = reportId });
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

public class SaveVisionCaptureRequest
{
    [Required]
    public byte[] CaptureImageJpeg { get; set; } = Array.Empty<byte>();
    public string? DetectedBarcode { get; set; }
    public string? DetectedProductName { get; set; }
    public string? DetectedBrand { get; set; }
    public string? ProviderUsed { get; set; }
    public decimal? Confidence { get; set; }
    public Guid? ScanHistoryId { get; set; }
    public bool IsTrainingData { get; set; }
}

public class CreateCorrectionReportRequest
{
    public Guid VisionCaptureId { get; set; }
    public string? AiGuess { get; set; }
    public string? UserCorrection { get; set; }
    public string? UserNote { get; set; }
}

public class UpdateCorrectionStatusRequest
{
    [Required]
    [RegularExpression("^(Approved|Rejected)$", ErrorMessage = "Status must be 'Approved' or 'Rejected'.")]
    public string Status { get; set; } = string.Empty;
}

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/scanner")]
public class ScannerAdminController : ControllerBase
{
    private readonly ILogger<ScannerAdminController> _logger;
    private readonly IScannerRepository _repository;

    public ScannerAdminController(ILogger<ScannerAdminController> logger, IScannerRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    /// <summary>
    /// Get correction reports, optionally filtered by status
    /// </summary>
    [HttpGet("corrections")]
    public async Task<IActionResult> GetCorrectionReports([FromQuery] string? status, [FromQuery] int limit = 100, CancellationToken ct = default)
    {
        List<CorrectionReportRecord> reports = await _repository.GetCorrectionReportsAsync(status, limit, ct);
        return Ok(reports);
    }

    /// <summary>
    /// Update the status of a correction report (Approve or Reject)
    /// </summary>
    [HttpPut("corrections/{id}")]
    public async Task<IActionResult> UpdateCorrectionStatus(Guid id, [FromBody] UpdateCorrectionStatusRequest request, CancellationToken ct)
    {
        var reviewerId = GetUserId();
        if (reviewerId == null) return Unauthorized();
        _logger.LogInformation("Admin {ReviewerId} updating correction {ReportId} to {Status}", reviewerId.Value, id, request.Status);

        await _repository.UpdateCorrectionStatusAsync(id, request.Status, reviewerId.Value, ct);
        return NoContent();
    }

    /// <summary>
    /// Export training data rows for AI model improvement
    /// </summary>
    [HttpGet("training-export")]
    public async Task<IActionResult> GetTrainingExport([FromQuery] int limit = 500, CancellationToken ct = default)
    {
        List<TrainingExportRow> rows = await _repository.GetTrainingExportAsync(limit, ct);
        return Ok(rows);
    }
}
