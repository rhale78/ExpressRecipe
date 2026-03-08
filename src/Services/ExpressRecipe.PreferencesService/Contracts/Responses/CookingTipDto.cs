namespace ExpressRecipe.PreferencesService.Contracts.Responses;

public class CookingTipDto
{
    public Guid Id { get; set; }
    public string TechniqueCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? WhyExplanation { get; set; }
    public bool IsNiche { get; set; }
}
