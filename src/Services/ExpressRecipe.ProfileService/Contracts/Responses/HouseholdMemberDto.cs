namespace ExpressRecipe.ProfileService.Contracts.Responses;

public class HouseholdMemberDto
{
    public Guid Id { get; set; }
    public Guid HouseholdId { get; set; }
    public string MemberType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public short? BirthYear { get; set; }
    public Guid? LinkedUserId { get; set; }
    public bool HasUserAccount { get; set; }
    public bool IsGuest { get; set; }
    public string? GuestSubtype { get; set; }
    public DateTime? GuestExpiresAt { get; set; }
    public Guid? SourceHouseholdId { get; set; }
    public DateTime CreatedAt { get; set; }
}
