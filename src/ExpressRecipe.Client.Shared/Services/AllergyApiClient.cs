using ExpressRecipe.Shared.DTOs.User;

namespace ExpressRecipe.Client.Shared.Services;

public interface IAllergyApiClient
{
    // Incidents
    Task<Guid?> CreateIncidentAsync(CreateAllergyIncidentV2Request request);
    Task<AllergyIncidentV2Dto?> GetIncidentAsync(Guid id);
    Task<List<AllergyIncidentV2Dto>?> GetIncidentsAsync(Guid? memberId = null, int limit = 50);

    // Suspects
    Task<List<SuspectedAllergenDto>?> GetSuspectsAsync(Guid? memberId = null);
    Task<bool> ConfirmSuspectAsync(Guid id);
    Task<bool> ClearSuspectAsync(Guid id);

    // Cleared ingredients
    Task<List<ClearedIngredientDto>?> GetClearedIngredientsAsync(Guid? memberId = null);

    // Report
    Task<AllergyReportModel?> GetReportAsync(Guid? memberId = null);
}

public class AllergyApiClient : ApiClientBase, IAllergyApiClient
{
    public AllergyApiClient(HttpClient httpClient, ITokenProvider tokenProvider)
        : base(httpClient, tokenProvider) { }

    public async Task<Guid?> CreateIncidentAsync(CreateAllergyIncidentV2Request request)
    {
        var result = await PostAsync<CreateAllergyIncidentV2Request, IdResponse>(
            "/api/allergy/incidents", request);
        return result?.Id;
    }

    public Task<AllergyIncidentV2Dto?> GetIncidentAsync(Guid id)
        => GetAsync<AllergyIncidentV2Dto>($"/api/allergy/incidents/{id}");

    public Task<List<AllergyIncidentV2Dto>?> GetIncidentsAsync(Guid? memberId = null, int limit = 50)
    {
        var qs = memberId.HasValue ? $"?memberId={memberId}&limit={limit}" : $"?limit={limit}";
        return GetAsync<List<AllergyIncidentV2Dto>>($"/api/allergy/incidents{qs}");
    }

    public Task<List<SuspectedAllergenDto>?> GetSuspectsAsync(Guid? memberId = null)
    {
        var qs = memberId.HasValue ? $"?memberId={memberId}" : string.Empty;
        return GetAsync<List<SuspectedAllergenDto>>($"/api/allergy/suspects{qs}");
    }

    public async Task<bool> ConfirmSuspectAsync(Guid id)
        => await PostAsync<object>($"/api/allergy/suspects/{id}/confirm", new { });

    public async Task<bool> ClearSuspectAsync(Guid id)
        => await DeleteAsync($"/api/allergy/suspects/{id}");

    public Task<List<ClearedIngredientDto>?> GetClearedIngredientsAsync(Guid? memberId = null)
    {
        var qs = memberId.HasValue ? $"?memberId={memberId}" : string.Empty;
        return GetAsync<List<ClearedIngredientDto>>($"/api/allergy/cleared{qs}");
    }

    public Task<AllergyReportModel?> GetReportAsync(Guid? memberId = null)
    {
        var route = memberId.HasValue ? $"/api/allergy/report/{memberId}" : "/api/allergy/report";
        return GetAsync<AllergyReportModel>(route);
    }

    private sealed record IdResponse(Guid Id);
}