using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace ExpressRecipe.MealPlanningService.Services.GoogleCalendar;

public interface IGoogleCalendarService
{
    Task<bool> IsConnectedAsync(Guid userId, CancellationToken ct = default);
    Task<List<CalendarEventDto>> GetEventsAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<string?> CreateCookEventAsync(Guid userId, string summary, DateTime startUtc, DateTime endUtc, CancellationToken ct = default);
    Task DeleteCookEventAsync(Guid userId, string googleEventId, CancellationToken ct = default);
}

public sealed record CalendarEventDto
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public DateOnly Date { get; init; }
    public TimeOnly? StartTime { get; init; }
    public TimeOnly? EndTime { get; init; }
    public bool IsAllDay { get; init; }
    public bool IsBusy { get; init; }
}

public sealed record CalendarTokenDto
{
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
}

public sealed class GoogleCalendarService : IGoogleCalendarService
{
    private readonly IHttpClientFactory _http;
    private readonly IGoogleCalendarTokenRepository _tokens;
    private readonly ILogger<GoogleCalendarService> _logger;
    private const string CalendarApiBase = "https://www.googleapis.com/calendar/v3";

    public GoogleCalendarService(IHttpClientFactory http, IGoogleCalendarTokenRepository tokens, ILogger<GoogleCalendarService> logger)
    {
        _http   = http;
        _tokens = tokens;
        _logger = logger;
    }

    public async Task<bool> IsConnectedAsync(Guid userId, CancellationToken ct = default)
        => await _tokens.GetTokenAsync(userId, ct) is not null;

    public async Task<List<CalendarEventDto>> GetEventsAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        string? token = await GetValidTokenAsync(userId, ct);
        if (token is null) { return new(); }

        HttpClient client = _http.CreateClient("GoogleCalendar");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        string url = $"{CalendarApiBase}/calendars/primary/events" +
            $"?timeMin={from:yyyy-MM-dd}T00:00:00Z&timeMax={to:yyyy-MM-dd}T23:59:59Z" +
            "&singleEvents=true&orderBy=startTime&fields=items(id,summary,start,end,transparency)";

        GoogleCalendarEventsResponse? response;
        try
        {
            response = await client.GetFromJsonAsync<GoogleCalendarEventsResponse>(url, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google Calendar API failed for user {UserId}", userId);
            return new();
        }

        if (response?.Items is null) { return new(); }

        List<CalendarEventDto> results = new();
        foreach (GoogleCalendarItem item in response.Items)
        {
            bool isAllDay = item.Start?.Date is not null;
            DateOnly date = isAllDay
                ? DateOnly.Parse(item.Start!.Date!)
                : DateOnly.Parse(item.Start!.DateTime![..10]);
            results.Add(new CalendarEventDto
            {
                Id        = item.Id ?? string.Empty,
                Title     = item.Summary ?? string.Empty,
                Date      = date,
                IsAllDay  = isAllDay,
                IsBusy    = item.Transparency != "transparent",
                StartTime = isAllDay ? null : TimeOnly.Parse(item.Start.DateTime![11..16]),
                EndTime   = isAllDay || item.End?.DateTime is null ? null : TimeOnly.Parse(item.End.DateTime[11..16])
            });
        }
        return results;
    }

    public async Task<string?> CreateCookEventAsync(Guid userId, string summary, DateTime startUtc, DateTime endUtc, CancellationToken ct = default)
    {
        string? token = await GetValidTokenAsync(userId, ct);
        if (token is null) { return null; }
        HttpClient client = _http.CreateClient("GoogleCalendar");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        HttpResponseMessage resp = await client.PostAsJsonAsync(
            $"{CalendarApiBase}/calendars/primary/events",
            new
            {
                summary,
                start = new { dateTime = startUtc.ToString("o"), timeZone = "UTC" },
                end   = new { dateTime = endUtc.ToString("o"),   timeZone = "UTC" }
            }, ct);
        if (!resp.IsSuccessStatusCode) { return null; }
        return (await resp.Content.ReadFromJsonAsync<GoogleCreatedEventResponse>(ct))?.Id;
    }

    public async Task DeleteCookEventAsync(Guid userId, string googleEventId, CancellationToken ct = default)
    {
        string? token = await GetValidTokenAsync(userId, ct);
        if (token is null) { return; }
        HttpClient client = _http.CreateClient("GoogleCalendar");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        HttpResponseMessage resp = await client.DeleteAsync($"{CalendarApiBase}/calendars/primary/events/{googleEventId}", ct);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("Failed to delete Google Calendar event {EventId} for user {UserId}: {StatusCode}",
                googleEventId, userId, resp.StatusCode);
        }
    }

    private async Task<string?> GetValidTokenAsync(Guid userId, CancellationToken ct)
    {
        CalendarTokenDto? token = await _tokens.GetTokenAsync(userId, ct);
        if (token is null) { return null; }
        if (token.ExpiresAt > DateTime.UtcNow.AddMinutes(5)) { return token.AccessToken; }
        CalendarTokenDto? refreshed = await RefreshTokenAsync(token, ct);
        if (refreshed is null) { return null; }
        await _tokens.UpdateTokenAsync(userId, refreshed.AccessToken, refreshed.ExpiresAt, ct);
        return refreshed.AccessToken;
    }

    private async Task<CalendarTokenDto?> RefreshTokenAsync(CalendarTokenDto current, CancellationToken ct)
    {
        string clientId     = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID")     ?? string.Empty;
        string clientSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET") ?? string.Empty;
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            _logger.LogWarning("GOOGLE_CLIENT_ID or GOOGLE_CLIENT_SECRET environment variable is not set; cannot refresh token");
            return null;
        }
        HttpResponseMessage resp = await _http.CreateClient("GoogleCalendar").PostAsync(
            "https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "client_id",     clientId },
                { "client_secret", clientSecret },
                { "refresh_token", current.RefreshToken },
                { "grant_type",    "refresh_token" }
            }), ct);
        if (!resp.IsSuccessStatusCode) { return null; }
        GoogleTokenResponse? tokenResp = await resp.Content.ReadFromJsonAsync<GoogleTokenResponse>(ct);
        if (tokenResp is null) { return null; }
        return current with { AccessToken = tokenResp.AccessToken, ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResp.ExpiresIn) };
    }
}

// Response shapes
internal sealed record GoogleCalendarEventsResponse { public List<GoogleCalendarItem>? Items { get; init; } }
internal sealed record GoogleCalendarItem
{
    public string? Id           { get; init; }
    public string? Summary      { get; init; }
    public string? Transparency { get; init; }
    public GoogleCalendarTime? Start { get; init; }
    public GoogleCalendarTime? End   { get; init; }
}
internal sealed record GoogleCalendarTime  { public string? Date { get; init; } public string? DateTime { get; init; } }
internal sealed record GoogleCreatedEventResponse { public string? Id { get; init; } }
internal sealed record GoogleTokenResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("access_token")] public string AccessToken { get; init; } = string.Empty;
    [System.Text.Json.Serialization.JsonPropertyName("expires_in")]   public int    ExpiresIn   { get; init; }
}
