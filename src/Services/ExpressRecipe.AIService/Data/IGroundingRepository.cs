using Microsoft.Data.SqlClient;

namespace ExpressRecipe.AIService.Data;

public interface IGroundingRepository
{
    Task<List<CookingTechniqueIssueDto>> FindMatchingIssuesAsync(string userMessage,
        int maxResults = 4, CancellationToken ct = default);
    Task<List<IngredientPairingDto>> FindMatchingPairingsAsync(string dishName,
        int maxResults = 10, CancellationToken ct = default);
}

public sealed record CookingTechniqueIssueDto
{
    public string IssueName { get; init; } = string.Empty;
    public string Cause { get; init; } = string.Empty;
    public string Fix { get; init; } = string.Empty;
}

public sealed record IngredientPairingDto
{
    public string PairingType { get; init; } = string.Empty;
    public string Suggestion { get; init; } = string.Empty;
    public string? Notes { get; init; }
}

/// <summary>
/// No-op grounding repository used when the AI database connection is not configured.
/// Returns empty lists so the cooking assistant still works via pure AI inference.
/// </summary>
public sealed class NullGroundingRepository : IGroundingRepository
{
    public Task<List<CookingTechniqueIssueDto>> FindMatchingIssuesAsync(
        string userMessage, int maxResults = 4, CancellationToken ct = default)
        => Task.FromResult(new List<CookingTechniqueIssueDto>());

    public Task<List<IngredientPairingDto>> FindMatchingPairingsAsync(
        string dishName, int maxResults = 10, CancellationToken ct = default)
        => Task.FromResult(new List<IngredientPairingDto>());
}

public sealed class GroundingRepository : IGroundingRepository
{
    private readonly string _connectionString;

    public GroundingRepository(string connectionString) { _connectionString = connectionString; }

    public async Task<List<CookingTechniqueIssueDto>> FindMatchingIssuesAsync(
        string userMessage, int maxResults = 4, CancellationToken ct = default)
    {
        // Split user message into words, match against Keywords column
        // Simple LIKE search — sufficient for seed table size (~15 rows)
        string[] words = userMessage.ToLower().Split(' ', ',', '.', '?', '!')
            .Where(w => w.Length > 3).Distinct().ToArray();

        if (words.Length == 0) { return new List<CookingTechniqueIssueDto>(); }

        // Build: WHERE IsActive=1 AND (Keywords LIKE @w0 OR Keywords LIKE @w1 OR ...)
        string whereClause = string.Join(" OR ",
            words.Select((_, i) => $"LOWER(Keywords) LIKE @w{i}"));

        string sql = $"SELECT TOP (@Max) IssueName, Cause, Fix " +
                     $"FROM CookingTechniqueIssue " +
                     $"WHERE IsActive=1 AND ({whereClause})";

        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@Max", maxResults);
        for (int i = 0; i < words.Length; i++)
        {
            cmd.Parameters.AddWithValue($"@w{i}", $"%{words[i]}%");
        }

        List<CookingTechniqueIssueDto> results = new();
        await using SqlDataReader r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            results.Add(new CookingTechniqueIssueDto
            {
                IssueName = r.GetString(0),
                Cause     = r.GetString(1),
                Fix       = r.GetString(2)
            });
        }
        return results;
    }

    public async Task<List<IngredientPairingDto>> FindMatchingPairingsAsync(
        string dishName, int maxResults = 10, CancellationToken ct = default)
    {
        string[] words = dishName.ToLower().Split(' ', ',')
            .Where(w => w.Length > 2).Distinct().ToArray();

        if (words.Length == 0) { return new List<IngredientPairingDto>(); }

        string whereClause = string.Join(" OR ",
            words.Select((_, i) => $"LOWER(DishKeywords) LIKE @w{i}"));

        string sql = $"SELECT TOP (@Max) PairingType, Suggestion, Notes " +
                     $"FROM IngredientPairing " +
                     $"WHERE {whereClause} " +
                     $"ORDER BY PairingType";

        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@Max", maxResults);
        for (int i = 0; i < words.Length; i++)
        {
            cmd.Parameters.AddWithValue($"@w{i}", $"%{words[i]}%");
        }

        List<IngredientPairingDto> results = new();
        await using SqlDataReader r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            results.Add(new IngredientPairingDto
            {
                PairingType = r.GetString(0),
                Suggestion  = r.GetString(1),
                Notes       = r.IsDBNull(2) ? null : r.GetString(2)
            });
        }
        return results;
    }
}
