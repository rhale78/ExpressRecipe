namespace ExpressRecipe.Client.Shared.Models.Cooking;

public class CookingTimerDto
{
    public Guid Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? RecipeName { get; set; }
    public int DurationSeconds { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsExpired => Status == "Expired";
}

public class CreateCookingTimerRequest
{
    public string Label { get; set; } = string.Empty;
    public int DurationSeconds { get; set; }
    public Guid? RecipeId { get; set; }
    public bool AutoStart { get; set; }
}

public class CookSessionSummaryDto
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public DateTime CookedAt { get; set; }
    public int? Rating { get; set; }
    public bool? WouldMakeAgain { get; set; }
    public string? GeneralNotes { get; set; }
}

public class StartCookSessionRequest
{
    public Guid RecipeId { get; set; }
}

public class UpdateCookSessionRequest
{
    public Guid RecipeId { get; set; }
    public int? Rating { get; set; }
    public bool? WouldMakeAgain { get; set; }
    public string? GeneralNotes { get; set; }
    public string? IssueNotes { get; set; }
    public string? FixNotes { get; set; }
}

public class UserRecipeNoteDto
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public string NoteType { get; set; } = string.Empty;
    public string NoteText { get; set; } = string.Empty;
    public bool IsFromAI { get; set; }
    public bool IsDismissed { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AddRecipeNoteRequest
{
    public string NoteType { get; set; } = "General";
    public string NoteText { get; set; } = string.Empty;
}
