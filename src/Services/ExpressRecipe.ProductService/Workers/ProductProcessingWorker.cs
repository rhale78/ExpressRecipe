using ExpressRecipe.ProductService.Data;
using ExpressRecipe.ProductService.Services;
using ExpressRecipe.ProductService.Logging;
using ExpressRecipe.ProductService.Messages;
using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Client.Shared.Services;
using System.Diagnostics;
using System.Threading.Tasks.Dataflow;

namespace ExpressRecipe.ProductService.Workers;

/// <summary>
/// Background service that processes staged products using efficient batch processing with TPL Dataflow
/// </summary>
public class ProductProcessingWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ProductProcessingWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly TimeSpan PROCESSING_INTERVAL = TimeSpan.FromSeconds(5);
    private const string ProcessingSessionId = "product-processing";

    public ProductProcessingWorker(
        IServiceProvider serviceProvider,
        ILogger<ProductProcessingWorker> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ProductProcessingWorker starting with optimized batch processing...");

        // Check if processing is enabled
        var processingEnabled = _configuration.GetValue<bool>("ProductImport:AutoProcessing", true);
        if (!processingEnabled)
        {
            _logger.LogInformation("Auto-processing is disabled in configuration. Worker will not run.");
            return;
        }

        // Wait for migrations to complete
        await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);

        try
        {
            await ProcessStagedProductsAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("ProductProcessingWorker is stopping.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in ProductProcessingWorker");
        }
    }

    private async Task ProcessStagedProductsAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting staged product processing with TPL Dataflow");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var stagingRepo = scope.ServiceProvider.GetRequiredService<IProductStagingRepository>();
                var productRepo = scope.ServiceProvider.GetRequiredService<IProductRepository>();
                var ingredientRepo = scope.ServiceProvider.GetRequiredService<IIngredientRepository>();
                var productImageRepo = scope.ServiceProvider.GetRequiredService<IProductImageRepository>();
                var batchProcessorLogger = scope.ServiceProvider.GetRequiredService<ILogger<BatchProductProcessor>>();

                // Check for pending products
                var pendingCount = await stagingRepo.GetPendingCountAsync();

                if (pendingCount > 0)
                {
                    _logger.LogInformation("Found {Count} pending products to process", pendingCount);

                    // Run AI verification on pending products before batch processing
                    var verificationService = scope.ServiceProvider.GetService<IProductAIVerificationService>();
                    if (verificationService != null)
                    {
                        var verifyBatch = await stagingRepo.GetPendingProductsAsync(1000);
                        if (verifyBatch.Count > 0)
                        {
                            var sw = Stopwatch.StartNew();
                            int[] verifyCounts = { 0, 0 }; // [0] = valid, [1] = invalid

                            var semaphore = new SemaphoreSlim(8);
                            var verifyTasks = verifyBatch.Select(async product =>
                            {
                                await semaphore.WaitAsync(stoppingToken);
                                try
                                {
                                    var (isValid, notes) = await verificationService.VerifyProductAsync(product, stoppingToken);
                                    await stagingRepo.UpdateAIVerificationAsync(product.Id, isValid, notes);
                                    product.AIVerificationPassed = isValid;
                                    product.AIVerificationNotes = notes;
                                    if (isValid)
                                        Interlocked.Increment(ref verifyCounts[0]);
                                    else
                                        Interlocked.Increment(ref verifyCounts[1]);
                                }
                                finally { semaphore.Release(); }
                            });
                            await Task.WhenAll(verifyTasks);

                            _logger.LogAIVerificationBatch(verifyBatch.Count, sw.ElapsedMilliseconds, verifyCounts[0], verifyCounts[1]);
                        }
                    }

                    var ingredientClient = scope.ServiceProvider.GetRequiredService<IIngredientServiceClient>();

                    // Create batch processor with optimal settings
                    var maxParallelism = _configuration.GetValue<int>("ProductImport:MaxParallelism", 4);
                    var batchSize = _configuration.GetValue<int>("ProductImport:BatchSize", 5000);
                    var bufferSize = _configuration.GetValue<int>("ProductImport:BufferSize", 50000);

                    var batchProcessor = new BatchProductProcessor(
                        batchProcessorLogger,
                        _configuration,
                        ingredientClient,
                        maxParallelism,
                        batchSize,
                        bufferSize);

                    // Process using dataflow pipeline
                    var result = await batchProcessor.ProcessStagedProductsAsync(
                        stagingRepo,
                        productRepo,
                        ingredientRepo,
                        productImageRepo,
                        stoppingToken);

                    _logger.LogInformation(
                        "Batch processing complete: {Success} succeeded, {Failed} failed",
                        result.SuccessCount,
                        result.FailureCount);

                    // Optionally publish progress update if message bus is available
                    var messageBus = scope.ServiceProvider.GetService<IMessageBus>();
                    if (messageBus != null)
                    {
                        try
                        {
                            await messageBus.PublishAsync(new ImportProgressUpdated(
                                ImportSessionId: ProcessingSessionId,
                                ServiceName: "ProductService",
                                TotalRecords: pendingCount,
                                ProcessedRecords: result.SuccessCount + result.FailureCount,
                                SuccessCount: result.SuccessCount,
                                FailureCount: result.FailureCount,
                                RecordsPerSecond: 0,
                                EstimatedTimeRemaining: null,
                                Status: "Completed",
                                UpdatedAt: DateTimeOffset.UtcNow), cancellationToken: stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to publish ImportProgressUpdated message");
                        }
                    }
                }

                // Wait before next check
                await Task.Delay(PROCESSING_INTERVAL, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during processing cycle. Will retry on next cycle.");
                await Task.Delay(PROCESSING_INTERVAL, stoppingToken);
            }
        }
    }
}
