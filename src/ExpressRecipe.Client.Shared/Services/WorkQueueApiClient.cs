using System.Net.Http.Json;

namespace ExpressRecipe.Client.Shared.Services;

// ── DTOs ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Work-queue item surfaced to the user as an action card.
/// Returned by GET /api/work-queue.
/// </summary>
public class WorkQueueItemDto
{
    public Guid     Id                { get; set; }
    public Guid     UserId            { get; set; }
    public Guid     HouseholdId       { get; set; }
    public string   ItemType          { get; set; } = string.Empty;
    public string   Title             { get; set; } = string.Empty;
    public string?  Body              { get; set; }
    public int      Priority          { get; set; }
    public string?  ActionPayload     { get; set; }
    public string   Status            { get; set; } = "Pending";
    public string?  ActionTaken       { get; set; }
    public DateTime? ActionedAt       { get; set; }
    public DateTime? SnoozeUntil      { get; set; }
    public string?  RelatedEntityType { get; set; }
    public Guid?    RelatedEntityId   { get; set; }
    public DateTime CreatedAt         { get; set; }
}

// ── Interface ─────────────────────────────────────────────────────────────────

/// <summary>
/// Client-side service that wraps API calls to /api/work-queue.
/// </summary>
public interface IWorkQueueClientService
{
    Task<List<WorkQueueItemDto>> GetItemsAsync(CancellationToken ct = default);
    Task ActionItemAsync(Guid itemId, string actionTaken, string? actionData = null,
        CancellationToken ct = default);
    Task DismissItemAsync(Guid itemId, CancellationToken ct = default);
    Task SnoozeItemAsync(Guid itemId, int hours = 24, CancellationToken ct = default);
    /// <summary>
    /// Returns count + hasCritical from the lightweight /count endpoint.
    /// Use this for badge refreshes to avoid fetching the full item list.
    /// </summary>
    Task<(int Count, bool HasCritical)> GetSummaryAsync(CancellationToken ct = default);
}

// ── Implementation ────────────────────────────────────────────────────────────

public class WorkQueueApiClient : IWorkQueueClientService
{
    private readonly HttpClient   _httpClient;
    private readonly ITokenProvider _tokenProvider;

    public WorkQueueApiClient(HttpClient httpClient, ITokenProvider tokenProvider)
    {
        _httpClient    = httpClient;
        _tokenProvider = tokenProvider;
    }

    private async Task<bool> EnsureAuthenticatedAsync()
    {
        string? token = await _tokenProvider.GetAccessTokenAsync();
        if (string.IsNullOrEmpty(token))
            return false;

        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return true;
    }

    public async Task<List<WorkQueueItemDto>> GetItemsAsync(CancellationToken ct = default)
    {
        if (!await EnsureAuthenticatedAsync())
            return new List<WorkQueueItemDto>();

        try
        {
            return await _httpClient.GetFromJsonAsync<List<WorkQueueItemDto>>(
                "/api/work-queue", ct) ?? new List<WorkQueueItemDto>();
        }
        catch
        {
            return new List<WorkQueueItemDto>();
        }
    }

    public async Task ActionItemAsync(Guid itemId, string actionTaken, string? actionData = null,
        CancellationToken ct = default)
    {
        if (!await EnsureAuthenticatedAsync()) return;

        try
        {
            await _httpClient.PostAsJsonAsync(
                $"/api/work-queue/{itemId}/action",
                new { actionTaken, actionData },
                ct);
        }
        catch { /* best-effort */ }
    }

    public async Task DismissItemAsync(Guid itemId, CancellationToken ct = default)
    {
        if (!await EnsureAuthenticatedAsync()) return;

        try
        {
            await _httpClient.DeleteAsync($"/api/work-queue/{itemId}", ct);
        }
        catch { /* best-effort */ }
    }

    public async Task SnoozeItemAsync(Guid itemId, int hours = 24, CancellationToken ct = default)
    {
        if (!await EnsureAuthenticatedAsync()) return;

        try
        {
            await _httpClient.PostAsJsonAsync(
                $"/api/work-queue/{itemId}/snooze",
                new { hours },
                ct);
        }
        catch { /* best-effort */ }
    }

    public async Task<(int Count, bool HasCritical)> GetSummaryAsync(CancellationToken ct = default)
    {
        if (!await EnsureAuthenticatedAsync()) return (0, false);

        try
        {
            var result = await _httpClient.GetFromJsonAsync<WorkQueueSummaryDto>(
                "/api/work-queue/count", ct);
            return (result?.Count ?? 0, result?.HasCritical ?? false);
        }
        catch
        {
            return (0, false);
        }
    }

    private sealed class WorkQueueSummaryDto
    {
        public int  Count       { get; set; }
        public bool HasCritical { get; set; }
    }
}
