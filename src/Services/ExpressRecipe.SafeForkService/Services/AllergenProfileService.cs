using ExpressRecipe.SafeForkService.Contracts.Requests;
using ExpressRecipe.SafeForkService.Contracts.Responses;
using ExpressRecipe.SafeForkService.Data;
using ExpressRecipe.SafeForkService.Models;
using ExpressRecipe.Shared.Services;

namespace ExpressRecipe.SafeForkService.Services;

public class AllergenProfileService : IAllergenProfileService
{
    private readonly IAllergenProfileRepository _profileRepo;
    private readonly ITemporaryScheduleRepository _scheduleRepo;
    private readonly IAdaptationOverrideRepository _overrideRepo;
    private readonly AllergenResolutionService _resolutionService;
    private readonly ISafeForkEventPublisher _publisher;
    private readonly HybridCacheService _cache;
    private readonly ILogger<AllergenProfileService> _logger;

    private static readonly TimeSpan ProfileCacheExpiry = TimeSpan.FromMinutes(30);

    public AllergenProfileService(
        IAllergenProfileRepository profileRepo,
        ITemporaryScheduleRepository scheduleRepo,
        IAdaptationOverrideRepository overrideRepo,
        AllergenResolutionService resolutionService,
        ISafeForkEventPublisher publisher,
        HybridCacheService cache,
        ILogger<AllergenProfileService> logger)
    {
        _profileRepo = profileRepo;
        _scheduleRepo = scheduleRepo;
        _overrideRepo = overrideRepo;
        _resolutionService = resolutionService;
        _publisher = publisher;
        _cache = cache;
        _logger = logger;
    }

    public async Task<AllergenProfileDto?> GetEffectiveProfileAsync(Guid memberId, bool includeSchedules, CancellationToken ct)
    {
        string cacheKey = $"allergen:member:{memberId}";

        List<AllergenProfileEntryDto> entries = await _cache.GetOrSetAsync(
            cacheKey,
            async (innerCt) => await _profileRepo.GetByMemberIdAsync(memberId, innerCt),
            ProfileCacheExpiry,
            cancellationToken: ct) ?? new List<AllergenProfileEntryDto>();

        AllergenProfileDto profile = new AllergenProfileDto
        {
            MemberId = memberId,
            Entries = entries
        };

        if (includeSchedules)
        {
            profile.ActiveSchedules = await _scheduleRepo.GetActiveAsync(memberId, ct);
        }

        return profile;
    }

    public async Task<UnionProfileDto> ComputeUnionProfileAsync(IReadOnlyList<Guid> memberIds, CancellationToken ct)
    {
        List<AllergenProfileEntryDto> allEntries = new List<AllergenProfileEntryDto>();

        foreach (Guid memberId in memberIds)
        {
            List<AllergenProfileEntryDto> memberEntries = await _profileRepo.GetByMemberIdAsync(memberId, ct);
            allEntries.AddRange(memberEntries);
        }

        List<string> hardExcludes = allEntries
            .Where(e => e.HouseholdExclude)
            .Select(e => e.FreeFormName ?? e.AllergenId?.ToString() ?? string.Empty)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct()
            .ToList();

        return new UnionProfileDto
        {
            AllEntries = allEntries,
            HardExcludes = hardExcludes
        };
    }

    public Task<RecipeEvaluationResult> EvaluateRecipeAsync(IReadOnlyList<RecipeIngredientDto> ingredients, UnionProfileDto profile, CancellationToken ct)
    {
        List<ConflictItem> conflicts = new List<ConflictItem>();

        foreach (RecipeIngredientDto ingredient in ingredients)
        {
            foreach (AllergenProfileEntryDto entry in profile.AllEntries)
            {
                bool nameMatch = !string.IsNullOrWhiteSpace(entry.FreeFormName)
                    && !string.IsNullOrWhiteSpace(ingredient.Name)
                    && ingredient.Name.Contains(entry.FreeFormName, StringComparison.OrdinalIgnoreCase);

                bool ingredientIdMatch = ingredient.IngredientId.HasValue
                    && entry.AllergenId.HasValue
                    && ingredient.IngredientId.Value == entry.AllergenId.Value;

                if (nameMatch || ingredientIdMatch)
                {
                    conflicts.Add(new ConflictItem
                    {
                        MemberId = entry.MemberId,
                        AllergenProfileId = entry.Id,
                        AllergenName = entry.FreeFormName ?? entry.AllergenId?.ToString() ?? string.Empty,
                        Severity = entry.Severity,
                        ExposureThreshold = entry.ExposureThreshold
                    });
                }
            }
        }

        bool hasAnaphylacticRisk = conflicts.Any(c =>
            string.Equals(c.Severity, "LifeThreatening", StringComparison.OrdinalIgnoreCase));

        ConflictReport report = new ConflictReport
        {
            HasConflicts = conflicts.Count > 0,
            HasAnaphylacticRisk = hasAnaphylacticRisk,
            Conflicts = conflicts
        };

        string suggestedStrategy = hasAnaphylacticRisk ? "SeparateMeal" : "AdaptAll";

        RecipeEvaluationResult result = new RecipeEvaluationResult
        {
            IsSafe = conflicts.Count == 0,
            ConflictReport = report,
            SuggestedStrategy = suggestedStrategy
        };

        return Task.FromResult(result);
    }

    public async Task<string> ResolveAdaptationStrategyAsync(ConflictReport report, Guid householdId, Guid? recipeInstanceId, CancellationToken ct)
    {
        // Hard rule: anaphylactic risk always forces SeparateMeal
        if (report.HasAnaphylacticRisk)
        {
            _logger.LogWarning(
                "Anaphylactic risk detected for household {HouseholdId} — forcing SeparateMeal strategy",
                householdId);
            return "SeparateMeal";
        }

        // Priority 1: recipe-instance + member-specific override
        if (recipeInstanceId.HasValue && report.Conflicts.Count > 0)
        {
            foreach (ConflictItem conflict in report.Conflicts)
            {
                List<AdaptationOverrideEntry> overrides = await _overrideRepo.GetAsync(
                    householdId, recipeInstanceId, conflict.MemberId, ct);

                AdaptationOverrideEntry? memberRecipeOverride = overrides.FirstOrDefault();
                if (memberRecipeOverride != null)
                {
                    return memberRecipeOverride.StrategyCode;
                }
            }

            // Priority 2: recipe-instance level (no specific member)
            List<AdaptationOverrideEntry> recipeOverrides = await _overrideRepo.GetAsync(
                householdId, recipeInstanceId, null, ct);
            AdaptationOverrideEntry? recipeOverride = recipeOverrides.FirstOrDefault();
            if (recipeOverride != null)
            {
                return recipeOverride.StrategyCode;
            }
        }

        // Priority 3: household-level member override
        if (report.Conflicts.Count > 0)
        {
            foreach (ConflictItem conflict in report.Conflicts)
            {
                List<AdaptationOverrideEntry> overrides = await _overrideRepo.GetAsync(
                    householdId, null, conflict.MemberId, ct);

                AdaptationOverrideEntry? memberOverride = overrides.FirstOrDefault();
                if (memberOverride != null)
                {
                    return memberOverride.StrategyCode;
                }
            }
        }

        // Priority 4: household-level default override
        List<AdaptationOverrideEntry> householdOverrides = await _overrideRepo.GetAsync(
            householdId, null, null, ct);
        AdaptationOverrideEntry? householdOverride = householdOverrides.FirstOrDefault();
        if (householdOverride != null)
        {
            return householdOverride.StrategyCode;
        }

        // Priority 5: system default
        return "AdaptAll";
    }

    public Task<List<SubstituteDto>> GetSubstitutesAsync(RecipeIngredientDto ingredient, Guid allergenId, RecipeContextDto context, CancellationToken ct)
    {
        List<SubstituteDto> substitutes = new List<SubstituteDto>
        {
            new SubstituteDto
            {
                Name = $"Alternative for {ingredient.Name}",
                Reason = "Common allergen-free substitute"
            },
            new SubstituteDto
            {
                Name = $"Plant-based {ingredient.Name} substitute",
                Reason = "Suitable for allergen avoidance"
            }
        };

        if (context.EatingDisorderRecovery)
        {
            // Filter out substitutes that reference calorie, weight, or macro content
            substitutes = substitutes
                .Where(s => !s.Reason.Contains("calorie", StringComparison.OrdinalIgnoreCase)
                    && !s.Reason.Contains("weight", StringComparison.OrdinalIgnoreCase)
                    && !s.Reason.Contains("macro", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return Task.FromResult(substitutes);
    }

    public async Task<Guid> AddFreeformAllergenAsync(Guid memberId, string freeFormText, string? brand, CancellationToken ct)
    {
        Guid entryId = await _profileRepo.AddFreeformEntryAsync(memberId, freeFormText, brand, ct: ct);

        // Attempt synchronous resolution first; fall back to saga if unavailable
        bool resolved = false;
        try
        {
            resolved = await _resolutionService.TryResolveAsync(entryId, memberId, freeFormText, brand, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Synchronous resolution failed for entry {EntryId} — saga fallback not triggered in this path", entryId);
        }

        if (resolved)
        {
            AllergenProfileEntryDto? entry = (await _profileRepo.GetByMemberIdAsync(memberId, ct))
                .FirstOrDefault(e => e.Id == entryId);

            if (entry != null && string.Equals(entry.ExposureThreshold, "AirborneSensitive", StringComparison.OrdinalIgnoreCase))
            {
                await _profileRepo.SetHouseholdExcludeAsync(entryId, true, ct);
                await _publisher.PublishAirborneSensitivityDetectedAsync(
                    memberId, null, entryId, freeFormText, ct);
            }
        }

        await _publisher.PublishAllergenProfileUpdatedAsync(memberId, null, ct);

        return entryId;
    }

    public async Task<Guid> AddTemporaryScheduleAsync(Guid memberId, string scheduleType, DateTimeOffset start, DateTimeOffset end, string? configJson, CancellationToken ct)
    {
        AddTemporaryScheduleRequest request = new AddTemporaryScheduleRequest
        {
            ScheduleType = scheduleType,
            ActiveFrom = start,
            ActiveUntil = end,
            ConfigJson = configJson
        };

        return await _scheduleRepo.AddAsync(memberId, request, ct);
    }

    public async Task<List<TemporaryScheduleDto>> GetActiveSchedulesAsync(Guid memberId, CancellationToken ct)
    {
        return await _scheduleRepo.GetActiveAsync(memberId, ct);
    }

    public async Task<List<AllergenProfileEntryDto>> GetHouseholdHardExcludesAsync(Guid householdId, CancellationToken ct)
    {
        // This method requires resolving household members from the ProfileService.
        // Without a member ID list, we cannot query the AllergenProfile table directly.
        // Callers that have the member IDs should use IAllergenProfileRepository.GetHouseholdHardExcludesAsync
        // with the resolved member ID list. Returning empty here is intentional until a household-member
        // HTTP client is wired into this service.
        _logger.LogDebug(
            "GetHouseholdHardExcludesAsync: no ProfileService HTTP client available — returning empty list for household {HouseholdId}",
            householdId);
        return await Task.FromResult(new List<AllergenProfileEntryDto>());
    }
}
