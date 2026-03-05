namespace ExpressRecipe.RecipeParser.Models;

/// <summary>
/// Recipe data used for rich exports (PDF, HTML). Wraps ParsedRecipe and adds
/// display-only fields like ratings, images, and review counts.
/// </summary>
public sealed class RecipeExportData
{
    public ParsedRecipe Recipe { get; set; } = new();

    // Display/rich fields (all optional - export gracefully when missing)
    public double? AverageRating { get; set; }          // 0-5 stars
    public int? RatingCount { get; set; }
    public string? ThumbnailUrl { get; set; }            // main image URL
    public List<string> ImageUrls { get; set; } = new(); // additional images
    public string? Notes { get; set; }                   // cook's notes
    public string? Source { get; set; }                  // e.g. "Grandma's cookbook"
    public string? SourceUrl { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }               // author display name
    public List<string> Allergens { get; set; } = new();
    public List<string> DietaryTags { get; set; } = new(); // vegan, gluten-free, etc.
}
