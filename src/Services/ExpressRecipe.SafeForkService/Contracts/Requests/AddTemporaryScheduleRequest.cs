namespace ExpressRecipe.SafeForkService.Contracts.Requests;

public class AddTemporaryScheduleRequest
{
    public string ScheduleType { get; set; } = string.Empty;
    public DateTimeOffset ActiveFrom { get; set; }
    public DateTimeOffset ActiveUntil { get; set; }
    public string? ConfigJson { get; set; }
}
