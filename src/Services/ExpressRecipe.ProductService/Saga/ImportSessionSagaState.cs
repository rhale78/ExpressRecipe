using ExpressRecipe.Messaging.Saga.Abstractions;

namespace ExpressRecipe.ProductService.Saga;

/// <summary>
/// Tracks the overall state of an import session (e.g., OpenFoodFacts import).
/// </summary>
public class ImportSessionSagaState : ISagaState
{
    // ISagaState required fields
    public string CorrelationId { get; set; } = string.Empty;
    public long CurrentMask { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public SagaStatus Status { get; set; }

    // Domain-specific fields
    public string ServiceName { get; set; } = string.Empty;
    public string DataSourceName { get; set; } = string.Empty; // "OpenFoodFacts", "USDA", "OpenPrices", "RecipeDataset"
    public int TotalRecords { get; set; }
    public int ProcessedRecords { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public double RecordsPerSecond { get; set; }
    public DateTimeOffset? EstimatedCompletionAt { get; set; }
    public string? FilePath { get; set; }
    public string? SourceUrl { get; set; }
}
