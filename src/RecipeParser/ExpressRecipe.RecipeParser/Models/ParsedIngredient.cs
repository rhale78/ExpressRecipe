namespace ExpressRecipe.RecipeParser.Models;

public sealed class ParsedIngredient
{
    public string? Quantity { get; set; }
    public string? Unit { get; set; }
    public string Name { get; set; } = "";
    public string? Preparation { get; set; }
    public bool IsOptional { get; set; }
    public string? GroupHeading { get; set; }
    public int Column { get; set; } = 1;
}
