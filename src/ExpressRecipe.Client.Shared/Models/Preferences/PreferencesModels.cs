namespace ExpressRecipe.Client.Shared.Models.Preferences;

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

public class UpsertCookProfileRequest
{
    public bool CooksForHousehold { get; set; } = true;
    public string CookingFrequency { get; set; } = "Regular";
    public string OverallSkillLevel { get; set; } = "HomeCook";
    public string CookRole { get; set; } = "PrimaryHomeChef";
    public bool EatingDisorderRecovery { get; set; }
}

public class TechniqueComfortDto
{
    public Guid Id { get; set; }
    public Guid MemberId { get; set; }
    public string TechniqueCode { get; set; } = string.Empty;
    public string ComfortLevel { get; set; } = string.Empty;
}

public class SetTechniqueComfortRequest
{
    public string ComfortLevel { get; set; } = string.Empty;
}

public class DismissedTipDto
{
    public Guid Id { get; set; }
    public Guid MemberId { get; set; }
    public Guid TipId { get; set; }
    public DateTime DismissedAt { get; set; }
}
