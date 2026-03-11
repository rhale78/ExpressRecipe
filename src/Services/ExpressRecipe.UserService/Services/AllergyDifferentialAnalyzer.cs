using ExpressRecipe.UserService.Data;
using System.Net.Http.Json;

namespace ExpressRecipe.UserService.Services;

/// <summary>
/// Runs differential analysis on allergy incidents to identify suspected allergens.
/// For each product name that appears in reaction incidents more than control incidents,
/// a SuspectedAllergen row is upserted with a confidence score.
/// If the score crosses NotifyThreshold, an AllergyAlert notification is sent.
/// </summary>
public class AllergyDifferentialAnalyzer
{
    private const decimal NotifyThreshold = 0.50m;
    private const int     LookbackDays    = 180;
    private const int     MinIncidents    = 2;   // Need at least 2 incidents before suspecting

    private readonly IAllergyIncidentRepository _repo;
    private readonly IHttpClientFactory         _http;
    private readonly ILogger<AllergyDifferentialAnalyzer> _logger;

    public AllergyDifferentialAnalyzer(
        IAllergyIncidentRepository repo,
        IHttpClientFactory http,
        ILogger<AllergyDifferentialAnalyzer> logger)
    {
        _repo   = repo;
        _http   = http;
        _logger = logger;
    }

    /// <summary>
    /// Runs analysis for all members associated with a given household.
    /// memberIds should include null (= primary user) and any FamilyMember GUIDs affected.
    /// </summary>
    public async Task RunForMembersAsync(Guid householdId, IEnumerable<(Guid? MemberId, string MemberName)> members,
        CancellationToken ct = default)
    {
        foreach (var (memberId, memberName) in members)
        {
            await RunForMemberAsync(householdId, memberId, memberName, ct);
        }
    }

    public async Task RunForMemberAsync(Guid householdId, Guid? memberId, string memberName,
        CancellationToken ct = default)
    {
        try
        {
            var stats = await _repo.GetProductReactionStatsAsync(householdId, memberId, LookbackDays, ct);

            foreach (var (productName, reactionCount, totalCount) in stats)
            {
                if (totalCount < MinIncidents) continue;

                decimal confidence = (decimal)reactionCount / totalCount;

                // Skip ingredients the user has explicitly cleared
                if (await _repo.IsIngredientClearedAsync(householdId, memberId, productName, ct))
                    continue;

                await _repo.UpsertSuspectedAllergenAsync(
                    householdId, memberId, productName, confidence, reactionCount, ct);

                if (confidence >= NotifyThreshold)
                {
                    await NotifyNewSuspectAsync(householdId, memberName, productName, confidence, ct);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error running allergy differential analysis for household {HouseholdId} member {MemberId}",
                householdId, memberId);
        }
    }

    private async Task NotifyNewSuspectAsync(Guid householdId, string memberName,
        string ingredientName, decimal confidence, CancellationToken ct)
    {
        HttpClient client = _http.CreateClient("NotificationService");
        try
        {
            using var response = await client.PostAsJsonAsync("/api/Notification/internal", new
            {
                userId            = householdId,        // primary user receives the notification
                type              = "AllergyAlert",
                priority          = "High",
                title             = $"⚠ Possible allergen detected: {ingredientName}",
                message           = $"Based on logged reactions, {ingredientName} may be causing issues for " +
                                    $"{memberName}. Review in the Allergy section. " +
                                    "This is not a medical diagnosis — consult your doctor.",
                relatedEntityType = "SuspectedAllergen"
            }, cancellationToken: ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Allergy notification for {IngredientName} returned {StatusCode}",
                    ingredientName, (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send allergy notification for {IngredientName}", ingredientName);
        }
    }
}
