using System.Net.Http.Json;
using ExpressRecipe.Client.Shared.Models.Inventory;

namespace ExpressRecipe.Client.Shared.Services;

public interface IInventoryApiClient
{
    // Basic CRUD
    Task<InventoryItemDto?> GetInventoryItemAsync(Guid id);
    Task<InventorySearchResult?> SearchInventoryAsync(InventorySearchRequest request);
    Task<InventorySummaryDto?> GetInventorySummaryAsync();
    Task<Guid?> CreateInventoryItemAsync(CreateInventoryItemRequest request);
    Task<bool> UpdateInventoryItemAsync(Guid id, UpdateInventoryItemRequest request);
    Task<bool> DeleteInventoryItemAsync(Guid id);
    Task<bool> AdjustQuantityAsync(AdjustInventoryQuantityRequest request);
    Task<bool> BulkAddInventoryItemsAsync(BulkAddInventoryItemsRequest request);
    Task<InventoryItemValidationResult?> ValidateInventoryItemAsync(CreateInventoryItemRequest request);

    // Household Management
    Task<HouseholdDto?> CreateHouseholdAsync(CreateHouseholdRequest request);
    Task<List<HouseholdDto>?> GetUserHouseholdsAsync();
    Task<HouseholdDto?> GetHouseholdAsync(Guid householdId);
    Task<bool> UpdateHouseholdAsync(Guid householdId, UpdateHouseholdRequest request);
    Task<bool> AddHouseholdMemberAsync(Guid householdId, AddHouseholdMemberRequest request);
    Task<List<HouseholdMemberDto>?> GetHouseholdMembersAsync(Guid householdId);
    Task<bool> UpdateMemberRoleAsync(Guid householdId, Guid memberId, UpdateMemberRoleRequest request);
    Task<bool> RemoveMemberAsync(Guid householdId, Guid memberId);

    // Address & Location Management
    Task<List<AddressDto>?> GetHouseholdAddressesAsync(Guid householdId);
    Task<AddressDto?> CreateAddressAsync(Guid householdId, CreateAddressRequest request);
    Task<bool> UpdateAddressAsync(Guid addressId, UpdateAddressRequest request);
    Task<bool> DeleteAddressAsync(Guid addressId);
    Task<AddressDto?> DetectCurrentAddressAsync(Guid householdId, DetectAddressRequest request);
    Task<List<StorageLocationDto>?> GetStorageLocationsAsync(Guid? addressId = null, Guid? householdId = null);
    Task<StorageLocationDto?> CreateStorageLocationAsync(CreateStorageLocationRequest request);
    Task<bool> UpdateStorageLocationAsync(Guid locationId, UpdateStorageLocationRequest request);

    // Scanning Operations
    Task<Guid?> StartInventoryScanSessionAsync(StartScanSessionRequest request);
    Task<ScanSessionDto?> GetActiveScanSessionAsync();
    Task<bool> ScanAddItemAsync(Guid sessionId, ScanAddItemRequest request);
    Task<bool> ScanUseItemAsync(Guid sessionId, ScanUseItemRequest request);
    Task<bool> ScanDisposeItemAsync(Guid sessionId, ScanDisposeItemRequest request);
    Task<ScanSessionResultDto?> EndInventoryScanSessionAsync(Guid sessionId);

    // Reports
    Task<List<InventoryItemDto>?> GetLowStockItemsAsync(int? threshold = null, Guid? householdId = null, Guid? addressId = null);
    Task<List<InventoryItemDto>?> GetRunningOutItemsAsync(int? days = null, Guid? householdId = null, Guid? addressId = null);
    Task<List<InventoryItemDto>?> GetExpiringItemsAsync(int? days = null, Guid? householdId = null, Guid? addressId = null);
    Task<InventoryReportDto?> GetInventoryReportAsync(Guid? householdId = null, Guid? addressId = null);

    // Allergen Discovery
    Task<List<AllergenDiscoveryDto>?> GetAllergenDiscoveriesAsync();
    Task<bool> AddToUserAllergensAsync(Guid discoveryId);
}

public class InventoryApiClient : IInventoryApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ITokenProvider _tokenProvider;

    public InventoryApiClient(HttpClient httpClient, ITokenProvider tokenProvider)
    {
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
    }

    private async Task<bool> EnsureAuthenticatedAsync()
    {
        var token = await _tokenProvider.GetAccessTokenAsync();
        if (string.IsNullOrEmpty(token))
            return false;

        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return true;
    }

    public async Task<InventoryItemDto?> GetInventoryItemAsync(Guid id)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            return await _httpClient.GetFromJsonAsync<InventoryItemDto>($"/api/inventory/{id}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<InventorySearchResult?> SearchInventoryAsync(InventorySearchRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            // Return empty results for now since endpoint doesn't exist yet
            return new InventorySearchResult
            {
                Items = new List<InventoryItemDto>(),
                TotalCount = 0,
                Page = request.Page,
                PageSize = request.PageSize
            };
        }
        catch
        {
            return null;
        }
    }

    public async Task<InventorySummaryDto?> GetInventorySummaryAsync()
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            // Return empty summary for now since endpoint doesn't exist yet
            return new InventorySummaryDto
            {
                TotalItems = 0,
                ExpiredItems = 0,
                ExpiringSoonItems = 0,
                LowStockItems = 0,
                ItemsByLocation = new Dictionary<string, int>(),
                ItemsByCategory = new Dictionary<string, int>()
            };
        }
        catch
        {
            return null;
        }
    }

    public async Task<Guid?> CreateInventoryItemAsync(CreateInventoryItemRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/inventory", request);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<CreateInventoryItemResponse>();
            return result?.Id;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> UpdateInventoryItemAsync(Guid id, UpdateInventoryItemRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PutAsJsonAsync($"/api/inventory/{id}", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DeleteInventoryItemAsync(Guid id)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.DeleteAsync($"/api/inventory/{id}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> AdjustQuantityAsync(AdjustInventoryQuantityRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/inventory/adjust-quantity", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> BulkAddInventoryItemsAsync(BulkAddInventoryItemsRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/inventory/bulk", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<InventoryItemValidationResult?> ValidateInventoryItemAsync(CreateInventoryItemRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/inventory/validate", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<InventoryItemValidationResult>();
        }
        catch
        {
            return null;
        }
    }

    // Household Management
    public async Task<HouseholdDto?> CreateHouseholdAsync(CreateHouseholdRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/inventory/household", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<HouseholdDto>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<HouseholdDto>?> GetUserHouseholdsAsync()
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            return await _httpClient.GetFromJsonAsync<List<HouseholdDto>>("/api/inventory/household");
        }
        catch
        {
            return null;
        }
    }

    public async Task<HouseholdDto?> GetHouseholdAsync(Guid householdId)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            return await _httpClient.GetFromJsonAsync<HouseholdDto>($"/api/inventory/household/{householdId}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> UpdateHouseholdAsync(Guid householdId, UpdateHouseholdRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PutAsJsonAsync($"/api/inventory/household/{householdId}", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> AddHouseholdMemberAsync(Guid householdId, AddHouseholdMemberRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/inventory/household/{householdId}/members", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<HouseholdMemberDto>?> GetHouseholdMembersAsync(Guid householdId)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            return await _httpClient.GetFromJsonAsync<List<HouseholdMemberDto>>($"/api/inventory/household/{householdId}/members");
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> UpdateMemberRoleAsync(Guid householdId, Guid memberId, UpdateMemberRoleRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PutAsJsonAsync($"/api/inventory/household/{householdId}/members/{memberId}", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> RemoveMemberAsync(Guid householdId, Guid memberId)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.DeleteAsync($"/api/inventory/household/{householdId}/members/{memberId}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // Address & Location Management
    public async Task<List<AddressDto>?> GetHouseholdAddressesAsync(Guid householdId)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            return await _httpClient.GetFromJsonAsync<List<AddressDto>>($"/api/inventory/household/{householdId}/addresses");
        }
        catch
        {
            return null;
        }
    }

    public async Task<AddressDto?> CreateAddressAsync(Guid householdId, CreateAddressRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/inventory/household/{householdId}/addresses", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<AddressDto>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> UpdateAddressAsync(Guid addressId, UpdateAddressRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PutAsJsonAsync($"/api/inventory/household/addresses/{addressId}", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DeleteAddressAsync(Guid addressId)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.DeleteAsync($"/api/inventory/household/addresses/{addressId}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<AddressDto?> DetectCurrentAddressAsync(Guid householdId, DetectAddressRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/inventory/household/{householdId}/addresses/detect", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<AddressDto>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<StorageLocationDto>?> GetStorageLocationsAsync(Guid? addressId = null, Guid? householdId = null)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var queryParams = new List<string>();
            if (addressId.HasValue)
                queryParams.Add($"addressId={addressId.Value}");
            if (householdId.HasValue)
                queryParams.Add($"householdId={householdId.Value}");

            var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
            return await _httpClient.GetFromJsonAsync<List<StorageLocationDto>>($"/api/inventory/locations{query}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<StorageLocationDto?> CreateStorageLocationAsync(CreateStorageLocationRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/inventory/locations", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<StorageLocationDto>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> UpdateStorageLocationAsync(Guid locationId, UpdateStorageLocationRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PutAsJsonAsync($"/api/inventory/locations/{locationId}", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // Scanning Operations
    public async Task<Guid?> StartInventoryScanSessionAsync(StartScanSessionRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/inventory/scan/start", request);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<GuidResponse>();
            return result?.Id;
        }
        catch
        {
            return null;
        }
    }

    public async Task<ScanSessionDto?> GetActiveScanSessionAsync()
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            return await _httpClient.GetFromJsonAsync<ScanSessionDto>("/api/inventory/scan/active");
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> ScanAddItemAsync(Guid sessionId, ScanAddItemRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/inventory/scan/{sessionId}/add", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> ScanUseItemAsync(Guid sessionId, ScanUseItemRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/inventory/scan/{sessionId}/use", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> ScanDisposeItemAsync(Guid sessionId, ScanDisposeItemRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/inventory/scan/{sessionId}/dispose", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<ScanSessionResultDto?> EndInventoryScanSessionAsync(Guid sessionId)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var response = await _httpClient.PostAsync($"/api/inventory/scan/{sessionId}/end", null);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ScanSessionResultDto>();
        }
        catch
        {
            return null;
        }
    }

    // Reports
    public async Task<List<InventoryItemDto>?> GetLowStockItemsAsync(int? threshold = null, Guid? householdId = null, Guid? addressId = null)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var queryParams = new List<string>();
            if (threshold.HasValue)
                queryParams.Add($"threshold={threshold.Value}");
            if (householdId.HasValue)
                queryParams.Add($"householdId={householdId.Value}");
            if (addressId.HasValue)
                queryParams.Add($"addressId={addressId.Value}");

            var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
            return await _httpClient.GetFromJsonAsync<List<InventoryItemDto>>($"/api/inventory/low-stock{query}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<InventoryItemDto>?> GetRunningOutItemsAsync(int? days = null, Guid? householdId = null, Guid? addressId = null)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var queryParams = new List<string>();
            if (days.HasValue)
                queryParams.Add($"days={days.Value}");
            if (householdId.HasValue)
                queryParams.Add($"householdId={householdId.Value}");
            if (addressId.HasValue)
                queryParams.Add($"addressId={addressId.Value}");

            var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
            return await _httpClient.GetFromJsonAsync<List<InventoryItemDto>>($"/api/inventory/running-out{query}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<InventoryItemDto>?> GetExpiringItemsAsync(int? days = null, Guid? householdId = null, Guid? addressId = null)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var queryParams = new List<string>();
            if (days.HasValue)
                queryParams.Add($"days={days.Value}");
            if (householdId.HasValue)
                queryParams.Add($"householdId={householdId.Value}");
            if (addressId.HasValue)
                queryParams.Add($"addressId={addressId.Value}");

            var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
            return await _httpClient.GetFromJsonAsync<List<InventoryItemDto>>($"/api/inventory/about-to-expire{query}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<InventoryReportDto?> GetInventoryReportAsync(Guid? householdId = null, Guid? addressId = null)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var queryParams = new List<string>();
            if (householdId.HasValue)
                queryParams.Add($"householdId={householdId.Value}");
            if (addressId.HasValue)
                queryParams.Add($"addressId={addressId.Value}");

            var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
            return await _httpClient.GetFromJsonAsync<InventoryReportDto>($"/api/inventory/report{query}");
        }
        catch
        {
            return null;
        }
    }

    // Allergen Discovery
    public async Task<List<AllergenDiscoveryDto>?> GetAllergenDiscoveriesAsync()
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            return await _httpClient.GetFromJsonAsync<List<AllergenDiscoveryDto>>("/api/inventory/allergen-discoveries");
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> AddToUserAllergensAsync(Guid discoveryId)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PostAsync($"/api/inventory/allergen-discoveries/{discoveryId}/add-to-profile", null);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private class CreateInventoryItemResponse
    {
        public Guid Id { get; set; }
    }

    private class GuidResponse
    {
        public Guid Id { get; set; }
    }
}
