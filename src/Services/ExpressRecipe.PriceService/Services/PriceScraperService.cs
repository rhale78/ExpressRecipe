using HtmlAgilityPack;
using System.Text.RegularExpressions;

namespace ExpressRecipe.PriceService.Services;

/// <summary>
/// Service for scraping product prices from retailer websites
/// Note: This is a basic implementation. Production use requires:
/// - Respecting robots.txt
/// - Rate limiting
/// - User agent rotation
/// - Legal compliance with terms of service
/// </summary>
public class PriceScraperService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PriceScraperService> _logger;

    public PriceScraperService(HttpClient httpClient, ILogger<PriceScraperService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        // Set user agent to mimic browser
        _httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Accept",
            "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
    }

    /// <summary>
    /// Scrape price from Walmart
    /// </summary>
    public async Task<PriceData?> ScrapeWalmartAsync(string productUrl)
    {
        try
        {
            _logger.LogInformation("Scraping Walmart price from: {Url}", productUrl);

            var html = await _httpClient.GetStringAsync(productUrl);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Walmart price selectors (these change frequently - need monitoring)
            var priceNode = doc.DocumentNode.SelectSingleNode("//span[@itemprop='price']")
                         ?? doc.DocumentNode.SelectSingleNode("//span[contains(@class, 'price-characteristic')]");

            if (priceNode == null)
            {
                _logger.LogWarning("Could not find price on Walmart page");
                return null;
            }

            var priceText = priceNode.InnerText.Trim();
            var price = ExtractPrice(priceText);

            if (price == null)
                return null;

            // Extract product name
            var nameNode = doc.DocumentNode.SelectSingleNode("//h1[@itemprop='name']")
                        ?? doc.DocumentNode.SelectSingleNode("//h1");
            var productName = nameNode?.InnerText.Trim();

            return new PriceData
            {
                Retailer = "Walmart",
                RetailerUrl = productUrl,
                Price = price.Value,
                Currency = "USD",
                ProductName = productName,
                InStock = !html.Contains("out of stock", StringComparison.OrdinalIgnoreCase),
                ScrapedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scrape Walmart price from: {Url}", productUrl);
            return null;
        }
    }

    /// <summary>
    /// Scrape price from Target
    /// </summary>
    public async Task<PriceData?> ScrapeTargetAsync(string productUrl)
    {
        try
        {
            _logger.LogInformation("Scraping Target price from: {Url}", productUrl);

            var html = await _httpClient.GetStringAsync(productUrl);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Target price selectors
            var priceNode = doc.DocumentNode.SelectSingleNode("//span[@data-test='product-price']")
                         ?? doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'styles__PriceFontSize')]");

            if (priceNode == null)
            {
                _logger.LogWarning("Could not find price on Target page");
                return null;
            }

            var priceText = priceNode.InnerText.Trim();
            var price = ExtractPrice(priceText);

            if (price == null)
                return null;

            // Extract product name
            var nameNode = doc.DocumentNode.SelectSingleNode("//h1[@data-test='product-title']")
                        ?? doc.DocumentNode.SelectSingleNode("//h1");
            var productName = nameNode?.InnerText.Trim();

            return new PriceData
            {
                Retailer = "Target",
                RetailerUrl = productUrl,
                Price = price.Value,
                Currency = "USD",
                ProductName = productName,
                InStock = !html.Contains("out of stock", StringComparison.OrdinalIgnoreCase),
                ScrapedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scrape Target price from: {Url}", productUrl);
            return null;
        }
    }

    /// <summary>
    /// Scrape price from Amazon
    /// Note: Amazon actively blocks scrapers - use Product Advertising API instead for production
    /// </summary>
    public async Task<PriceData?> ScrapeAmazonAsync(string productUrl)
    {
        try
        {
            _logger.LogInformation("Scraping Amazon price from: {Url}", productUrl);

            var html = await _httpClient.GetStringAsync(productUrl);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Amazon price selectors (frequently updated to block scrapers)
            var priceNode = doc.DocumentNode.SelectSingleNode("//span[@class='a-price-whole']")
                         ?? doc.DocumentNode.SelectSingleNode("//span[@id='priceblock_ourprice']")
                         ?? doc.DocumentNode.SelectSingleNode("//span[@id='priceblock_dealprice']");

            if (priceNode == null)
            {
                _logger.LogWarning("Could not find price on Amazon page (consider using Product Advertising API)");
                return null;
            }

            var priceText = priceNode.InnerText.Trim();
            var price = ExtractPrice(priceText);

            if (price == null)
                return null;

            // Extract product name
            var nameNode = doc.DocumentNode.SelectSingleNode("//span[@id='productTitle']")
                        ?? doc.DocumentNode.SelectSingleNode("//h1[@id='title']");
            var productName = nameNode?.InnerText.Trim();

            // Check availability
            var availNode = doc.DocumentNode.SelectSingleNode("//div[@id='availability']");
            var inStock = availNode?.InnerText.Contains("In Stock", StringComparison.OrdinalIgnoreCase) ?? false;

            return new PriceData
            {
                Retailer = "Amazon",
                RetailerUrl = productUrl,
                Price = price.Value,
                Currency = "USD",
                ProductName = productName,
                InStock = inStock,
                ScrapedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scrape Amazon price from: {Url}", productUrl);
            return null;
        }
    }

    /// <summary>
    /// Scrape price from Kroger
    /// </summary>
    public async Task<PriceData?> ScrapeKrogerAsync(string productUrl)
    {
        try
        {
            _logger.LogInformation("Scraping Kroger price from: {Url}", productUrl);

            var html = await _httpClient.GetStringAsync(productUrl);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Kroger price selectors
            var priceNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'ProductDetails-sellBy-price')]")
                         ?? doc.DocumentNode.SelectSingleNode("//span[contains(@class, 'kds-Price')]");

            if (priceNode == null)
            {
                _logger.LogWarning("Could not find price on Kroger page");
                return null;
            }

            var priceText = priceNode.InnerText.Trim();
            var price = ExtractPrice(priceText);

            if (price == null)
                return null;

            // Extract product name
            var nameNode = doc.DocumentNode.SelectSingleNode("//h1[contains(@class, 'ProductDetails')]")
                        ?? doc.DocumentNode.SelectSingleNode("//h1");
            var productName = nameNode?.InnerText.Trim();

            return new PriceData
            {
                Retailer = "Kroger",
                RetailerUrl = productUrl,
                Price = price.Value,
                Currency = "USD",
                ProductName = productName,
                InStock = !html.Contains("out of stock", StringComparison.OrdinalIgnoreCase),
                ScrapedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scrape Kroger price from: {Url}", productUrl);
            return null;
        }
    }

    /// <summary>
    /// Extract decimal price from various formatted price strings
    /// </summary>
    private decimal? ExtractPrice(string priceText)
    {
        try
        {
            // Remove currency symbols and non-numeric characters except decimal point
            var cleanText = Regex.Replace(priceText, @"[^\d\.]", "");

            if (string.IsNullOrWhiteSpace(cleanText))
                return null;

            if (decimal.TryParse(cleanText, out var price))
            {
                return price;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Search for product by UPC/EAN across multiple retailers
    /// </summary>
    public async Task<List<PriceData>> SearchByBarcodeAsync(string barcode)
    {
        var results = new List<PriceData>();

        // In production, this would query retailer APIs or databases
        // For now, this is a placeholder
        _logger.LogInformation("Searching prices for barcode: {Barcode}", barcode);

        // TODO: Implement actual barcode-based price lookups
        // Options:
        // 1. Google Shopping API
        // 2. Individual retailer APIs (Walmart, Target, etc.)
        // 3. Third-party price comparison APIs

        return results;
    }
}

/// <summary>
/// Price data from retailer
/// </summary>
public class PriceData
{
    public string Retailer { get; set; } = string.Empty;
    public string RetailerUrl { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Currency { get; set; } = "USD";
    public string? ProductName { get; set; }
    public bool InStock { get; set; }
    public DateTime ScrapedAt { get; set; }
    public string? ImageUrl { get; set; }
    public string? Description { get; set; }
}
