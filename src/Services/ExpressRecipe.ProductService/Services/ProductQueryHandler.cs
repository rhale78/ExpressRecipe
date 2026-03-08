using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.Core.Messages;
using ExpressRecipe.ProductService.Data;
using ExpressRecipe.Shared.Messages;

namespace ExpressRecipe.ProductService.Services;

/// <summary>
/// Handles incoming barcode query messages from other services (e.g. PriceService).
/// Responds via the request/response pattern so callers never need to make REST calls
/// when messaging is enabled.
/// </summary>
public sealed class ProductQueryHandler :
    IRequestHandler<RequestProductByBarcodeQuery, ProductByBarcodeResponse>,
    IRequestHandler<RequestProductsByBarcodesQuery, ProductsByBarcodesResponse>
{
    private readonly IProductRepository _productRepository;
    private readonly ILogger<ProductQueryHandler> _logger;

    public ProductQueryHandler(
        IProductRepository productRepository,
        ILogger<ProductQueryHandler> logger)
    {
        _productRepository = productRepository;
        _logger = logger;
    }

    public async Task<ProductByBarcodeResponse> HandleAsync(
        RequestProductByBarcodeQuery request,
        MessageContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[ProductQueryHandler] Barcode lookup: {Barcode}", request.Barcode);

        var product = await _productRepository.GetByBarcodeAsync(request.Barcode);

        if (product == null)
        {
            return new ProductByBarcodeResponse(
                request.CorrelationId,
                request.Barcode,
                Found: false,
                ProductId: null,
                Name: null,
                Brand: null,
                Category: null);
        }

        return new ProductByBarcodeResponse(
            request.CorrelationId,
            request.Barcode,
            Found: true,
            ProductId: product.Id,
            Name: product.Name,
            Brand: product.Brand,
            Category: product.Category);
    }

    public async Task<ProductsByBarcodesResponse> HandleAsync(
        RequestProductsByBarcodesQuery request,
        MessageContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[ProductQueryHandler] Bulk barcode lookup: {Count} barcodes", request.Barcodes.Count);

        var products = await _productRepository.GetByBarcodesAsync(request.Barcodes);

        var responseMap = new Dictionary<string, ProductByBarcodeResponse>(StringComparer.OrdinalIgnoreCase);

        foreach (var barcode in request.Barcodes)
        {
            if (products.TryGetValue(barcode, out var product))
            {
                responseMap[barcode] = new ProductByBarcodeResponse(
                    request.CorrelationId,
                    barcode,
                    Found: true,
                    ProductId: product.Id,
                    Name: product.Name,
                    Brand: product.Brand,
                    Category: product.Category);
            }
            else
            {
                responseMap[barcode] = new ProductByBarcodeResponse(
                    request.CorrelationId,
                    barcode,
                    Found: false,
                    ProductId: null,
                    Name: null,
                    Brand: null,
                    Category: null);
            }
        }

        return new ProductsByBarcodesResponse(request.CorrelationId, responseMap);
    }
}
