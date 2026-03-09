namespace ExpressRecipe.SafeForkService.Contracts.Responses;

public class AllergenProfileDto
{
    public Guid MemberId { get; set; }
    public List<AllergenProfileEntryDto> Entries { get; set; } = new();
    public List<TemporaryScheduleDto> ActiveSchedules { get; set; } = new();
}
