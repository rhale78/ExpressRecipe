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

public class AdminApiClient : ApiClientBase, IAdminApiClient
{
    public AdminApiClient(HttpClient httpClient, ITokenProvider tokenProvider)
        : base(httpClient, tokenProvider)
    {
    }

    public async Task<ImportStatusDto?> ImportUSDADatabaseAsync()
    {
        return await PostAsync<object, ImportStatusDto>("/api/admin/import/usda", new { });
    }

    public async Task<ImportStatusDto?> ImportFDARecallsAsync()
    {
        return await PostAsync<object, ImportStatusDto>("/api/admin/import/fda", new { });
    }

    public async Task<ImportStatusDto?> ImportOpenFoodFactsAsync()
    {
        return await PostAsync<object, ImportStatusDto>("/api/admin/import/openfoodfacts", new { });
    }

    public async Task<ImportStatusDto?> GetImportStatusAsync(Guid importId)
    {
        return await GetAsync<ImportStatusDto>($"/api/admin/import/status/{importId}");
    }

    public async Task<List<ImportHistoryDto>> GetImportHistoryAsync()
    {
        return await GetAsync<List<ImportHistoryDto>>("/api/admin/import/history") ?? new List<ImportHistoryDto>();
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
