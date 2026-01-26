using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ExpressRecipe.RecallService.Services;

namespace ExpressRecipe.RecallService.Controllers
{
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
        private static readonly Dictionary<Guid, ImportJobStatus> _importJobs = [];

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
            Guid importId = Guid.NewGuid();
            ImportJobStatus jobStatus = new ImportJobStatus
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
        /// NOTE: USDA no longer provides a public API. This endpoint returns an informational message.
        /// Use /api/admin/import/meat-poultry-recalls instead for actual meat/poultry recall imports.
        /// </summary>
        [HttpPost("import/usda-recalls")]
        public async Task<ActionResult<ImportStatusDto>> ImportUSDARecalls()
        {
            Guid importId = Guid.NewGuid();
            ImportJobStatus jobStatus = new ImportJobStatus
            {
                ImportId = importId,
                Source = "USDA",
                Status = "InProgress",
                StartedAt = DateTime.UtcNow
            };

            _importJobs[importId] = jobStatus;

            _logger.LogInformation("Starting USDA recall import job {ImportId} (will return error - no API available)", importId);

            // Run import in background
            _ = Task.Run(async () =>
            {
                try
                {
                    RecallImportResult result = await _fdaImportService.ImportUSDARecallsAsync();

                    jobStatus.Status = result.ErrorMessage != null ? "Failed" : "Completed";
                    jobStatus.CompletedAt = DateTime.UtcNow;
                    jobStatus.TotalRecords = result.TotalProcessed;
                    jobStatus.ProcessedRecords = result.TotalProcessed;
                    jobStatus.SuccessCount = result.SuccessCount;
                    jobStatus.ErrorCount = result.FailureCount;
                    jobStatus.ErrorMessage = result.ErrorMessage;

                    _logger.LogWarning("USDA recall import {ImportId} completed with message: {Message}",
                        importId, result.ErrorMessage ?? "No data source available");
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
        /// Import meat/poultry recalls from FDA API (includes USDA-regulated products)
        /// This is the recommended method for importing meat, poultry, and egg product recalls.
        /// </summary>
        [HttpPost("import/meat-poultry-recalls")]
        public async Task<ActionResult<ImportStatusDto>> ImportMeatPoultryRecalls([FromQuery] int limit = 50)
        {
            Guid importId = Guid.NewGuid();
            ImportJobStatus jobStatus = new ImportJobStatus
            {
                ImportId = importId,
                Source = "USDA-MEAT",
                Status = "InProgress",
                StartedAt = DateTime.UtcNow
            };

            _importJobs[importId] = jobStatus;

            _logger.LogInformation("Starting meat/poultry recall import job {ImportId}", importId);

            // Run import in background
            _ = Task.Run(async () =>
            {
                try
                {
                    RecallImportResult result = await _fdaImportService.ImportMeatPoultryRecallsFromFDAAsync(limit);

                    jobStatus.Status = "Completed";
                    jobStatus.CompletedAt = DateTime.UtcNow;
                    jobStatus.TotalRecords = result.TotalProcessed;
                    jobStatus.ProcessedRecords = result.TotalProcessed;
                    jobStatus.SuccessCount = result.SuccessCount;
                    jobStatus.ErrorCount = result.FailureCount;
                    jobStatus.ErrorMessage = result.ErrorMessage;

                    _logger.LogInformation("Meat/poultry recall import {ImportId} completed: {Success} successful, {Failed} failed",
                        importId, result.SuccessCount, result.FailureCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Meat/poultry recall import {ImportId} failed", importId);
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
            return !_importJobs.TryGetValue(importId, out ImportJobStatus? jobStatus)
                ? (ActionResult<ImportStatusDto>)NotFound(new { message = "Import job not found" })
                : (ActionResult<ImportStatusDto>)Ok(MapToDto(jobStatus));
        }

        /// <summary>
        /// Get import history
        /// </summary>
        [HttpGet("import/history")]
        public ActionResult<List<ImportHistoryDto>> GetImportHistory()
        {
            List<ImportHistoryDto> history = _importJobs.Values
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
}
