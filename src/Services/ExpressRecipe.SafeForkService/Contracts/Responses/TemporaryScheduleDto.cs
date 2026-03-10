namespace ExpressRecipe.SafeForkService.Contracts.Responses;

public class TemporaryScheduleDto
{
    public Guid Id { get; set; }
    public Guid MemberId { get; set; }
    public string ScheduleType { get; set; } = string.Empty;
    public DateTimeOffset ActiveFrom { get; set; }
    public DateTimeOffset ActiveUntil { get; set; }
    public string? ConfigJson { get; set; }
}
