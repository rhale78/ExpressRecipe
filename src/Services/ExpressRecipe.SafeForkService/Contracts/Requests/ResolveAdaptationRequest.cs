using ExpressRecipe.SafeForkService.Contracts.Responses;

namespace ExpressRecipe.SafeForkService.Contracts.Requests;

public class ResolveAdaptationRequest
{
    public ConflictReport ConflictReport { get; set; } = new();
    public Guid HouseholdId { get; set; }
    public Guid? RecipeInstanceId { get; set; }
}
