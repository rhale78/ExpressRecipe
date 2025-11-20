using System.Net.Http.Json;
using ExpressRecipe.Client.Shared.Models.Price;

namespace ExpressRecipe.Client.Shared.Services;

public interface IPriceApiClient
{
    // Price History
    Task<ProductPriceHistoryDto?> GetProductPriceHistoryAsync(Guid productId);
    Task<bool> RecordPriceAsync(RecordPriceRequest request);

    // Price Alerts
    Task<List<PriceAlertDto>> GetUserPriceAlertsAsync();
    Task<bool> CreatePriceAlertAsync(CreatePriceAlertRequest request);
    Task<bool> UpdatePriceAlertAsync(UpdatePriceAlertRequest request);
    Task<bool> DeletePriceAlertAsync(Guid alertId);

    // Shopping Budget
    Task<List<ShoppingBudgetDto>> GetUserBudgetsAsync();
    Task<ShoppingBudgetDto?> GetActiveBudgetAsync();
    Task<BudgetAnalyticsDto?> GetBudgetAnalyticsAsync(Guid budgetId);
    Task<Guid> CreateBudgetAsync(CreateShoppingBudgetRequest request);
    Task<bool> UpdateBudgetAsync(UpdateShoppingBudgetRequest request);
    Task<bool> DeleteBudgetAsync(Guid budgetId);

    // Budget Transactions
    Task<List<BudgetTransactionDto>> GetBudgetTransactionsAsync(Guid budgetId);
    Task<bool> RecordTransactionAsync(RecordBudgetTransactionRequest request);
    Task<bool> DeleteTransactionAsync(Guid transactionId);

    // Price Comparison
    Task<PriceComparisonResult?> ComparePricesAsync(PriceComparisonRequest request);

    // Best Price Alerts
    Task<List<BestPriceAlertDto>> GetBestPriceAlertsAsync();

    // Store Preferences
    Task<List<StorePreferenceDto>> GetStorePreferencesAsync();
    Task<bool> UpdateStorePreferenceAsync(UpdateStorePreferenceRequest request);

    // Price Trends
    Task<List<PriceTrendDto>> GetPriceTrendsAsync(GetPriceTrendsRequest request);
}

public class PriceApiClient : IPriceApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ITokenProvider _tokenProvider;

    public PriceApiClient(HttpClient httpClient, ITokenProvider tokenProvider)
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

    // Price History
    public async Task<ProductPriceHistoryDto?> GetProductPriceHistoryAsync(Guid productId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<ProductPriceHistoryDto>($"/api/prices/products/{productId}/history");
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> RecordPriceAsync(RecordPriceRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/prices/record", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // Price Alerts
    public async Task<List<PriceAlertDto>> GetUserPriceAlertsAsync()
    {
        if (!await EnsureAuthenticatedAsync())
            return new List<PriceAlertDto>();

        try
        {
            var alerts = await _httpClient.GetFromJsonAsync<List<PriceAlertDto>>("/api/prices/alerts");
            return alerts ?? new List<PriceAlertDto>();
        }
        catch
        {
            return new List<PriceAlertDto>();
        }
    }

    public async Task<bool> CreatePriceAlertAsync(CreatePriceAlertRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/prices/alerts", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> UpdatePriceAlertAsync(UpdatePriceAlertRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PutAsJsonAsync($"/api/prices/alerts/{request.AlertId}", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DeletePriceAlertAsync(Guid alertId)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.DeleteAsync($"/api/prices/alerts/{alertId}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // Shopping Budget
    public async Task<List<ShoppingBudgetDto>> GetUserBudgetsAsync()
    {
        if (!await EnsureAuthenticatedAsync())
            return new List<ShoppingBudgetDto>();

        try
        {
            var budgets = await _httpClient.GetFromJsonAsync<List<ShoppingBudgetDto>>("/api/prices/budgets");
            return budgets ?? new List<ShoppingBudgetDto>();
        }
        catch
        {
            return new List<ShoppingBudgetDto>();
        }
    }

    public async Task<ShoppingBudgetDto?> GetActiveBudgetAsync()
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            return await _httpClient.GetFromJsonAsync<ShoppingBudgetDto>("/api/prices/budgets/active");
        }
        catch
        {
            return null;
        }
    }

    public async Task<BudgetAnalyticsDto?> GetBudgetAnalyticsAsync(Guid budgetId)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            return await _httpClient.GetFromJsonAsync<BudgetAnalyticsDto>($"/api/prices/budgets/{budgetId}/analytics");
        }
        catch
        {
            return null;
        }
    }

    public async Task<Guid> CreateBudgetAsync(CreateShoppingBudgetRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return Guid.Empty;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/prices/budgets", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<Guid>();
        }
        catch
        {
            return Guid.Empty;
        }
    }

    public async Task<bool> UpdateBudgetAsync(UpdateShoppingBudgetRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PutAsJsonAsync($"/api/prices/budgets/{request.BudgetId}", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DeleteBudgetAsync(Guid budgetId)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.DeleteAsync($"/api/prices/budgets/{budgetId}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // Budget Transactions
    public async Task<List<BudgetTransactionDto>> GetBudgetTransactionsAsync(Guid budgetId)
    {
        if (!await EnsureAuthenticatedAsync())
            return new List<BudgetTransactionDto>();

        try
        {
            var transactions = await _httpClient.GetFromJsonAsync<List<BudgetTransactionDto>>($"/api/prices/budgets/{budgetId}/transactions");
            return transactions ?? new List<BudgetTransactionDto>();
        }
        catch
        {
            return new List<BudgetTransactionDto>();
        }
    }

    public async Task<bool> RecordTransactionAsync(RecordBudgetTransactionRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/prices/transactions", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DeleteTransactionAsync(Guid transactionId)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.DeleteAsync($"/api/prices/transactions/{transactionId}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // Price Comparison
    public async Task<PriceComparisonResult?> ComparePricesAsync(PriceComparisonRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/prices/compare", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<PriceComparisonResult>();
        }
        catch
        {
            return null;
        }
    }

    // Best Price Alerts
    public async Task<List<BestPriceAlertDto>> GetBestPriceAlertsAsync()
    {
        if (!await EnsureAuthenticatedAsync())
            return new List<BestPriceAlertDto>();

        try
        {
            var alerts = await _httpClient.GetFromJsonAsync<List<BestPriceAlertDto>>("/api/prices/best-price-alerts");
            return alerts ?? new List<BestPriceAlertDto>();
        }
        catch
        {
            return new List<BestPriceAlertDto>();
        }
    }

    // Store Preferences
    public async Task<List<StorePreferenceDto>> GetStorePreferencesAsync()
    {
        if (!await EnsureAuthenticatedAsync())
            return new List<StorePreferenceDto>();

        try
        {
            var prefs = await _httpClient.GetFromJsonAsync<List<StorePreferenceDto>>("/api/prices/store-preferences");
            return prefs ?? new List<StorePreferenceDto>();
        }
        catch
        {
            return new List<StorePreferenceDto>();
        }
    }

    public async Task<bool> UpdateStorePreferenceAsync(UpdateStorePreferenceRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return false;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/prices/store-preferences", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // Price Trends
    public async Task<List<PriceTrendDto>> GetPriceTrendsAsync(GetPriceTrendsRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/prices/trends", request);
            response.EnsureSuccessStatusCode();
            var trends = await response.Content.ReadFromJsonAsync<List<PriceTrendDto>>();
            return trends ?? new List<PriceTrendDto>();
        }
        catch
        {
            return new List<PriceTrendDto>();
        }
    }
}
