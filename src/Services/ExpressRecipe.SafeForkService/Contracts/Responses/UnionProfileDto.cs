namespace ExpressRecipe.SafeForkService.Contracts.Responses;

public class UnionProfileDto
{
    public Guid HouseholdId { get; set; }
    public List<AllergenProfileEntryDto> AllEntries { get; set; } = new();
    public List<string> HardExcludes { get; set; } = new();
}
