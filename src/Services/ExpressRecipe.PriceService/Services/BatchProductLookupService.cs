using System.Collections.Concurrent;
using System.Threading.Tasks.Dataflow;
using ExpressRecipe.Shared.Models;

namespace ExpressRecipe.PriceService.Services;

/// <summary>
/// Batches product lookups using TPL Dataflow to reduce strain on ProductService
/// </summary>
public interface IBatchProductLookupService
{
    Task<ProductDto?> GetProductByBarcodeAsync(string barcode, CancellationToken cancellationToken = default);
    Task<Dictionary<string, ProductDto>> GetProductsByBarcodesAsync(IEnumerable<string> barcodes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pre-populates the cache with a known product, avoiding a round-trip to ProductService.
    /// Called by <see cref="ProductEventSubscriber"/> when product lifecycle events are received.
    /// </summary>
    void CacheProduct(string barcode, ProductDto product);
}

public class BatchProductLookupService : IBatchProductLookupService, IDisposable
{
    private readonly IProductServiceClient _productServiceClient;
    private readonly ILogger<BatchProductLookupService> _logger;
    private readonly IConfiguration _configuration;
    
    // Dataflow blocks
    private readonly BatchBlock<ProductLookupRequest> _batchBlock;
    private readonly ActionBlock<ProductLookupRequest[]> _processorBlock;
    
    // Cache for recently looked up products
    private readonly ConcurrentDictionary<string, ProductDto?> _productCache;
    private readonly TimeSpan _cacheExpiration;
    
    // Pending requests waiting for batched response
    private readonly ConcurrentDictionary<string, TaskCompletionSource<ProductDto?>> _pendingRequests;
    
    // Configuration
    private readonly int _batchSize;
    private readonly TimeSpan _batchTimeout;
    
    public BatchProductLookupService(
        IProductServiceClient productServiceClient,
        ILogger<BatchProductLookupService> logger,
        IConfiguration configuration)
    {
        _productServiceClient = productServiceClient;
        _logger = logger;
        _configuration = configuration;
        
        _batchSize = configuration.GetValue("PriceService:ProductLookup:BatchSize", 100);
        _batchTimeout = TimeSpan.FromMilliseconds(configuration.GetValue("PriceService:ProductLookup:BatchTimeoutMs", 500));
        _cacheExpiration = TimeSpan.FromMinutes(configuration.GetValue("PriceService:ProductLookup:CacheMinutes", 30));
        
        _productCache = new ConcurrentDictionary<string, ProductDto?>(StringComparer.OrdinalIgnoreCase);
        _pendingRequests = new ConcurrentDictionary<string, TaskCompletionSource<ProductDto?>>(StringComparer.OrdinalIgnoreCase);
        
        // Create batching block
        _batchBlock = new BatchBlock<ProductLookupRequest>(
            _batchSize,
            new GroupingDataflowBlockOptions
            {
                BoundedCapacity = _batchSize * 10, // Limit memory usage
                EnsureOrdered = false
            });
        
        // Create processor block
        _processorBlock = new ActionBlock<ProductLookupRequest[]>(
            async batch => await ProcessBatchAsync(batch),
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = configuration.GetValue("PriceService:ProductLookup:MaxParallelBatches", 4),
                BoundedCapacity = 20
            });
        
        // Link the blocks
        _batchBlock.LinkTo(_processorBlock, new DataflowLinkOptions { PropagateCompletion = true });
        
        // Start batch timeout timer
        _ = TriggerBatchTimeoutAsync();
        
        _logger.LogInformation("BatchProductLookupService initialized: BatchSize={BatchSize}, Timeout={Timeout}ms, CacheExpiration={CacheMinutes}min",
            _batchSize, _batchTimeout.TotalMilliseconds, _cacheExpiration.TotalMinutes);
    }
    
    public void CacheProduct(string barcode, ProductDto product)
    {
        if (string.IsNullOrWhiteSpace(barcode))
            return;

        _productCache[barcode.Trim()] = product;
    }

    public async Task<ProductDto?> GetProductByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(barcode))
            return null;
        
        barcode = barcode.Trim();
        
        // Check cache first
        if (_productCache.TryGetValue(barcode, out var cachedProduct))
        {
            return cachedProduct;
        }
        
        // Create a task completion source for this request
        var tcs = _pendingRequests.GetOrAdd(barcode, _ => new TaskCompletionSource<ProductDto?>(TaskCreationOptions.RunContinuationsAsynchronously));
        
        // If we just created this TCS, post the request to the batch
        if (tcs.Task.Status == TaskStatus.WaitingForActivation)
        {
            var request = new ProductLookupRequest
            {
                Barcode = barcode,
                CompletionSource = tcs,
                RequestedAt = DateTime.UtcNow
            };
            
            if (!await _batchBlock.SendAsync(request, cancellationToken))
            {
                _logger.LogWarning("Failed to queue product lookup for barcode {Barcode}", barcode);
                _pendingRequests.TryRemove(barcode, out _);
                tcs.TrySetResult(null);
            }
        }
        
        // Wait for the batched result
        return await tcs.Task;
    }
    
    public async Task<Dictionary<string, ProductDto>> GetProductsByBarcodesAsync(IEnumerable<string> barcodes, CancellationToken cancellationToken = default)
    {
        var tasks = barcodes
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(async barcode =>
            {
                var product = await GetProductByBarcodeAsync(barcode, cancellationToken);
                return (Barcode: barcode, Product: product);
            });
        
        var results = await Task.WhenAll(tasks);
        
        return results
            .Where(r => r.Product != null)
            .ToDictionary(r => r.Barcode, r => r.Product!, StringComparer.OrdinalIgnoreCase);
    }
    
    private async Task ProcessBatchAsync(ProductLookupRequest[] batch)
    {
        if (batch.Length == 0) return;
        
        _logger.LogInformation("Processing product lookup batch: {Count} barcodes", batch.Length);
        
        try
        {
            // For now, we'll call ProductService individually for each item in the batch
            // TODO: Add bulk lookup endpoint to ProductService for better efficiency
            var lookupTasks = batch.Select(async request =>
            {
                try
                {
                    var product = await _productServiceClient.GetProductByBarcodeAsync(request.Barcode, CancellationToken.None);
                    
                    // Cache the result
                    _productCache.TryAdd(request.Barcode, product);
                    
                    // Complete the request
                    _pendingRequests.TryRemove(request.Barcode, out _);
                    request.CompletionSource.TrySetResult(product);
                    
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to lookup product for barcode {Barcode}", request.Barcode);
                    
                    _pendingRequests.TryRemove(request.Barcode, out _);
                    request.CompletionSource.TrySetResult(null);
                    
                    return false;
                }
            });
            
            var results = await Task.WhenAll(lookupTasks);
            var successCount = results.Count(r => r);
            
            _logger.LogInformation("Batch complete: {Success}/{Total} successful lookups", successCount, batch.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process product lookup batch");
            
            // Fail all pending requests in this batch
            foreach (var request in batch)
            {
                _pendingRequests.TryRemove(request.Barcode, out _);
                request.CompletionSource.TrySetResult(null);
            }
        }
    }
    
    private async Task TriggerBatchTimeoutAsync()
    {
        while (true)
        {
            await Task.Delay(_batchTimeout);
            
            try
            {
                // Trigger batch even if not full
                _batchBlock.TriggerBatch();
            }
            catch
            {
                // Block might be completed, ignore
                break;
            }
        }
    }
    
    public void Dispose()
    {
        _batchBlock.Complete();
        _processorBlock.Completion.Wait(TimeSpan.FromSeconds(5));
    }
    
    private class ProductLookupRequest
    {
        public string Barcode { get; set; } = string.Empty;
        public TaskCompletionSource<ProductDto?> CompletionSource { get; set; } = null!;
        public DateTime RequestedAt { get; set; }
    }
}
