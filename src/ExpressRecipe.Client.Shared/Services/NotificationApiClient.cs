using System.Net.Http.Json;
using ExpressRecipe.Client.Shared.Models.Notification;

namespace ExpressRecipe.Client.Shared.Services;

public interface INotificationApiClient
{
    Task<NotificationDto?> GetNotificationAsync(Guid id);
    Task<NotificationSearchResult?> SearchNotificationsAsync(NotificationSearchRequest request);
    Task<NotificationSummaryDto?> GetNotificationSummaryAsync();
    Task<bool> MarkAsReadAsync(Guid notificationId, bool isRead = true);
    Task<bool> MarkAllAsReadAsync();
    Task<bool> DeleteNotificationAsync(Guid id);
    Task<bool> DeleteAllReadAsync();

    Task<NotificationPreferencesDto?> GetPreferencesAsync();
    Task<bool> UpdatePreferencesAsync(UpdateNotificationPreferencesRequest request);

    // Generate notifications manually (usually done by backend)
    Task<bool> GenerateExpiringItemNotificationsAsync();
    Task<bool> GenerateLowStockNotificationsAsync();
}

public class NotificationApiClient : INotificationApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ITokenProvider _tokenProvider;

    public NotificationApiClient(HttpClient httpClient, ITokenProvider tokenProvider)
    {
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
    }

    private async Task<bool> EnsureAuthenticatedAsync()
    {
        var token = await _tokenProvider.GetTokenAsync();
        if (string.IsNullOrEmpty(token))
            return false;

        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return true;
    }

    public async Task<NotificationDto?> GetNotificationAsync(Guid id)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            return await _httpClient.GetFromJsonAsync<NotificationDto>($"/api/notifications/{id}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<NotificationSearchResult?> SearchNotificationsAsync(NotificationSearchRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/notifications/search", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<NotificationSearchResult>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<NotificationSummaryDto?> GetNotificationSummaryAsync()
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            return await _httpClient.GetFromJsonAsync<NotificationSummaryDto>("/api/notifications/summary");
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> MarkAsReadAsync(Guid notificationId, bool isRead = true)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var request = new MarkNotificationReadRequest
            {
                NotificationId = notificationId,
                IsRead = isRead
            };

            var response = await _httpClient.PostAsJsonAsync("/api/notifications/mark-read", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> MarkAllAsReadAsync()
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PostAsync("/api/notifications/mark-all-read", null);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DeleteNotificationAsync(Guid id)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.DeleteAsync($"/api/notifications/{id}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DeleteAllReadAsync()
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.DeleteAsync("/api/notifications/delete-all-read");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<NotificationPreferencesDto?> GetPreferencesAsync()
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            return await _httpClient.GetFromJsonAsync<NotificationPreferencesDto>("/api/notifications/preferences");
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> UpdatePreferencesAsync(UpdateNotificationPreferencesRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PutAsJsonAsync("/api/notifications/preferences", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> GenerateExpiringItemNotificationsAsync()
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PostAsync("/api/notifications/generate-expiring", null);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> GenerateLowStockNotificationsAsync()
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PostAsync("/api/notifications/generate-low-stock", null);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
