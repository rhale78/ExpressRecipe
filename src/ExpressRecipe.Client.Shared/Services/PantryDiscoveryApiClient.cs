using ExpressRecipe.Client.Shared.Models.Discovery;
using ExpressRecipe.Shared.Services;
using System.Globalization;

namespace ExpressRecipe.Client.Shared.Services;

public interface IPantryDiscoveryApiClient
{
    Task<PantryDiscoveryResult?> DiscoverAsync(
        decimal minMatch = 0.80m,
        string sortBy = "match",
        int limit = 24,
        bool respectDiet = true);
}

public class PantryDiscoveryApiClient : ApiClientBase, IPantryDiscoveryApiClient
{
    public PantryDiscoveryApiClient(HttpClient httpClient, ITokenProvider tokenProvider)
        : base(httpClient, tokenProvider)
    {
    }

    public async Task<PantryDiscoveryResult?> DiscoverAsync(
        decimal minMatch = 0.80m,
        string sortBy = "match",
        int limit = 24,
        bool respectDiet = true)
    {
        string encodedSortBy = Uri.EscapeDataString(sortBy);
        string encodedMinMatch = minMatch.ToString("F2", CultureInfo.InvariantCulture);
        string url = $"/api/discover?minMatch={encodedMinMatch}&sortBy={encodedSortBy}&limit={limit}&respectDiet={respectDiet}";
        return await GetAsync<PantryDiscoveryResult>(url);
    }
}
