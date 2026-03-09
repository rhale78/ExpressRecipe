using ExpressRecipe.InventoryService.Data;
using System.Net.Http.Json;

namespace ExpressRecipe.InventoryService.Services;

public sealed class StorageReminderWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<StorageReminderWorker> _logger;

    public StorageReminderWorker(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory http,
        ILogger<StorageReminderWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _http = http;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            DateTime now = DateTime.UtcNow;
            DateTime nextRun = now.Date.AddHours(7); // 7am UTC daily
            if (nextRun <= now)
            {
                nextRun = nextRun.AddDays(1);
            }

            try
            {
                await Task.Delay(nextRun - now, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                await RunChecksAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "StorageReminderWorker failed during RunChecksAsync");
            }
        }
    }

    internal async Task RunChecksAsync(CancellationToken ct)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        IInventoryStorageReminderQuery query = scope.ServiceProvider
            .GetRequiredService<IInventoryStorageReminderQuery>();

        await CheckFreezerBurnRiskAsync(query, ct);
        await CheckOutageItemSafetyAsync(query, ct);
    }

    internal async Task CheckFreezerBurnRiskAsync(IInventoryStorageReminderQuery query, CancellationToken ct)
    {
        List<FreezerBurnRiskItem> risky = await query.GetFreezerBurnRiskItemsAsync(ct);
        if (risky.Count == 0)
        {
            return;
        }

        HttpClient client = _http.CreateClient("notificationservice");

        foreach (IGrouping<Guid, FreezerBurnRiskItem> group in risky.GroupBy(x => x.HouseholdId))
        {
            string itemList = string.Join(", ", group.Select(x =>
                $"{x.ItemName} ({x.DaysInFreezer}d in {x.LocationName})"));
            try
            {
                await client.PostAsJsonAsync("/api/notifications/internal/household", new
                {
                    householdId = group.Key,
                    type = "FreezerBurnRisk",
                    title = "⚠️ Freezer burn risk",
                    message = $"Consider using soon: {itemList}"
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to send freezer burn notification for household {HouseholdId}", group.Key);
            }
        }
    }

    internal async Task CheckOutageItemSafetyAsync(IInventoryStorageReminderQuery query, CancellationToken ct)
    {
        List<OutageStorageLocation> outages = await query.GetActiveOutagesAsync(ct);
        if (outages.Count == 0)
        {
            return;
        }

        HttpClient client = _http.CreateClient("notificationservice");

        foreach (OutageStorageLocation outage in outages)
        {
            double hoursOut = (DateTime.UtcNow - outage.OutageStartedAt).TotalHours;

            double safeHours = outage.StorageType switch
            {
                "Freezer"      => 36,   // conservative: full=48h, half-full=24h → use 36h
                "Refrigerator" => 4,
                _              => 8
            };

            if (hoursOut >= safeHours * 0.75 && !outage.WarningSent)
            {
                int affectedCount = await query.GetItemCountInStorageAsync(outage.LocationId, ct);
                try
                {
                    await client.PostAsJsonAsync("/api/notifications/internal/household", new
                    {
                        householdId = outage.HouseholdId,
                        type = "OutageSafetyWarning",
                        title = $"🔴 {outage.OutageType} — {outage.LocationName}",
                        message = $"{affectedCount} item(s) at risk. " +
                                  $"Estimated safe window: ~{safeHours}h total. " +
                                  $"Outage has been active {hoursOut:F0}h."
                    }, ct);
                    await query.MarkOutageWarningSentAsync(outage.LocationId, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to send outage warning for location {LocationId}", outage.LocationId);
                }
            }
        }
    }
}
