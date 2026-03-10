namespace ExpressRecipe.SafeForkService.Models;

public class TemporaryScheduleEntry
{
    public Guid Id { get; set; }
    public Guid MemberId { get; set; }
    public string ScheduleType { get; set; } = string.Empty;
    public DateTime ActiveFrom { get; set; }
    public DateTime ActiveUntil { get; set; }
    public string? ConfigJson { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}
