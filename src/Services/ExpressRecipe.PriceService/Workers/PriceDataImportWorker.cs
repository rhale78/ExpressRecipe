using ExpressRecipe.PriceService.Data;
using ExpressRecipe.PriceService.Services;

namespace ExpressRecipe.PriceService.Workers;

public class PriceDataImportWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PriceDataImportWorker> _logger;
    private readonly IConfiguration _configuration;

    // Run daily at 3:00 AM
    private static readonly TimeOnly RunTime = new(3, 0, 0);

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

        var autoImport = _configuration.GetValue<bool>("PriceImport:AutoImport", false);
        if (!autoImport)
        {
            _logger.LogInformation("PriceDataImportWorker: auto-import is disabled (PriceImport:AutoImport=false)");
            return;
        }

        // Startup check: trigger import if we have fewer than 1000 prices
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
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
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PriceDataImportWorker encountered an error; will retry in 1 hour");
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }

        _logger.LogInformation("PriceDataImportWorker stopped");
    }

    public async Task RunImportAsync(CancellationToken cancellationToken = default)
    {
        var openPricesEnabled = _configuration.GetValue<bool>("PriceImport:OpenPricesEnabled", false);

        if (!openPricesEnabled)
        {
            _logger.LogInformation("PriceDataImportWorker: OpenPrices import is disabled");
            return;
        }

        _logger.LogInformation("PriceDataImportWorker: starting scheduled import");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var importService = scope.ServiceProvider.GetRequiredService<OpenPricesImportService>();

            // In production the caller would provide a stream from a downloaded/mounted file.
            // Here we emit a warning if no file is configured so the service doesn't crash.
            var dataFile = _configuration["PriceImport:OpenPricesFilePath"];
            if (string.IsNullOrWhiteSpace(dataFile) || !File.Exists(dataFile))
            {
                _logger.LogWarning("PriceDataImportWorker: OpenPrices data file not found at '{Path}'. Skipping import.",
                    dataFile);
                return;
            }

            var result = await importService.ImportFromFileAsync(dataFile, cancellationToken);
            _logger.LogInformation(
                "PriceDataImportWorker: import complete – processed={Processed} imported={Imported} errors={Errors}",
                result.Processed, result.Imported, result.Errors);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PriceDataImportWorker: import failed");
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
            else
            {
                _logger.LogInformation("PriceDataImportWorker: price count is {Count}, skipping startup import", count);
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
        if (next <= now)
            next = next.AddDays(1);
        return next - now;
    }
}
