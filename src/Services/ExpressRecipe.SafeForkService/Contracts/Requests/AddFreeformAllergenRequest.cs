namespace ExpressRecipe.SafeForkService.Contracts.Requests;

public class AddFreeformAllergenRequest
{
    public string FreeFormText { get; set; } = string.Empty;
    public string? Brand { get; set; }
}
