namespace ExpressRecipe.SafeForkService.Models;

public class AllergenProfileEntry
{
    public Guid Id { get; set; }
    public Guid MemberId { get; set; }
    public Guid? AllergenId { get; set; }
    public string? FreeFormName { get; set; }
    public string? FreeFormBrand { get; set; }
    public Guid? LinkedIngredientId { get; set; }
    public Guid? LinkedProductId { get; set; }
    public bool IsUnresolved { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string ExposureThreshold { get; set; } = "IngestionOnly";
    public string Severity { get; set; } = "Moderate";
    public bool HouseholdExclude { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}
