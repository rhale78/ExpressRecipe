using ExpressRecipe.MealPlanningService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;

namespace ExpressRecipe.MealPlanningService.Controllers;

// MealPlanningService — internal endpoint for other services to push items.
// This endpoint is service-to-service only — protected by internal network only.
[ApiController]
[Route("api/work-queue/internal")]
public sealed class WorkQueueInternalController : ControllerBase
{
    private readonly IWorkQueueRepository _repo;
    private readonly IConfiguration? _configuration;

    public WorkQueueInternalController(
        IWorkQueueRepository repo,
        IConfiguration? configuration = null)
    {
        _repo = repo;
        _configuration = configuration;
    }

    [AllowAnonymous]
    [HttpPost("upsert")]
    public async Task<IActionResult> UpsertItem(
        [FromBody] UpsertWorkQueueItemRequest req, CancellationToken ct = default)
    {
        string? configuredKey = _configuration?["InternalApi:Key"];
        if (!string.IsNullOrEmpty(configuredKey))
        {
            string? providedKey = Request.Headers["X-Internal-Api-Key"].FirstOrDefault();
            if (!IsValidApiKey(providedKey, configuredKey))
                return Unauthorized(new { error = "Invalid or missing X-Internal-Api-Key header" });
        }

        await _repo.UpsertAsync(req, ct);
        return NoContent();
    }

    private static bool IsValidApiKey(string? provided, string configured)
    {
        if (provided is null) return false;
        byte[] a = Encoding.UTF8.GetBytes(provided);
        byte[] b = Encoding.UTF8.GetBytes(configured);
        if (a.Length != b.Length)
        {
            byte[] padded = new byte[Math.Max(a.Length, b.Length)];
            Buffer.BlockCopy(a.Length < b.Length ? a : b, 0, padded, 0, Math.Min(a.Length, b.Length));
            if (a.Length < b.Length) { a = padded; } else { b = padded; }
        }
        return CryptographicOperations.FixedTimeEquals(a, b);
    }
}
