namespace ExpressRecipe.VisionService.Services;

public interface IProductNameMatcher
{
    Task<ProductMatchResult?> MatchAsync(string text, CancellationToken ct = default);
}

public class ProductMatchResult
{
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string? Brand { get; init; }
    public double Score { get; init; }
}

/// <summary>
/// HTTP implementation that calls ProductService GET /api/products/search?q={text}&amp;limit=3
/// </summary>
public class ProductServiceNameMatcher : IProductNameMatcher
{
    private const double DefaultMatchScore = 0.8;

    private readonly HttpClient _httpClient;
    private readonly ILogger<ProductServiceNameMatcher> _logger;

    public ProductServiceNameMatcher(HttpClient httpClient, ILogger<ProductServiceNameMatcher> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ProductMatchResult?> MatchAsync(string text, CancellationToken ct = default)
    {
        try
        {
            string encoded = Uri.EscapeDataString(text);
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/products/search?q={encoded}&limit=3", ct);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            List<ProductSearchItem>? items = await response.Content.ReadFromJsonAsync<List<ProductSearchItem>>(cancellationToken: ct);

            if (items == null || items.Count == 0)
            {
                return null;
            }

            ProductSearchItem top = items[0];
            return new ProductMatchResult
            {
                ProductId = top.Id,
                ProductName = top.Name,
                Brand = top.Brand,
                Score = DefaultMatchScore
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ProductNameMatcher failed for text: {Text}", text);
            return null;
        }
    }
}

internal class ProductSearchItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Brand { get; set; }
}
