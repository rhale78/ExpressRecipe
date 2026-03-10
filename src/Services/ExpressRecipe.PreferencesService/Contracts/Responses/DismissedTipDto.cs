namespace ExpressRecipe.PreferencesService.Contracts.Responses;

public class DismissedTipDto
{
    public Guid Id { get; set; }
    public Guid MemberId { get; set; }
    public Guid TipId { get; set; }
    public DateTime DismissedAt { get; set; }
}
