namespace ExpressRecipe.SafeForkService.Contracts.Responses;

public class RecipeIngredientDto
{
    public Guid? IngredientId { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class RecipeContextDto
{
    public Guid? RecipeId { get; set; }
    public string RecipeName { get; set; } = string.Empty;
    public bool EatingDisorderRecovery { get; set; }
}

public class ConflictItem
{
    public Guid MemberId { get; set; }
    public Guid AllergenProfileId { get; set; }
    public string AllergenName { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string ExposureThreshold { get; set; } = string.Empty;
}

public class ConflictReport
{
    public bool HasConflicts { get; set; }
    public bool HasAnaphylacticRisk { get; set; }
    public List<ConflictItem> Conflicts { get; set; } = new();
}

public class RecipeEvaluationResult
{
    public bool IsSafe { get; set; }
    public ConflictReport ConflictReport { get; set; } = new();
    public string SuggestedStrategy { get; set; } = "AdaptAll";
}

public class SubstituteDto
{
    public Guid? IngredientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}
