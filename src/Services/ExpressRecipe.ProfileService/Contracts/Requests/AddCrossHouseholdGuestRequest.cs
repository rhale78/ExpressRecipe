namespace ExpressRecipe.ProfileService.Contracts.Requests;

public class AddCrossHouseholdGuestRequest
{
    public string DisplayName { get; set; } = string.Empty;
    public Guid SourceHouseholdId { get; set; }
}
