using System.Net.Http.Json;

namespace ExpressRecipe.Client.Shared.Services
{
    // DTOs for Sync Service
    public class SyncStatus
    {
        public bool IsEnabled { get; set; }
        public bool IsOnline { get; set; }
        public DateTime? LastSyncTime { get; set; }
        public int PendingChanges { get; set; }
        public int ConflictsCount { get; set; }
        public string Status { get; set; } = "Idle"; // Idle, Syncing, Error
        public string? ErrorMessage { get; set; }
    }

    public class SyncResult
    {
        public bool Success { get; set; }
        public int ItemsSynced { get; set; }
        public int ConflictsResolved { get; set; }
        public int ConflictsRemaining { get; set; }
        public List<string> SyncedEntities { get; set; } = [];
        public List<SyncConflict> Conflicts { get; set; } = [];
        public DateTime SyncedAt { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class SyncConflict
    {
        public Guid Id { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public Guid EntityId { get; set; }
        public string Field { get; set; } = string.Empty;
        public object? LocalValue { get; set; }
        public object? ServerValue { get; set; }
        public DateTime LocalModifiedAt { get; set; }
        public DateTime ServerModifiedAt { get; set; }
        public string ConflictResolution { get; set; } = "Unresolved"; // Unresolved, UseLocal, UseServer, Merged
    }

    public class SyncSettings
    {
        public bool AutoSync { get; set; } = true;
        public bool SyncOnWifiOnly { get; set; } = true;
        public int SyncIntervalMinutes { get; set; } = 15;
        public bool SyncProducts { get; set; } = true;
        public bool SyncRecipes { get; set; } = true;
        public bool SyncInventory { get; set; } = true;
        public bool SyncShoppingLists { get; set; } = true;
        public bool SyncMealPlans { get; set; } = true;
        public bool SyncUserProfile { get; set; } = true;
    }

    public class ConflictResolutionRequest
    {
        public Guid ConflictId { get; set; }
        public string Resolution { get; set; } = string.Empty; // UseLocal, UseServer, Merged
        public object? MergedValue { get; set; }
    }

    public class SyncQueueItem
    {
        public Guid Id { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public Guid EntityId { get; set; }
        public string Operation { get; set; } = string.Empty; // Create, Update, Delete
        public DateTime QueuedAt { get; set; }
        public int RetryCount { get; set; }
        public bool IsSynced { get; set; }
    }

    public interface ISyncApiClient
    {
        Task<SyncStatus> GetSyncStatusAsync();
        Task<SyncResult> TriggerSyncAsync();
        Task<SyncResult> SyncEntityAsync(string entityType, Guid entityId);
        Task<List<SyncConflict>> GetConflictsAsync();
        Task<bool> ResolveConflictAsync(ConflictResolutionRequest request);
        Task<bool> ResolveAllConflictsAsync(string resolution);
        Task<SyncSettings> GetSyncSettingsAsync();
        Task<bool> UpdateSyncSettingsAsync(SyncSettings settings);
        Task<List<SyncQueueItem>> GetSyncQueueAsync();
        Task<bool> ClearSyncQueueAsync();
        Task<bool> EnableSyncAsync();
        Task<bool> DisableSyncAsync();
        Task<bool> ResetSyncAsync();
    }

    public class SyncApiClient : ISyncApiClient
    {
        private readonly HttpClient _httpClient;

        public SyncApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<SyncStatus> GetSyncStatusAsync()
        {
            HttpResponseMessage response = await _httpClient.GetAsync("/api/sync/status");

            return !response.IsSuccessStatusCode
                ? new SyncStatus { Status = "Error", ErrorMessage = "Failed to get sync status" }
                : await response.Content.ReadFromJsonAsync<SyncStatus>() ?? new SyncStatus();
        }

        public async Task<SyncResult> TriggerSyncAsync()
        {
            HttpResponseMessage response = await _httpClient.PostAsync("/api/sync/trigger", null);

            return !response.IsSuccessStatusCode
                ? new SyncResult { Success = false, ErrorMessage = "Sync failed" }
                : await response.Content.ReadFromJsonAsync<SyncResult>() ?? new SyncResult();
        }

        public async Task<SyncResult> SyncEntityAsync(string entityType, Guid entityId)
        {
            HttpResponseMessage response = await _httpClient.PostAsync($"/api/sync/entity/{entityType}/{entityId}", null);

            return !response.IsSuccessStatusCode
                ? new SyncResult { Success = false, ErrorMessage = "Entity sync failed" }
                : await response.Content.ReadFromJsonAsync<SyncResult>() ?? new SyncResult();
        }

        public async Task<List<SyncConflict>> GetConflictsAsync()
        {
            HttpResponseMessage response = await _httpClient.GetAsync("/api/sync/conflicts");

            return !response.IsSuccessStatusCode ? [] : await response.Content.ReadFromJsonAsync<List<SyncConflict>>() ?? [];
        }

        public async Task<bool> ResolveConflictAsync(ConflictResolutionRequest request)
        {
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/sync/conflicts/resolve", request);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> ResolveAllConflictsAsync(string resolution)
        {
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/sync/conflicts/resolve-all", new { Resolution = resolution });
            return response.IsSuccessStatusCode;
        }

        public async Task<SyncSettings> GetSyncSettingsAsync()
        {
            HttpResponseMessage response = await _httpClient.GetAsync("/api/sync/settings");

            return !response.IsSuccessStatusCode
                ? new SyncSettings()
                : await response.Content.ReadFromJsonAsync<SyncSettings>() ?? new SyncSettings();
        }

        public async Task<bool> UpdateSyncSettingsAsync(SyncSettings settings)
        {
            HttpResponseMessage response = await _httpClient.PutAsJsonAsync("/api/sync/settings", settings);
            return response.IsSuccessStatusCode;
        }

        public async Task<List<SyncQueueItem>> GetSyncQueueAsync()
        {
            HttpResponseMessage response = await _httpClient.GetAsync("/api/sync/queue");

            return !response.IsSuccessStatusCode ? [] : await response.Content.ReadFromJsonAsync<List<SyncQueueItem>>() ?? [];
        }

        public async Task<bool> ClearSyncQueueAsync()
        {
            HttpResponseMessage response = await _httpClient.DeleteAsync("/api/sync/queue");
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> EnableSyncAsync()
        {
            HttpResponseMessage response = await _httpClient.PostAsync("/api/sync/enable", null);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> DisableSyncAsync()
        {
            HttpResponseMessage response = await _httpClient.PostAsync("/api/sync/disable", null);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> ResetSyncAsync()
        {
            HttpResponseMessage response = await _httpClient.PostAsync("/api/sync/reset", null);
            return response.IsSuccessStatusCode;
        }
    }
}
