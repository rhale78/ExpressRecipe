using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.Core.Options;
using ExpressRecipe.Shared.Messages;

namespace ExpressRecipe.PriceService.Services;

/// <summary>
/// Implements <see cref="IProductServiceClient"/> using the message bus
/// request/response pattern instead of REST HTTP calls.
/// Used when messaging is enabled; <see cref="ProductServiceClient"/> is used as fallback.
/// </summary>
public sealed class MessagingProductServiceClient : IProductServiceClient
{
    private readonly IMessageBus _bus;
    private readonly ILogger<MessagingProductServiceClient> _logger;
    private readonly TimeSpan _requestTimeout;

    public MessagingProductServiceClient(
        IMessageBus bus,
        ILogger<MessagingProductServiceClient> logger,
        IConfiguration configuration)
    {
        _bus            = bus;
        _logger         = logger;
        _requestTimeout = TimeSpan.FromSeconds(
            configuration.GetValue("PriceService:ProductLookup:MessagingTimeoutSeconds", 5));
    }

    public async Task<ProductDto?> GetProductByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(barcode))
            return null;

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_requestTimeout);

            var request = new RequestProductByBarcodeQuery(
                CorrelationId: Guid.NewGuid().ToString(),
                Barcode: barcode);

            var opts = new RequestOptions { Timeout = _requestTimeout };
            var response = await _bus.RequestAsync<RequestProductByBarcodeQuery, ProductByBarcodeResponse>(
                request, opts, cts.Token);

            if (!response.Found || response.ProductId == null)
                return null;

            return new ProductDto
            {
                Id       = response.ProductId.Value,
                UPC      = response.Barcode,
                Name     = response.Name,
                Brand    = response.Brand,
                Category = response.Category
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("[MessagingProductClient] Timeout looking up barcode {Barcode}", barcode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[MessagingProductClient] Error looking up barcode {Barcode}", barcode);
            return null;
        }
    }

    public async Task<Dictionary<string, ProductDto>> GetProductsByBarcodesAsync(
        IEnumerable<string> barcodes, CancellationToken cancellationToken = default)
    {
        var barcodeList = barcodes
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (barcodeList.Count == 0)
            return new Dictionary<string, ProductDto>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_requestTimeout);

            var request = new RequestProductsByBarcodesQuery(
                CorrelationId: Guid.NewGuid().ToString(),
                Barcodes: barcodeList);

            var opts = new RequestOptions { Timeout = _requestTimeout };
            var response = await _bus.RequestAsync<RequestProductsByBarcodesQuery, ProductsByBarcodesResponse>(
                request, opts, cts.Token);

            var result = new Dictionary<string, ProductDto>(StringComparer.OrdinalIgnoreCase);
            foreach (var (barcode, item) in response.Products)
            {
                if (item.Found && item.ProductId != null)
                {
                    result[barcode] = new ProductDto
                    {
                        Id       = item.ProductId.Value,
                        UPC      = item.Barcode,
                        Name     = item.Name,
                        Brand    = item.Brand,
                        Category = item.Category
                    };
                }
            }

            _logger.LogInformation("[MessagingProductClient] Bulk lookup: {Requested} barcodes -> {Found} products",
                barcodeList.Count, result.Count);

            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("[MessagingProductClient] Timeout on bulk barcode lookup: {Count} barcodes", barcodeList.Count);
            return new Dictionary<string, ProductDto>(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[MessagingProductClient] Error on bulk barcode lookup: {Count} barcodes", barcodeList.Count);
            return new Dictionary<string, ProductDto>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
