using System.Text.Json;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using ExpressRecipe.ProductService.Data;
using ExpressRecipe.Shared.DTOs.Product;
using ExpressRecipe.Client.Shared.Services;

namespace ExpressRecipe.ProductService.Services;

/// <summary>
/// Service for importing product data from OpenFoodFacts API
/// API Documentation: https://world.openfoodfacts.org/data
/// Largest open food products database with 2M+ products
/// </summary>
public class OpenFoodFactsImportService
{
    private readonly HttpClient _httpClient;
    private readonly IProductRepository _productRepository;
    private readonly IProductStagingRepository _stagingRepository;
    private readonly IProductImageRepository _productImageRepository;
    private readonly ILogger<OpenFoodFactsImportService> _logger;
    private readonly IIngredientServiceClient _ingredientClient;
    private readonly IConfiguration _configuration;

    public OpenFoodFactsImportService(
        HttpClient httpClient,
        IProductRepository productRepository,
        IProductStagingRepository stagingRepository,
        IProductImageRepository productImageRepository,
        ILogger<OpenFoodFactsImportService> logger,
        IIngredientServiceClient ingredientClient,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _productRepository = productRepository;
        _stagingRepository = stagingRepository;
        _productImageRepository = productImageRepository;
        _logger = logger;
        _ingredientClient = ingredientClient;
        _configuration = configuration;

        _httpClient.BaseAddress = new Uri("https://world.openfoodfacts.org/");
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "ExpressRecipe/1.0 (Dietary Management Platform)");
    }

    public async Task<ImportResult> ImportProductByBarcodeAsync(string barcode)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/v2/product/{barcode}.json");
            if (!response.IsSuccessStatusCode) return new ImportResult { Success = false, ErrorMessage = $"API error: {response.StatusCode}" };

            var json = await response.Content.ReadAsStringAsync();
            var product = JsonDocument.Parse(json).RootElement.GetProperty("product");
            return await ProcessProductAsync(product, barcode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import {Barcode}", barcode);
            return new ImportResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<BatchImportResult> SearchAndImportAsync(string query, int pageSize = 50, int maxResults = 200)
    {
        // Proxy to USDA or implemented if needed
        return new BatchImportResult { TotalProcessed = 0 };
    }

    public async Task<BatchImportResult> ImportDeltaUpdatesAsync(int days = 14, int maxProducts = 5000, CancellationToken cancellationToken = default)
    {
        var result = new BatchImportResult();
        var cutoffTimestamp = DateTimeOffset.UtcNow.AddDays(-days).ToUnixTimeSeconds();

        _logger.LogInformation("Starting delta update: products modified in last {Days} days (cutoff: {Cutoff:u})",
            days, DateTimeOffset.FromUnixTimeSeconds(cutoffTimestamp));

        const int pageSize = 200;
        const int flushSize = 5000;
        const string fields = "code,product_name,generic_name,brands,categories,categories_hierarchy," +
                              "allergens,allergens_hierarchy,ingredients_text,ingredients_text_en," +
                              "image_url,image_front_url,image_small_url,image_front_small_url," +
                              "nutriscore_grade,nova_group,ecoscore_grade,lang,countries," +
                              "last_modified_t,selected_images";

        int page = 1;
        int processedCount = 0;
        bool reachedCutoff = false;
        var stagingBatch = new List<StagedProduct>();

        while (!reachedCutoff && processedCount < maxProducts && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                var url = $"api/v2/search?fields={fields}&sort_by=last_modified_t&page_size={pageSize}&page={page}";

                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Delta update API failed on page {Page}: {StatusCode}", page, response.StatusCode);
                    break;
                }

                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

                if (!doc.RootElement.TryGetProperty("products", out var products) || products.GetArrayLength() == 0)
                    break;

                foreach (var product in products.EnumerateArray())
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    // Stop paging once we've gone past the lookback window
                    if (product.TryGetProperty("last_modified_t", out var lastModifiedEl) &&
                        lastModifiedEl.ValueKind == JsonValueKind.Number &&
                        lastModifiedEl.GetInt64() < cutoffTimestamp)
                    {
                        reachedCutoff = true;
                        break;
                    }

                    var barcode = GetStringValue(product, "code");
                    var productName = GetStringValue(product, "product_name", "product_name_en");

                    if (string.IsNullOrWhiteSpace(barcode) || string.IsNullOrWhiteSpace(productName))
                        continue;

                    var lang = GetStringValue(product, "lang", "lc");
                    if (!string.IsNullOrWhiteSpace(lang) && !lang.StartsWith("en", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var brand = GetStringValue(product, "brands");
                    var ingredientsEn = GetStringValue(product, "ingredients_text_en")
                                        ?? GetStringValue(product, "ingredients_text");
                    if (!LanguageDetector.ShouldImportProduct(productName, brand ?? "", ingredientsEn ?? ""))
                        continue;

                    var categories = GetStringValue(product, "categories")?.ToLower() ?? "";
                    if (categories.Contains("beauty") || categories.Contains("pet-food") ||
                        categories.Contains("cosmetic") || productName.Contains("shampoo", StringComparison.OrdinalIgnoreCase) ||
                        productName.Contains("lotion", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var countries = GetStringValue(product, "countries");
                    if (!IsUsProduct(countries))
                        continue;

                    stagingBatch.Add(MapToStagedProduct(product, barcode));
                    processedCount++;
                }

                if (stagingBatch.Count >= flushSize)
                {
                    var inserted = await _stagingRepository.BulkInsertStagingProductsAsync(stagingBatch).ConfigureAwait(false);
                    result.SuccessCount += inserted;
                    result.FailureCount += stagingBatch.Count - inserted;
                    result.TotalProcessed = processedCount;
                    stagingBatch.Clear();
                    _logger.LogInformation("[Delta] Progress: {Count} products staged so far", processedCount);

                    // Inter-item delay to prevent overwhelming CPU/disk (configured via ProductImport:BatchDelayMs)
                    var batchDelayMs = _configuration.GetValue<int>("ProductImport:BatchDelayMs", 0);
                    if (batchDelayMs > 0)
                        await Task.Delay(batchDelayMs, cancellationToken).ConfigureAwait(false);
                }

                page++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during delta update page {Page}", page);
                result.Errors.Add($"Page {page}: {ex.Message}");
                break;
            }
        }

        if (stagingBatch.Count > 0)
        {
            var inserted = await _stagingRepository.BulkInsertStagingProductsAsync(stagingBatch).ConfigureAwait(false);
            result.SuccessCount += inserted;
            result.FailureCount += stagingBatch.Count - inserted;
        }

        result.TotalProcessed = processedCount;
        _logger.LogInformation("Delta update completed: {Success} staged, {Failed} failed, {Pages} pages fetched (cutoff reached: {CutoffReached})",
            result.SuccessCount, result.FailureCount, page - 1, reachedCutoff);

        return result;
    }

    public async Task<BatchImportResult> ImportFromBulkDataAsync(
        string? dataFileUrl = null,
        int maxProducts = 10000,
        CancellationToken cancellationToken = default,
        IProgress<ImportProgress>? progress = null)
    {
        var result = new BatchImportResult();
        dataFileUrl ??= "https://static.openfoodfacts.org/data/openfoodfacts-products.jsonl.gz";

        try
        {
            _logger.LogInformation("Starting JSONL bulk import from {Url} (streaming directly, no local file)", dataFileUrl);
            progress?.Report(new ImportProgress { Message = "Downloading data file...", PercentComplete = 0 });

            using var response = await _httpClient.GetAsync(dataFileUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var gzipStream = new System.IO.Compression.GZipStream(stream, System.IO.Compression.CompressionMode.Decompress);
            using var reader = new StreamReader(gzipStream);

            int lineNumber = 0;
            int processedCount = 0;
            int skippedCount = 0;
            var batchSize = 5000;
            var stagingBatch = new List<StagedProduct>();

            while (!reader.EndOfStream && processedCount < maxProducts && !cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync();
                lineNumber++;

                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    var product = JsonDocument.Parse(line).RootElement;

                    var lang = GetStringValue(product, "lang", "lc");
                    if (lang != null && !lang.StartsWith("en", StringComparison.OrdinalIgnoreCase))
                    {
                        skippedCount++;
                        continue;
                    }

                    var categories = GetStringValue(product, "categories", "categories_tags")?.ToLower() ?? "";
                    var productName = GetStringValue(product, "product_name", "product_name_en")?.ToLower() ?? "";

                    if (categories.Contains("beauty") || categories.Contains("pet-food") ||
                        categories.Contains("cosmetic") || productName.Contains("shampoo") ||
                        productName.Contains("lotion"))
                    {
                        skippedCount++;
                        continue;
                    }

                    var barcode = GetStringValue(product, "code");
                    if (string.IsNullOrWhiteSpace(barcode) || string.IsNullOrWhiteSpace(productName))
                    {
                        skippedCount++;
                        continue;
                    }

                    if (!IsUsProduct(GetStringValue(product, "countries")))
                    {
                        skippedCount++;
                        continue;
                    }

                    stagingBatch.Add(MapToStagedProduct(product, barcode));

                    if (stagingBatch.Count >= batchSize)
                    {
                        var enableAugmentation = _configuration.GetValue<bool>("ProductImport:EnableDataAugmentation", false);
                        var diagnoseImages = _configuration.GetValue<bool>("ProductImport:DiagnoseJsonImages", false);

                        if (enableAugmentation)
                        {
                            var augmented = await _stagingRepository.BulkAugmentStagingProductsAsync(stagingBatch, "JSON");
                            var inserted = await _stagingRepository.BulkInsertStagingProductsAsync(stagingBatch);
                            result.SuccessCount += inserted;
                            result.FailureCount += stagingBatch.Count - inserted - augmented;
                            processedCount += inserted;
                            result.TotalProcessed = processedCount;
                            if (augmented > 0)
                                _logger.LogInformation("[JSON] Augmented {Count} existing records", augmented);
                            if (diagnoseImages && inserted > 0)
                            {
                                var withImages = stagingBatch.Where(p => !string.IsNullOrWhiteSpace(p.ImageUrl)).ToList();
                                if (withImages.Any())
                                {
                                    _logger.LogWarning("[JSON-IMAGES] Found {Count} products with images in this batch", withImages.Count);
                                    foreach (var p in withImages.Take(3))
                                        _logger.LogWarning("[JSON-IMAGES] Barcode: {Barcode}, Name: {Name}, ImageUrl: {Url}", p.Barcode, p.ProductName, p.ImageUrl);
                                }
                            }
                        }
                        else
                        {
                            var inserted = await _stagingRepository.BulkInsertStagingProductsAsync(stagingBatch);
                            result.SuccessCount += inserted;
                            result.FailureCount += stagingBatch.Count - inserted;
                            processedCount += inserted;
                            result.TotalProcessed = processedCount;
                        }

                        stagingBatch.Clear();

                        var pct = (int)((processedCount / (double)maxProducts) * 100);
                        progress?.Report(new ImportProgress { Message = $"Imported {processedCount} products to staging (skipped {skippedCount})...", PercentComplete = pct, ProductsImported = processedCount });
                        _logger.LogInformation("[JSON] Bulk import progress: {Processed} imported, {Skipped} skipped", processedCount, skippedCount);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process line {LineNumber}", lineNumber);
                    result.FailureCount++;
                    result.Errors.Add($"Line {lineNumber}: {ex.Message}");
                }
            }

            if (stagingBatch.Any())
            {
                var inserted = await _stagingRepository.BulkInsertStagingProductsAsync(stagingBatch);
                result.SuccessCount += inserted;
                result.FailureCount += stagingBatch.Count - inserted;
                processedCount += inserted;
                result.TotalProcessed = processedCount;
            }

            progress?.Report(new ImportProgress { Message = $"Import complete! Imported {processedCount} products.", PercentComplete = 100, ProductsImported = processedCount });
            _logger.LogInformation("JSONL bulk import completed: {Success} staged, {Failed} failed, {Skipped} skipped", result.SuccessCount, result.FailureCount, skippedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import from bulk data at {Url}", dataFileUrl);
            result.Errors.Add($"Bulk import error: {ex.Message}");
        }

        return result;
    }

    public async Task<BatchImportResult> ImportFromCsvDataAsync(
        string? dataFileUrl = null,
        int maxProducts = 10000,
        CancellationToken cancellationToken = default,
        IProgress<ImportProgress>? progress = null)
    {
        var result = new BatchImportResult();
        dataFileUrl ??= "https://static.openfoodfacts.org/data/en.openfoodfacts.org.products.csv.gz";

        try
        {
            _logger.LogInformation("Starting CSV bulk import from {Url} (streaming directly, no local file)", dataFileUrl);
            progress?.Report(new ImportProgress { Message = "Downloading CSV data file...", PercentComplete = 0 });

            using var response = await _httpClient.GetAsync(dataFileUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var gzipStream = new System.IO.Compression.GZipStream(stream, System.IO.Compression.CompressionMode.Decompress);
            using var reader = new StreamReader(gzipStream);

            var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null,
                BadDataFound = null,
                Delimiter = "\t" // OpenFoodFacts CSV is tab-delimited
            };

            using var csv = new CsvReader(reader, csvConfig);

            int recordNumber = 0;
            int processedCount = 0;
            int skippedCount = 0;
            var batchSize = 5000;
            var stagingBatch = new List<StagedProduct>();

            await csv.ReadAsync();
            csv.ReadHeader();
            _logger.LogInformation("CSV Headers found: {Headers}", string.Join(", ", csv.HeaderRecord ?? Array.Empty<string>()));

            while (await csv.ReadAsync() && processedCount < maxProducts && !cancellationToken.IsCancellationRequested)
            {
                recordNumber++;

                try
                {
                    var barcode = csv.GetField<string>("code");
                    var lang = csv.GetField<string>("lang");
                    var productName = csv.GetField<string>("product_name");

                    if (recordNumber <= 10)
                    {
                        _logger.LogInformation(
                            "CSV Debug Record {RecordNumber}: code={Code}, lang={Lang}, product_name={ProductName}",
                            recordNumber, barcode ?? "(null)", lang ?? "(null)", productName ?? "(null)");
                    }

                    if (string.IsNullOrWhiteSpace(barcode) || string.IsNullOrWhiteSpace(productName))
                    {
                        skippedCount++;
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(lang) && !lang.StartsWith("en", StringComparison.OrdinalIgnoreCase))
                    {
                        skippedCount++;
                        continue;
                    }

                    var ingredientsText = csv.GetField<string>("ingredients_text");
                    var ingredientsTextEn = csv.GetField<string>("ingredients_text_en");
                    var brand = csv.GetField<string>("brands");

                    if (!LanguageDetector.ShouldImportProduct(productName ?? "", brand ?? "", ingredientsTextEn ?? ingredientsText ?? ""))
                    {
                        skippedCount++;
                        if (recordNumber <= 20)
                            _logger.LogInformation("Skipping non-English product (content-based): {ProductName} (Barcode: {Barcode})", productName, barcode);
                        continue;
                    }

                    var categories = csv.GetField<string>("categories")?.ToLower() ?? "";
                    if (categories.Contains("beauty") || categories.Contains("pet-food") ||
                        categories.Contains("cosmetic") || productName.ToLower().Contains("shampoo") ||
                        productName.ToLower().Contains("lotion"))
                    {
                        skippedCount++;
                        continue;
                    }

                    var genericName = csv.GetField<string>("generic_name");
                    if (!string.IsNullOrWhiteSpace(genericName) &&
                        (genericName.Length > 200 || genericName.Count(c => c == ',') > 5))
                    {
                        _logger.LogDebug("Filtering suspicious GenericName: {Name}", genericName[..Math.Min(50, genericName.Length)]);
                        genericName = null;
                    }

                    var stagedProduct = new StagedProduct
                    {
                        ExternalId = barcode,
                        Barcode = barcode,
                        ProductName = productName,
                        GenericName = genericName,
                        Brands = brand,
                        Lang = lang,
                        Countries = csv.GetField<string>("countries"),
                        NutriScore = csv.GetField<string>("nutriscore_grade"),
                        EcoScore = csv.GetField<string>("ecoscore_grade"),
                        ImageUrl = csv.GetField<string>("image_url")
                            ?? csv.GetField<string>("image_front_url")
                            ?? csv.GetField<string>("image_small_url"),
                        ImageSmallUrl = csv.GetField<string>("image_small_url")
                            ?? csv.GetField<string>("image_thumb_url"),
                        IngredientsText = ingredientsText,
                        IngredientsTextEn = ingredientsTextEn,
                        Allergens = csv.GetField<string>("allergens"),
                        Categories = csv.GetField<string>("categories")
                    };

                    var novaStr = csv.GetField<string>("nova_group");
                    if (int.TryParse(novaStr, out var novaGroup))
                        stagedProduct.NovaGroup = novaGroup;

                    if (!IsUsProduct(stagedProduct.Countries))
                    {
                        skippedCount++;
                        continue;
                    }

                    if (recordNumber <= 100)
                    {
                        _logger.LogInformation(
                            "CSV Record {RecordNumber} - Barcode: {Barcode}, Product: {ProductName}\n" +
                            "  ImageUrl: {ImageUrl}\n" +
                            "  image_front_url: {FrontUrl}\n" +
                            "  image_nutrition_url: {NutritionUrl}\n" +
                            "  image_ingredients_url: {IngredientsUrl}",
                            recordNumber, barcode, productName,
                            stagedProduct.ImageUrl ?? "(null)",
                            csv.GetField<string>("image_front_url") ?? "(null)",
                            csv.GetField<string>("image_nutrition_url") ?? "(null)",
                            csv.GetField<string>("image_ingredients_url") ?? "(null)");
                    }

                    stagingBatch.Add(stagedProduct);

                    if (stagingBatch.Count >= batchSize)
                    {
                        var enableAugmentation = _configuration.GetValue<bool>("ProductImport:EnableDataAugmentation", false);

                        if (enableAugmentation)
                        {
                            var augmented = await _stagingRepository.BulkAugmentStagingProductsAsync(stagingBatch, "CSV");
                            var inserted = await _stagingRepository.BulkInsertStagingProductsAsync(stagingBatch);
                            result.SuccessCount += inserted;
                            result.FailureCount += stagingBatch.Count - inserted - augmented;
                            processedCount += inserted;
                            result.TotalProcessed = processedCount;
                            if (augmented > 0)
                                _logger.LogInformation("[CSV] Augmented {Count} existing records with additional data", augmented);
                        }
                        else
                        {
                            var inserted = await _stagingRepository.BulkInsertStagingProductsAsync(stagingBatch);
                            result.SuccessCount += inserted;
                            result.FailureCount += stagingBatch.Count - inserted;
                            processedCount += inserted;
                            result.TotalProcessed = processedCount;
                        }

                        stagingBatch.Clear();

                        if (processedCount % 5000 == 0)
                        {
                            var pct = (int)((processedCount / (double)maxProducts) * 100);
                            progress?.Report(new ImportProgress { Message = $"Imported {processedCount} products from CSV (skipped {skippedCount})...", PercentComplete = pct, ProductsImported = processedCount });
                            _logger.LogInformation("CSV bulk import progress: {Processed} imported, {Skipped} skipped", processedCount, skippedCount);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process CSV record {RecordNumber}", recordNumber);
                    result.FailureCount++;
                    result.Errors.Add($"Record {recordNumber}: {ex.Message}");
                }
            }

            if (stagingBatch.Any())
            {
                var inserted = await _stagingRepository.BulkInsertStagingProductsAsync(stagingBatch);
                result.SuccessCount += inserted;
                result.FailureCount += stagingBatch.Count - inserted;
                processedCount += inserted;
                result.TotalProcessed = processedCount;
            }

            progress?.Report(new ImportProgress { Message = $"CSV import complete! Imported {processedCount} products.", PercentComplete = 100, ProductsImported = processedCount });
            _logger.LogInformation("CSV bulk import completed: {Success} staged, {Failed} failed, {Skipped} skipped", result.SuccessCount, result.FailureCount, skippedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import from CSV at {Url}", dataFileUrl);
            result.Errors.Add($"CSV bulk import error: {ex.Message}");
        }

        return result;
    }

    private async Task<ImportResult> ProcessProductAsync(JsonElement product, string barcode)
    {
        var result = new ImportResult { ExternalId = barcode };

        try
        {
            var productName = GetStringValue(product, "product_name", "product_name_en", "generic_name");
            var brand = GetStringValue(product, "brands");
            var categories = GetStringValue(product, "categories");
            var ingredientsText = GetStringValue(product, "ingredients_text", "ingredients_text_en");

            result.ProductName = productName ?? "Unknown Product";

            if (!LanguageDetector.ShouldImportProduct(productName ?? "", brand ?? "", ingredientsText ?? ""))
            {
                return new ImportResult { Success = false, ErrorMessage = "Product is not in English - skipped", ProductName = result.ProductName, ExternalId = barcode };
            }

            var existing = await _productRepository.GetProductByBarcodeAsync(barcode);
            if (existing != null)
            {
                result.ProductId = existing.Id;
                result.Success = true;
                result.AlreadyExists = true;
                await SaveProductImagesAsync(existing.Id, product, barcode);
                return result;
            }

            var category = DetermineCategory(categories, productName);
            var imageUrl = GetStringValue(product, "image_url", "image_front_url", "image_front_small_url", "image_thumb_url") ?? GetNestedImageUrl(product);

            var productId = await _productRepository.CreateProductAsync(new CreateProductRequest {
                Name = productName ?? "Unknown Product",
                Brand = brand,
                Barcode = barcode,
                Category = category,
                ImageUrl = imageUrl
            });

            await SaveProductImagesAsync(productId, product, barcode);
            result.ProductId = productId;

            if (!string.IsNullOrWhiteSpace(ingredientsText))
            {
                var ingredients = await _ingredientClient.ParseIngredientListAsync(ingredientsText);
                int orderIndex = 0;
                foreach (var ingredient in ingredients.Take(50))
                {
                    await _productRepository.AddIngredientToProductAsync(productId, ingredient, orderIndex: orderIndex++);
                }
                result.IngredientCount = ingredients.Count;
            }

            result.Success = true;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing product {Barcode}", barcode);
            result.Success = false;
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    /// <summary>
    /// Returns true when the OpenFoodFacts countries field indicates the product is sold in the United States.
    /// Handles both free-text ("United States") and taxonomy tags ("en:united-states").
    /// </summary>
    private static bool IsUsProduct(string? countries)
    {
        if (string.IsNullOrWhiteSpace(countries)) return false;
        return countries.Contains("united states", StringComparison.OrdinalIgnoreCase)
            || countries.Contains("en:united-states", StringComparison.OrdinalIgnoreCase);
    }

    private string? GetStringValue(JsonElement element, params string[] fieldNames)
    {
        foreach (var fieldName in fieldNames)
        {
            if (element.TryGetProperty(fieldName, out var value) && value.ValueKind == JsonValueKind.String)
            {
                var str = value.GetString();
                if (!string.IsNullOrWhiteSpace(str)) return str;
            }
        }
        return null;
    }

    private string? GetNestedImageUrl(JsonElement product)
    {
        try
        {
            if (product.TryGetProperty("selected_images", out var selectedImages) &&
                selectedImages.TryGetProperty("front", out var front) &&
                front.TryGetProperty("display", out var display))
            {
                return GetStringValue(display, "en", "fr", "de", "it");
            }
        }
        catch { }
        return null;
    }

    private string? DetermineCategory(string? categories, string? productName)
    {
        if (string.IsNullOrWhiteSpace(categories)) return "General";
        var parts = categories.Split(',', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0].Trim() : "General";
    }

    private StagedProduct MapToStagedProduct(JsonElement product, string barcode)
    {
        var stagedProduct = new StagedProduct
        {
            ExternalId = GetStringValue(product, "code", "_id") ?? barcode,
            Barcode = barcode,
            ProductName = GetStringValue(product, "product_name", "product_name_en"),
            GenericName = GetStringValue(product, "generic_name", "generic_name_en"),
            Brands = GetStringValue(product, "brands"),
            Lang = GetStringValue(product, "lang", "lc"),
            Countries = GetStringValue(product, "countries"),
            NutriScore = GetStringValue(product, "nutriscore_grade"),
            EcoScore = GetStringValue(product, "ecoscore_grade"),
            ImageUrl = GetStringValue(product, "image_url", "image_front_url", "image_front_small_url", "image_thumb_url")
                ?? GetNestedImageUrl(product),
            ImageSmallUrl = GetStringValue(product, "image_small_url", "image_front_small_url", "image_thumb_url")
        };

        stagedProduct.IngredientsText = GetStringValue(product, "ingredients_text");
        stagedProduct.IngredientsTextEn = GetStringValue(product, "ingredients_text_en");
        stagedProduct.Allergens = GetStringValue(product, "allergens");

        if (product.TryGetProperty("allergens_hierarchy", out var allergensHierarchy))
            stagedProduct.AllergensHierarchy = allergensHierarchy.GetRawText();

        stagedProduct.Categories = GetStringValue(product, "categories");

        if (product.TryGetProperty("categories_hierarchy", out var categoriesHierarchy))
            stagedProduct.CategoriesHierarchy = categoriesHierarchy.GetRawText();

        if (product.TryGetProperty("nova_group", out var novaGroup) && novaGroup.ValueKind == JsonValueKind.Number)
            stagedProduct.NovaGroup = novaGroup.GetInt32();

        if (product.TryGetProperty("nutriments", out var nutriments))
            stagedProduct.NutritionData = nutriments.GetRawText();

        stagedProduct.RawJson = product.GetRawText();

        return stagedProduct;
    }

    private async Task SaveProductImagesAsync(Guid productId, JsonElement product, string barcode)
    {
        var images = new List<(string url, string type)>();
        var front = GetStringValue(product, "image_front_url");
        if (front != null) images.Add((front, "Front"));
        var ingredients = GetStringValue(product, "image_ingredients_url");
        if (ingredients != null) images.Add((ingredients, "Ingredients"));
        var nutrition = GetStringValue(product, "image_nutrition_url");
        if (nutrition != null) images.Add((nutrition, "Nutrition"));

        foreach (var img in images)
        {
            // Fixed AddImageAsync parameters
            await _productImageRepository.AddImageAsync(
                productId, 
                img.type, 
                img.url, 
                null, // localFilePath
                null, // fileName
                null, // fileSize
                null, // mimeType
                null, // width
                null, // height
                img.type == "Front", // isPrimary
                0, // displayOrder
                false, // isUserUploaded
                "OpenFoodFacts", // sourceSystem
                barcode, // sourceId
                null // userId
            );
        }
    }
}
