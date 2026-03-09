using System.Net.Http.Json;
using ExpressRecipe.MealPlanningService.Data;

namespace ExpressRecipe.MealPlanningService.Services;

public sealed class CookingTimerWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<CookingTimerWorker> _logger;

    public CookingTimerWorker(IServiceScopeFactory scopeFactory,
        IHttpClientFactory http, ILogger<CookingTimerWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _http = http;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            try { await ProcessExpiredTimersAsync(stoppingToken); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "CookingTimerWorker failed");
            }
        }
    }

    private async Task ProcessExpiredTimersAsync(CancellationToken ct)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        ICookingTimerRepository timers = scope.ServiceProvider.GetRequiredService<ICookingTimerRepository>();

        List<CookingTimerDto> expired = await timers.GetExpiredUnnotifiedTimersAsync(ct);
        if (expired.Count == 0) { return; }

        HttpClient client = _http.CreateClient("NotificationService");

        foreach (CookingTimerDto timer in expired)
        {
            try
            {
                HttpResponseMessage response = await client.PostAsJsonAsync("/api/Notification/internal", new
                {
                    userId            = timer.UserId,
                    type              = "CookingTimer",
                    priority          = "High",
                    title             = $"⏱ Timer done: {timer.Label}",
                    message           = $"Your {timer.Label} timer has finished.",
                    relatedEntityType = "CookingTimer",
                    relatedEntityId   = timer.Id
                }, ct);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Notification service returned {StatusCode} for timer {Id}",
                        (int)response.StatusCode, timer.Id);
                    continue;
                }

                await timers.MarkNotificationSentAsync(timer.Id, ct);
                _logger.LogInformation("Timer expired notification sent: {Label} for user {UserId}",
                    timer.Label, timer.UserId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to send timer notification for {Id}", timer.Id);
            }
        }
    }
}
