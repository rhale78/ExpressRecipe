namespace ExpressRecipe.InventoryService.Services;

using ExpressRecipe.InventoryService.Data;

public interface IEquipmentCapabilityResolver
{
    Task<List<EquipmentInstanceDto>> ResolveAsync(Guid householdId, string capability,
        CancellationToken ct = default);
    Task<string?> GetSubstituteMessageAsync(Guid householdId, string requiredEquipmentName,
        CancellationToken ct = default);
}

public sealed class EquipmentCapabilityResolver : IEquipmentCapabilityResolver
{
    private readonly IEquipmentRepository _equipment;

    // Maps common equipment names (from recipes) to the capability they require
    private static readonly Dictionary<string, string> EquipmentToCapability =
        new(StringComparer.OrdinalIgnoreCase)
    {
        {"Crock Pot",        "SlowCook"      }, {"Slow Cooker",    "SlowCook"      },
        {"Instant Pot",      "PressureCook"  }, {"Pressure Cooker","PressureCook"  },
        {"Rice Cooker",      "RiceCook"      }, {"Air Fryer",      "AirFry"        },
        {"Dehydrator",       "Dehydrate"     }, {"Freeze Dryer",   "FreezeDry"     },
        {"Stand Mixer",      "Mix"           }, {"Hand Mixer",     "Mix"           },
        {"Blender",          "Blend"         }, {"Food Processor", "Blend"         },
        {"Smoker",           "Smoke"         }, {"Sous Vide",      "Steam"         },
    };

    public EquipmentCapabilityResolver(IEquipmentRepository equipment)
    { _equipment = equipment; }

    public async Task<List<EquipmentInstanceDto>> ResolveAsync(Guid householdId,
        string capability, CancellationToken ct = default)
        => await _equipment.GetInstancesByCapabilityAsync(householdId, capability, ct);

    // Returns a human-readable substitute message, or null if no capable equipment found.
    // Example: recipe needs "Crock Pot" → household has Instant Pot with SlowCook capability
    // Returns: "Your 'Big Instant Pot' can substitute — it supports SlowCook."
    public async Task<string?> GetSubstituteMessageAsync(Guid householdId,
        string requiredEquipmentName, CancellationToken ct = default)
    {
        if (!EquipmentToCapability.TryGetValue(requiredEquipmentName, out string? capability))
        { return null; }

        List<EquipmentInstanceDto> capable = await ResolveAsync(householdId, capability, ct);
        if (capable.Count == 0) { return null; }

        EquipmentInstanceDto match = capable[0];
        return $"Your '{match.DisplayName}' can substitute — it supports {capability}.";
    }
}
