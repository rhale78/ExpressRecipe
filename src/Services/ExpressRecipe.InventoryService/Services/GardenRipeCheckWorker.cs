using System.Net.Http.Json;
using ExpressRecipe.InventoryService.Data;

namespace ExpressRecipe.InventoryService.Services;

public sealed class GardenRipeCheckWorker : BackgroundService
{
    private readonly IGardenRepository _garden;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<GardenRipeCheckWorker> _logger;

    public GardenRipeCheckWorker(IGardenRepository garden, IHttpClientFactory http, ILogger<GardenRipeCheckWorker> logger)
    {
        _garden = garden;
        _http   = http;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            DateTime now     = DateTime.UtcNow;
            DateTime nextRun = now.Date.AddHours(6);
            if (nextRun <= now) { nextRun = nextRun.AddDays(1); }
            await Task.Delay(nextRun - now, stoppingToken);
            try { await SendRipeCheckNotificationsAsync(stoppingToken); }
            catch (Exception ex) { _logger.LogError(ex, "GardenRipeCheckWorker failed"); }
        }
    }

    private async Task SendRipeCheckNotificationsAsync(CancellationToken ct)
    {
        List<GardenPlantingDto> due = await _garden.GetRipeCheckDuePlantingsAsync(3, ct);
        HttpClient client = _http.CreateClient("NotificationService");
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (IGrouping<Guid, GardenPlantingDto> group in due.GroupBy(p => p.HouseholdId))
        {
            Guid householdId = group.Key;
            string plantList = string.Join(", ", group.Select(p =>
                p.ExpectedRipeDate.HasValue && p.ExpectedRipeDate.Value <= today
                    ? $"{p.PlantName} (ready!)"
                    : p.PlantName));
            try
            {
                await client.PostAsJsonAsync("/api/notifications/internal/household",
                    new { householdId, type = "GardenRipeCheck", title = "🌱 Time to check the garden",
                          message = $"{plantList} may be ready to harvest" }, ct);
                _logger.LogInformation("Sent garden ripe check for household {Id}: {Plants}", householdId, plantList);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to send garden notification for household {Id}", householdId); }
        }
    }
}
