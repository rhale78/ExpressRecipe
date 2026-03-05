namespace ExpressRecipe.RecipeParser.Models;

public sealed class RecipeIndexEntry
{
    public string Title { get; set; } = "";
    public string FileName { get; set; } = ""; // relative link target e.g. "chocolate-cake.html"
    public string? ThumbnailUrl { get; set; }
    public double? AverageRating { get; set; }
    public string? Category { get; set; }
    public string? Yield { get; set; }
    public string? PrepTime { get; set; }
    public string? Description { get; set; }
}
