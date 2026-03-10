namespace ExpressRecipe.AIService.Services;

public interface ICookingAssistantService
{
    Task<CookingAssistantResponse> AskSomethingSeemsBrokenAsync(
        CookingAssistantRequest req, CancellationToken ct = default);
    Task<CookingAssistantResponse> GetPairingsAsync(
        CookingAssistantRequest req, CancellationToken ct = default);
    Task<CookingAssistantResponse> TroubleshootProblemAsync(
        CookingAssistantRequest req, CancellationToken ct = default);
    Task<CookingAssistantResponse> GetVariationsAsync(
        CookingAssistantRequest req, CancellationToken ct = default);
    Task<CookingAssistantResponse> FixIssueAsync(
        CookingAssistantRequest req, CancellationToken ct = default);
    Task<CookingAssistantResponse> AdaptRecipeAsync(
        CookingAssistantRequest req, string targetMethod, CancellationToken ct = default);
}

public sealed record CookingAssistantRequest
{
    public Guid? RecipeId { get; init; }
    public string? RecipeName { get; init; }
    public string? RecipeIngredients { get; init; }  // comma-separated for context
    public string UserMessage { get; init; } = string.Empty;
    public Guid HouseholdId { get; init; }
    public List<string> HouseholdAllergens { get; init; } = new();
    public List<string> HouseholdDietaryRestrictions { get; init; } = new();
}

public sealed record CookingAssistantResponse
{
    public bool Success { get; init; }
    public string Suggestion { get; init; } = string.Empty;
    public string Explanation { get; init; } = string.Empty;
    public List<string> RelatedTips { get; init; } = new();
    public CookingNoteToSave? CookingNoteToSave { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed record CookingNoteToSave
{
    public string NoteType { get; init; } = "Tip";   // Tip|Warning
    public string NoteText { get; init; } = string.Empty;
    public bool SaveNote { get; init; }
}
