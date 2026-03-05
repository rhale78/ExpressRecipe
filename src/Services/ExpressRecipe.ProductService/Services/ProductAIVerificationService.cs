using ExpressRecipe.ProductService.Data;
using Microsoft.Extensions.Logging;

namespace ExpressRecipe.ProductService.Services;

public interface IProductAIVerificationService
{
    Task<(bool IsValid, string? Notes)> VerifyProductAsync(StagedProduct product, CancellationToken cancellationToken = default);
}

public class ProductAIVerificationService : IProductAIVerificationService
{
    private readonly ILogger<ProductAIVerificationService> _logger;

    public ProductAIVerificationService(ILogger<ProductAIVerificationService> logger)
    {
        _logger = logger;
    }

    public Task<(bool IsValid, string? Notes)> VerifyProductAsync(StagedProduct product, CancellationToken cancellationToken = default)
    {
        // Local AI/heuristic verification - no network required
        var issues = new List<string>();

        // Check product name
        if (string.IsNullOrWhiteSpace(product.ProductName))
            issues.Add("Missing product name");
        else if (product.ProductName.Length > 500)
            issues.Add($"Product name too long ({product.ProductName.Length} chars)");

        // Check barcode format (if present)
        // Valid lengths: EAN-8 (8), UPC-A (12), EAN-13 (13), GTIN-14 (14)
        if (!string.IsNullOrWhiteSpace(product.Barcode))
        {
            var digits = product.Barcode.Trim().Replace("-", "").Replace(" ", "");
            if (!digits.All(char.IsDigit))
                issues.Add($"Barcode contains non-digit characters: {product.Barcode}");
            else if (digits.Length is not (8 or 12 or 13 or 14))
                issues.Add($"Barcode length {digits.Length} is not a standard EAN/UPC format");
        }

        // Check ingredients text is not clearly corrupted
        if (!string.IsNullOrWhiteSpace(product.IngredientsText))
        {
            var text = product.IngredientsText;
            // Check for obviously broken encodings
            if (text.Contains("ï¿½") || text.Contains("â€"))
                issues.Add("Ingredients text contains encoding artifacts");
            // Check if it's just a list of numbers (corrupted)
            if (text.Length > 10 && text.All(c => char.IsDigit(c) || c == ' ' || c == ','))
                issues.Add("Ingredients text appears to be numeric only (corrupted)");
        }

        // Check ExternalId is present
        if (string.IsNullOrWhiteSpace(product.ExternalId))
            issues.Add("Missing external ID");

        bool isValid = issues.Count == 0;
        string? notes = issues.Count > 0 ? string.Join("; ", issues) : null;

        _logger.LogDebug("[ProductAIVerification] Product {ExternalId}: Valid={IsValid}, Notes={Notes}",
            product.ExternalId, isValid, notes);

        return Task.FromResult((isValid, notes));
    }
}
