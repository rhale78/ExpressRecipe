namespace ExpressRecipe.SafeForkService.Contracts.Responses;

public class SubstituteDto
{
    public Guid? IngredientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}
