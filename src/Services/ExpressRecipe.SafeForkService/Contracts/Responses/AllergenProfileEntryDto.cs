namespace ExpressRecipe.SafeForkService.Contracts.Responses;

public class AllergenProfileEntryDto
{
    public Guid Id { get; set; }
    public Guid MemberId { get; set; }
    public Guid? AllergenId { get; set; }
    public string? FreeFormName { get; set; }
    public string? FreeFormBrand { get; set; }
    public bool IsUnresolved { get; set; }
    public string ExposureThreshold { get; set; } = "IngestionOnly";
    public string Severity { get; set; } = "Moderate";
    public bool HouseholdExclude { get; set; }
    public DateTime CreatedAt { get; set; }
}
