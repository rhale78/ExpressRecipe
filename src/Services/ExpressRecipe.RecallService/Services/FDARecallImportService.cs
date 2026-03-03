using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Data.SqlClient;
using System.Data;

namespace ExpressRecipe.RecallService.Services;

/// <summary>
/// Service for importing food recall data from FDA openFDA API
/// API Documentation: https://open.fda.gov/apis/food/enforcement/
/// </summary>
public class FDARecallImportService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _connectionString;
    private readonly ILogger<FDARecallImportService> _logger;

    public FDARecallImportService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<FDARecallImportService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _connectionString = configuration.GetConnectionString("recalldb")
            ?? throw new InvalidOperationException("Recall database connection not configured");
        _logger = logger;
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

            var httpClient = _httpClientFactory.CreateClient("FDA");

            // Query FDA openFDA API for food enforcement reports
            var url = $"food/enforcement.json?limit={limit}&sort=report_date:desc";
            var response = await httpClient.GetAsync(url);

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

            var rawRecalls = results.EnumerateArray().ToList();
            var batch = new List<RecallData>();

            foreach (var recall in rawRecalls)
            {
                try
                {
                    var dataObj = MapRecallData(recall);
                    if (dataObj != null) batch.Add(dataObj);
                    result.TotalProcessed++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse recall data");
                    result.FailureCount++;
                }
            }

            if (batch.Any())
            {
                var upsertResult = await BulkUpsertRecallsAsync(batch);
                result.SuccessCount = upsertResult;
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

            var httpClient = _httpClientFactory.CreateClient("FDA");

            var url = $"food/enforcement.json?search=product_description:{searchTerm}&limit={limit}";
            var response = await httpClient.GetAsync(url);

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

            // Ensure large text values fit NVARCHAR(MAX)
            var lotVal = (object?)codeInfo ?? DBNull.Value;
            var distVal = (object?)distributionPattern ?? DBNull.Value;
            var lotParam = insertCmd.Parameters.Add("@LotNumber", System.Data.SqlDbType.NVarChar, -1);
            lotParam.Value = lotVal;
            var distParam = insertCmd.Parameters.Add("@DistributionArea", System.Data.SqlDbType.NVarChar, -1);
            distParam.Value = distVal;

            await insertCmd.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// Import USDA recalls
    /// NOTE: USDA FSIS no longer provides a public API or RSS feed for recall data.
    /// Alternative approaches:
    /// 1. Web scraping (requires HTML parsing and may break with website changes)
    /// 2. Use FDA API which includes some meat/poultry recalls
    /// 3. Manual data entry or periodic CSV imports
    /// 
    /// Current implementation: Returns empty result with informational message
    /// </summary>
    public async Task<RecallImportResult> ImportUSDARecallsAsync()
    {
        var result = new RecallImportResult();

        _logger.LogWarning("USDA FSIS recall import is currently unavailable. USDA has discontinued their public API and RSS feed.");
        
        result.ErrorMessage = "USDA FSIS no longer provides a public API or RSS feed for recall data. " +
                             "Alternative data sources may be needed (web scraping, FDA API for meat/poultry, or manual imports).";

        // TODO: Implement alternative data source:
        // Option 1: Web scraping https://www.fsis.usda.gov/recalls (requires consent and may violate ToS)
        // Option 2: FDA openFDA API includes some USDA-regulated products (meat, poultry, egg)
        // Option 3: Subscribe to USDA email alerts and manual data entry
        // Option 4: Check for data.gov datasets: https://catalog.data.gov/dataset?q=usda+recall

        return result;
    }

    /// <summary>
    /// Import meat, poultry, and egg product recalls from FDA API
    /// FDA's enforcement API includes USDA-regulated products
    /// </summary>
    public async Task<RecallImportResult> ImportMeatPoultryRecallsFromFDAAsync(int limit = 50)
    {
        var result = new RecallImportResult();

        try
        {
            _logger.LogInformation("Importing meat/poultry recalls from FDA API");

            var httpClient = _httpClientFactory.CreateClient("FDA");

            // FDA API includes USDA-regulated meat, poultry, and egg products
            // Filter for these product types
            var url = $"food/enforcement.json?search=product_description:(meat OR poultry OR chicken OR beef OR pork OR turkey OR egg)&limit={limit}&sort=report_date:desc";
            var response = await httpClient.GetAsync(url);

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
                    // Process as USDA source if it's meat/poultry related
                    await ProcessMeatPoultryRecallAsync(recall, connection);
                    result.SuccessCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process meat/poultry recall");
                    result.FailureCount++;
                    result.Errors.Add(ex.Message);
                }

                result.TotalProcessed++;
            }

            _logger.LogInformation("Meat/poultry import completed: {Success} successful, {Failed} failed",
                result.SuccessCount, result.FailureCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import meat/poultry recalls from FDA");
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Process meat/poultry recall from FDA API and tag as USDA-relevant
    /// </summary>
    private async Task ProcessMeatPoultryRecallAsync(JsonElement recall, Microsoft.Data.SqlClient.SqlConnection connection)
    {
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
                _logger.LogDebug("Meat/poultry recall {RecallNumber} already exists", recallNumber);
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
            _ => "High" // Default High for meat/poultry
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

        // Insert recall with USDA-MEAT source tag
        using var insertCmd = connection.CreateCommand();
        insertCmd.CommandText = @"
            INSERT INTO Recall (
                ExternalId, Source, Title, Description, Reason, Severity,
                RecallDate, PublishedDate, Status, ImportedAt
            )
            VALUES (
                @ExternalId, @Source, @Title, @Description, @Reason, @Severity,
                @RecallDate, @PublishedDate, @Status, GETUTCDATE()
            )";

        insertCmd.Parameters.AddWithValue("@ExternalId", recallNumber);
        insertCmd.Parameters.AddWithValue("@Source", "USDA-MEAT"); // Tag as USDA-related
        insertCmd.Parameters.AddWithValue("@Title", productDescription.Length > 500
            ? productDescription.Substring(0, 500)
            : productDescription);
        insertCmd.Parameters.AddWithValue("@Description", productDescription);
        insertCmd.Parameters.AddWithValue("@Reason", reason);
        insertCmd.Parameters.AddWithValue("@Severity", severity);
        insertCmd.Parameters.AddWithValue("@RecallDate", recallDate);
        insertCmd.Parameters.AddWithValue("@PublishedDate", publishedDate);
        insertCmd.Parameters.AddWithValue("@Status", status);

        await insertCmd.ExecuteNonQueryAsync();

        _logger.LogInformation("Imported meat/poultry recall {RecallNumber}: {Description}",
            recallNumber, productDescription.Substring(0, Math.Min(100, productDescription.Length)));
    }

    /// <summary>
    /// Process USDA recall from API (DEPRECATED - API no longer available)
    /// </summary>
    [Obsolete("USDA FSIS Public API is no longer available")]
    private async Task ProcessUSDARecallFromApiAsync(JsonElement recall, Microsoft.Data.SqlClient.SqlConnection connection)
    {
        // Extract recall data from API response
        var recallNumber = recall.TryGetProperty("recallNumber", out var rn) ? rn.GetString() : null;
        var companyName = recall.TryGetProperty("companyName", out var cn) ? cn.GetString() : null;
        var productName = recall.TryGetProperty("productName", out var pn) ? pn.GetString() : "";
        var problem = recall.TryGetProperty("problem", out var prob) ? prob.GetString() : null;
        var recallClass = recall.TryGetProperty("recallClass", out var rc) ? rc.GetString() : null;
        var recallDateStr = recall.TryGetProperty("recallDate", out var rd) ? rd.GetString() : null;
        var recallUrl = recall.TryGetProperty("url", out var url) ? url.GetString() : null;

        // Use recallNumber as unique identifier, fallback to combination if not available
        var externalId = recallNumber ?? $"USDA-{companyName}-{recallDateStr}";
        
        if (string.IsNullOrEmpty(externalId)) return;

        // Check if exists
        using (var checkCmd = connection.CreateCommand())
        {
            checkCmd.CommandText = "SELECT COUNT(*) FROM Recall WHERE ExternalId = @ExternalId";
            checkCmd.Parameters.AddWithValue("@ExternalId", externalId);
            var count = (int)await checkCmd.ExecuteScalarAsync()!;

            if (count > 0)
            {
                _logger.LogDebug("USDA recall {RecallNumber} already exists", externalId);
                return;
            }
        }

        // Parse date
        DateTime? recallDate = null;
        if (!string.IsNullOrEmpty(recallDateStr) && DateTime.TryParse(recallDateStr, out var pd))
            recallDate = pd;

        recallDate ??= DateTime.UtcNow;

        // Determine severity from recall class
        var severity = recallClass switch
        {
            "I" or "Class I" => "Critical",
            "II" or "Class II" => "High",
            "III" or "Class III" => "Medium",
            _ => "High" // Default to High for USDA recalls
        };

        // Build title from company and product
        var title = string.IsNullOrEmpty(companyName) 
            ? productName 
            : $"{companyName} - {productName}";

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

        insertCmd.Parameters.AddWithValue("@ExternalId", externalId);
        insertCmd.Parameters.AddWithValue("@Source", "USDA");
        insertCmd.Parameters.AddWithValue("@Title", title.Length > 500 ? title.Substring(0, 500) : title);
        insertCmd.Parameters.AddWithValue("@Description", (object?)productName ?? DBNull.Value);
        insertCmd.Parameters.AddWithValue("@Reason", (object?)problem ?? DBNull.Value);
        insertCmd.Parameters.AddWithValue("@Severity", severity);
        insertCmd.Parameters.AddWithValue("@RecallDate", recallDate);
        insertCmd.Parameters.AddWithValue("@PublishedDate", recallDate);
        insertCmd.Parameters.AddWithValue("@Status", "Active");
        insertCmd.Parameters.AddWithValue("@SourceUrl", (object?)recallUrl ?? DBNull.Value);

        await insertCmd.ExecuteNonQueryAsync();

        _logger.LogInformation("Imported USDA recall: {Title}", title.Substring(0, Math.Min(100, title.Length)));
    }

    /// <summary>
    /// Process USDA recall from RSS feed (legacy method - RSS feed deprecated)
    /// This method is kept for backward compatibility but the RSS feed may no longer be available
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

    private RecallData? MapRecallData(JsonElement recall)
    {
        var recallNumber = recall.TryGetProperty("recall_number", out var rn) ? rn.GetString() : null;
        if (string.IsNullOrEmpty(recallNumber)) return null;

        var classification = recall.TryGetProperty("classification", out var c) ? c.GetString() : "Unknown";
        var status = recall.TryGetProperty("status", out var s) ? s.GetString() : "Active";
        var productDescription = recall.TryGetProperty("product_description", out var pd) ? pd.GetString() : "";
        var reason = recall.TryGetProperty("reason_for_recall", out var r) ? r.GetString() : "";
        var recallInitDate = recall.TryGetProperty("recall_initiation_date", out var rid) ? rid.GetString() : null;
        var reportDate = recall.TryGetProperty("report_date", out var rpd) ? rpd.GetString() : null;
        var codeInfo = recall.TryGetProperty("code_info", out var ci) ? ci.GetString() : null;
        var distributionPattern = recall.TryGetProperty("distribution_pattern", out var dp) ? dp.GetString() : null;

        var severity = classification switch
        {
            "Class I" => "Critical",
            "Class II" => "High",
            "Class III" => "Medium",
            _ => "Low"
        };

        DateTime? recallDate = null;
        DateTime? publishedDate = null;
        if (!string.IsNullOrEmpty(recallInitDate) && DateTime.TryParse(recallInitDate, out var rd)) recallDate = rd;
        if (!string.IsNullOrEmpty(reportDate) && DateTime.TryParse(reportDate, out var pd2)) publishedDate = pd2;
        publishedDate ??= recallDate ?? DateTime.UtcNow;
        recallDate ??= publishedDate.Value;

        var data = new RecallData
        {
            ExternalId = recallNumber,
            Source = "FDA",
            Title = productDescription.Length > 500 ? productDescription.Substring(0, 500) : productDescription,
            Description = productDescription,
            Reason = reason,
            Severity = severity,
            RecallDate = recallDate.Value,
            PublishedDate = publishedDate.Value,
            Status = status
        };

        if (!string.IsNullOrEmpty(productDescription))
        {
            var productNames = productDescription.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => p.Length > 3)
                .Take(10);

            foreach (var p in productNames)
            {
                var parts = p.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var brand = parts.Length > 0 && parts[0].All(char.IsUpper) && parts[0].Length > 2 ? parts[0] : null;
                data.Products.Add(new RecallProductData { ProductName = p, Brand = brand, LotNumber = codeInfo, DistributionArea = distributionPattern });
            }
        }

        return data;
    }

    private async Task<int> BulkUpsertRecallsAsync(List<RecallData> batch)
    {
        using var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            // 1. Create temp table for Recalls
            const string createRecallTemp = @"
                CREATE TABLE #TempRecall (
                    ExternalId NVARCHAR(100), Source NVARCHAR(100), Title NVARCHAR(500), 
                    Description NVARCHAR(MAX), Reason NVARCHAR(MAX), Severity NVARCHAR(50),
                    RecallDate DATETIME2, PublishedDate DATETIME2, Status NVARCHAR(50)
                )";
            using (var cmd = new SqlCommand(createRecallTemp, connection, transaction)) await cmd.ExecuteNonQueryAsync();

            var dt = new DataTable();
            dt.Columns.Add("ExternalId", typeof(string)); dt.Columns.Add("Source", typeof(string));
            dt.Columns.Add("Title", typeof(string)); dt.Columns.Add("Description", typeof(string));
            dt.Columns.Add("Reason", typeof(string)); dt.Columns.Add("Severity", typeof(string));
            dt.Columns.Add("RecallDate", typeof(DateTime)); dt.Columns.Add("PublishedDate", typeof(DateTime));
            dt.Columns.Add("Status", typeof(string));

            foreach (var r in batch) dt.Rows.Add(r.ExternalId, r.Source, r.Title, r.Description, r.Reason, r.Severity, r.RecallDate, r.PublishedDate, r.Status);

            using (var bulkCopy = new Microsoft.Data.SqlClient.SqlBulkCopy(connection, Microsoft.Data.SqlClient.SqlBulkCopyOptions.Default, transaction))
            {
                bulkCopy.DestinationTableName = "#TempRecall";
                await bulkCopy.WriteToServerAsync(dt);
            }

            // 2. MERGE into Recall table and capture IDs
            const string mergeSql = @"
                MERGE Recall AS target
                USING #TempRecall AS source
                ON (target.ExternalId = source.ExternalId AND target.Source = source.Source)
                WHEN NOT MATCHED THEN
                    INSERT (Id, ExternalId, Source, Title, Description, Reason, Severity, RecallDate, PublishedDate, Status, ImportedAt)
                    VALUES (NEWID(), source.ExternalId, source.Source, source.Title, source.Description, source.Reason, source.Severity, source.RecallDate, source.PublishedDate, source.Status, GETUTCDATE())
                OUTPUT INSERTED.Id, source.ExternalId INTO @OutputTable (Id, ExternalId);";

            using (var cmd = new SqlCommand($"DECLARE @OutputTable TABLE (Id UNIQUEIDENTIFIER, ExternalId NVARCHAR(100)); {mergeSql} SELECT Id, ExternalId FROM @OutputTable", connection, transaction))
            {
                var idMap = new Dictionary<string, Guid>();
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync()) idMap[reader.GetString(1)] = reader.GetGuid(0);
                reader.Close();

                // 3. Bulk insert products
                var productDt = new DataTable();
                productDt.Columns.Add("RecallId", typeof(Guid));
                productDt.Columns.Add("ProductName", typeof(string));
                productDt.Columns.Add("Brand", typeof(string));
                productDt.Columns.Add("LotNumber", typeof(string));
                productDt.Columns.Add("DistributionArea", typeof(string));

                foreach (var r in batch)
                {
                    if (idMap.TryGetValue(r.ExternalId, out var recallId))
                    {
                        foreach (var p in r.Products)
                            productDt.Rows.Add(recallId, p.ProductName.Length > 300 ? p.ProductName.Substring(0, 300) : p.ProductName, p.Brand, p.LotNumber, p.DistributionArea);
                    }
                }

                if (productDt.Rows.Count > 0)
                {
                    using var bulkCopy = new Microsoft.Data.SqlClient.SqlBulkCopy(connection, Microsoft.Data.SqlClient.SqlBulkCopyOptions.Default, transaction);
                    bulkCopy.DestinationTableName = "RecallProduct";
                    bulkCopy.BatchSize = 5000;

                    // IMPORTANT: map by column name to avoid ordinal mismatch with destination schema
                    // (RecallProduct likely has an identity/Id column at ordinal 0)
                    bulkCopy.ColumnMappings.Add("RecallId", "RecallId");
                    bulkCopy.ColumnMappings.Add("ProductName", "ProductName");
                    bulkCopy.ColumnMappings.Add("Brand", "Brand");
                    bulkCopy.ColumnMappings.Add("LotNumber", "LotNumber");
                    bulkCopy.ColumnMappings.Add("DistributionArea", "DistributionArea");

                    await bulkCopy.WriteToServerAsync(productDt);
                }
            }

            await transaction.CommitAsync();
            return batch.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed bulk upsert of recalls");
            await transaction.RollbackAsync();
            throw;
        }
    }

    private class RecallData
    {
        public string ExternalId { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Reason { get; set; }
        public string Severity { get; set; } = "Low";
        public DateTime RecallDate { get; set; }
        public DateTime PublishedDate { get; set; }
        public string Status { get; set; } = "Active";
        public List<RecallProductData> Products { get; set; } = new();
    }

    private class RecallProductData
    {
        public string ProductName { get; set; } = string.Empty;
        public string? Brand { get; set; }
        public string? LotNumber { get; set; }
        public string? DistributionArea { get; set; }
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
