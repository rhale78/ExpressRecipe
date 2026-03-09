using ExpressRecipe.VisionService.Logging;
using ExpressRecipe.VisionService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";

    /// <summary>
    /// Analyze an image using the vision provider chain.
    /// Accepts multipart/form-data (field: "image") OR JSON body { base64Image, options }.
    /// </summary>
    [HttpPost("analyze")]
    public async Task<IActionResult> Analyze(CancellationToken ct)
    {
        (byte[]? imageBytes, VisionOptions? bodyOptions) = await TryReadImageAndOptionsAsync(ct);

        if (imageBytes == null || imageBytes.Length == 0)
        {
            _logger.LogNoImageProvided(GetUserId());
            return BadRequest(new { error = "No image data provided. Send multipart/form-data with field 'image' or JSON { base64Image }." });
        }

        VisionOptions options = bodyOptions ?? new VisionOptions();

        _logger.LogAnalyzeRequest(GetUserId(), imageBytes.Length);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        VisionResult result = await _visionService.AnalyzeAsync(imageBytes, options, ct);
        sw.Stop();
        _logger.LogAnalyzeComplete(GetUserId(), result.ProviderUsed ?? "none", result.Confidence, sw.ElapsedMilliseconds);
        return Ok(result);
    }

    /// <summary>
    /// Extract text from an image using PaddleOCR.
    /// Accepts multipart/form-data (field: "image") OR JSON body { base64Image }.
    /// </summary>
    [HttpPost("ocr")]
    public async Task<IActionResult> ExtractText(CancellationToken ct)
    {
        var userId = GetUserId();
        byte[]? imageBytes = await TryReadImageBytesAsync(ct);

        if (imageBytes == null || imageBytes.Length == 0)
        {
            _logger.LogNoImageProvided(userId);
            return BadRequest(new { error = "No image data provided." });
        }

        _logger.LogOcrRequest(userId, imageBytes.Length);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        VisionResult result = await _visionService.ExtractTextAsync(imageBytes, ct);
        sw.Stop();
        var textCount = result.DetectedText?.Length ?? 0;
        _logger.LogOcrComplete(userId, textCount, sw.ElapsedMilliseconds);
        return Ok(result);
    }

    /// <summary>
    /// Returns provider availability status.
    /// </summary>
    [HttpGet("health")]
    [AllowAnonymous]
    public async Task<IActionResult> Health(CancellationToken ct)
    {
        _logger.LogHealthCheck();
        VisionHealthStatus status = await _visionService.GetHealthAsync(ct);
        return Ok(status);
    }

    private async Task<(byte[]? imageBytes, VisionOptions? options)> TryReadImageAndOptionsAsync(CancellationToken ct)
    {
        // Try multipart/form-data first
        if (Request.HasFormContentType)
        {
            IFormFile? file = Request.Form.Files.GetFile("image");
            if (file != null && file.Length > 0)
            {
                using System.IO.MemoryStream ms = new System.IO.MemoryStream();
                await file.CopyToAsync(ms, ct);
                return (ms.ToArray(), null);
            }
        }

        // Try JSON body
        if (Request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true)
        {
            VisionAnalyzeRequest? body = await Request.ReadFromJsonAsync<VisionAnalyzeRequest>(cancellationToken: ct);
            if (!string.IsNullOrWhiteSpace(body?.Base64Image))
            {
                byte[] buffer = new byte[((body.Base64Image.Length + 3) / 4) * 3];
                if (!Convert.TryFromBase64String(body.Base64Image, buffer, out int bytesWritten))
                {
                    _logger.LogWarning("Invalid Base64 image data received in vision request");
                    return (null, null);
                }

                byte[] imageBytes = new byte[bytesWritten];
                Array.Copy(buffer, imageBytes, bytesWritten);

                VisionOptions? options = body.Options == null ? null : new VisionOptions
                {
                    AllowOnnx = body.Options.AllowOnnx,
                    AllowPaddleOcr = body.Options.AllowPaddleOcr,
                    AllowOllamaVision = body.Options.AllowOllamaVision,
                    AllowAzureVision = body.Options.AllowAzureVision,
                    MinConfidence = body.Options.MinConfidence
                };

                return (imageBytes, options);
            }
        }

        return (null, null);
    }

    private async Task<byte[]?> TryReadImageBytesAsync(CancellationToken ct)
    {
        (byte[]? imageBytes, VisionOptions? _) = await TryReadImageAndOptionsAsync(ct);
        return imageBytes;
    }
}
