namespace ExpressRecipe.ProfileService.Contracts.Requests;

public class AddTemporaryVisitorRequest
{
    public string DisplayName { get; set; } = string.Empty;
    public DateTime GuestExpiresAt { get; set; }
}
