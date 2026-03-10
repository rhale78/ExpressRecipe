using System.Net.Http.Json;
using ExpressRecipe.Client.Shared.Models.Discovery;

namespace ExpressRecipe.Client.Shared.Services;

public interface IPantryDiscoveryApiClient
{
    Task<PantryDiscoveryResult?> DiscoverAsync(
        decimal minMatch = 0.80m,
        string sortBy = "match",
        int limit = 24,
        bool respectDiet = true);
}

public class PantryDiscoveryApiClient : IPantryDiscoveryApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ITokenProvider _tokenProvider;

    public PantryDiscoveryApiClient(HttpClient httpClient, ITokenProvider tokenProvider)
    {
        _httpClient    = httpClient;
        _tokenProvider = tokenProvider;
    }

    private async Task<bool> EnsureAuthenticatedAsync()
    {
        string? token = await _tokenProvider.GetAccessTokenAsync();
        if (string.IsNullOrEmpty(token)) { return false; }

        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return true;
    }

    public async Task<PantryDiscoveryResult?> DiscoverAsync(
        decimal minMatch = 0.80m,
        string sortBy = "match",
        int limit = 24,
        bool respectDiet = true)
    {
        if (!await EnsureAuthenticatedAsync()) { return null; }

        try
        {
            string url = $"/api/discover?minMatch={minMatch}&sortBy={sortBy}&limit={limit}&respectDiet={respectDiet}";
            return await _httpClient.GetFromJsonAsync<PantryDiscoveryResult>(url);
        }
        catch
        {
            return null;
        }
    }
}
