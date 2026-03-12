using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using ExpressRecipe.PriceService.Data;

namespace ExpressRecipe.PriceService.Services;

/// <summary>
/// Imports Bureau of Labor Statistics Average Price Data.
/// Source: https://www.bls.gov/charts/consumer-price-index/consumer-price-index-average-price-data.htm
/// Format: CSV (series_id, year, period, value) or expanded form with item_name, area_name
/// DataSource code: "BLS_CPI"
/// </summary>
public class BlsPriceImportService
{
    public const string DataSourceCode = "BLS_CPI";

    private readonly IPriceRepository _repository;
    private readonly IPriceUnitNormalizer _unitNormalizer;
    private readonly ILogger<BlsPriceImportService> _logger;
    private readonly IConfiguration _configuration;

    public BlsPriceImportService(
        IPriceRepository repository,
        IPriceUnitNormalizer unitNormalizer,
        ILogger<BlsPriceImportService> logger,
        IConfiguration configuration)
    {
        _repository = repository;
        _unitNormalizer = unitNormalizer;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Parse a BLS CSV row into a <see cref="PriceHistoryRecord"/>.
    /// Supports both compact (series_id, year, period, value) and expanded
    /// (item_name, area_name, year, period, value) header formats.
    /// </summary>
    public PriceHistoryRecord? ParseRow(IReaderRow row, string[]? headers)
    {
        try
        {
            string? itemName = null;
            string? areaName = null;

            if (headers != null && Array.Exists(headers, h => h.Equals("item_name", StringComparison.OrdinalIgnoreCase)))
            {
                itemName = row.GetField<string?>("item_name");
                areaName = row.GetField<string?>("area_name");
            }
            else
            {
                // Fall back to series_id as product name
                itemName = row.GetField<string?>("series_id");
            }

            if (string.IsNullOrWhiteSpace(itemName)) { return null; }

            var year = row.GetField<int?>("year") ?? 0;
            var period = row.GetField<string?>("period") ?? string.Empty; // e.g. "M01" for January
            var value = row.GetField<decimal?>("value");

            if (year < 1900 || !value.HasValue || value.Value <= 0) { return null; }

            // Convert BLS period (M01-M12) to a date
            if (!int.TryParse(period.TrimStart('M'), out var month) || month < 1 || month > 12)
            {
                month = 1;
            }
            var observedAt = new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.Zero);

            string? city = null;
            string? state = null;
            if (!string.IsNullOrWhiteSpace(areaName))
            {
                var parts = areaName.Split(',', 2, StringSplitOptions.TrimEntries);
                city = parts[0];
                if (parts.Length == 2) { state = parts[1]; }
            }

            return new PriceHistoryRecord
            {
                ProductId = Guid.Empty,
                ProductName = itemName,
                StoreName = null,
                StoreChain = null,
                IsOnline = false,
                BasePrice = value.Value,
                FinalPrice = value.Value,
                Currency = "USD",
                Unit = null,
                Quantity = 1m,
                DataSource = DataSourceCode,
                ExternalId = $"{itemName}|{areaName}|{year}-{month:D2}",
                ObservedAt = observedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BlsPrice: Failed to parse CSV row");
            return null;
        }
    }

    /// <summary>Import price history records from a CSV file path.</summary>
    public async Task<ImportResult> ImportFromFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("BlsPrice: File not found at {Path}", filePath);
            return new ImportResult { Success = false, ErrorMessage = $"File not found: {filePath}" };
        }

        var processed = 0;
        var imported = 0;
        var errors = 0;
        var batch = new List<PriceHistoryRecord>();
        const int batchSize = 5000;
        string[]? headers = null;

        var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null,
            Delimiter = "\t" // BLS often uses tab-delimited files
        };

        try
        {
            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, csvConfig);
            await csv.ReadAsync();
            csv.ReadHeader();
            headers = csv.HeaderRecord;

            while (await csv.ReadAsync())
            {
                cancellationToken.ThrowIfCancellationRequested();
                processed++;

                var record = ParseRow(csv, headers);
                if (record == null) { errors++; continue; }

                batch.Add(record);
                if (batch.Count >= batchSize)
                {
                    await _repository.BulkInsertPriceHistoryAsync(batch, cancellationToken);
                    imported += batch.Count;
                    batch.Clear();
                    _logger.LogInformation("BlsPrice: Processed {Processed} rows, imported {Imported}", processed, imported);

                    // Inter-item delay to prevent overwhelming CPU/disk (configured via PriceImport:BatchDelayMs)
                    var batchDelayMs = _configuration.GetValue<int>("PriceImport:BatchDelayMs", 0);
                    if (batchDelayMs > 0)
                        await Task.Delay(batchDelayMs, cancellationToken);
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

            _logger.LogInformation("BlsPrice: Import complete. Processed={Processed}, Imported={Imported}, Errors={Errors}", processed, imported, errors);
            return new ImportResult { Processed = processed, Imported = imported, Errors = errors, Success = true };
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BlsPrice: Import failed");
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
