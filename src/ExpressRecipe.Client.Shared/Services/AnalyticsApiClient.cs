using System.Net.Http.Json;
using ExpressRecipe.Client.Shared.Models.Analytics;

namespace ExpressRecipe.Client.Shared.Services;

public interface IAnalyticsApiClient
{
    // Dashboard
    Task<AnalyticsDashboardDto?> GetDashboardAsync();

    // Spending Reports
    Task<SpendingSummaryDto?> GetSpendingSummaryAsync();
    Task<SpendingReportDto?> GetSpendingReportAsync(SpendingReportRequest request);

    // Nutrition Reports
    Task<NutritionSummaryDto?> GetNutritionSummaryAsync();
    Task<NutritionReportDto?> GetNutritionReportAsync(NutritionReportRequest request);

    // Inventory Reports
    Task<InventorySummaryDto?> GetInventorySummaryAsync();
    Task<InventoryReportDto?> GetInventoryReportAsync(InventoryReportRequest request);

    // Waste Reports
    Task<WasteSummaryDto?> GetWasteSummaryAsync();
    Task<WasteReportDto?> GetWasteReportAsync(WasteReportRequest request);

    // Export
    Task<ExportReportResponse?> ExportReportAsync(ExportReportRequest request);
}

public class AnalyticsApiClient : IAnalyticsApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ITokenProvider _tokenProvider;

    public AnalyticsApiClient(HttpClient httpClient, ITokenProvider tokenProvider)
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

    public async Task<AnalyticsDashboardDto?> GetDashboardAsync()
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            return await _httpClient.GetFromJsonAsync<AnalyticsDashboardDto>("/api/analytics/dashboard");
        }
        catch
        {
            return null;
        }
    }

    public async Task<SpendingSummaryDto?> GetSpendingSummaryAsync()
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            return await _httpClient.GetFromJsonAsync<SpendingSummaryDto>("/api/analytics/spending/summary");
        }
        catch
        {
            return null;
        }
    }

    public async Task<SpendingReportDto?> GetSpendingReportAsync(SpendingReportRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/analytics/spending/report", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<SpendingReportDto>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<NutritionSummaryDto?> GetNutritionSummaryAsync()
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            return await _httpClient.GetFromJsonAsync<NutritionSummaryDto>("/api/analytics/nutrition/summary");
        }
        catch
        {
            return null;
        }
    }

    public async Task<NutritionReportDto?> GetNutritionReportAsync(NutritionReportRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/analytics/nutrition/report", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<NutritionReportDto>();
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
            return await _httpClient.GetFromJsonAsync<InventorySummaryDto>("/api/analytics/inventory/summary");
        }
        catch
        {
            return null;
        }
    }

    public async Task<InventoryReportDto?> GetInventoryReportAsync(InventoryReportRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/analytics/inventory/report", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<InventoryReportDto>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<WasteSummaryDto?> GetWasteSummaryAsync()
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            return await _httpClient.GetFromJsonAsync<WasteSummaryDto>("/api/analytics/waste/summary");
        }
        catch
        {
            return null;
        }
    }

    public async Task<WasteReportDto?> GetWasteReportAsync(WasteReportRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/analytics/waste/report", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<WasteReportDto>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<ExportReportResponse?> ExportReportAsync(ExportReportRequest request)
    {
        if (!await EnsureAuthenticatedAsync())
            return null;

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/analytics/export", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ExportReportResponse>();
        }
        catch
        {
            return null;
        }
    }
}
