using System.Net.Http.Json;
using ExpressRecipe.Client.Shared.Models.Inventory;

namespace ExpressRecipe.Client.Shared.Services
{
    public interface IInventoryApiClient
    {
        Task<InventoryItemDto?> GetInventoryItemAsync(Guid id);
        Task<InventorySearchResult?> SearchInventoryAsync(InventorySearchRequest request);
        Task<InventorySummaryDto?> GetInventorySummaryAsync();
        Task<Guid?> CreateInventoryItemAsync(CreateInventoryItemRequest request);
        Task<bool> UpdateInventoryItemAsync(Guid id, UpdateInventoryItemRequest request);
        Task<bool> DeleteInventoryItemAsync(Guid id);
        Task<bool> AdjustQuantityAsync(AdjustInventoryQuantityRequest request);
        Task<bool> BulkAddInventoryItemsAsync(BulkAddInventoryItemsRequest request);
        Task<InventoryItemValidationResult?> ValidateInventoryItemAsync(CreateInventoryItemRequest request);
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
            {
                return false;
            }

            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            return true;
        }

        public async Task<InventoryItemDto?> GetInventoryItemAsync(Guid id)
        {
            if (!await EnsureAuthenticatedAsync())
            {
                return null;
            }

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
            {
                return null;
            }

            try
            {
                // Return empty results for now since endpoint doesn't exist yet
                return new InventorySearchResult
                {
                    Items = [],
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
            {
                return null;
            }

            try
            {
                // Return empty summary for now since endpoint doesn't exist yet
                return new InventorySummaryDto
                {
                    TotalItems = 0,
                    ExpiredItems = 0,
                    ExpiringSoonItems = 0,
                    LowStockItems = 0,
                    ItemsByLocation = [],
                    ItemsByCategory = []
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
            {
                return null;
            }

            try
            {
                HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/inventory", request);
                response.EnsureSuccessStatusCode();
                CreateInventoryItemResponse? result = await response.Content.ReadFromJsonAsync<CreateInventoryItemResponse>();
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
            {
                return false;
            }

            try
            {
                HttpResponseMessage response = await _httpClient.PutAsJsonAsync($"/api/inventory/{id}", request);
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
            {
                return false;
            }

            try
            {
                HttpResponseMessage response = await _httpClient.DeleteAsync($"/api/inventory/{id}");
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
            {
                return false;
            }

            try
            {
                HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/inventory/adjust-quantity", request);
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
            {
                return false;
            }

            try
            {
                HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/inventory/bulk", request);
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
            {
                return null;
            }

            try
            {
                HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/inventory/validate", request);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<InventoryItemValidationResult>();
            }
            catch
            {
                return null;
            }
        }

        private class CreateInventoryItemResponse
        {
            public Guid Id { get; set; }
        }
    }
}
