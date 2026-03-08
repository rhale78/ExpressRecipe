using ExpressRecipe.ProfileService.Data;

namespace ExpressRecipe.ProfileService.Services;

/// <summary>
/// Background service that purges expired TemporaryVisitor records daily at 02:00 UTC.
/// </summary>
public class GuestExpiryWorker : BackgroundService
{
    private readonly IHouseholdMemberRepository _repository;
    private readonly ILogger<GuestExpiryWorker> _logger;

    public GuestExpiryWorker(IHouseholdMemberRepository repository, ILogger<GuestExpiryWorker> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("GuestExpiryWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan delay = CalculateDelayUntilNext2AmUtc();

            _logger.LogDebug("GuestExpiryWorker sleeping for {Delay} until next 02:00 UTC run", delay);

            await Task.Delay(delay, stoppingToken);

            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            await PurgeExpiredGuestsAsync(stoppingToken);
        }

        _logger.LogInformation("GuestExpiryWorker stopped");
    }

    private async Task PurgeExpiredGuestsAsync(CancellationToken ct)
    {
        try
        {
            List<ExpressRecipe.ProfileService.Contracts.Responses.HouseholdMemberDto> expired =
                await _repository.GetExpiredTemporaryVisitorsAsync(ct);

            int count = expired.Count;

            await _repository.PurgeExpiredTemporaryVisitorsAsync(ct);

            _logger.LogInformation("GuestExpiryWorker purged {Count} expired temporary visitor(s)", count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GuestExpiryWorker encountered an error during purge");
        }
    }

    private static TimeSpan CalculateDelayUntilNext2AmUtc()
    {
        DateTime now = DateTime.UtcNow;
        DateTime next2Am = now.Date.AddHours(2);

        if (now >= next2Am)
        {
            next2Am = next2Am.AddDays(1);
        }

        return next2Am - now;
    }
}
