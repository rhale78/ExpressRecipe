namespace ExpressRecipe.SafeForkService.Contracts.Responses;

public class RecipeIngredientDto
{
    public Guid? IngredientId { get; set; }
    public string Name { get; set; } = string.Empty;
}
