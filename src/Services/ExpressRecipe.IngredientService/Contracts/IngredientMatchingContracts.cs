using System.ComponentModel.DataAnnotations;

namespace ExpressRecipe.IngredientService.Contracts;

public sealed class IngredientMatchRequest
{
    [Required]
    public string Text { get; init; } = string.Empty;
    public string? SourceService { get; init; }
}

public sealed class IngredientBulkMatchRequest
{
    [Required]
    public List<string> Texts { get; init; } = new();
    public string? SourceService { get; init; }
    public Guid? SourceEntityId { get; init; }
}

public sealed class ConfirmMatchRequest
{
    [Required]
    public Guid IngredientId { get; init; }
    public bool CreateAlias { get; init; } = true;
    [Required]
    public string ResolvedBy { get; init; } = string.Empty;
}

public sealed class CreateAndResolveRequest
{
    [Required]
    public string NewIngredientName { get; init; } = string.Empty;
    public string Category { get; init; } = "General";
}

public sealed class RejectQueueItemRequest
{
    public string Reason { get; init; } = string.Empty;
}
