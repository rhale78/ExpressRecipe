using ExpressRecipe.Messaging.Saga.Abstractions;

namespace ExpressRecipe.SafeForkService.Saga;

public class AllergenResolutionSagaState : ISagaState
{
    public string CorrelationId { get; set; } = string.Empty;
    public long CurrentMask { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public SagaStatus Status { get; set; }

    public Guid AllergenProfileId { get; set; }
    public Guid MemberId { get; set; }
    public string FreeFormText { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public Guid? IngredientId { get; set; }
    public Guid? ProductId { get; set; }
    public string? MatchMethod { get; set; }
    public int LinksWritten { get; set; }
    public string? LastError { get; set; }
}
