using ExpressRecipe.ProfileService.Data;

namespace ExpressRecipe.ProfileService.Services;

/// <summary>
/// Background service that purges expired TemporaryVisitor records daily at 02:00 UTC.
/// Uses IServiceScopeFactory to avoid the captive dependency anti-pattern (singleton holding scoped repo).
/// </summary>
public class GuestExpiryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GuestExpiryWorker> _logger;

    public GuestExpiryWorker(IServiceScopeFactory scopeFactory, ILogger<GuestExpiryWorker> logger)
    {
        _scopeFactory = scopeFactory;
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
            using IServiceScope scope = _scopeFactory.CreateScope();
            IHouseholdMemberRepository repository =
                scope.ServiceProvider.GetRequiredService<IHouseholdMemberRepository>();

            List<ExpressRecipe.ProfileService.Contracts.Responses.HouseholdMemberDto> expired =
                await repository.GetExpiredTemporaryVisitorsAsync(ct);

            int count = expired.Count;

            await repository.PurgeExpiredTemporaryVisitorsAsync(ct);

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
