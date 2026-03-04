using ExpressRecipe.PriceService.Data;

namespace ExpressRecipe.PriceService.Services;

public interface IOpenPricesImportService
{
    Task<ImportResult> ImportFromUrlAsync(string url, string format, CancellationToken cancellationToken = default);
    Task<ImportResult> ImportFromParquetStreamAsync(Stream stream, CancellationToken cancellationToken = default);
    Task<ImportResult> ImportFromCsvStreamAsync(Stream stream, CancellationToken cancellationToken = default);
    Task<ImportResult> ImportFromJsonlStreamAsync(Stream stream, CancellationToken cancellationToken = default);
    Task<ImportResult> ImportFromFileAsync(string filePath, CancellationToken cancellationToken = default);
    bool FileExists(string filePath);
}
