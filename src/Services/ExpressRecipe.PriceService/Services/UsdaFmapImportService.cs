using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using ExpressRecipe.PriceService.Data;

namespace ExpressRecipe.PriceService.Services;

/// <summary>
/// Imports USDA ERS Food-at-Home Monthly Area Prices data.
/// Source: https://www.ers.usda.gov/data-products/food-at-home-monthly-area-prices/
/// Format: CSV/Excel with columns: food_group, area, date, mean_unit_value, unit
/// DataSource code: "USDA_FMAP"
/// </summary>
public class UsdaFmapImportService
{
    public const string DataSourceCode = "USDA_FMAP";

    private readonly IPriceRepository _repository;
    private readonly IPriceUnitNormalizer _unitNormalizer;
    private readonly ILogger<UsdaFmapImportService> _logger;
    private readonly IConfiguration _configuration;

    public UsdaFmapImportService(
        IPriceRepository repository,
        IPriceUnitNormalizer unitNormalizer,
        ILogger<UsdaFmapImportService> logger,
        IConfiguration configuration)
    {
        _repository = repository;
        _unitNormalizer = unitNormalizer;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Parse a CSV stream row into a <see cref="PriceHistoryRecord"/>.
    /// Expected CSV headers: food_group, area, date, mean_unit_value, unit
    /// </summary>
    public PriceHistoryRecord? ParseRow(IReaderRow row)
    {
        try
        {
            var foodGroup = row.GetField<string>("food_group") ?? string.Empty;
            var area = row.GetField<string>("area") ?? string.Empty;
            var dateStr = row.GetField<string>("date") ?? string.Empty;
            var meanValue = row.GetField<decimal?>("mean_unit_value");
            var unit = row.GetField<string?>("unit");

            if (string.IsNullOrWhiteSpace(foodGroup) || !meanValue.HasValue || meanValue.Value <= 0)
            {
                return null;
            }

            if (!DateTimeOffset.TryParse(dateStr, out var observedAt))
            {
                // Try parsing year-month format like "2023-01"
                if (DateTime.TryParseExact(dateStr, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                {
                    observedAt = new DateTimeOffset(dt, TimeSpan.Zero);
                }
                else
                {
                    _logger.LogWarning("UsdaFmap: Cannot parse date '{Date}' for food group '{FoodGroup}'", dateStr, foodGroup);
                    return null;
                }
            }

            // Parse city/state from area string (e.g. "Northeast urban" or "Chicago, IL")
            string? city = null;
            string? state = null;
            if (!string.IsNullOrWhiteSpace(area))
            {
                var parts = area.Split(',', 2, StringSplitOptions.TrimEntries);
                if (parts.Length == 2)
                {
                    city = parts[0];
                    state = parts[1];
                }
                else
                {
                    city = area;
                }
            }

            var metrics = _unitNormalizer.ComputeUnitPrices(meanValue.Value, unit, 1m);

            return new PriceHistoryRecord
            {
                ProductId = Guid.Empty, // Category-level; no product link
                ProductName = foodGroup,
                StoreName = null,
                StoreChain = null,
                IsOnline = false,
                BasePrice = meanValue.Value,
                FinalPrice = meanValue.Value,
                Currency = "USD",
                Unit = metrics.NormalizedUnit ?? unit,
                Quantity = 1m,
                PricePerOz = metrics.PricePerOz,
                PricePerHundredG = metrics.PricePerHundredG,
                DataSource = DataSourceCode,
                ExternalId = $"{foodGroup}|{area}|{dateStr}",
                ObservedAt = observedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "UsdaFmap: Failed to parse CSV row");
            return null;
        }
    }

    /// <summary>
    /// Import price history records from a CSV file path.
    /// </summary>
    public async Task<ImportResult> ImportFromFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("UsdaFmap: File not found at {Path}", filePath);
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
                    _logger.LogInformation("UsdaFmap: Processed {Processed} rows, imported {Imported}", processed, imported);
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

            _logger.LogInformation("UsdaFmap: Import complete. Processed={Processed}, Imported={Imported}, Errors={Errors}", processed, imported, errors);
            return new ImportResult { Processed = processed, Imported = imported, Errors = errors, Success = true };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UsdaFmap: Import failed");
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
