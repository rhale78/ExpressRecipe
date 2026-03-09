namespace ExpressRecipe.SafeForkService.Contracts.Responses;

public class RecipeEvaluationResult
{
    public bool IsSafe { get; set; }
    public ConflictReport ConflictReport { get; set; } = new();
    public string SuggestedStrategy { get; set; } = "AdaptAll";
}
