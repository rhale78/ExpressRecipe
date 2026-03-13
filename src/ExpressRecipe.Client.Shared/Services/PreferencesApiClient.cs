using ExpressRecipe.Client.Shared.Models.Preferences;

namespace ExpressRecipe.Client.Shared.Services;

public interface IPreferencesApiClient
{
    Task<CookProfileDto?> GetCookProfileAsync(Guid memberId);
    Task<bool> UpsertCookProfileAsync(Guid memberId, UpsertCookProfileRequest request);
    Task<TechniqueComfortDto?> GetTechniqueComfortAsync(Guid memberId, string techniqueCode);
    Task<bool> SetTechniqueComfortAsync(Guid memberId, string techniqueCode, SetTechniqueComfortRequest request);
    Task<List<DismissedTipDto>?> GetDismissedTipsAsync(Guid memberId);
    Task<bool> DismissTipAsync(Guid memberId, Guid tipId);
    Task<bool> RestoreTipAsync(Guid memberId, Guid tipId);
}

public class PreferencesApiClient : ApiClientBase, IPreferencesApiClient
{
    public PreferencesApiClient(HttpClient httpClient, ITokenProvider tokenProvider)
        : base(httpClient, tokenProvider)
    {
    }

    public async Task<CookProfileDto?> GetCookProfileAsync(Guid memberId)
        => await GetAsync<CookProfileDto>($"/api/cookprofile/{memberId}");

    public async Task<bool> UpsertCookProfileAsync(Guid memberId, UpsertCookProfileRequest request)
        => await PutAsync($"/api/cookprofile/{memberId}", request);

    public async Task<TechniqueComfortDto?> GetTechniqueComfortAsync(Guid memberId, string techniqueCode)
        => await GetAsync<TechniqueComfortDto>($"/api/cookprofile/{memberId}/techniques/{Uri.EscapeDataString(techniqueCode)}");

    public async Task<bool> SetTechniqueComfortAsync(Guid memberId, string techniqueCode, SetTechniqueComfortRequest request)
        => await PutAsync($"/api/cookprofile/{memberId}/techniques/{Uri.EscapeDataString(techniqueCode)}", request);

    public async Task<List<DismissedTipDto>?> GetDismissedTipsAsync(Guid memberId)
        => await GetAsync<List<DismissedTipDto>>($"/api/cookprofile/{memberId}/dismissedtips");

    public async Task<bool> DismissTipAsync(Guid memberId, Guid tipId)
        => await PostAsync($"/api/cookprofile/{memberId}/dismissedtips/{tipId}", new { });

    public async Task<bool> RestoreTipAsync(Guid memberId, Guid tipId)
        => await DeleteAsync($"/api/cookprofile/{memberId}/dismissedtips/{tipId}");
}
