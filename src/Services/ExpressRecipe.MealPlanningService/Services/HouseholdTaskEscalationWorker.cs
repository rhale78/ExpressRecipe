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
            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
            try
            {
                await EscalateOverdueTasksAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HouseholdTaskEscalationWorker failed");
            }
        }
    }

    private async Task EscalateOverdueTasksAsync(CancellationToken ct)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        IHouseholdTaskRepository tasks   = scope.ServiceProvider.GetRequiredService<IHouseholdTaskRepository>();
        IHouseholdMemberQuery    members = scope.ServiceProvider.GetRequiredService<IHouseholdMemberQuery>();

        List<HouseholdTaskDto> due = await tasks.GetEscalationDueTasksAsync(ct);
        HttpClient client = _http.CreateClient("NotificationService");

        foreach (HouseholdTaskDto task in due)
        {
            // Notify all active household members
            List<Guid> userIds = await members.GetActiveMemberUserIdsAsync(task.HouseholdId, ct);
            foreach (Guid userId in userIds)
            {
                try
                {
                    await client.PostAsJsonAsync("/api/Notification/internal", new
                    {
                        userId,
                        type              = "ThawEscalation",
                        title             = $"⏰ Reminder: {task.Title}",
                        message           = $"This was due {task.DueAt:h:mm tt} — don't forget to move it before your meal.",
                        relatedEntityType = task.RelatedEntityType,
                        relatedEntityId   = task.RelatedEntityId
                    }, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed escalation notification for task {TaskId} user {UserId}", task.Id, userId);
                }
            }
            await tasks.MarkEscalationSentAsync(task.Id, ct);
        }
    }
}
