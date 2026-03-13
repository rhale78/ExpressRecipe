using ExpressRecipe.Client.Shared.Models.SafeFork;

namespace ExpressRecipe.Client.Shared.Services;

public interface ISafeForkApiClient
{
    Task<AllergenProfileDto?> GetEffectiveProfileAsync(Guid memberId, bool includeSchedules = true);
    Task<Guid?> AddCuratedAllergenAsync(Guid memberId, AddCuratedAllergenRequest request);
    Task<Guid?> AddFreeformAllergenAsync(Guid memberId, AddFreeformAllergenRequest request);
    Task<bool> DeleteAllergenEntryAsync(Guid memberId, Guid entryId);
    Task<List<TemporaryScheduleDto>?> GetActiveSchedulesAsync(Guid memberId);
    Task<bool> AddScheduleAsync(Guid memberId, AddTemporaryScheduleRequest request);
    Task<bool> DeleteScheduleAsync(Guid memberId, Guid scheduleId);
}

public class SafeForkApiClient : ApiClientBase, ISafeForkApiClient
{
    public SafeForkApiClient(HttpClient httpClient, ITokenProvider tokenProvider)
        : base(httpClient, tokenProvider)
    {
    }

    public async Task<AllergenProfileDto?> GetEffectiveProfileAsync(Guid memberId, bool includeSchedules = true)
        => await GetAsync<AllergenProfileDto>($"/api/allergenprofile/{memberId}?includeSchedules={includeSchedules.ToString().ToLower()}");

    public async Task<Guid?> AddCuratedAllergenAsync(Guid memberId, AddCuratedAllergenRequest request)
    {
        var result = await PostAsync<AddCuratedAllergenRequest, EntryIdResponse>(
            $"/api/allergenprofile/{memberId}/curated", request);
        return result?.EntryId;
    }

    public async Task<Guid?> AddFreeformAllergenAsync(Guid memberId, AddFreeformAllergenRequest request)
    {
        var result = await PostAsync<AddFreeformAllergenRequest, EntryIdResponse>(
            $"/api/allergenprofile/{memberId}/freeform", request);
        return result?.EntryId;
    }

    public async Task<bool> DeleteAllergenEntryAsync(Guid memberId, Guid entryId)
        => await DeleteAsync($"/api/allergenprofile/{memberId}/entry/{entryId}");

    public async Task<List<TemporaryScheduleDto>?> GetActiveSchedulesAsync(Guid memberId)
        => await GetAsync<List<TemporaryScheduleDto>>($"/api/safeforkschedules/{memberId}");

    public async Task<bool> AddScheduleAsync(Guid memberId, AddTemporaryScheduleRequest request)
        => await PostAsync($"/api/safeforkschedules/{memberId}", request);

    public async Task<bool> DeleteScheduleAsync(Guid memberId, Guid scheduleId)
        => await DeleteAsync($"/api/safeforkschedules/{memberId}/{scheduleId}");

    private record EntryIdResponse(Guid EntryId);
}
