namespace ExpressRecipe.Client.Shared.Models.SafeFork;

public class AllergenProfileDto
{
    public Guid MemberId { get; set; }
    public List<AllergenProfileEntryDto> Entries { get; set; } = new();
    public List<TemporaryScheduleDto> ActiveSchedules { get; set; } = new();
}

public class AllergenProfileEntryDto
{
    public Guid Id { get; set; }
    public Guid MemberId { get; set; }
    public Guid? AllergenId { get; set; }
    public string? FreeFormName { get; set; }
    public string? FreeFormBrand { get; set; }
    public bool IsUnresolved { get; set; }
    public string ExposureThreshold { get; set; } = "IngestionOnly";
    public string Severity { get; set; } = "Moderate";
    public bool HouseholdExclude { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TemporaryScheduleDto
{
    public Guid Id { get; set; }
    public Guid MemberId { get; set; }
    public string ScheduleType { get; set; } = string.Empty;
    public DateTimeOffset ActiveFrom { get; set; }
    public DateTimeOffset ActiveUntil { get; set; }
    public string? ConfigJson { get; set; }
}

public class AddCuratedAllergenRequest
{
    public Guid AllergenId { get; set; }
    public string ExposureThreshold { get; set; } = "IngestionOnly";
    public string Severity { get; set; } = "Moderate";
}

public class AddFreeformAllergenRequest
{
    public string FreeFormText { get; set; } = string.Empty;
    public string? Brand { get; set; }
}

public class AddTemporaryScheduleRequest
{
    public string ScheduleType { get; set; } = string.Empty;
    public DateTimeOffset ActiveFrom { get; set; }
    public DateTimeOffset ActiveUntil { get; set; }
    public string? ConfigJson { get; set; }
}
