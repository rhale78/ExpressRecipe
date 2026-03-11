namespace ExpressRecipe.RecipeService.Data;

public sealed record LogCookSessionRequest
{
    public Guid RecipeId { get; init; }
    public Guid HouseholdId { get; init; }
    public DateTimeOffset? CookedAt { get; init; }
    public int? ServingsMade { get; init; }
    public int? Rating { get; init; }
    public bool? WouldMakeAgain { get; init; }
    public string? GeneralNotes { get; init; }
    public string? IssueNotes { get; init; }
    public string? FixNotes { get; init; }
    public bool AIHelpUsed { get; init; }
}

public sealed record CookSessionDto
{
    public Guid Id { get; init; }
    public Guid RecipeId { get; init; }
    public string RecipeName { get; init; } = string.Empty;
    public DateTime CookedAt { get; init; }
    public int? ServingsMade { get; init; }
    public int? Rating { get; init; }
    public bool? WouldMakeAgain { get; init; }
    public string? GeneralNotes { get; init; }
    public string? IssueNotes { get; init; }
    public string? FixNotes { get; init; }
    public bool AIHelpUsed { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed record SaveRecipeNoteRequest
{
    public Guid RecipeId { get; init; }
    public string NoteType { get; init; } = "General";
    public string NoteText { get; init; } = string.Empty;
    public bool IsFromAI { get; init; }
    public int DisplayOrder { get; init; }
}

public sealed record RecipeNoteDto
{
    public Guid Id { get; init; }
    public Guid RecipeId { get; init; }
    public string NoteType { get; init; } = string.Empty;
    public string NoteText { get; init; } = string.Empty;
    public bool IsFromAI { get; init; }
    public bool IsDismissed { get; init; }
    public int DisplayOrder { get; init; }
    public DateTime CreatedAt { get; init; }
}
