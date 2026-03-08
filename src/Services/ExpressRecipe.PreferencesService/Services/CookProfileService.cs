using ExpressRecipe.PreferencesService.Contracts.Requests;
using ExpressRecipe.PreferencesService.Contracts.Responses;
using ExpressRecipe.PreferencesService.Data;

namespace ExpressRecipe.PreferencesService.Services;

public class CookProfileService : ICookProfileService
{
    private readonly ICookProfileRepository _repository;
    private readonly ILogger<CookProfileService> _logger;

    public CookProfileService(
        ICookProfileRepository repository,
        ILogger<CookProfileService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public Task<CookProfileDto?> GetCookProfileAsync(Guid memberId, CancellationToken ct)
    {
        return _repository.GetByMemberIdAsync(memberId, ct);
    }

    public Task<Guid> UpsertCookProfileAsync(Guid memberId, UpsertCookProfileRequest request, CancellationToken ct)
    {
        return _repository.UpsertAsync(memberId, request, ct);
    }

    public Task<TechniqueComfortDto?> GetTechniqueComfortAsync(Guid memberId, string techniqueCode, CancellationToken ct)
    {
        return _repository.GetTechniqueComfortAsync(memberId, techniqueCode, ct);
    }

    public Task SetTechniqueComfortAsync(Guid memberId, string techniqueCode, SetTechniqueComfortRequest request, CancellationToken ct)
    {
        return _repository.UpsertTechniqueComfortAsync(memberId, techniqueCode, request, ct);
    }

    public Task<List<DismissedTipDto>> GetDismissedTipsAsync(Guid memberId, CancellationToken ct)
    {
        return _repository.GetDismissedTipsAsync(memberId, ct);
    }

    public Task DismissTipAsync(Guid memberId, Guid tipId, CancellationToken ct)
    {
        return _repository.DismissTipAsync(memberId, tipId, ct);
    }

    public Task RestoreTipAsync(Guid memberId, Guid tipId, CancellationToken ct)
    {
        return _repository.RestoreTipAsync(memberId, tipId, ct);
    }

    /// <summary>
    /// Returns tips for <paramref name="techniqueCode"/> applying skill-level rules:
    /// <list type="bullet">
    ///   <item>Beginner — all tips with why explanations</item>
    ///   <item>HomeCook — all tips, no why explanation shown</item>
    ///   <item>ConfidentCook — only techniques with a "Learning" TechniqueComfort override</item>
    ///   <item>Advanced — only niche tips</item>
    ///   <item>Professional — no tips</item>
    /// </list>
    /// Dismissed tips are always excluded. TechniqueComfort "Learning" override beats OverallSkillLevel.
    /// </summary>
    public async Task<List<CookingTipDto>> GetTipsForMemberAsync(Guid memberId, string techniqueCode, CancellationToken ct)
    {
        CookProfileDto? profile = await _repository.GetByMemberIdAsync(memberId, ct);

        if (profile is null)
        {
            _logger.LogDebug("No cook profile for member {MemberId} — returning empty tip list", memberId);
            return new List<CookingTipDto>();
        }

        if (string.Equals(profile.OverallSkillLevel, "Professional", StringComparison.OrdinalIgnoreCase))
        {
            return new List<CookingTipDto>();
        }

        TechniqueComfortDto? comfort = await _repository.GetTechniqueComfortAsync(memberId, techniqueCode, ct);
        List<DismissedTipDto> dismissed = await _repository.GetDismissedTipsAsync(memberId, ct);
        HashSet<Guid> dismissedIds = new HashSet<Guid>(dismissed.Select(d => d.TipId));

        List<CookingTipDto> catalog = TipCatalog.GetTipsForTechnique(techniqueCode);

        // TechniqueComfort "Learning" override — treat member as Beginner for this technique
        bool learningOverride = comfort is not null &&
            string.Equals(comfort.ComfortLevel, "Learning", StringComparison.OrdinalIgnoreCase);

        string effectiveSkill = learningOverride ? "Beginner" : profile.OverallSkillLevel;

        List<CookingTipDto> filtered = FilterBySkill(catalog, effectiveSkill);

        // Strip why explanations for non-Beginner paths
        bool showWhy = string.Equals(effectiveSkill, "Beginner", StringComparison.OrdinalIgnoreCase);

        List<CookingTipDto> result = new List<CookingTipDto>();
        foreach (CookingTipDto tip in filtered)
        {
            if (dismissedIds.Contains(tip.Id))
            {
                continue;
            }

            result.Add(new CookingTipDto
            {
                Id = tip.Id,
                TechniqueCode = tip.TechniqueCode,
                Title = tip.Title,
                WhyExplanation = showWhy ? tip.WhyExplanation : null,
                IsNiche = tip.IsNiche
            });
        }

        return result;
    }

    private static List<CookingTipDto> FilterBySkill(List<CookingTipDto> catalog, string skillLevel)
    {
        if (string.Equals(skillLevel, "Beginner", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(skillLevel, "HomeCook", StringComparison.OrdinalIgnoreCase))
        {
            return catalog;
        }

        if (string.Equals(skillLevel, "ConfidentCook", StringComparison.OrdinalIgnoreCase))
        {
            // Returned only when the Learning override applies — so return all (same as HomeCook path)
            return catalog;
        }

        if (string.Equals(skillLevel, "Advanced", StringComparison.OrdinalIgnoreCase))
        {
            return catalog.Where(t => t.IsNiche).ToList();
        }

        return new List<CookingTipDto>();
    }
}
