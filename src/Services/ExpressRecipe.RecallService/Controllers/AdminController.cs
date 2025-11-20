using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ExpressRecipe.RecallService.Services;

namespace ExpressRecipe.RecallService.Controllers;

/// <summary>
/// Admin controller for recall data import operations
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")] // Requires admin role
public class AdminController : ControllerBase
{
    private readonly FDARecallImportService _fdaImportService;
    private readonly ILogger<AdminController> _logger;

    // In-memory tracking of import jobs (in production, use database or distributed cache)
    private static readonly Dictionary<Guid, ImportJobStatus> _importJobs = new();

    public AdminController(
        FDARecallImportService fdaImportService,
        ILogger<AdminController> logger)
    {
        _fdaImportService = fdaImportService;
        _logger = logger;
    }

    /// <summary>
    /// Start FDA recall database import
    /// </summary>
    [HttpPost("import/fda")]
    public async Task<ActionResult<ImportStatusDto>> ImportFDARecalls(
        [FromBody] ImportRequest? request = null)
    {
        var importId = Guid.NewGuid();
        var jobStatus = new ImportJobStatus
        {
            ImportId = importId,
            Source = "FDA",
            Status = "InProgress",
            StartedAt = DateTime.UtcNow
        };

        _importJobs[importId] = jobStatus;

        _logger.LogInformation("Starting FDA recall import job {ImportId}", importId);

        // Run import in background
        _ = Task.Run(async () =>
        {
            try
            {
                var limit = request?.MaxResults ?? 100;

                RecallImportResult result;
                if (!string.IsNullOrWhiteSpace(request?.Query))
                {
                    // Search by keyword
                    result = await _fdaImportService.SearchAndImportRecallsAsync(request.Query, limit);
                }
                else
                {
                    // Import recent recalls
                    result = await _fdaImportService.ImportRecentRecallsAsync(limit);
                }

                jobStatus.Status = "Completed";
                jobStatus.CompletedAt = DateTime.UtcNow;
                jobStatus.TotalRecords = result.TotalProcessed;
                jobStatus.ProcessedRecords = result.TotalProcessed;
                jobStatus.SuccessCount = result.SuccessCount;
                jobStatus.ErrorCount = result.FailureCount;
                jobStatus.ErrorMessage = result.ErrorMessage;

                _logger.LogInformation("FDA import {ImportId} completed: {Success} successful, {Failed} failed",
                    importId, result.SuccessCount, result.FailureCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FDA import {ImportId} failed", importId);
                jobStatus.Status = "Failed";
                jobStatus.CompletedAt = DateTime.UtcNow;
                jobStatus.ErrorMessage = ex.Message;
            }
        });

        return Ok(MapToDto(jobStatus));
    }

    /// <summary>
    /// Start USDA FSIS recall database import
    /// </summary>
    [HttpPost("import/usda-recalls")]
    public async Task<ActionResult<ImportStatusDto>> ImportUSDARecalls()
    {
        var importId = Guid.NewGuid();
        var jobStatus = new ImportJobStatus
        {
            ImportId = importId,
            Source = "USDA",
            Status = "InProgress",
            StartedAt = DateTime.UtcNow
        };

        _importJobs[importId] = jobStatus;

        _logger.LogInformation("Starting USDA recall import job {ImportId}", importId);

        // Run import in background
        _ = Task.Run(async () =>
        {
            try
            {
                var result = await _fdaImportService.ImportUSDARecallsAsync();

                jobStatus.Status = "Completed";
                jobStatus.CompletedAt = DateTime.UtcNow;
                jobStatus.TotalRecords = result.TotalProcessed;
                jobStatus.ProcessedRecords = result.TotalProcessed;
                jobStatus.SuccessCount = result.SuccessCount;
                jobStatus.ErrorCount = result.FailureCount;
                jobStatus.ErrorMessage = result.ErrorMessage;

                _logger.LogInformation("USDA recall import {ImportId} completed: {Success} successful, {Failed} failed",
                    importId, result.SuccessCount, result.FailureCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "USDA recall import {ImportId} failed", importId);
                jobStatus.Status = "Failed";
                jobStatus.CompletedAt = DateTime.UtcNow;
                jobStatus.ErrorMessage = ex.Message;
            }
        });

        return Ok(MapToDto(jobStatus));
    }

    /// <summary>
    /// Get import job status
    /// </summary>
    [HttpGet("import/status/{importId}")]
    public ActionResult<ImportStatusDto> GetImportStatus(Guid importId)
    {
        if (!_importJobs.TryGetValue(importId, out var jobStatus))
        {
            return NotFound(new { message = "Import job not found" });
        }

        return Ok(MapToDto(jobStatus));
    }

    /// <summary>
    /// Get import history
    /// </summary>
    [HttpGet("import/history")]
    public ActionResult<List<ImportHistoryDto>> GetImportHistory()
    {
        var history = _importJobs.Values
            .OrderByDescending(j => j.StartedAt)
            .Take(50)
            .Select(j => new ImportHistoryDto
            {
                Id = j.ImportId,
                Source = j.Source,
                Status = j.Status,
                RecordsImported = j.SuccessCount,
                StartedAt = j.StartedAt,
                CompletedAt = j.CompletedAt
            })
            .ToList();

        return Ok(history);
    }

    /// <summary>
    /// Clear all import jobs (for testing)
    /// </summary>
    [HttpDelete("import/clear")]
    public IActionResult ClearImportHistory()
    {
        _importJobs.Clear();
        _logger.LogInformation("Import history cleared");
        return Ok(new { message = "Import history cleared" });
    }

    private ImportStatusDto MapToDto(ImportJobStatus job)
    {
        return new ImportStatusDto
        {
            ImportId = job.ImportId,
            Source = job.Source,
            Status = job.Status,
            TotalRecords = job.TotalRecords,
            ProcessedRecords = job.ProcessedRecords,
            SuccessCount = job.SuccessCount,
            ErrorCount = job.ErrorCount,
            ErrorMessage = job.ErrorMessage,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt
        };
    }
}

/// <summary>
/// Internal tracking of import job status
/// </summary>
internal class ImportJobStatus
{
    public Guid ImportId { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public int TotalRecords { get; set; }
    public int ProcessedRecords { get; set; }
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// DTO for import requests
/// </summary>
public class ImportRequest
{
    public string? Query { get; set; }
    public int? MaxResults { get; set; }
}

/// <summary>
/// DTO for import status
/// </summary>
public class ImportStatusDto
{
    public Guid ImportId { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public int TotalRecords { get; set; }
    public int ProcessedRecords { get; set; }
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int ProgressPercentage => TotalRecords > 0 ? (int)((ProcessedRecords / (double)TotalRecords) * 100) : 0;
}

/// <summary>
/// DTO for import history
/// </summary>
public class ImportHistoryDto
{
    public Guid Id { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int RecordsImported { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? Duration => CompletedAt.HasValue ? CompletedAt.Value - StartedAt : null;
}
