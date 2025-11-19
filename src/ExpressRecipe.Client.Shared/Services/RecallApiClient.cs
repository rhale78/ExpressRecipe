using System.Net.Http.Json;

namespace ExpressRecipe.Client.Shared.Services;

// DTOs for Recall Service
public class RecallAlert
{
    public Guid Id { get; set; }
    public string RecallNumber { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Classification { get; set; } = string.Empty; // Class I, II, III
    public string Status { get; set; } = string.Empty; // Ongoing, Completed
    public string Source { get; set; } = string.Empty; // FDA, USDA
    public DateTime RecallDate { get; set; }
    public DateTime? TerminationDate { get; set; }
    public string? DistributionPattern { get; set; }
    public string? RecallingFirm { get; set; }
    public string? DetailsUrl { get; set; }
    public List<string> AffectedProducts { get; set; } = new();
    public bool AffectsUserInventory { get; set; }
}

public class UserRecallNotification
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid RecallId { get; set; }
    public RecallAlert Recall { get; set; } = new();
    public List<string> AffectedInventoryItems { get; set; } = new();
    public bool IsRead { get; set; }
    public bool IsDismissed { get; set; }
    public DateTime NotifiedAt { get; set; }
}

public class RecallSearchRequest
{
    public string? ProductName { get; set; }
    public string? Brand { get; set; }
    public string? Classification { get; set; }
    public string? Source { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public bool OnlyActive { get; set; } = true;
}

public class RecallSubscription
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? ProductName { get; set; }
    public string? Brand { get; set; }
    public string? Category { get; set; }
    public bool EmailNotifications { get; set; } = true;
    public bool PushNotifications { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

public interface IRecallApiClient
{
    Task<List<RecallAlert>> GetActiveRecallsAsync();
    Task<List<RecallAlert>> SearchRecallsAsync(RecallSearchRequest request);
    Task<RecallAlert?> GetRecallDetailsAsync(Guid recallId);
    Task<List<UserRecallNotification>> GetUserRecallNotificationsAsync();
    Task<int> GetUnreadRecallCountAsync();
    Task<bool> MarkRecallAsReadAsync(Guid notificationId);
    Task<bool> DismissRecallAsync(Guid notificationId);
    Task<bool> CheckInventoryForRecallsAsync();
    Task<List<RecallSubscription>> GetSubscriptionsAsync();
    Task<Guid> CreateSubscriptionAsync(RecallSubscription subscription);
    Task<bool> DeleteSubscriptionAsync(Guid subscriptionId);
}

public class RecallApiClient : IRecallApiClient
{
    private readonly HttpClient _httpClient;

    public RecallApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<RecallAlert>> GetActiveRecallsAsync()
    {
        var response = await _httpClient.GetAsync("/api/recalls/active");

        if (!response.IsSuccessStatusCode)
        {
            return new List<RecallAlert>();
        }

        return await response.Content.ReadFromJsonAsync<List<RecallAlert>>() ?? new List<RecallAlert>();
    }

    public async Task<List<RecallAlert>> SearchRecallsAsync(RecallSearchRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/recalls/search", request);

        if (!response.IsSuccessStatusCode)
        {
            return new List<RecallAlert>();
        }

        return await response.Content.ReadFromJsonAsync<List<RecallAlert>>() ?? new List<RecallAlert>();
    }

    public async Task<RecallAlert?> GetRecallDetailsAsync(Guid recallId)
    {
        var response = await _httpClient.GetAsync($"/api/recalls/{recallId}");

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<RecallAlert>();
    }

    public async Task<List<UserRecallNotification>> GetUserRecallNotificationsAsync()
    {
        var response = await _httpClient.GetAsync("/api/recalls/notifications");

        if (!response.IsSuccessStatusCode)
        {
            return new List<UserRecallNotification>();
        }

        return await response.Content.ReadFromJsonAsync<List<UserRecallNotification>>() ?? new List<UserRecallNotification>();
    }

    public async Task<int> GetUnreadRecallCountAsync()
    {
        var response = await _httpClient.GetAsync("/api/recalls/notifications/unread-count");

        if (!response.IsSuccessStatusCode)
        {
            return 0;
        }

        return await response.Content.ReadFromJsonAsync<int>();
    }

    public async Task<bool> MarkRecallAsReadAsync(Guid notificationId)
    {
        var response = await _httpClient.PutAsync($"/api/recalls/notifications/{notificationId}/read", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DismissRecallAsync(Guid notificationId)
    {
        var response = await _httpClient.PutAsync($"/api/recalls/notifications/{notificationId}/dismiss", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> CheckInventoryForRecallsAsync()
    {
        var response = await _httpClient.PostAsync("/api/recalls/check-inventory", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<List<RecallSubscription>> GetSubscriptionsAsync()
    {
        var response = await _httpClient.GetAsync("/api/recalls/subscriptions");

        if (!response.IsSuccessStatusCode)
        {
            return new List<RecallSubscription>();
        }

        return await response.Content.ReadFromJsonAsync<List<RecallSubscription>>() ?? new List<RecallSubscription>();
    }

    public async Task<Guid> CreateSubscriptionAsync(RecallSubscription subscription)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/recalls/subscriptions", subscription);

        if (!response.IsSuccessStatusCode)
        {
            return Guid.Empty;
        }

        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    public async Task<bool> DeleteSubscriptionAsync(Guid subscriptionId)
    {
        var response = await _httpClient.DeleteAsync($"/api/recalls/subscriptions/{subscriptionId}");
        return response.IsSuccessStatusCode;
    }
}
