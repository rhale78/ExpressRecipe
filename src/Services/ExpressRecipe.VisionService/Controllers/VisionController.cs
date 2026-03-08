using ExpressRecipe.VisionService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpressRecipe.VisionService.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class VisionController : ControllerBase
{
    private readonly IVisionService _visionService;
    private readonly ILogger<VisionController> _logger;

    public VisionController(IVisionService visionService, ILogger<VisionController> logger)
    {
        _visionService = visionService;
        _logger = logger;
    }

    /// <summary>
    /// Analyze an image using the vision provider chain.
    /// Accepts multipart/form-data (field: "image") OR JSON body { base64Image, options }.
    /// </summary>
    [HttpPost("analyze")]
    public async Task<IActionResult> Analyze(CancellationToken ct)
    {
        byte[]? imageBytes = await TryReadImageBytesAsync(ct);

        if (imageBytes == null || imageBytes.Length == 0)
        {
            return BadRequest(new { error = "No image data provided. Send multipart/form-data with field 'image' or JSON { base64Image }." });
        }

        VisionOptions options = TryReadOptionsFromQuery() ?? new VisionOptions();

        _logger.LogInformation("Vision analyze request: {Size} bytes", imageBytes.Length);

        VisionResult result = await _visionService.AnalyzeAsync(imageBytes, options, ct);
        return Ok(result);
    }

    /// <summary>
    /// Extract text from an image using PaddleOCR.
    /// Accepts multipart/form-data (field: "image") OR JSON body { base64Image }.
    /// </summary>
    [HttpPost("ocr")]
    public async Task<IActionResult> ExtractText(CancellationToken ct)
    {
        byte[]? imageBytes = await TryReadImageBytesAsync(ct);

        if (imageBytes == null || imageBytes.Length == 0)
        {
            return BadRequest(new { error = "No image data provided." });
        }

        VisionResult result = await _visionService.ExtractTextAsync(imageBytes, ct);
        return Ok(result);
    }

    /// <summary>
    /// Returns provider availability status.
    /// </summary>
    [HttpGet("health")]
    [AllowAnonymous]
    public async Task<IActionResult> Health(CancellationToken ct)
    {
        VisionHealthStatus status = await _visionService.GetHealthAsync(ct);
        return Ok(status);
    }

    private async Task<byte[]?> TryReadImageBytesAsync(CancellationToken ct)
    {
        // Try multipart/form-data first
        if (Request.HasFormContentType)
        {
            IFormFile? file = Request.Form.Files.GetFile("image");
            if (file != null && file.Length > 0)
            {
                using System.IO.MemoryStream ms = new System.IO.MemoryStream();
                await file.CopyToAsync(ms, ct);
                return ms.ToArray();
            }
        }

        // Try JSON body
        if (Request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true)
        {
            VisionAnalyzeRequest? body = await Request.ReadFromJsonAsync<VisionAnalyzeRequest>(cancellationToken: ct);
            if (!string.IsNullOrWhiteSpace(body?.Base64Image))
            {
                return Convert.FromBase64String(body.Base64Image);
            }
        }

        return null;
    }

    private VisionOptions? TryReadOptionsFromQuery()
    {
        return null; // Query params not implemented; use defaults
    }
}
