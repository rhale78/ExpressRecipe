using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using ExpressRecipe.PriceService.Data;

namespace ExpressRecipe.PriceService.Services;

/// <summary>
/// Imports Costco grocery data from the Kaggle grocery-dataset CSV.
/// Expected columns: Sub_Category, Price, Discount, Rating, Product_Description
/// DataSource code: "KAGGLE_COSTCO"
/// </summary>
public class CostcoKaggleImportService
{
    public const string DataSourceCode = "KAGGLE_COSTCO";
    public const string StoreName = "Costco";
    public const string StoreChain = "Costco";

    private readonly IPriceRepository _repository;
    private readonly IPriceUnitNormalizer _unitNormalizer;
    private readonly ILogger<CostcoKaggleImportService> _logger;
    private readonly IConfiguration _configuration;

    public CostcoKaggleImportService(
        IPriceRepository repository,
        IPriceUnitNormalizer unitNormalizer,
        ILogger<CostcoKaggleImportService> logger,
        IConfiguration configuration)
    {
        _repository = repository;
        _unitNormalizer = unitNormalizer;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Parse a single Costco CSV row. Returns (record, deal) where deal is non-null when
    /// a discount > 0 is present.
    /// </summary>
    public (PriceHistoryRecord? Record, CreateEnhancedDealRequest? Deal) ParseRow(IReaderRow row, Guid? storeId = null)
    {
        try
        {
            var description = row.GetField<string?>("Product_Description");
            if (string.IsNullOrWhiteSpace(description)) { return (null, null); }

            var priceStr = row.GetField<string?>("Price") ?? string.Empty;
            var discountStr = row.GetField<string?>("Discount") ?? string.Empty;
            var subCategory = row.GetField<string?>("Sub_Category") ?? string.Empty;

            // Prices may be formatted like "$12.99" or "12.99"
            priceStr = priceStr.TrimStart('$').Trim();
            discountStr = discountStr.TrimStart('$').Trim();

            if (!decimal.TryParse(priceStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var basePrice) || basePrice <= 0m)
            {
                return (null, null);
            }

            decimal.TryParse(discountStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var discountAmount);
            var finalPrice = Math.Max(0m, basePrice - discountAmount);

            var record = new PriceHistoryRecord
            {
                ProductId = Guid.Empty,
                ProductName = description,
                StoreName = StoreName,
                StoreChain = StoreChain,
                IsOnline = false,
                BasePrice = Math.Round(basePrice, 4),
                FinalPrice = Math.Round(finalPrice, 4),
                Currency = "USD",
                DataSource = DataSourceCode,
                ExternalId = null,
                ObservedAt = DateTimeOffset.UtcNow
            };

            CreateEnhancedDealRequest? deal = null;
            if (discountAmount > 0m && storeId.HasValue)
            {
                deal = new CreateEnhancedDealRequest
                {
                    ProductId = Guid.Empty,
                    StoreId = storeId.Value,
                    DealType = "Clearance",
                    DiscountType = "InstantRebate",
                    OriginalPrice = basePrice,
                    SalePrice = finalPrice,
                    RebateAmount = discountAmount,
                    StartDate = DateTime.UtcNow.Date,
                    EndDate = DateTime.UtcNow.Date.AddDays(7)
                };
            }

            return (record, deal);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CostcoKaggle: Failed to parse row");
            return (null, null);
        }
    }

    /// <summary>Import from a local CSV file.</summary>
    public async Task<ImportResult> ImportFromFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("CostcoKaggle: File not found at {Path}", filePath);
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

                var (record, deal) = ParseRow(csv);
                if (record == null) { errors++; continue; }

                batch.Add(record);

                if (deal != null && deal.ProductId != Guid.Empty)
                {
                    try { await _repository.CreateEnhancedDealAsync(deal, cancellationToken); }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "CostcoKaggle: Failed to create deal for {Product}", record.ProductName);
                    }
                }

                if (batch.Count >= batchSize)
                {
                    await _repository.BulkInsertPriceHistoryAsync(batch, cancellationToken);
                    imported += batch.Count;
                    batch.Clear();
                    if (processed % 10_000 == 0)
                    {
                        _logger.LogInformation("CostcoKaggle: Processed {Processed} rows, imported {Imported}", processed, imported);
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

            _logger.LogInformation("CostcoKaggle: Import complete. Processed={Processed}, Imported={Imported}, Errors={Errors}", processed, imported, errors);
            return new ImportResult { Processed = processed, Imported = imported, Errors = errors, Success = true };
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CostcoKaggle: Import failed");
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
