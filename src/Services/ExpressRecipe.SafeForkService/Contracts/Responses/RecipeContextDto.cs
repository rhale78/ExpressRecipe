namespace ExpressRecipe.SafeForkService.Contracts.Responses;

public class RecipeContextDto
{
    public Guid? RecipeId { get; set; }
    public string RecipeName { get; set; } = string.Empty;
    public bool EatingDisorderRecovery { get; set; }
}
