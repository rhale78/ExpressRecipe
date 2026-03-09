namespace ExpressRecipe.Client.Shared.Models.Product;

public class ProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Description { get; set; }
    public string? UPC { get; set; }
    public string? ImageUrl { get; set; }
    public List<string> Allergens { get; set; } = new();
    public List<ProductIngredientDto> Ingredients { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

public class ProductIngredientDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid IngredientId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
    public string? Quantity { get; set; }
    public string? Notes { get; set; }
    public string? IngredientListString { get; set; }
}

public class ProductSearchRequest
{
    public string? SearchTerm { get; set; }
    public string? Brand { get; set; }
    public List<string>? Allergens { get; set; }
    public List<string>? Restrictions { get; set; } // Dietary restrictions to exclude
    public string? FirstLetter { get; set; }
    public string? SortBy { get; set; } = "name"; // "name", "brand", "created"
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class ProductSearchResult
{
    public List<ProductDto> Products { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public class CreateProductRequest
{
    public string Name { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? UPC { get; set; }
    public List<string> Allergens { get; set; } = new();
    public List<string> Ingredients { get; set; } = new();
}

// ---------------------------------------------------------------------------
// Food Catalog client models
// ---------------------------------------------------------------------------

public class FoodGroupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? FunctionalRole { get; set; }
    public int MemberCount { get; set; }
}

public class FoodGroupDetailResponse
{
    public FoodGroupDto Group { get; set; } = null!;
    public List<FoodGroupMemberDto> Members { get; set; } = new();
}

public class FoodGroupMemberDto
{
    public Guid Id { get; set; }
    public Guid FoodGroupId { get; set; }
    public Guid? IngredientId { get; set; }
    public string? CustomName { get; set; }
    public string? SubstitutionRatio { get; set; }
    public string? SubstitutionNotes { get; set; }
    public string? BestFor { get; set; }
    public string? NotSuitableFor { get; set; }
    public int RankOrder { get; set; }
    public bool IsHomemadeRecipeAvailable { get; set; }
    public Guid? HomemadeRecipeId { get; set; }
    /// <summary>Allergens this substitute is free of (deserialized from AllergenFreeJson).</summary>
    public string[] AllergenFree { get; set; } = Array.Empty<string>();
}

public class SubstituteOptionDto
{
    public Guid FoodGroupMemberId { get; set; }
    public Guid? IngredientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? SubstitutionRatio { get; set; }
    public string? SubstitutionNotes { get; set; }
    public string? BestFor { get; set; }
    public string? NotSuitableFor { get; set; }
    public int RankOrder { get; set; }
    public string[] AllergenFree { get; set; } = Array.Empty<string>();
    public bool IsOnHand { get; set; }
    public decimal? UserHistoryRating { get; set; }
    public bool UserUsedBefore { get; set; }
    public bool HasHomemadeRecipe { get; set; }
    public Guid? HomemadeRecipeId { get; set; }
    public string? HomemadeRecipeName { get; set; }
}

public class RecordSubstitutionRequest
{
    public Guid? OriginalIngredientId { get; set; }
    public string? OriginalCustomName { get; set; }
    public Guid? SubstituteIngredientId { get; set; }
    public string? SubstituteCustomName { get; set; }
    public Guid? RecipeId { get; set; }
    public int? UserRating { get; set; }
    public string? Notes { get; set; }
}
