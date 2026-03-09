namespace ExpressRecipe.InventoryService.Data;

public sealed record EquipmentTemplateDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string? Description { get; init; }
    public List<string> DefaultCapabilities { get; init; } = new();
}

public sealed record EquipmentInstanceDto
{
    public Guid Id { get; init; }
    public Guid HouseholdId { get; init; }
    public Guid? AddressId { get; init; }
    public Guid TemplateId { get; init; }
    public string TemplateName { get; init; } = string.Empty;
    public string? CustomName { get; init; }
    public string? Brand { get; init; }
    public string? ModelNumber { get; init; }
    public decimal? SizeValue { get; init; }
    public string? SizeUnit { get; init; }
    public string? Notes { get; init; }
    public bool IsActive { get; init; }
    public List<string> Capabilities { get; init; } = new();
    public DateTime CreatedAt { get; init; }
}

public sealed record EquipmentSubstituteDto
{
    public Guid InstanceId { get; init; }
    public string InstanceName { get; init; } = string.Empty;
    public string Capability { get; init; } = string.Empty;
}

public interface IEquipmentRepository
{
    Task<List<EquipmentTemplateDto>> GetTemplatesAsync(CancellationToken ct = default);
    Task<EquipmentTemplateDto?> GetTemplateByIdAsync(Guid templateId, CancellationToken ct = default);
    Task<Guid> CreateInstanceAsync(Guid householdId, Guid? addressId, Guid templateId,
        string? customName, string? brand, string? modelNumber,
        decimal? sizeValue, string? sizeUnit, string? notes,
        IEnumerable<string> capabilities, CancellationToken ct = default);
    Task<List<EquipmentInstanceDto>> GetInstancesByHouseholdAsync(Guid householdId, CancellationToken ct = default);
    Task DeactivateInstanceAsync(Guid instanceId, CancellationToken ct = default);
    Task<List<EquipmentInstanceDto>> ResolveByCapabilityAsync(Guid householdId, string capability, CancellationToken ct = default);
    Task<List<EquipmentSubstituteDto>> FindSubstitutesAsync(Guid householdId, string equipmentName, CancellationToken ct = default);
}
