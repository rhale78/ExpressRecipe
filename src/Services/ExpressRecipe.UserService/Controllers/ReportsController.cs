using ExpressRecipe.Shared.DTOs.User;
using ExpressRecipe.UserService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpressRecipe.UserService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IReportsRepository _reportsRepository;
    private readonly ILogger<ReportsController> _logger;

    public ReportsController(
        IReportsRepository reportsRepository,
        ILogger<ReportsController> logger)
    {
        _reportsRepository = reportsRepository;
        _logger = logger;
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return null;
        }
        return userId;
    }

    #region Report Types

    /// <summary>
    /// Get available report types
    /// </summary>
    [HttpGet("types")]
    public async Task<ActionResult<List<ReportTypeDto>>> GetReportTypes()
    {
        try
        {
            var reportTypes = await _reportsRepository.GetReportTypesAsync();
            return Ok(reportTypes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving report types");
            return StatusCode(500, new { message = "An error occurred while retrieving report types" });
        }
    }

    /// <summary>
    /// Get report type by ID
    /// </summary>
    [HttpGet("types/{id:guid}")]
    public async Task<ActionResult<ReportTypeDto>> GetReportType(Guid id)
    {
        try
        {
            var reportType = await _reportsRepository.GetReportTypeByIdAsync(id);

            if (reportType == null)
            {
                return NotFound(new { message = "Report type not found" });
            }

            return Ok(reportType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving report type {ReportTypeId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving the report type" });
        }
    }

    #endregion

    #region Saved Reports

    /// <summary>
    /// Get user's saved reports
    /// </summary>
    [HttpGet("saved")]
    public async Task<ActionResult<List<SavedReportDto>>> GetSavedReports()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var savedReports = await _reportsRepository.GetUserSavedReportsAsync(userId.Value);
            return Ok(savedReports);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving saved reports");
            return StatusCode(500, new { message = "An error occurred while retrieving saved reports" });
        }
    }

    /// <summary>
    /// Get saved report by ID
    /// </summary>
    [HttpGet("saved/{id:guid}")]
    public async Task<ActionResult<SavedReportDto>> GetSavedReport(Guid id)
    {
        try
        {
            var savedReport = await _reportsRepository.GetSavedReportByIdAsync(id);

            if (savedReport == null)
            {
                return NotFound(new { message = "Saved report not found" });
            }

            var userId = GetCurrentUserId();
            if (savedReport.UserId != userId)
            {
                return Forbid();
            }

            return Ok(savedReport);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving saved report {SavedReportId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving the saved report" });
        }
    }

    /// <summary>
    /// Create a saved report
    /// </summary>
    [HttpPost("saved")]
    public async Task<ActionResult<Guid>> CreateSavedReport([FromBody] CreateSavedReportRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var savedReportId = await _reportsRepository.CreateSavedReportAsync(userId.Value, request);

            return CreatedAtAction(nameof(GetSavedReport), new { id = savedReportId }, new { id = savedReportId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating saved report");
            return StatusCode(500, new { message = "An error occurred while creating the saved report" });
        }
    }

    /// <summary>
    /// Update a saved report
    /// </summary>
    [HttpPut("saved/{id:guid}")]
    public async Task<ActionResult> UpdateSavedReport(Guid id, [FromBody] UpdateSavedReportRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var success = await _reportsRepository.UpdateSavedReportAsync(id, userId.Value, request);

            if (!success)
            {
                return NotFound(new { message = "Saved report not found" });
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating saved report {SavedReportId}", id);
            return StatusCode(500, new { message = "An error occurred while updating the saved report" });
        }
    }

    /// <summary>
    /// Delete a saved report
    /// </summary>
    [HttpDelete("saved/{id:guid}")]
    public async Task<ActionResult> DeleteSavedReport(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var success = await _reportsRepository.DeleteSavedReportAsync(id, userId.Value);

            if (!success)
            {
                return NotFound(new { message = "Saved report not found" });
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting saved report {SavedReportId}", id);
            return StatusCode(500, new { message = "An error occurred while deleting the saved report" });
        }
    }

    #endregion

    #region Report History

    /// <summary>
    /// Get user's report generation history
    /// </summary>
    [HttpGet("history")]
    public async Task<ActionResult<List<ReportHistoryDto>>> GetReportHistory(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            if (pageNumber < 1 || pageSize < 1 || pageSize > 100)
            {
                return BadRequest(new { message = "Invalid pagination parameters" });
            }

            var history = await _reportsRepository.GetUserReportHistoryAsync(userId.Value, pageNumber, pageSize);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving report history");
            return StatusCode(500, new { message = "An error occurred while retrieving report history" });
        }
    }

    /// <summary>
    /// Create a report history entry
    /// </summary>
    [HttpPost("history")]
    public async Task<ActionResult<Guid>> CreateReportHistory([FromBody] CreateReportHistoryRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var historyId = await _reportsRepository.CreateReportHistoryAsync(userId.Value, request);

            return Ok(new { id = historyId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating report history");
            return StatusCode(500, new { message = "An error occurred while creating report history" });
        }
    }

    #endregion
}
