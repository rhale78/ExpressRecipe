using System.Threading.Tasks.Dataflow;
using ExpressRecipe.PriceService.Data;

namespace ExpressRecipe.PriceService.Services;

/// <summary>
/// Batches price data inserts using TPL Dataflow for high-throughput scenarios
/// </summary>
public interface IBatchPriceInsertService
{
    Task<bool> QueuePriceAsync(UpsertProductPriceRequest price, CancellationToken cancellationToken = default);
    Task<int> FlushAsync(CancellationToken cancellationToken = default);
    Task ShutdownAsync();
}

public class BatchPriceInsertService : IBatchPriceInsertService, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BatchPriceInsertService> _logger;
    private readonly IConfiguration _configuration;

    // Dataflow blocks
    private readonly BatchBlock<PriceInsertRequest> _batchBlock;
    private readonly ActionBlock<PriceInsertRequest[]> _processorBlock;

    // Statistics
    private long _totalQueued;
    private long _totalProcessed;
    private long _totalErrors;

    // Configuration
    private readonly int _batchSize;
    private readonly TimeSpan _batchTimeout;
    private readonly CancellationTokenSource _timerCts;

    public BatchPriceInsertService(
        IServiceProvider serviceProvider,
        ILogger<BatchPriceInsertService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
        
        _batchSize = configuration.GetValue("PriceService:PriceBatch:BatchSize", 1000);
        _batchTimeout = TimeSpan.FromMilliseconds(configuration.GetValue("PriceService:PriceBatch:BatchTimeoutMs", 2000));
        
        // Create batching block - groups incoming prices into batches
        _batchBlock = new BatchBlock<PriceInsertRequest>(
            _batchSize,
            new GroupingDataflowBlockOptions
            {
                BoundedCapacity = _batchSize * 50, // Limit memory to ~50 batches
                EnsureOrdered = true
            });
        
        // Create processor block - writes batches to database
        _processorBlock = new ActionBlock<PriceInsertRequest[]>(
            async batch => await ProcessBatchAsync(batch),
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = configuration.GetValue("PriceService:PriceBatch:MaxParallelBatches", 2),
                BoundedCapacity = 10 // Limit to 10 batches in flight
            });
        
        // Link the blocks
        _batchBlock.LinkTo(_processorBlock, new DataflowLinkOptions { PropagateCompletion = true });
        
        // Start batch timeout timer
        _timerCts = new CancellationTokenSource();
        _ = TriggerBatchTimeoutAsync(_timerCts.Token);
        
        _logger.LogInformation("BatchPriceInsertService initialized: BatchSize={BatchSize}, Timeout={Timeout}ms",
            _batchSize, _batchTimeout.TotalMilliseconds);
    }
    
    public async Task<bool> QueuePriceAsync(UpsertProductPriceRequest price, CancellationToken cancellationToken = default)
    {
        var request = new PriceInsertRequest
        {
            Price = price,
            QueuedAt = DateTime.UtcNow
        };
        
        var queued = await _batchBlock.SendAsync(request, cancellationToken);
        
        if (queued)
        {
            Interlocked.Increment(ref _totalQueued);
        }
        else
        {
            _logger.LogWarning("Failed to queue price for product {ProductId}", price.ProductId);
        }
        
        return queued;
    }
    
    public async Task<int> FlushAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Flushing price batch queue...");
        
        // Trigger immediate batch processing
        _batchBlock.TriggerBatch();
        
        // Wait a bit for processing
        await Task.Delay(500, cancellationToken);
        
        return (int)_totalProcessed;
    }
    
    public async Task ShutdownAsync()
    {
        _logger.LogInformation("Shutting down BatchPriceInsertService. Queued={Queued}, Processed={Processed}, Errors={Errors}",
            _totalQueued, _totalProcessed, _totalErrors);
        
        // Stop accepting new items
        _batchBlock.Complete();
        
        // Wait for processing to complete
        await _processorBlock.Completion.WaitAsync(TimeSpan.FromSeconds(30));
        
        _timerCts.Cancel();
    }
    
    private async Task ProcessBatchAsync(PriceInsertRequest[] batch)
    {
        if (batch.Length == 0) return;

        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var prices = batch.Select(r => r.Price).ToList();

            _logger.LogInformation("Inserting batch of {Count} prices to database", batch.Length);

            // Create scope to resolve scoped IPriceRepository
            using var scope = _serviceProvider.CreateScope();
            var priceRepository = scope.ServiceProvider.GetRequiredService<IPriceRepository>();

            var inserted = await priceRepository.BulkUpsertProductPricesAsync(prices);

            Interlocked.Add(ref _totalProcessed, inserted);

            sw.Stop();

            var avgQueueTime = batch.Average(r => (DateTime.UtcNow - r.QueuedAt).TotalMilliseconds);

            _logger.LogInformation(
                "Price batch complete: {Inserted}/{Total} inserted in {Elapsed}ms. Avg queue time: {AvgQueue}ms. Rate: {Rate:F0} prices/sec",
                inserted, batch.Length, sw.ElapsedMilliseconds, avgQueueTime, inserted / sw.Elapsed.TotalSeconds);

            // Sample logging for first and last items
            if (batch.Length > 0)
            {
                var first = batch[0].Price;
                var last = batch[^1].Price;

                _logger.LogDebug(
                    "Batch range: First=[{FirstUpc}] {FirstProduct} ${FirstPrice}, Last=[{LastUpc}] {LastProduct} ${LastPrice}",
                    first.Upc ?? "N/A", first.ProductName, first.Price,
                    last.Upc ?? "N/A", last.ProductName, last.Price);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process price batch of {Count} items", batch.Length);
            Interlocked.Add(ref _totalErrors, batch.Length);
        }
    }
    
    private async Task TriggerBatchTimeoutAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_batchTimeout, cancellationToken);
                
                // Trigger batch even if not full
                _batchBlock.TriggerBatch();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in batch timeout trigger");
            }
        }
    }
    
    public void Dispose()
    {
        _timerCts?.Cancel();
        _timerCts?.Dispose();
        _batchBlock.Complete();
        
        try
        {
            _processorBlock.Completion.Wait(TimeSpan.FromSeconds(10));
        }
        catch
        {
            // Ignore timeout on disposal
        }
    }
    
    private class PriceInsertRequest
    {
        public UpsertProductPriceRequest Price { get; set; } = null!;
        public DateTime QueuedAt { get; set; }
    }
}
