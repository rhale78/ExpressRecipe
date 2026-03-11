using ExpressRecipe.UserService.Data;

namespace ExpressRecipe.UserService.Services;

public interface IAllergyDifferentialAnalyzer
{
    Task RunForMemberAsync(
        Guid householdId, Guid? memberId, string memberName,
        CancellationToken ct = default);

    Task RunForMembersAsync(
        Guid householdId, IEnumerable<(Guid? MemberId, string MemberName)> members,
        CancellationToken ct = default);
}

public sealed class AllergyDifferentialAnalyzer : IAllergyDifferentialAnalyzer
{
    // Severity weights applied to the final confidence score.
    private static readonly Dictionary<string, decimal> SeverityWeights =
        new(StringComparer.OrdinalIgnoreCase)
        {
            { "Intolerance",   1.0m },
            { "Itchy",         1.5m },
            { "Rash",          2.0m },
            { "ThroatClosing", 2.8m },
            { "ERVisit",       3.0m }
        };

    // Minimum non-reaction uses before an ingredient is considered "safe"
    private const int SafeUsageThreshold = 3;

    // Confidence thresholds
    private const decimal SuspectThreshold  = 0.15m;
    private const decimal NotifyThreshold   = 0.50m;
    private const decimal HighConfidenceMin = 0.70m;

    private readonly IAllergyIncidentRepository _repo;
    private readonly IIngredientFetchService _ingredients;
    private readonly ILogger<AllergyDifferentialAnalyzer> _logger;

    public AllergyDifferentialAnalyzer(
        IAllergyIncidentRepository repo,
        IIngredientFetchService ingredients,
        ILogger<AllergyDifferentialAnalyzer> logger)
    {
        _repo        = repo;
        _ingredients = ingredients;
        _logger      = logger;
    }

    public async Task RunForMembersAsync(
        Guid householdId, IEnumerable<(Guid? MemberId, string MemberName)> members,
        CancellationToken ct = default)
    {
        foreach (var (memberId, memberName) in members)
        {
            await RunForMemberAsync(householdId, memberId, memberName, ct);
        }
    }

    public async Task RunForMemberAsync(
        Guid householdId, Guid? memberId, string memberName,
        CancellationToken ct = default)
    {
        // ── 1. Fetch all incident-product rows for this member ────────────
        List<AllergyIncidentProductRow> allRows =
            await _repo.GetReactionProductsForMemberAsync(householdId, memberId, ct);

        List<AllergyIncidentProductRow> reactionRows =
            allRows.Where(r => r.HadReaction).ToList();
        List<AllergyIncidentProductRow> controlRows =
            allRows.Where(r => !r.HadReaction).ToList();

        if (reactionRows.Count == 0) { return; }

        // ── 2. Build reaction ingredient map ─────────────────────────────
        // Group by IncidentId so one incident with multiple reaction products counts as 1 incident.
        Dictionary<Guid, List<AllergyIncidentProductRow>> byIncident =
            reactionRows
                .GroupBy(r => r.IncidentId)
                .ToDictionary(g => g.Key, g => g.ToList());

        Dictionary<string, IngredientReactionData> reactionMap =
            new(StringComparer.OrdinalIgnoreCase);

        int totalIncidents = byIncident.Count;

        foreach (KeyValuePair<Guid, List<AllergyIncidentProductRow>> kvp in byIncident)
        {
            decimal maxWeight = kvp.Value
                .Select(r => SeverityWeights.GetValueOrDefault(r.SeverityLevel, 1.0m))
                .Max();

            HashSet<string> incidentIngredients = new(StringComparer.OrdinalIgnoreCase);
            foreach (AllergyIncidentProductRow row in kvp.Value)
            {
                if (row.ProductId is null) { continue; }
                List<string> ingredients =
                    await _ingredients.GetNormalizedIngredientsAsync(row.ProductId.Value, ct);
                foreach (string ing in ingredients) { incidentIngredients.Add(ing); }
            }

            foreach (string ingredient in incidentIngredients)
            {
                if (!reactionMap.TryGetValue(ingredient, out IngredientReactionData? data))
                {
                    data = new IngredientReactionData();
                    reactionMap[ingredient] = data;
                }
                data.IncidentCount++;
                if (maxWeight > data.MaxSeverityWeight) { data.MaxSeverityWeight = maxWeight; }
            }
        }

        // ── 3. Build safe ingredient set ─────────────────────────────────
        HashSet<string> safeIngredients =
            await _ingredients.GetSafeIngredientSetAsync(
                householdId, memberId, SafeUsageThreshold, ct);

        // Also add ingredients from control rows (same meal, no reaction)
        foreach (AllergyIncidentProductRow ctrl in controlRows)
        {
            if (ctrl.ProductId is null) { continue; }
            List<string> ctrlIngs =
                await _ingredients.GetNormalizedIngredientsAsync(ctrl.ProductId.Value, ct);
            foreach (string ing in ctrlIngs) { safeIngredients.Add(ing); }
        }

        // ── 4. Score candidates ───────────────────────────────────────────
        foreach (KeyValuePair<string, IngredientReactionData> kvp in reactionMap)
        {
            string ingredientName = kvp.Key;
            IngredientReactionData data = kvp.Value;

            if (safeIngredients.Contains(ingredientName))
            {
                await _repo.InsertClearedIngredientAsync(
                    householdId, memberId, memberName, ingredientName, null, ct);
                continue;
            }

            // Confidence = incidentFrequency × normalizedSeverityWeight
            decimal frequency  = (decimal)data.IncidentCount / totalIncidents;
            decimal normWeight = data.MaxSeverityWeight / 3.0m;   // normalize to 0-1
            decimal confidence = frequency * normWeight;

            if (confidence < SuspectThreshold) { continue; }

            await _repo.UpsertSuspectedAllergenAsync(
                householdId, memberId, memberName,
                ingredientName, ingredientId: null,
                confidence, data.IncidentCount, ct);

            _logger.LogInformation(
                "Suspect allergen {Ingredient} for member {Member}: confidence={Conf:P0}",
                ingredientName, memberName, confidence);
        }
    }

    private sealed class IngredientReactionData
    {
        public int     IncidentCount     { get; set; }
        public decimal MaxSeverityWeight { get; set; } = 1.0m;
    }
}
