namespace ExpressRecipe.Shared.DTOs.Product;

// ---------------------------------------------------------------------------
// Food Group DTOs
// ---------------------------------------------------------------------------

public class FoodGroupDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? FunctionalRole { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public int MemberCount { get; init; }
}

public class FoodGroupMemberDto
{
    public Guid Id { get; init; }
    public Guid FoodGroupId { get; init; }
    public Guid? IngredientId { get; init; }
    public Guid? ProductId { get; init; }
    public string? CustomName { get; init; }
    public string? SubstitutionRatio { get; init; }
    public string? SubstitutionNotes { get; init; }
    public string? BestFor { get; init; }
    public string? NotSuitableFor { get; init; }
    public int RankOrder { get; init; }
    public string? AllergenFreeJson { get; init; }
    public bool IsHomemadeRecipeAvailable { get; init; }
    public Guid? HomemadeRecipeId { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Allergens this substitute is free of – deserialized from <see cref="AllergenFreeJson"/>
    /// and included in API responses so clients do not need to parse the raw JSON.
    /// </summary>
    public string[] AllergenFree
    {
        get
        {
            if (string.IsNullOrWhiteSpace(AllergenFreeJson)) { return Array.Empty<string>(); }
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<string[]>(AllergenFreeJson)
                       ?? Array.Empty<string>();
            }
            catch (System.Text.Json.JsonException)
            {
                return Array.Empty<string>();
            }
        }
    }
}

public class SubstitutionHistoryDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public Guid? OriginalIngredientId { get; init; }
    public string? OriginalCustomName { get; init; }
    public Guid? SubstituteIngredientId { get; init; }
    public string? SubstituteCustomName { get; init; }
    public Guid? RecipeId { get; init; }
    public DateTime? CookedAt { get; init; }
    public int? UserRating { get; init; }
    public string? Notes { get; init; }
    public DateTime CreatedAt { get; init; }
}

// ---------------------------------------------------------------------------
// Records for creating/inserting
// ---------------------------------------------------------------------------

public record FoodGroupRecord(
    string Name,
    string? Description,
    string? FunctionalRole);

public record FoodGroupMemberRecord(
    Guid FoodGroupId,
    Guid? IngredientId,
    Guid? ProductId,
    string? CustomName,
    string? SubstitutionRatio,
    string? SubstitutionNotes,
    string? BestFor,
    string? NotSuitableFor,
    int RankOrder,
    string? AllergenFreeJson,
    bool IsHomemadeRecipeAvailable,
    Guid? HomemadeRecipeId);

public record SubstitutionHistoryRecord(
    Guid UserId,
    Guid? OriginalIngredientId,
    string? OriginalCustomName,
    Guid? SubstituteIngredientId,
    string? SubstituteCustomName,
    Guid? RecipeId,
    DateTime? CookedAt,
    int? UserRating,
    string? Notes);

// ---------------------------------------------------------------------------
// SubstituteOption – returned by IFoodSubstitutionService
// ---------------------------------------------------------------------------

public class SubstituteOption
{
    public Guid FoodGroupMemberId { get; init; }
    public Guid? IngredientId { get; init; }
    public Guid? ProductId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? SubstitutionRatio { get; init; }
    public string? SubstitutionNotes { get; init; }
    public string? BestFor { get; init; }
    public string? NotSuitableFor { get; init; }
    public int RankOrder { get; init; }
    public string[] AllergenFree { get; init; } = Array.Empty<string>();
    public bool IsOnHand { get; init; }
    public decimal? UserHistoryRating { get; init; }
    public bool UserUsedBefore { get; init; }
    public bool HasHomemadeRecipe { get; init; }
    public Guid? HomemadeRecipeId { get; init; }
    public string? HomemadeRecipeName { get; init; }
}
