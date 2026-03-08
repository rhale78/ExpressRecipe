namespace ExpressRecipe.SafeForkService.Models;

public class AdaptationOverrideEntry
{
    public Guid Id { get; set; }
    public Guid HouseholdId { get; set; }
    public Guid? RecipeInstanceId { get; set; }
    public Guid? MemberId { get; set; }
    public string StrategyCode { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
