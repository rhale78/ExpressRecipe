namespace ExpressRecipe.PreferencesService.Contracts.Requests;

public class UpsertCookProfileRequest
{
    public bool CooksForHousehold { get; set; } = true;
    public string CookingFrequency { get; set; } = "Regular";
    public string OverallSkillLevel { get; set; } = "HomeCook";
    public string CookRole { get; set; } = "PrimaryHomeChef";
    public bool EatingDisorderRecovery { get; set; }
}
