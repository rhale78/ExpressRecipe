namespace ExpressRecipe.Shared.DTOs.Recipe;

public class RecipeShareTokenDto
{
    public Guid Id { get; init; }
    public Guid RecipeId { get; init; }
    public string Token { get; init; } = string.Empty;
    public Guid CreatedBy { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime ExpiresAt { get; init; }
    public int ViewCount { get; init; }
    public int? MaxViews { get; init; }

    /// <summary>Populated when returned with recipe details.</summary>
    public RecipeDto? Recipe { get; init; }
}
