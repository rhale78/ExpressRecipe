namespace ExpressRecipe.Client.Shared.Services;

/// <summary>
/// Scoped per-circuit state that caches the work-queue pending count so the
/// navigation badge and the /queue page both read the same value without double-fetching.
/// Fires OnChange so subscribers (e.g. MainLayout, WorkQueueBadge) re-render reactively.
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
            // Use the lightweight /count endpoint so we don't fetch the full item list
            // just to update the badge.  HasCritical uses Priority <= 4 to match the
            // "Urgent" tier (🔴) shown in WorkQueueList and WorkQueueItemCard.
            (int count, bool hasCritical) = await _client.GetSummaryAsync(ct);
            PendingCount = count;
            HasCritical  = hasCritical;
        }
        catch
        {
            // Graceful degradation — keep last known values
        }

        OnChange?.Invoke();
    }
}
