using System.Net.Http.Json;
using ExpressRecipe.MealPlanningService.Data;

namespace ExpressRecipe.MealPlanningService.Services;

public sealed class HouseholdTaskEscalationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<HouseholdTaskEscalationWorker> _logger;

    public HouseholdTaskEscalationWorker(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory http,
        ILogger<HouseholdTaskEscalationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _http         = http;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EscalateOverdueTasksAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HouseholdTaskEscalationWorker failed");
            }
            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
        }
    }

    private async Task EscalateOverdueTasksAsync(CancellationToken ct)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        IHouseholdTaskRepository tasks   = scope.ServiceProvider.GetRequiredService<IHouseholdTaskRepository>();
        IHouseholdMemberQuery    members = scope.ServiceProvider.GetRequiredService<IHouseholdMemberQuery>();

        // Atomically claim all due tasks; no other worker instance can claim the same tasks.
        List<HouseholdTaskDto> claimed = await tasks.ClaimEscalationBatchAsync(ct);
        HttpClient client = _http.CreateClient("NotificationService");

        foreach (HouseholdTaskDto task in claimed)
        {
            List<Guid> userIds = await members.GetActiveMemberUserIdsAsync(task.HouseholdId, ct);
            if (userIds.Count == 0)
            {
                _logger.LogWarning(
                    "No active members found for household {HouseholdId}; task {TaskId} escalation skipped",
                    task.HouseholdId, task.Id);
                continue;
            }

            int successCount = 0;
            foreach (Guid userId in userIds)
            {
                try
                {
                    HttpResponseMessage response = await client.PostAsJsonAsync("/api/Notification/internal", new
                    {
                        userId,
                        type              = "ThawEscalation",
                        title             = $"⏰ Reminder: {task.Title}",
                        message           = $"This was due {task.DueAt:h:mm tt} — don't forget to move it before your meal.",
                        relatedEntityType = task.RelatedEntityType,
                        relatedEntityId   = task.RelatedEntityId
                    }, ct);

                    if (response.IsSuccessStatusCode)
                    {
                        successCount++;
                    }
                    else
                    {
                        _logger.LogWarning(
                            "NotificationService returned {StatusCode} for task {TaskId} user {UserId}",
                            (int)response.StatusCode, task.Id, userId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed escalation notification for task {TaskId} user {UserId}", task.Id, userId);
                }
            }

            if (successCount == 0)
            {
                _logger.LogError(
                    "All escalation notifications failed for task {TaskId} (household {HouseholdId}). " +
                    "Task is already marked Escalated and will not be retried. " +
                    "Check NotificationService health and member configuration.",
                    task.Id, task.HouseholdId);
            }
        }
    }
}
