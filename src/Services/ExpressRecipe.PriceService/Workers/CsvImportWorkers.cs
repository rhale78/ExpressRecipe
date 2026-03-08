using ExpressRecipe.PriceService.Services;

namespace ExpressRecipe.PriceService.Workers;

/// <summary>
/// Background worker that imports USDA FMAP data on a daily schedule.
/// Config: PriceImport:UsdaFmap:FilePath, :Enabled (default false)
/// </summary>
public class UsdaFmapImportWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<UsdaFmapImportWorker> _logger;
    private readonly IConfiguration _configuration;

    private static readonly TimeOnly RunTime = new(4, 0, 0);

    public UsdaFmapImportWorker(
        IServiceProvider serviceProvider,
        ILogger<UsdaFmapImportWorker> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = _configuration.GetValue<bool>("PriceImport:UsdaFmap:Enabled", false);
        if (!enabled)
        {
            _logger.LogInformation("UsdaFmapImportWorker: disabled by config");
            return;
        }

        _logger.LogInformation("UsdaFmapImportWorker: started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunImportAsync(stoppingToken);
                var delay = CalculateDelayUntilNextRun();
                _logger.LogInformation("UsdaFmapImportWorker: next run in {Delay}", delay);
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UsdaFmapImportWorker: error; retrying in 1 hour");
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
    }

    public async Task RunImportAsync(CancellationToken cancellationToken = default)
    {
        var filePath = _configuration["PriceImport:UsdaFmap:FilePath"];
        if (string.IsNullOrWhiteSpace(filePath))
        {
            _logger.LogWarning("UsdaFmapImportWorker: FilePath not configured");
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<UsdaFmapImportService>();
        var result = await service.ImportFromFileAsync(filePath, cancellationToken);
        _logger.LogInformation("UsdaFmapImportWorker: processed={Processed} imported={Imported}", result.Processed, result.Imported);
    }

    private static TimeSpan CalculateDelayUntilNextRun()
    {
        var now = DateTime.UtcNow;
        var next = now.Date.Add(RunTime.ToTimeSpan());
        if (next <= now) { next = next.AddDays(1); }
        return next - now;
    }
}

/// <summary>
/// Background worker that imports BLS CPI average price data on a daily schedule.
/// Config: PriceImport:BlsCpi:FilePath, :Enabled (default false)
/// </summary>
public class BlsPriceImportWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BlsPriceImportWorker> _logger;
    private readonly IConfiguration _configuration;

    private static readonly TimeOnly RunTime = new(5, 0, 0);

    public BlsPriceImportWorker(
        IServiceProvider serviceProvider,
        ILogger<BlsPriceImportWorker> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = _configuration.GetValue<bool>("PriceImport:BlsCpi:Enabled", false);
        if (!enabled)
        {
            _logger.LogInformation("BlsPriceImportWorker: disabled by config");
            return;
        }

        _logger.LogInformation("BlsPriceImportWorker: started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunImportAsync(stoppingToken);
                var delay = CalculateDelayUntilNextRun();
                _logger.LogInformation("BlsPriceImportWorker: next run in {Delay}", delay);
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BlsPriceImportWorker: error; retrying in 1 hour");
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
    }

    public async Task RunImportAsync(CancellationToken cancellationToken = default)
    {
        var filePath = _configuration["PriceImport:BlsCpi:FilePath"];
        if (string.IsNullOrWhiteSpace(filePath))
        {
            _logger.LogWarning("BlsPriceImportWorker: FilePath not configured");
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<BlsPriceImportService>();
        var result = await service.ImportFromFileAsync(filePath, cancellationToken);
        _logger.LogInformation("BlsPriceImportWorker: processed={Processed} imported={Imported}", result.Processed, result.Imported);
    }

    private static TimeSpan CalculateDelayUntilNextRun()
    {
        var now = DateTime.UtcNow;
        var next = now.Date.Add(RunTime.ToTimeSpan());
        if (next <= now) { next = next.AddDays(1); }
        return next - now;
    }
}

/// <summary>
/// Background worker that imports Walmart Kaggle grocery data.
/// Config: PriceImport:WalmartKaggle:FilePath, :Enabled (default false)
/// </summary>
public class WalmartKaggleImportWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WalmartKaggleImportWorker> _logger;
    private readonly IConfiguration _configuration;

    public WalmartKaggleImportWorker(
        IServiceProvider serviceProvider,
        ILogger<WalmartKaggleImportWorker> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = _configuration.GetValue<bool>("PriceImport:WalmartKaggle:Enabled", false);
        if (!enabled)
        {
            _logger.LogInformation("WalmartKaggleImportWorker: disabled by config");
            return;
        }

        _logger.LogInformation("WalmartKaggleImportWorker: starting one-time import");
        try
        {
            await RunImportAsync(stoppingToken);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WalmartKaggleImportWorker: import failed");
        }
    }

    public async Task RunImportAsync(CancellationToken cancellationToken = default)
    {
        var filePath = _configuration["PriceImport:WalmartKaggle:FilePath"];
        if (string.IsNullOrWhiteSpace(filePath))
        {
            _logger.LogWarning("WalmartKaggleImportWorker: FilePath not configured");
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<WalmartKaggleImportService>();
        var result = await service.ImportFromFileAsync(filePath, cancellationToken);
        _logger.LogInformation("WalmartKaggleImportWorker: processed={Processed} imported={Imported}", result.Processed, result.Imported);
    }
}

/// <summary>
/// Background worker that imports Costco Kaggle grocery data.
/// Config: PriceImport:CostcoKaggle:FilePath, :Enabled (default false)
/// </summary>
public class CostcoKaggleImportWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CostcoKaggleImportWorker> _logger;
    private readonly IConfiguration _configuration;

    public CostcoKaggleImportWorker(
        IServiceProvider serviceProvider,
        ILogger<CostcoKaggleImportWorker> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = _configuration.GetValue<bool>("PriceImport:CostcoKaggle:Enabled", false);
        if (!enabled)
        {
            _logger.LogInformation("CostcoKaggleImportWorker: disabled by config");
            return;
        }

        _logger.LogInformation("CostcoKaggleImportWorker: starting one-time import");
        try
        {
            await RunImportAsync(stoppingToken);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CostcoKaggleImportWorker: import failed");
        }
    }

    public async Task RunImportAsync(CancellationToken cancellationToken = default)
    {
        var filePath = _configuration["PriceImport:CostcoKaggle:FilePath"];
        if (string.IsNullOrWhiteSpace(filePath))
        {
            _logger.LogWarning("CostcoKaggleImportWorker: FilePath not configured");
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<CostcoKaggleImportService>();
        var result = await service.ImportFromFileAsync(filePath, cancellationToken);
        _logger.LogInformation("CostcoKaggleImportWorker: processed={Processed} imported={Imported}", result.Processed, result.Imported);
    }
}
