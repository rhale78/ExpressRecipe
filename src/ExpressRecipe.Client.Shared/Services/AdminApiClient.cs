namespace ExpressRecipe.Client.Shared.Services;

/// <summary>
/// API client for admin operations including database imports
/// </summary>
public interface IAdminApiClient
{
    Task<ImportStatusDto?> ImportUSDADatabaseAsync();
    Task<ImportStatusDto?> ImportFDARecallsAsync();
    Task<ImportStatusDto?> ImportOpenFoodFactsAsync();
    Task<ImportStatusDto?> GetImportStatusAsync(Guid importId);
    Task<List<ImportHistoryDto>> GetImportHistoryAsync();
}

public class AdminApiClient : IAdminApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITokenProvider _tokenProvider;

    public AdminApiClient(IHttpClientFactory httpClientFactory, ITokenProvider tokenProvider)
    {
        _httpClientFactory = httpClientFactory;
        _tokenProvider = tokenProvider;
    }

    public async Task<ImportStatusDto?> ImportUSDADatabaseAsync()
    {
        var client = await CreateClientAsync("ProductService");
        var response = await client.PostAsJsonAsync("/api/admin/import/usda", new { });
        return await response.Content.ReadFromJsonAsync<ImportStatusDto>();
    }

    public async Task<ImportStatusDto?> ImportFDARecallsAsync()
    {
        var client = await CreateClientAsync("RecallService");
        var response = await client.PostAsJsonAsync("/api/admin/import/fda", new { });
        return await response.Content.ReadFromJsonAsync<ImportStatusDto>();
    }

    public async Task<ImportStatusDto?> ImportOpenFoodFactsAsync()
    {
        var client = await CreateClientAsync("ProductService");
        var response = await client.PostAsJsonAsync("/api/admin/import/openfoodfacts", new { });
        return await response.Content.ReadFromJsonAsync<ImportStatusDto>();
    }

    public async Task<ImportStatusDto?> GetImportStatusAsync(Guid importId)
    {
        // Try ProductService first, then RecallService
        try
        {
            var client = await CreateClientAsync("ProductService");
            var response = await client.GetAsync($"/api/admin/import/status/{importId}");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<ImportStatusDto>();
            }
        }
        catch { }

        // Try RecallService
        try
        {
            var client = await CreateClientAsync("RecallService");
            var response = await client.GetAsync($"/api/admin/import/status/{importId}");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<ImportStatusDto>();
            }
        }
        catch { }

        return null;
    }

    public async Task<List<ImportHistoryDto>> GetImportHistoryAsync()
    {
        var history = new List<ImportHistoryDto>();

        // Get history from ProductService
        try
        {
            var client = await CreateClientAsync("ProductService");
            var response = await client.GetAsync("/api/admin/import/history");
            if (response.IsSuccessStatusCode)
            {
                var productHistory = await response.Content.ReadFromJsonAsync<List<ImportHistoryDto>>();
                if (productHistory != null)
                    history.AddRange(productHistory);
            }
        }
        catch { }

        // Get history from RecallService
        try
        {
            var client = await CreateClientAsync("RecallService");
            var response = await client.GetAsync("/api/admin/import/history");
            if (response.IsSuccessStatusCode)
            {
                var recallHistory = await response.Content.ReadFromJsonAsync<List<ImportHistoryDto>>();
                if (recallHistory != null)
                    history.AddRange(recallHistory);
            }
        }
        catch { }

        return history.OrderByDescending(h => h.StartedAt).ToList();
    }

    private async Task<HttpClient> CreateClientAsync(string serviceName)
    {
        var client = _httpClientFactory.CreateClient(serviceName);
        var token = await _tokenProvider.GetTokenAsync();

        if (!string.IsNullOrEmpty(token))
        {
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }
}

public class ImportStatusDto
{
    public Guid ImportId { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending"; // Pending, InProgress, Completed, Failed
    public int TotalRecords { get; set; }
    public int ProcessedRecords { get; set; }
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int ProgressPercentage => TotalRecords > 0 ? (int)((ProcessedRecords / (double)TotalRecords) * 100) : 0;
}

public class ImportHistoryDto
{
    public Guid Id { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int RecordsImported { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? Duration => CompletedAt.HasValue ? CompletedAt.Value - StartedAt : null;
}
