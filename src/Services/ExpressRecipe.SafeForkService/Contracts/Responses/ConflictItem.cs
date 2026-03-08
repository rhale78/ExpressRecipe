namespace ExpressRecipe.SafeForkService.Contracts.Responses;

public class ConflictItem
{
    public Guid MemberId { get; set; }
    public Guid AllergenProfileId { get; set; }
    public string AllergenName { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string ExposureThreshold { get; set; } = string.Empty;
}
