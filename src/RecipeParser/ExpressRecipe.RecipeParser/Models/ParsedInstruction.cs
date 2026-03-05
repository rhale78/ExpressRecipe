namespace ExpressRecipe.RecipeParser.Models;

public sealed class ParsedInstruction
{
    public int Step { get; set; }
    public string Text { get; set; } = "";
    public string? TimerText { get; set; }
    public List<string> Cookware { get; set; } = new();
}
