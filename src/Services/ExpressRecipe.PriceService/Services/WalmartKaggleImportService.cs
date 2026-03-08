using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using ExpressRecipe.PriceService.Data;

namespace ExpressRecipe.PriceService.Services;

/// <summary>
/// Imports Walmart grocery product data from the Kaggle walmart-grocery-product-dataset CSV.
/// Expected columns: SHIPPING_LOCATION(ZIP), DEPARTMENT, CATEGORY, SUBCATEGORY, SKU,
///                   PRODUCT_NAME, BRAND, PRICE_RETAIL, PRICE_CURRENT, PRODUCT_SIZE
/// DataSource code: "KAGGLE_WALMART"
/// </summary>
public class WalmartKaggleImportService
{
    public const string DataSourceCode = "KAGGLE_WALMART";
    public const string StoreName = "Walmart";
    public const string StoreChain = "Walmart";

    private readonly IPriceRepository _repository;
    private readonly IPriceUnitNormalizer _unitNormalizer;
    private readonly ILogger<WalmartKaggleImportService> _logger;
    private readonly IConfiguration _configuration;

    public WalmartKaggleImportService(
        IPriceRepository repository,
        IPriceUnitNormalizer unitNormalizer,
        ILogger<WalmartKaggleImportService> logger,
        IConfiguration configuration)
    {
        _repository = repository;
        _unitNormalizer = unitNormalizer;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Parse a single Walmart CSV row into a <see cref="PriceHistoryRecord"/>.
    /// Returns null for rows with missing or invalid required fields.
    /// </summary>
    public PriceHistoryRecord? ParseRow(IReaderRow row)
    {
        try
        {
            var productName = row.GetField<string?>("PRODUCT_NAME");
            if (string.IsNullOrWhiteSpace(productName)) { return null; }

            var sku = row.GetField<string?>("SKU");
            var priceRetail = row.GetField<decimal?>("PRICE_RETAIL");
            var priceCurrent = row.GetField<decimal?>("PRICE_CURRENT");
            var productSize = row.GetField<string?>("PRODUCT_SIZE");
            var zipCode = row.GetField<string?>("SHIPPING_LOCATION");

            var basePrice = priceRetail ?? priceCurrent ?? 0m;
            var finalPrice = priceCurrent ?? basePrice;

            if (basePrice <= 0m && finalPrice <= 0m) { return null; }
            if (basePrice == 0m) { basePrice = finalPrice; }

            // Parse size/unit from PRODUCT_SIZE (e.g. "12 Count", "32 fl oz", "2 lbs")
            decimal? quantity = null;
            string? unit = null;
            if (!string.IsNullOrWhiteSpace(productSize))
            {
                ParseProductSize(productSize, out quantity, out unit);
            }

            var metrics = _unitNormalizer.ComputeUnitPrices(finalPrice, unit, quantity);

            return new PriceHistoryRecord
            {
                ProductId = Guid.Empty,
                ProductName = productName,
                StoreName = StoreName,
                StoreChain = StoreChain,
                IsOnline = true,
                BasePrice = Math.Round(basePrice, 4),
                FinalPrice = Math.Round(finalPrice, 4),
                Currency = "USD",
                Unit = metrics.NormalizedUnit ?? unit,
                Quantity = quantity,
                PricePerOz = metrics.PricePerOz,
                PricePerHundredG = metrics.PricePerHundredG,
                DataSource = DataSourceCode,
                ExternalId = sku,
                ObservedAt = DateTimeOffset.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WalmartKaggle: Failed to parse row");
            return null;
        }
    }

    private static void ParseProductSize(string productSize, out decimal? quantity, out string? unit)
    {
        quantity = null;
        unit = null;

        var parts = productSize.Trim().Split(' ', 2, StringSplitOptions.TrimEntries);
        if (parts.Length >= 1 && decimal.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var qty))
        {
            quantity = qty;
            if (parts.Length == 2)
            {
                unit = parts[1];
            }
        }
        else
        {
            unit = productSize;
        }
    }

    /// <summary>Import from a local CSV file.</summary>
    public async Task<ImportResult> ImportFromFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("WalmartKaggle: File not found at {Path}", filePath);
            return new ImportResult { Success = false, ErrorMessage = $"File not found: {filePath}" };
        }

        var processed = 0;
        var imported = 0;
        var errors = 0;
        var batch = new List<PriceHistoryRecord>();
        const int batchSize = 5000;

        var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null
        };

        try
        {
            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, csvConfig);
            await csv.ReadAsync();
            csv.ReadHeader();

            while (await csv.ReadAsync())
            {
                cancellationToken.ThrowIfCancellationRequested();
                processed++;

                var record = ParseRow(csv);
                if (record == null) { errors++; continue; }

                batch.Add(record);
                if (batch.Count >= batchSize)
                {
                    await _repository.BulkInsertPriceHistoryAsync(batch, cancellationToken);
                    imported += batch.Count;
                    batch.Clear();
                    if (processed % 10_000 == 0)
                    {
                        _logger.LogInformation("WalmartKaggle: Processed {Processed} rows, imported {Imported}", processed, imported);
                    }
                }
            }

            if (batch.Count > 0)
            {
                await _repository.BulkInsertPriceHistoryAsync(batch, cancellationToken);
                imported += batch.Count;
            }

            await _repository.LogImportAsync(new PriceImportLogDto
            {
                DataSource = DataSourceCode,
                RecordsProcessed = processed,
                RecordsImported = imported,
                ErrorCount = errors,
                Success = true
            });

            _logger.LogInformation("WalmartKaggle: Import complete. Processed={Processed}, Imported={Imported}, Errors={Errors}", processed, imported, errors);
            return new ImportResult { Processed = processed, Imported = imported, Errors = errors, Success = true };
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WalmartKaggle: Import failed");
            await _repository.LogImportAsync(new PriceImportLogDto
            {
                DataSource = DataSourceCode,
                RecordsProcessed = processed,
                RecordsImported = imported,
                ErrorCount = errors,
                ErrorMessage = ex.Message,
                Success = false
            });
            return new ImportResult { Processed = processed, Imported = imported, Errors = errors, Success = false, ErrorMessage = ex.Message };
        }
    }
}
