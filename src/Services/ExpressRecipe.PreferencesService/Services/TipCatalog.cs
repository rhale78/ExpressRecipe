using ExpressRecipe.PreferencesService.Contracts.Responses;

namespace ExpressRecipe.PreferencesService.Services;

/// <summary>
/// Static catalog of demo cooking tips, keyed by technique code.
/// Used until a full tip-catalog database table is implemented.
/// </summary>
internal static class TipCatalog
{
    private static readonly Dictionary<string, List<CookingTipDto>> Tips =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["knife-skills"] = new List<CookingTipDto>
            {
                new CookingTipDto
                {
                    Id = new Guid("a1000000-0000-0000-0000-000000000001"),
                    TechniqueCode = "knife-skills",
                    Title = "Use a pinch grip for control",
                    WhyExplanation = "Gripping the blade between thumb and forefinger gives far more precision than a handle grip.",
                    IsNiche = false
                },
                new CookingTipDto
                {
                    Id = new Guid("a1000000-0000-0000-0000-000000000002"),
                    TechniqueCode = "knife-skills",
                    Title = "Rock the tip, don't lift",
                    WhyExplanation = "Keeping the tip on the board reduces fatigue and speeds up chopping.",
                    IsNiche = false
                },
                new CookingTipDto
                {
                    Id = new Guid("a1000000-0000-0000-0000-000000000003"),
                    TechniqueCode = "knife-skills",
                    Title = "Try the brunoise for uniform dice",
                    WhyExplanation = "Brunoise produces perfectly even 3 mm cubes that cook at identical rates.",
                    IsNiche = true
                }
            },
            ["sauteing"] = new List<CookingTipDto>
            {
                new CookingTipDto
                {
                    Id = new Guid("a2000000-0000-0000-0000-000000000001"),
                    TechniqueCode = "sauteing",
                    Title = "Let the pan heat before adding oil",
                    WhyExplanation = "A hot pan creates a non-stick surface through the Leidenfrost effect.",
                    IsNiche = false
                },
                new CookingTipDto
                {
                    Id = new Guid("a2000000-0000-0000-0000-000000000002"),
                    TechniqueCode = "sauteing",
                    Title = "Don't crowd the pan",
                    WhyExplanation = "Crowding lowers the surface temperature and causes steaming rather than browning.",
                    IsNiche = false
                },
                new CookingTipDto
                {
                    Id = new Guid("a2000000-0000-0000-0000-000000000003"),
                    TechniqueCode = "sauteing",
                    Title = "Deglaze with wine for a pan sauce",
                    WhyExplanation = "The fond (browned bits) dissolves in liquid and carries intense Maillard flavour.",
                    IsNiche = true
                }
            },
            ["braising"] = new List<CookingTipDto>
            {
                new CookingTipDto
                {
                    Id = new Guid("a3000000-0000-0000-0000-000000000001"),
                    TechniqueCode = "braising",
                    Title = "Sear before braising for depth",
                    WhyExplanation = "Browning the surface creates Maillard compounds that enrich the braise liquid.",
                    IsNiche = false
                },
                new CookingTipDto
                {
                    Id = new Guid("a3000000-0000-0000-0000-000000000002"),
                    TechniqueCode = "braising",
                    Title = "Keep liquid at one-third of meat height",
                    WhyExplanation = "Too much liquid dilutes flavour; too little causes scorching.",
                    IsNiche = false
                },
                new CookingTipDto
                {
                    Id = new Guid("a3000000-0000-0000-0000-000000000003"),
                    TechniqueCode = "braising",
                    Title = "Use a cartouche instead of a lid",
                    WhyExplanation = "A parchment cartouche lets moisture circulate slowly without pressure-cooking.",
                    IsNiche = true
                }
            }
        };

    public static List<CookingTipDto> GetTipsForTechnique(string techniqueCode)
    {
        if (Tips.TryGetValue(techniqueCode, out List<CookingTipDto>? tipList))
        {
            return tipList;
        }
        return new List<CookingTipDto>();
    }
}
