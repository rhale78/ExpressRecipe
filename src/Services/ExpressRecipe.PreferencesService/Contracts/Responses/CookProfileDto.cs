namespace ExpressRecipe.PreferencesService.Contracts.Responses;

public class CookProfileDto
{
    public Guid Id { get; set; }
    public Guid MemberId { get; set; }
    public bool CooksForHousehold { get; set; }
    public string CookingFrequency { get; set; } = "Regular";
    public string OverallSkillLevel { get; set; } = "HomeCook";
    public string CookRole { get; set; } = "PrimaryHomeChef";
    public bool EatingDisorderRecovery { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
