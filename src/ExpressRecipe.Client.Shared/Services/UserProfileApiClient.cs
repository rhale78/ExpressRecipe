using ExpressRecipe.Client.Shared.Models.User;

namespace ExpressRecipe.Client.Shared.Services;

/// <summary>
/// API client for user profile and family member management
/// </summary>
public interface IUserProfileApiClient
{
    // User profile
    Task<UserProfileDto?> GetProfileAsync();
    Task<bool> UpdateProfileAsync(UpdateUserProfileRequest request);

    // Family members
    Task<List<FamilyMemberDto>?> GetFamilyMembersAsync();
    Task<FamilyMemberDto?> GetFamilyMemberAsync(Guid id);
    Task<Guid?> CreateFamilyMemberAsync(CreateFamilyMemberRequest request);
    Task<bool> UpdateFamilyMemberAsync(Guid id, UpdateFamilyMemberRequest request);
    Task<bool> DeleteFamilyMemberAsync(Guid id);

    // Allergens and restrictions
    Task<AllergensAndRestrictionsDto?> GetAllergensAndRestrictionsAsync();

    // Admin
    Task<bool> SuspendUserAsync(Guid userId);
    Task<bool> ActivateUserAsync(Guid userId);
}

public class UserProfileApiClient : ApiClientBase, IUserProfileApiClient
{
    public UserProfileApiClient(HttpClient httpClient, ITokenProvider tokenProvider)
        : base(httpClient, tokenProvider)
    {
    }

    // User profile
    public async Task<UserProfileDto?> GetProfileAsync()
    {
        return await GetAsync<UserProfileDto>("/api/userprofile/me");
    }

    public async Task<bool> UpdateProfileAsync(UpdateUserProfileRequest request)
    {
        return await PutAsync("/api/userprofile/me", request);
    }

    // Family members
    public async Task<List<FamilyMemberDto>?> GetFamilyMembersAsync()
    {
        return await GetAsync<List<FamilyMemberDto>>("/api/family");
    }

    public async Task<FamilyMemberDto?> GetFamilyMemberAsync(Guid id)
    {
        return await GetAsync<FamilyMemberDto>($"/api/family/{id}");
    }

    public async Task<Guid?> CreateFamilyMemberAsync(CreateFamilyMemberRequest request)
    {
        var response = await PostAsync<CreateFamilyMemberRequest, CreateFamilyMemberResponse>("/api/family", request);
        return response?.FamilyMemberId;
    }

    public async Task<bool> UpdateFamilyMemberAsync(Guid id, UpdateFamilyMemberRequest request)
    {
        return await PutAsync($"/api/family/{id}", request);
    }

    public async Task<bool> DeleteFamilyMemberAsync(Guid id)
    {
        return await DeleteAsync($"/api/family/{id}");
    }

    // Allergens and restrictions
    public async Task<AllergensAndRestrictionsDto?> GetAllergensAndRestrictionsAsync()
    {
        return await GetAsync<AllergensAndRestrictionsDto>("/api/allergymgmt");
    }

    // Admin
    public async Task<bool> SuspendUserAsync(Guid userId)
    {
        return await PostAsync($"/api/userprofile/admin/{userId}/suspend", new { });
    }

    public async Task<bool> ActivateUserAsync(Guid userId)
    {
        return await PostAsync($"/api/userprofile/admin/{userId}/activate", new { });
    }

    // Helper response classes
    private class CreateFamilyMemberResponse
    {
        public Guid FamilyMemberId { get; set; }
    }
}
