namespace ExpressRecipe.ProductService.Services;

/// <summary>
/// Result of a single product/food import
/// </summary>
public class ImportResult
{
    public bool Success { get; set; }
    public Guid? ProductId { get; set; }
    public string? ExternalId { get; set; }
    public string? ProductName { get; set; }
    public bool AlreadyExists { get; set; }
    public int IngredientCount { get; set; }
    public bool HasNutrition { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Result of batch import operation
/// </summary>
public class BatchImportResult
{
    public int TotalProcessed { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public List<ImportResult> Results { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

public class ImportProgress
{
    public string Message { get; set; } = "";
    public int PercentComplete { get; set; }
    public int ProductsImported { get; set; }
}
