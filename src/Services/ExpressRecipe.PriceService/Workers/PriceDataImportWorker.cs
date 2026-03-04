using ExpressRecipe.PriceService.Data;
using ExpressRecipe.PriceService.Services;

namespace ExpressRecipe.PriceService.Workers;

public class PriceDataImportWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PriceDataImportWorker> _logger;
    private readonly IConfiguration _configuration;

    private static readonly TimeOnly RunTime = new(3, 0, 0);
    private const string DefaultOpenPricesUrl = "https://huggingface.co/datasets/openfoodfacts/openprices/resolve/main/data/prices.parquet";

    public PriceDataImportWorker(
        IServiceProvider serviceProvider,
        ILogger<PriceDataImportWorker> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PriceDataImportWorker started");

        var autoImport = _configuration.GetValue<bool>("PriceImport:AutoImport", true);
        if (!autoImport)
        {
            _logger.LogInformation("PriceDataImportWorker: auto-import is disabled");
            return;
        }

        await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);
        if (!stoppingToken.IsCancellationRequested)
        {
            await TriggerImportIfNeededAsync(stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var delay = CalculateDelayUntilNextRun();
                _logger.LogInformation("PriceDataImportWorker: next run in {Delay}", delay);
                await Task.Delay(delay, stoppingToken);

                if (!stoppingToken.IsCancellationRequested)
                    await RunImportAsync(stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PriceDataImportWorker encountered an error; retrying in 1 hour");
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
    }

    public async Task RunImportAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("PriceDataImportWorker: starting import process. Working Dir: {Dir}", Directory.GetCurrentDirectory());

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var importService = scope.ServiceProvider.GetRequiredService<IOpenPricesImportService>();

            var dataFile = _configuration["PriceImport:OpenPricesFilePath"];
            var url = _configuration["PriceImport:OpenPricesUrl"] ?? DefaultOpenPricesUrl;
            var format = _configuration["PriceImport:OpenPricesFormat"] ?? "parquet";

            // 1. PRIORITY: Local File (If exists, do NOT call web)
            if (!string.IsNullOrWhiteSpace(dataFile) && importService.FileExists(dataFile))
            {
                _logger.LogInformation("STRATEGY: Local file found at {Path}. Skipping web call.", dataFile);
                var result = await importService.ImportFromFileAsync(dataFile, cancellationToken);
                _logger.LogInformation("PriceDataImportWorker: File import result - processed={Processed} imported={Imported}", result.Processed, result.Imported);
                return; // Exit successfully
            }

            // 2. ATTEMPT WEB
            _logger.LogInformation("STRATEGY: Local file not found. Attempting web import from {Url}", url);
            try
            {
                var webResult = await importService.ImportFromUrlAsync(url, format, cancellationToken);
                if (webResult.Success && webResult.Imported > 0)
                {
                    _logger.LogInformation("PriceDataImportWorker: Web import successful");
                    return;
                }
                
                _logger.LogWarning("PriceDataImportWorker: Web import returned no data or was unsuccessful.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "STRATEGY: Web import failed. Initiating fallback to local file search.");
            }

            // 3. FALLBACK: Search for file again if web failed
            if (!string.IsNullOrWhiteSpace(dataFile) && importService.FileExists(dataFile))
            {
                _logger.LogInformation("FALLBACK: Found local file {Path} after web failure. Importing...", dataFile);
                await importService.ImportFromFileAsync(dataFile, cancellationToken);
            }
            else
            {
                _logger.LogError("CRITICAL: Both web and local file ({Path}) are unavailable.", dataFile ?? "N/A");
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PriceDataImportWorker: Import process failed completely");
        }
    }

    private async Task TriggerImportIfNeededAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IPriceRepository>();
            var count = await repository.GetProductPriceCountAsync();

            if (count < 1000)
            {
                _logger.LogInformation("PriceDataImportWorker: price count is {Count} (< 1000), triggering startup import", count);
                await RunImportAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PriceDataImportWorker: startup import check failed");
        }
    }

    private static TimeSpan CalculateDelayUntilNextRun()
    {
        var now = DateTime.UtcNow;
        var next = now.Date.Add(RunTime.ToTimeSpan());
        if (next <= now) next = next.AddDays(1);
        return next - now;
    }
}
