using System.Text.Json;
using System.Xml.Linq;

using Microsoft.Data.SqlClient;

namespace ExpressRecipe.RecallService.Services
{
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
            RecallImportResult result = new RecallImportResult();

            try
            {
                _logger.LogInformation("Importing {Limit} recent FDA food recalls", limit);

                HttpClient httpClient = _httpClientFactory.CreateClient("FDA");

                // Query FDA openFDA API for food enforcement reports
                var url = $"food/enforcement.json?limit={limit}&sort=report_date:desc";
                HttpResponseMessage response = await httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    result.ErrorMessage = $"FDA API returned {response.StatusCode}";
                    return result;
                }

                var json = await response.Content.ReadAsStringAsync();
                JsonDocument data = JsonDocument.Parse(json);

                if (!data.RootElement.TryGetProperty("results", out JsonElement results))
                {
                    result.ErrorMessage = "No results found in FDA response";
                    return result;
                }

                using SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString);
                await connection.OpenAsync();

                foreach (JsonElement recall in results.EnumerateArray())
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
            RecallImportResult result = new RecallImportResult();

            try
            {
                _logger.LogInformation("Searching FDA recalls for: {SearchTerm}", searchTerm);

                HttpClient httpClient = _httpClientFactory.CreateClient("FDA");

                var url = $"food/enforcement.json?search=product_description:{searchTerm}&limit={limit}";
                HttpResponseMessage response = await httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    result.ErrorMessage = $"FDA API returned {response.StatusCode}";
                    return result;
                }

                var json = await response.Content.ReadAsStringAsync();
                JsonDocument data = JsonDocument.Parse(json);

                if (!data.RootElement.TryGetProperty("results", out JsonElement results))
                {
                    result.ErrorMessage = "No results found for search term";
                    return result;
                }

                using SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString);
                await connection.OpenAsync();

                foreach (JsonElement recall in results.EnumerateArray())
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
            var recallNumber = recall.TryGetProperty("recall_number", out JsonElement rn) ? rn.GetString() : null;
            if (string.IsNullOrEmpty(recallNumber))
            {
                return;
            }

            // Check if recall already exists
            using (SqlCommand checkCmd = connection.CreateCommand())
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
            var classification = recall.TryGetProperty("classification", out JsonElement c) ? c.GetString() : "Unknown";
            var status = recall.TryGetProperty("status", out JsonElement s) ? s.GetString() : "Active";
            var productDescription = recall.TryGetProperty("product_description", out JsonElement pd) ? pd.GetString() : "";
            var reason = recall.TryGetProperty("reason_for_recall", out JsonElement r) ? r.GetString() : "";
            var recallInitDate = recall.TryGetProperty("recall_initiation_date", out JsonElement rid) ? rid.GetString() : null;
            var reportDate = recall.TryGetProperty("report_date", out JsonElement rpd) ? rpd.GetString() : null;

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

            if (!string.IsNullOrEmpty(recallInitDate) && DateTime.TryParse(recallInitDate, out DateTime rd))
            {
                recallDate = rd;
            }

            if (!string.IsNullOrEmpty(reportDate) && DateTime.TryParse(reportDate, out DateTime pd2))
            {
                publishedDate = pd2;
            }

            publishedDate ??= recallDate ?? DateTime.UtcNow;
            recallDate ??= publishedDate.Value;

            // Insert recall
            Guid recallId;
            using (SqlCommand insertCmd = connection.CreateCommand())
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
            var productDescription = recall.TryGetProperty("product_description", out JsonElement pd) ? pd.GetString() : "";
            var codeInfo = recall.TryGetProperty("code_info", out JsonElement ci) ? ci.GetString() : null;
            var distributionPattern = recall.TryGetProperty("distribution_pattern", out JsonElement dp) ? dp.GetString() : null;

            if (string.IsNullOrEmpty(productDescription))
            {
                return;
            }

            // Parse product description - often contains multiple products separated by semicolons or commas
            IEnumerable<string> products = productDescription.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
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

                using SqlCommand insertCmd = connection.CreateCommand();
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
                SqlParameter lotParam = insertCmd.Parameters.Add("@LotNumber", System.Data.SqlDbType.NVarChar, -1);
                lotParam.Value = lotVal;
                SqlParameter distParam = insertCmd.Parameters.Add("@DistributionArea", System.Data.SqlDbType.NVarChar, -1);
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
            RecallImportResult result = new RecallImportResult();

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
            RecallImportResult result = new RecallImportResult();

            try
            {
                _logger.LogInformation("Importing meat/poultry recalls from FDA API");

                HttpClient httpClient = _httpClientFactory.CreateClient("FDA");

                // FDA API includes USDA-regulated meat, poultry, and egg products
                // Filter for these product types
                var url = $"food/enforcement.json?search=product_description:(meat OR poultry OR chicken OR beef OR pork OR turkey OR egg)&limit={limit}&sort=report_date:desc";
                HttpResponseMessage response = await httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    result.ErrorMessage = $"FDA API returned {response.StatusCode}";
                    return result;
                }

                var json = await response.Content.ReadAsStringAsync();
                JsonDocument data = JsonDocument.Parse(json);

                if (!data.RootElement.TryGetProperty("results", out JsonElement results))
                {
                    result.ErrorMessage = "No results found in FDA response";
                    return result;
                }

                using SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString);
                await connection.OpenAsync();

                foreach (JsonElement recall in results.EnumerateArray())
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
            var recallNumber = recall.TryGetProperty("recall_number", out JsonElement rn) ? rn.GetString() : null;
            if (string.IsNullOrEmpty(recallNumber))
            {
                return;
            }

            // Check if recall already exists
            using (SqlCommand checkCmd = connection.CreateCommand())
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
            var classification = recall.TryGetProperty("classification", out JsonElement c) ? c.GetString() : "Unknown";
            var status = recall.TryGetProperty("status", out JsonElement s) ? s.GetString() : "Active";
            var productDescription = recall.TryGetProperty("product_description", out JsonElement pd) ? pd.GetString() : "";
            var reason = recall.TryGetProperty("reason_for_recall", out JsonElement r) ? r.GetString() : "";
            var recallInitDate = recall.TryGetProperty("recall_initiation_date", out JsonElement rid) ? rid.GetString() : null;
            var reportDate = recall.TryGetProperty("report_date", out JsonElement rpd) ? rpd.GetString() : null;

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

            if (!string.IsNullOrEmpty(recallInitDate) && DateTime.TryParse(recallInitDate, out DateTime rd))
            {
                recallDate = rd;
            }

            if (!string.IsNullOrEmpty(reportDate) && DateTime.TryParse(reportDate, out DateTime pd2))
            {
                publishedDate = pd2;
            }

            publishedDate ??= recallDate ?? DateTime.UtcNow;
            recallDate ??= publishedDate.Value;

            // Insert recall with USDA-MEAT source tag
            using SqlCommand insertCmd = connection.CreateCommand();
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
            var recallNumber = recall.TryGetProperty("recallNumber", out JsonElement rn) ? rn.GetString() : null;
            var companyName = recall.TryGetProperty("companyName", out JsonElement cn) ? cn.GetString() : null;
            var productName = recall.TryGetProperty("productName", out JsonElement pn) ? pn.GetString() : "";
            var problem = recall.TryGetProperty("problem", out JsonElement prob) ? prob.GetString() : null;
            var recallClass = recall.TryGetProperty("recallClass", out JsonElement rc) ? rc.GetString() : null;
            var recallDateStr = recall.TryGetProperty("recallDate", out JsonElement rd) ? rd.GetString() : null;
            var recallUrl = recall.TryGetProperty("url", out JsonElement url) ? url.GetString() : null;

            // Use recallNumber as unique identifier, fallback to combination if not available
            var externalId = recallNumber ?? $"USDA-{companyName}-{recallDateStr}";
        
            if (string.IsNullOrEmpty(externalId))
            {
                return;
            }

            // Check if exists
            using (SqlCommand checkCmd = connection.CreateCommand())
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
            if (!string.IsNullOrEmpty(recallDateStr) && DateTime.TryParse(recallDateStr, out DateTime pd))
            {
                recallDate = pd;
            }

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
            using SqlCommand insertCmd = connection.CreateCommand();
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

            if (string.IsNullOrEmpty(guid) || string.IsNullOrEmpty(title))
            {
                return;
            }

            // Check if exists
            using (SqlCommand checkCmd = connection.CreateCommand())
            {
                checkCmd.CommandText = "SELECT COUNT(*) FROM Recall WHERE ExternalId = @ExternalId";
                checkCmd.Parameters.AddWithValue("@ExternalId", guid);
                var count = (int)await checkCmd.ExecuteScalarAsync()!;

                if (count > 0)
                {
                    return;
                }
            }

            // Parse date
            DateTime? publishedDate = null;
            if (!string.IsNullOrEmpty(pubDate) && DateTime.TryParse(pubDate, out DateTime pd))
            {
                publishedDate = pd;
            }

            publishedDate ??= DateTime.UtcNow;

            // Insert recall
            using SqlCommand insertCmd = connection.CreateCommand();
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
        public List<string> Errors { get; set; } = [];
        public string? ErrorMessage { get; set; }
    }
}
