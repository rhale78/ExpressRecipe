using System.Text.Json;
using System.Xml.Linq;

namespace ExpressRecipe.RecallService.Services;

/// <summary>
/// Service for importing food recall data from FDA openFDA API
/// API Documentation: https://open.fda.gov/apis/food/enforcement/
/// </summary>
public class FDARecallImportService
{
    private readonly HttpClient _httpClient;
    private readonly string _connectionString;
    private readonly ILogger<FDARecallImportService> _logger;

    public FDARecallImportService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<FDARecallImportService> logger)
    {
        _httpClient = httpClient;
        _connectionString = configuration.GetConnectionString("recalldb")
            ?? throw new InvalidOperationException("Recall database connection not configured");
        _logger = logger;

        _httpClient.BaseAddress = new Uri("https://api.fda.gov/");
    }

    /// <summary>
    /// Import recent FDA food recalls
    /// </summary>
    public async Task<RecallImportResult> ImportRecentRecallsAsync(int limit = 100)
    {
        var result = new RecallImportResult();

        try
        {
            _logger.LogInformation("Importing {Limit} recent FDA food recalls", limit);

            // Query FDA openFDA API for food enforcement reports
            var url = $"food/enforcement.json?limit={limit}&sort=report_date:desc";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                result.ErrorMessage = $"FDA API returned {response.StatusCode}";
                return result;
            }

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonDocument.Parse(json);

            if (!data.RootElement.TryGetProperty("results", out var results))
            {
                result.ErrorMessage = "No results found in FDA response";
                return result;
            }

            using var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString);
            await connection.OpenAsync();

            foreach (var recall in results.EnumerateArray())
            {
                try
                {
                    await ProcessRecallAsync(recall, connection);
                    result.SuccessCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process recall");
                    result.FailureCount++;
                    result.Errors.Add(ex.Message);
                }

                result.TotalProcessed++;
            }

            _logger.LogInformation("Import completed: {Success} successful, {Failed} failed",
                result.SuccessCount, result.FailureCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import FDA recalls");
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Search and import recalls by keyword
    /// </summary>
    public async Task<RecallImportResult> SearchAndImportRecallsAsync(string searchTerm, int limit = 50)
    {
        var result = new RecallImportResult();

        try
        {
            _logger.LogInformation("Searching FDA recalls for: {SearchTerm}", searchTerm);

            var url = $"food/enforcement.json?search=product_description:{searchTerm}&limit={limit}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                result.ErrorMessage = $"FDA API returned {response.StatusCode}";
                return result;
            }

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonDocument.Parse(json);

            if (!data.RootElement.TryGetProperty("results", out var results))
            {
                result.ErrorMessage = "No results found for search term";
                return result;
            }

            using var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString);
            await connection.OpenAsync();

            foreach (var recall in results.EnumerateArray())
            {
                try
                {
                    await ProcessRecallAsync(recall, connection);
                    result.SuccessCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process recall");
                    result.FailureCount++;
                    result.Errors.Add(ex.Message);
                }

                result.TotalProcessed++;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search FDA recalls");
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Process and save a single recall to database
    /// </summary>
    private async Task ProcessRecallAsync(JsonElement recall, Microsoft.Data.SqlClient.SqlConnection connection)
    {
        // Extract recall data
        var recallNumber = recall.TryGetProperty("recall_number", out var rn) ? rn.GetString() : null;
        if (string.IsNullOrEmpty(recallNumber)) return;

        // Check if recall already exists
        using (var checkCmd = connection.CreateCommand())
        {
            checkCmd.CommandText = "SELECT COUNT(*) FROM Recall WHERE ExternalId = @ExternalId";
            checkCmd.Parameters.AddWithValue("@ExternalId", recallNumber);
            var count = (int)await checkCmd.ExecuteScalarAsync()!;

            if (count > 0)
            {
                _logger.LogDebug("Recall {RecallNumber} already exists", recallNumber);
                return;
            }
        }

        // Extract data
        var classification = recall.TryGetProperty("classification", out var c) ? c.GetString() : "Unknown";
        var status = recall.TryGetProperty("status", out var s) ? s.GetString() : "Active";
        var productDescription = recall.TryGetProperty("product_description", out var pd) ? pd.GetString() : "";
        var reason = recall.TryGetProperty("reason_for_recall", out var r) ? r.GetString() : "";
        var recallInitDate = recall.TryGetProperty("recall_initiation_date", out var rid) ? rid.GetString() : null;
        var reportDate = recall.TryGetProperty("report_date", out var rpd) ? rpd.GetString() : null;

        // Determine severity
        var severity = classification switch
        {
            "Class I" => "Critical",
            "Class II" => "High",
            "Class III" => "Medium",
            _ => "Low"
        };

        // Parse dates
        DateTime? recallDate = null;
        DateTime? publishedDate = null;

        if (!string.IsNullOrEmpty(recallInitDate) && DateTime.TryParse(recallInitDate, out var rd))
            recallDate = rd;

        if (!string.IsNullOrEmpty(reportDate) && DateTime.TryParse(reportDate, out var pd2))
            publishedDate = pd2;

        publishedDate ??= recallDate ?? DateTime.UtcNow;
        recallDate ??= publishedDate.Value;

        // Insert recall
        Guid recallId;
        using (var insertCmd = connection.CreateCommand())
        {
            insertCmd.CommandText = @"
                INSERT INTO Recall (
                    ExternalId, Source, Title, Description, Reason, Severity,
                    RecallDate, PublishedDate, Status, ImportedAt
                )
                OUTPUT INSERTED.Id
                VALUES (
                    @ExternalId, @Source, @Title, @Description, @Reason, @Severity,
                    @RecallDate, @PublishedDate, @Status, GETUTCDATE()
                )";

            insertCmd.Parameters.AddWithValue("@ExternalId", recallNumber);
            insertCmd.Parameters.AddWithValue("@Source", "FDA");
            insertCmd.Parameters.AddWithValue("@Title", productDescription.Length > 500
                ? productDescription.Substring(0, 500)
                : productDescription);
            insertCmd.Parameters.AddWithValue("@Description", productDescription);
            insertCmd.Parameters.AddWithValue("@Reason", reason);
            insertCmd.Parameters.AddWithValue("@Severity", severity);
            insertCmd.Parameters.AddWithValue("@RecallDate", recallDate);
            insertCmd.Parameters.AddWithValue("@PublishedDate", publishedDate);
            insertCmd.Parameters.AddWithValue("@Status", status);

            recallId = (Guid)await insertCmd.ExecuteScalarAsync()!;
        }

        // Extract product information and create RecallProduct records
        await ExtractAndSaveProductsAsync(recall, recallId, connection);

        _logger.LogInformation("Imported recall {RecallNumber}: {Description}",
            recallNumber, productDescription.Substring(0, Math.Min(100, productDescription.Length)));
    }

    /// <summary>
    /// Extract product information from recall and save to database
    /// </summary>
    private async Task ExtractAndSaveProductsAsync(JsonElement recall, Guid recallId, Microsoft.Data.SqlClient.SqlConnection connection)
    {
        var productDescription = recall.TryGetProperty("product_description", out var pd) ? pd.GetString() : "";
        var codeInfo = recall.TryGetProperty("code_info", out var ci) ? ci.GetString() : null;
        var distributionPattern = recall.TryGetProperty("distribution_pattern", out var dp) ? dp.GetString() : null;

        if (string.IsNullOrEmpty(productDescription)) return;

        // Parse product description - often contains multiple products separated by semicolons or commas
        var products = productDescription.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length > 3)
            .Take(10); // Limit to 10 products per recall

        foreach (var product in products)
        {
            // Extract brand if present (usually in ALL CAPS or at start)
            var parts = product.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var brand = parts.Length > 0 && parts[0].All(char.IsUpper) && parts[0].Length > 2
                ? parts[0]
                : null;

            using var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = @"
                INSERT INTO RecallProduct (
                    RecallId, ProductName, Brand, LotNumber, DistributionArea
                )
                VALUES (
                    @RecallId, @ProductName, @Brand, @LotNumber, @DistributionArea
                )";

            insertCmd.Parameters.AddWithValue("@RecallId", recallId);
            insertCmd.Parameters.AddWithValue("@ProductName", product.Length > 300
                ? product.Substring(0, 300)
                : product);
            insertCmd.Parameters.AddWithValue("@Brand", (object?)brand ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@LotNumber", (object?)codeInfo ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@DistributionArea", (object?)distributionPattern ?? DBNull.Value);

            await insertCmd.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// Import USDA recalls (separate from FDA)
    /// USDA FSIS Recalls: https://www.fsis.usda.gov/recalls
    /// </summary>
    public async Task<RecallImportResult> ImportUSDARecallsAsync()
    {
        var result = new RecallImportResult();

        try
        {
            _logger.LogInformation("Importing USDA FSIS recalls");

            // USDA provides RSS feed for recalls
            var response = await _httpClient.GetAsync("https://www.fsis.usda.gov/rss/fsis-recalls.xml");

            if (!response.IsSuccessStatusCode)
            {
                result.ErrorMessage = $"USDA RSS feed returned {response.StatusCode}";
                return result;
            }

            var xml = await response.Content.ReadAsStringAsync();
            var doc = XDocument.Parse(xml);

            using var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString);
            await connection.OpenAsync();

            foreach (var item in doc.Descendants("item"))
            {
                try
                {
                    await ProcessUSDARecallAsync(item, connection);
                    result.SuccessCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process USDA recall");
                    result.FailureCount++;
                    result.Errors.Add(ex.Message);
                }

                result.TotalProcessed++;
            }

            _logger.LogInformation("USDA import completed: {Success} successful, {Failed} failed",
                result.SuccessCount, result.FailureCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import USDA recalls");
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Process USDA recall from RSS feed
    /// </summary>
    private async Task ProcessUSDARecallAsync(XElement item, Microsoft.Data.SqlClient.SqlConnection connection)
    {
        var title = item.Element("title")?.Value;
        var link = item.Element("link")?.Value;
        var description = item.Element("description")?.Value;
        var pubDate = item.Element("pubDate")?.Value;
        var guid = item.Element("guid")?.Value;

        if (string.IsNullOrEmpty(guid) || string.IsNullOrEmpty(title)) return;

        // Check if exists
        using (var checkCmd = connection.CreateCommand())
        {
            checkCmd.CommandText = "SELECT COUNT(*) FROM Recall WHERE ExternalId = @ExternalId";
            checkCmd.Parameters.AddWithValue("@ExternalId", guid);
            var count = (int)await checkCmd.ExecuteScalarAsync()!;

            if (count > 0) return;
        }

        // Parse date
        DateTime? publishedDate = null;
        if (!string.IsNullOrEmpty(pubDate) && DateTime.TryParse(pubDate, out var pd))
            publishedDate = pd;

        publishedDate ??= DateTime.UtcNow;

        // Insert recall
        using var insertCmd = connection.CreateCommand();
        insertCmd.CommandText = @"
            INSERT INTO Recall (
                ExternalId, Source, Title, Description, Reason, Severity,
                RecallDate, PublishedDate, Status, SourceUrl, ImportedAt
            )
            VALUES (
                @ExternalId, @Source, @Title, @Description, @Reason, @Severity,
                @RecallDate, @PublishedDate, @Status, @SourceUrl, GETUTCDATE()
            )";

        insertCmd.Parameters.AddWithValue("@ExternalId", guid);
        insertCmd.Parameters.AddWithValue("@Source", "USDA");
        insertCmd.Parameters.AddWithValue("@Title", title.Length > 500 ? title.Substring(0, 500) : title);
        insertCmd.Parameters.AddWithValue("@Description", (object?)description ?? DBNull.Value);
        insertCmd.Parameters.AddWithValue("@Reason", DBNull.Value);
        insertCmd.Parameters.AddWithValue("@Severity", "High"); // USDA recalls are typically serious
        insertCmd.Parameters.AddWithValue("@RecallDate", publishedDate);
        insertCmd.Parameters.AddWithValue("@PublishedDate", publishedDate);
        insertCmd.Parameters.AddWithValue("@Status", "Active");
        insertCmd.Parameters.AddWithValue("@SourceUrl", (object?)link ?? DBNull.Value);

        await insertCmd.ExecuteNonQueryAsync();

        _logger.LogInformation("Imported USDA recall: {Title}", title.Substring(0, Math.Min(100, title.Length)));
    }
}

/// <summary>
/// Result of recall import operation
/// </summary>
public class RecallImportResult
{
    public int TotalProcessed { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public List<string> Errors { get; set; } = new();
    public string? ErrorMessage { get; set; }
}
