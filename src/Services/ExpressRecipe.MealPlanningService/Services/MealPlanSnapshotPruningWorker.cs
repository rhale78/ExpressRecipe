using Microsoft.Data.SqlClient;

namespace ExpressRecipe.MealPlanningService.Services;

public sealed class MealPlanSnapshotPruningWorker : BackgroundService
{
    private readonly string _connectionString;
    private readonly ILogger<MealPlanSnapshotPruningWorker> _logger;
    private const int AutoSnapshotRetentionDays = 90;
    private const int ChangeLogRetentionDays    = 180;

    public MealPlanSnapshotPruningWorker(string connectionString, ILogger<MealPlanSnapshotPruningWorker> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            DateTime now     = DateTime.UtcNow;
            DateTime nextRun = now.Date.AddHours(2);
            if (nextRun <= now) { nextRun = nextRun.AddDays(1); }
            await Task.Delay(nextRun - now, stoppingToken);
            try { await PruneAsync(stoppingToken); }
            catch (Exception ex) { _logger.LogError(ex, "MealPlanSnapshotPruningWorker failed"); }
        }
    }

    private async Task PruneAsync(CancellationToken ct)
    {
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);

        await using SqlCommand snap = new(@"DELETE FROM MealPlanSnapshot
            WHERE SnapshotType != 'Manual' AND CreatedAt < DATEADD(day, -@RetentionDays, GETUTCDATE())", conn);
        snap.Parameters.AddWithValue("@RetentionDays", AutoSnapshotRetentionDays);
        int snapRows = await snap.ExecuteNonQueryAsync(ct);

        await using SqlCommand log = new(@"DELETE FROM MealChangeLog
            WHERE ChangedAt < DATEADD(day, -@RetentionDays, GETUTCDATE())", conn);
        log.Parameters.AddWithValue("@RetentionDays", ChangeLogRetentionDays);
        int logRows = await log.ExecuteNonQueryAsync(ct);

        _logger.LogInformation("Pruned {Snap} auto-snapshots and {Log} change log rows", snapRows, logRows);
    }
}
