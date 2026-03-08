using ExpressRecipe.SafeForkService.Contracts.Responses;

namespace ExpressRecipe.SafeForkService.Contracts.Requests;

public class EvaluateRecipeRequest
{
    public List<RecipeIngredientDto> Ingredients { get; set; } = new();
    public UnionProfileDto Profile { get; set; } = new();
}
