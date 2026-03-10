namespace ExpressRecipe.PreferencesService.Contracts.Responses;

public class TechniqueComfortDto
{
    public Guid Id { get; set; }
    public Guid MemberId { get; set; }
    public string TechniqueCode { get; set; } = string.Empty;
    public string ComfortLevel { get; set; } = string.Empty;
}
