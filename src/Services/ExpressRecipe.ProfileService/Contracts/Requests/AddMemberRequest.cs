namespace ExpressRecipe.ProfileService.Contracts.Requests;

public class AddMemberRequest
{
    public string MemberType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public short? BirthYear { get; set; }
    public Guid? LinkedUserId { get; set; }
}
