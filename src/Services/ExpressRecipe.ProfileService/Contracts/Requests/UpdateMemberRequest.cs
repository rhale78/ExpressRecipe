namespace ExpressRecipe.ProfileService.Contracts.Requests;

public class UpdateMemberRequest
{
    public string DisplayName { get; set; } = string.Empty;
    public short? BirthYear { get; set; }
    public bool? HasUserAccount { get; set; }
}
