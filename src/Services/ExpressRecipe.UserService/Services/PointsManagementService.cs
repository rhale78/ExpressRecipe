using ExpressRecipe.UserService.Data;

namespace ExpressRecipe.UserService.Services;

/// <summary>
/// Background service to manage points-related tasks (daily bonuses, expiration, etc.)
/// </summary>
public class PointsManagementService : BackgroundService
{
    private readonly ILogger<PointsManagementService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(6);

    public PointsManagementService(
        ILogger<PointsManagementService> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Points Management Service started");

        // Wait until midnight (or next check time) to align with daily tasks
        await WaitUntilNextCheckTime(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPointsTasksAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing points management tasks");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Points Management Service stopped");
    }

    private async Task ProcessPointsTasksAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var pointsRepository = scope.ServiceProvider.GetRequiredService<IPointsRepository>();
        var activityRepository = scope.ServiceProvider.GetRequiredService<IActivityRepository>();

        _logger.LogInformation("Processing points management tasks");

        // Task 1: Award daily login bonus for active users
        // This would typically:
        // 1. Query users who logged in yesterday but not today
        // 2. Check if they already have daily login points
        // 3. Award points if eligible

        // Task 2: Award streak bonuses
        // 1. Check users with 7-day streaks -> award "Weekly Active" points
        // 2. Check users with 30-day streaks -> award "Monthly Active" points

        // Task 3: Expire old points (if applicable)
        // Some reward systems expire points after a certain period
        // 1. Query points older than expiration threshold
        // 2. Create negative transactions for expired points

        // Task 4: Calculate leaderboards
        // 1. Update cached leaderboard data for quick access

        _logger.LogInformation("Points management tasks completed");
    }

    private static async Task WaitUntilNextCheckTime(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var nextMidnight = now.Date.AddDays(1);
        var delay = nextMidnight - now;

        if (delay.TotalMilliseconds > 0)
        {
            await Task.Delay(delay, cancellationToken);
        }
    }
}
