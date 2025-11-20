using ExpressRecipe.UserService.Data;

namespace ExpressRecipe.UserService.Services;

/// <summary>
/// Background service to generate scheduled reports automatically
/// </summary>
public class ScheduledReportsService : BackgroundService
{
    private readonly ILogger<ScheduledReportsService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(30);

    public ScheduledReportsService(
        ILogger<ScheduledReportsService> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Scheduled Reports Service started");

        // Wait a bit before starting to allow app to fully initialize
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessScheduledReportsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing scheduled reports");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Scheduled Reports Service stopped");
    }

    private async Task ProcessScheduledReportsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var reportsRepository = scope.ServiceProvider.GetRequiredService<IReportsRepository>();

        _logger.LogInformation("Checking for reports due for generation");

        // This would typically:
        // 1. Query SavedReport table for reports with IsScheduled=true and NextRunAt <= now
        // 2. Generate each report
        // 3. Create ReportHistory entry
        // 4. Email results if EmailResults=true
        // 5. Update NextRunAt based on schedule frequency

        // Example logic (commented out - requires actual report generation implementation):
        /*
        var dueReports = await GetReportsDueForGenerationAsync();

        foreach (var savedReport in dueReports)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                _logger.LogInformation("Generating scheduled report {ReportId}: {ReportName}",
                    savedReport.Id, savedReport.ReportName);

                // Generate the report
                var reportData = await GenerateReportAsync(savedReport);

                // Save to history
                await reportsRepository.CreateReportHistoryAsync(savedReport.UserId, new CreateReportHistoryRequest
                {
                    ReportTypeId = savedReport.ReportTypeId,
                    SavedReportId = savedReport.Id,
                    Parameters = savedReport.Parameters,
                    ExportFormat = "PDF",
                    Status = "Completed"
                });

                // Email if requested
                if (savedReport.EmailResults && !string.IsNullOrEmpty(savedReport.EmailAddress))
                {
                    await EmailReportAsync(savedReport.EmailAddress, reportData);
                }

                // Update next run time
                await UpdateNextRunDateAsync(savedReport);

                _logger.LogInformation("Successfully generated report {ReportId}", savedReport.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating report {ReportId}", savedReport.Id);

                // Log failure to history
                await reportsRepository.CreateReportHistoryAsync(savedReport.UserId, new CreateReportHistoryRequest
                {
                    ReportTypeId = savedReport.ReportTypeId,
                    SavedReportId = savedReport.Id,
                    Status = "Failed",
                    ErrorMessage = ex.Message
                });
            }
        }
        */

        _logger.LogInformation("Scheduled reports check completed");
    }

    private DateTime CalculateNextRunDate(string scheduleFrequency, int? scheduleDay)
    {
        var now = DateTime.UtcNow;

        return scheduleFrequency switch
        {
            "Daily" => now.AddDays(1),
            "Weekly" => now.AddDays(7 - (int)now.DayOfWeek + (scheduleDay ?? 0)),
            "Monthly" => new DateTime(
                now.Month == 12 ? now.Year + 1 : now.Year,
                now.Month == 12 ? 1 : now.Month + 1,
                Math.Min(scheduleDay ?? 1, DateTime.DaysInMonth(now.Month == 12 ? now.Year + 1 : now.Year, now.Month == 12 ? 1 : now.Month + 1))),
            _ => now.AddDays(1)
        };
    }
}
