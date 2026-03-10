using System.Net.Http.Json;

namespace ExpressRecipe.MealPlanningService.Services;

public interface IHouseholdMemberQuery
{
    Task<List<Guid>> GetActiveMemberUserIdsAsync(Guid householdId, CancellationToken ct = default);
}

/// <summary>
/// Fetches active household member user IDs from InventoryService via HTTP.
/// </summary>
public sealed class HouseholdMemberHttpQuery : IHouseholdMemberQuery
{
    private readonly IHttpClientFactory _http;
    private readonly ILogger<HouseholdMemberHttpQuery> _logger;

    public HouseholdMemberHttpQuery(IHttpClientFactory http, ILogger<HouseholdMemberHttpQuery> logger)
    {
        _http   = http;
        _logger = logger;
    }

    public async Task<List<Guid>> GetActiveMemberUserIdsAsync(Guid householdId, CancellationToken ct = default)
    {
        HttpClient client = _http.CreateClient("InventoryService");
        try
        {
            List<HouseholdMemberSummary>? members =
                await client.GetFromJsonAsync<List<HouseholdMemberSummary>>(
                    $"/api/Household/{householdId}/members", ct);

            if (members is null) { return new List<Guid>(); }

            return members.Where(m => m.IsActive).Select(m => m.UserId).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch household members for {HouseholdId}", householdId);
            return new List<Guid>();
        }
    }

    // Minimal projection of InventoryService HouseholdMemberDto
    private sealed class HouseholdMemberSummary
    {
        public Guid UserId { get; set; }
        public bool IsActive { get; set; }
    }
}
