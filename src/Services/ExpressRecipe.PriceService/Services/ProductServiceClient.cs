using ExpressRecipe.Shared.Models;
using System.Net.Http.Json;

namespace ExpressRecipe.PriceService.Services;

public interface IProductServiceClient
{
    Task<ProductDto?> GetProductByBarcodeAsync(string barcode, CancellationToken cancellationToken = default);
    Task<Dictionary<string, ProductDto>> GetProductsByBarcodesAsync(IEnumerable<string> barcodes, CancellationToken cancellationToken = default);
}

public class ProductServiceClient : IProductServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ProductServiceClient> _logger;

    public ProductServiceClient(HttpClient httpClient, ILogger<ProductServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ProductDto?> GetProductByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
    {
        try
        {
            // Use a shorter timeout for this specific request to fail faster and not block price imports
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);

            var response = await _httpClient.GetAsync($"api/products/barcode/{barcode}", linkedCts.Token);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<ProductDto>(linkedCts.Token);
            }

            if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("ProductService lookup failed: {Barcode} -> {StatusCode}", barcode, response.StatusCode);
            }

            return null;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("ProductService timeout: {Barcode}", barcode);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "ProductService unavailable: {Barcode}", barcode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ProductService error: {Barcode}", barcode);
            return null;
        }
    }

    public async Task<Dictionary<string, ProductDto>> GetProductsByBarcodesAsync(IEnumerable<string> barcodes, CancellationToken cancellationToken = default)
    {
        try
        {
            var barcodeList = barcodes.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            if (!barcodeList.Any())
            {
                return new Dictionary<string, ProductDto>(StringComparer.OrdinalIgnoreCase);
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);

            var request = new { Barcodes = barcodeList };
            var response = await _httpClient.PostAsJsonAsync("api/products/barcode/bulk", request, linkedCts.Token);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<Dictionary<string, ProductDto>>(linkedCts.Token) 
                    ?? new Dictionary<string, ProductDto>(StringComparer.OrdinalIgnoreCase);

                sw.Stop();
                _logger.LogInformation("[ProductService] Bulk lookup: {Requested} barcodes -> {Found} products in {Ms}ms",
                    barcodeList.Count, result.Count, sw.ElapsedMilliseconds);

                return result;
            }

            sw.Stop();
            _logger.LogWarning("[ProductService] Bulk lookup failed: {Requested} barcodes -> {StatusCode} in {Ms}ms", 
                barcodeList.Count, response.StatusCode, sw.ElapsedMilliseconds);
            return new Dictionary<string, ProductDto>(StringComparer.OrdinalIgnoreCase);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("[ProductService] Bulk lookup timeout: {Count} barcodes", barcodes.Count());
            return new Dictionary<string, ProductDto>(StringComparer.OrdinalIgnoreCase);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "[ProductService] Unavailable for bulk lookup: {Count} barcodes", barcodes.Count());
            return new Dictionary<string, ProductDto>(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ProductService] Bulk lookup error: {Count} barcodes", barcodes.Count());
            return new Dictionary<string, ProductDto>(StringComparer.OrdinalIgnoreCase);
        }
    }
}

public class ProductDto
{
    public Guid Id { get; set; }
    public string? UPC { get; set; }
    public string? Name { get; set; }
    public string? Brand { get; set; }
    public string? Category { get; set; }
}
