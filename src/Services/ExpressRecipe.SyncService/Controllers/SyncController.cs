using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ExpressRecipe.SyncService.Data;

namespace ExpressRecipe.SyncService.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SyncController : ControllerBase
{
    private readonly ILogger<SyncController> _logger;
    private readonly ISyncRepository _repository;

    public SyncController(ILogger<SyncController> logger, ISyncRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost("devices")]
    public async Task<IActionResult> RegisterDevice([FromBody] RegisterDeviceRequest request)
    {
        var userId = GetUserId();
        var deviceId = await _repository.RegisterDeviceAsync(
            userId, request.DeviceName, request.DeviceType, request.OsVersion, request.AppVersion);
        return Ok(new { id = deviceId });
    }

    [HttpGet("devices")]
    public async Task<IActionResult> GetDevices()
    {
        var userId = GetUserId();
        var devices = await _repository.GetUserDevicesAsync(userId);
        return Ok(devices);
    }

    [HttpDelete("devices/{id}")]
    public async Task<IActionResult> UnregisterDevice(Guid id)
    {
        await _repository.UnregisterDeviceAsync(id);
        return NoContent();
    }

    [HttpPost("push")]
    public async Task<IActionResult> PushChanges([FromBody] PushChangesRequest request)
    {
        var userId = GetUserId();
        var syncIds = new List<Guid>();

        foreach (var change in request.Changes)
        {
            var syncId = await _repository.CreateSyncMetadataAsync(
                userId, request.DeviceId, change.EntityType, change.EntityId,
                change.Version, change.Operation, change.Data, change.ClientTimestamp);
            syncIds.Add(syncId);
        }

        return Ok(new { syncedCount = syncIds.Count, syncIds });
    }

    [HttpGet("pull")]
    public async Task<IActionResult> PullChanges([FromQuery] Guid deviceId, [FromQuery] DateTime since)
    {
        var userId = GetUserId();
        var changes = await _repository.GetPendingSyncsAsync(userId, deviceId, since);
        return Ok(changes);
    }

    [HttpGet("conflicts")]
    public async Task<IActionResult> GetConflicts()
    {
        var userId = GetUserId();
        var conflicts = await _repository.GetUnresolvedConflictsAsync(userId);
        return Ok(conflicts);
    }

    [HttpPost("conflicts/{id}/resolve")]
    public async Task<IActionResult> ResolveConflict(Guid id, [FromBody] ResolveConflictRequest request)
    {
        var userId = GetUserId();
        await _repository.ResolveConflictAsync(id, request.Resolution, request.ResolvedData, userId);
        return NoContent();
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats([FromQuery] Guid? deviceId)
    {
        var userId = GetUserId();
        var stats = await _repository.GetSyncStatsAsync(userId, deviceId);
        return Ok(stats);
    }
}

public class RegisterDeviceRequest
{
    public string DeviceName { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public string AppVersion { get; set; } = string.Empty;
}

public class PushChangesRequest
{
    public Guid DeviceId { get; set; }
    public List<SyncChange> Changes { get; set; } = new();
}

public class SyncChange
{
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public int Version { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
    public DateTime ClientTimestamp { get; set; }
}

public class ResolveConflictRequest
{
    public string Resolution { get; set; } = string.Empty;
    public string ResolvedData { get; set; } = string.Empty;
}
