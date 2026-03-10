namespace ExpressRecipe.Client.Shared.Services;

/// <summary>
/// Singleton that caches the work-queue pending count so the dashboard badge
/// and the /queue page both read the same value without double-fetching.
/// </summary>
public interface IWorkQueueState
{
    int  PendingCount  { get; }
    bool HasCritical   { get; }

    /// <summary>Refresh from the API. Called on initial page load and after actioning items.</summary>
    Task RefreshAsync(CancellationToken ct = default);

    /// <summary>Notify subscribers that state has changed (triggers badge re-render).</summary>
    event Action? OnChange;
}

public class WorkQueueState : IWorkQueueState
{
    private readonly IWorkQueueClientService _client;

    public int  PendingCount { get; private set; }
    public bool HasCritical  { get; private set; }

    public event Action? OnChange;

    public WorkQueueState(IWorkQueueClientService client)
    {
        _client = client;
    }

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        try
        {
            List<WorkQueueItemDto> items = await _client.GetItemsAsync(ct);
            PendingCount = items.Count;
            HasCritical  = items.Any(i => i.Priority <= 3);
        }
        catch
        {
            // Graceful degradation — keep last known values
        }

        OnChange?.Invoke();
    }
}
