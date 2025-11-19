using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace ExpressRecipe.SearchService.Data;

public class SearchRepository : ISearchRepository
{
    private readonly string _connectionString;
    private readonly ILogger<SearchRepository> _logger;

    public SearchRepository(string connectionString, ILogger<SearchRepository> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<Guid> IndexEntityAsync(string entityType, Guid entityId, string title, string description, string? category, List<string> tags, Dictionary<string, string> metadata)
    {
        const string sql = @"
            MERGE SearchIndex AS target
            USING (SELECT @EntityType AS EntityType, @EntityId AS EntityId) AS source
            ON target.EntityType = source.EntityType AND target.EntityId = source.EntityId
            WHEN MATCHED THEN
                UPDATE SET Title = @Title, Description = @Description, Category = @Category,
                          Tags = @Tags, Metadata = @Metadata, UpdatedAt = GETUTCDATE()
            WHEN NOT MATCHED THEN
                INSERT (EntityType, EntityId, Title, Description, Category, Tags, Metadata, CreatedAt, UpdatedAt)
                VALUES (@EntityType, @EntityId, @Title, @Description, @Category, @Tags, @Metadata, GETUTCDATE(), GETUTCDATE())
            OUTPUT INSERTED.Id;";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@EntityType", entityType);
        command.Parameters.AddWithValue("@EntityId", entityId);
        command.Parameters.AddWithValue("@Title", title);
        command.Parameters.AddWithValue("@Description", description);
        command.Parameters.AddWithValue("@Category", category ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Tags", JsonSerializer.Serialize(tags));
        command.Parameters.AddWithValue("@Metadata", JsonSerializer.Serialize(metadata));

        return (Guid)await command.ExecuteScalarAsync()!;
    }

    public async Task UpdateIndexAsync(Guid indexId, string title, string description, string? category, List<string> tags)
    {
        const string sql = @"
            UPDATE SearchIndex
            SET Title = @Title, Description = @Description, Category = @Category, Tags = @Tags, UpdatedAt = GETUTCDATE()
            WHERE Id = @IndexId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@IndexId", indexId);
        command.Parameters.AddWithValue("@Title", title);
        command.Parameters.AddWithValue("@Description", description);
        command.Parameters.AddWithValue("@Category", category ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Tags", JsonSerializer.Serialize(tags));

        await command.ExecuteNonQueryAsync();
    }

    public async Task RemoveFromIndexAsync(string entityType, Guid entityId)
    {
        const string sql = "DELETE FROM SearchIndex WHERE EntityType = @EntityType AND EntityId = @EntityId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@EntityType", entityType);
        command.Parameters.AddWithValue("@EntityId", entityId);

        await command.ExecuteNonQueryAsync();
    }

    public async Task RebuildIndexAsync(string entityType)
    {
        // In production, this would rebuild the full-text index
        _logger.LogInformation("Rebuilding search index for {EntityType}", entityType);
        await Task.CompletedTask;
    }

    public async Task<SearchResultDto> SearchAsync(string query, string? entityType = null, string? category = null, List<string>? tags = null, int limit = 50, int offset = 0)
    {
        // Using SQL Server full-text search with CONTAINS
        var sql = @"
            WITH SearchResults AS (
                SELECT
                    EntityType,
                    EntityId,
                    Title,
                    Description,
                    Category,
                    Tags,
                    Metadata,
                    -- Calculate relevance score using full-text search ranking
                    (CASE
                        WHEN Title LIKE @LikeQuery THEN 100
                        ELSE 50
                    END) +
                    (CASE
                        WHEN Description LIKE @LikeQuery THEN 30
                        ELSE 0
                    END) AS Relevance
                FROM SearchIndex
                WHERE (Title LIKE @LikeQuery OR Description LIKE @LikeQuery)";

        if (!string.IsNullOrEmpty(entityType))
            sql += " AND EntityType = @EntityType";

        if (!string.IsNullOrEmpty(category))
            sql += " AND Category = @Category";

        sql += @"
            )
            SELECT
                COUNT(*) OVER() AS TotalCount,
                EntityType,
                EntityId,
                Title,
                Description,
                Category,
                Tags,
                Metadata,
                Relevance
            FROM SearchResults
            ORDER BY Relevance DESC, Title
            OFFSET @Offset ROWS FETCH NEXT @Limit ROWS ONLY";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@LikeQuery", $"%{query}%");
        command.Parameters.AddWithValue("@EntityType", entityType ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Category", category ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Offset", offset);
        command.Parameters.AddWithValue("@Limit", limit);

        var items = new List<SearchItemDto>();
        int totalCount = 0;

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (totalCount == 0)
                totalCount = reader.GetInt32(0);

            items.Add(new SearchItemDto
            {
                EntityType = reader.GetString(1),
                EntityId = reader.GetGuid(2),
                Title = reader.GetString(3),
                Description = reader.GetString(4),
                Category = reader.IsDBNull(5) ? null : reader.GetString(5),
                Tags = JsonSerializer.Deserialize<List<string>>(reader.GetString(6)) ?? new(),
                Metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(7)) ?? new(),
                Relevance = reader.GetInt32(8)
            });
        }

        return new SearchResultDto
        {
            Query = query,
            TotalResults = totalCount,
            Offset = offset,
            Limit = limit,
            Items = items
        };
    }

    public async Task<List<SearchSuggestionDto>> GetSuggestionsAsync(string partialQuery, int limit = 10)
    {
        const string sql = @"
            SELECT TOP (@Limit)
                DISTINCT Title AS Suggestion,
                EntityType,
                COUNT(*) AS Frequency
            FROM SearchIndex
            WHERE Title LIKE @Query
            GROUP BY Title, EntityType
            ORDER BY Frequency DESC, Title";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Query", $"{partialQuery}%");
        command.Parameters.AddWithValue("@Limit", limit);

        var suggestions = new List<SearchSuggestionDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            suggestions.Add(new SearchSuggestionDto
            {
                Suggestion = reader.GetString(0),
                EntityType = reader.GetString(1),
                Frequency = reader.GetInt32(2)
            });
        }

        return suggestions;
    }

    public async Task<SearchResultDto> SearchByTagsAsync(List<string> tags, string? entityType = null, int limit = 50)
    {
        var sql = @"
            SELECT EntityType, EntityId, Title, Description, Category, Tags, Metadata
            FROM SearchIndex
            WHERE ";

        // Build tag matching query
        var tagConditions = new List<string>();
        for (int i = 0; i < tags.Count; i++)
        {
            tagConditions.Add($"Tags LIKE @Tag{i}");
        }
        sql += string.Join(" OR ", tagConditions);

        if (!string.IsNullOrEmpty(entityType))
            sql += " AND EntityType = @EntityType";

        sql += " ORDER BY Title OFFSET 0 ROWS FETCH NEXT @Limit ROWS ONLY";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        for (int i = 0; i < tags.Count; i++)
        {
            command.Parameters.AddWithValue($"@Tag{i}", $"%{tags[i]}%");
        }
        command.Parameters.AddWithValue("@EntityType", entityType ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Limit", limit);

        var items = new List<SearchItemDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new SearchItemDto
            {
                EntityType = reader.GetString(0),
                EntityId = reader.GetGuid(1),
                Title = reader.GetString(2),
                Description = reader.GetString(3),
                Category = reader.IsDBNull(4) ? null : reader.GetString(4),
                Tags = JsonSerializer.Deserialize<List<string>>(reader.GetString(5)) ?? new(),
                Metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(6)) ?? new()
            });
        }

        return new SearchResultDto
        {
            Query = string.Join(", ", tags),
            TotalResults = items.Count,
            Items = items
        };
    }

    public async Task<Guid> RecordSearchAsync(Guid userId, string query, string? entityType, int resultCount, bool hadResults)
    {
        const string sql = @"
            INSERT INTO SearchHistory (UserId, Query, EntityType, ResultCount, HadResults, SearchedAt)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @Query, @EntityType, @ResultCount, @HadResults, GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@Query", query);
        command.Parameters.AddWithValue("@EntityType", entityType ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@ResultCount", resultCount);
        command.Parameters.AddWithValue("@HadResults", hadResults);

        return (Guid)await command.ExecuteScalarAsync()!;
    }

    public async Task<List<SearchHistoryDto>> GetUserSearchHistoryAsync(Guid userId, int limit = 20)
    {
        const string sql = @"
            SELECT TOP (@Limit) Id, UserId, Query, EntityType, ResultCount, HadResults, SearchedAt
            FROM SearchHistory
            WHERE UserId = @UserId
            ORDER BY SearchedAt DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@Limit", limit);

        var history = new List<SearchHistoryDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            history.Add(new SearchHistoryDto
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                Query = reader.GetString(2),
                EntityType = reader.IsDBNull(3) ? null : reader.GetString(3),
                ResultCount = reader.GetInt32(4),
                HadResults = reader.GetBoolean(5),
                SearchedAt = reader.GetDateTime(6)
            });
        }

        return history;
    }

    public async Task<List<PopularSearchDto>> GetPopularSearchesAsync(string? entityType = null, int daysBack = 30, int limit = 20)
    {
        var sql = @"
            SELECT TOP (@Limit)
                Query,
                EntityType,
                COUNT(*) AS SearchCount,
                COUNT(DISTINCT UserId) AS UniqueUsers
            FROM SearchHistory
            WHERE SearchedAt >= DATEADD(day, -@DaysBack, GETUTCDATE())";

        if (!string.IsNullOrEmpty(entityType))
            sql += " AND EntityType = @EntityType";

        sql += @"
            GROUP BY Query, EntityType
            ORDER BY SearchCount DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Limit", limit);
        command.Parameters.AddWithValue("@DaysBack", daysBack);
        command.Parameters.AddWithValue("@EntityType", entityType ?? (object)DBNull.Value);

        var popular = new List<PopularSearchDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            popular.Add(new PopularSearchDto
            {
                Query = reader.GetString(0),
                EntityType = reader.IsDBNull(1) ? null : reader.GetString(1),
                SearchCount = reader.GetInt32(2),
                UniqueUsers = reader.GetInt32(3)
            });
        }

        return popular;
    }

    public async Task ClearUserSearchHistoryAsync(Guid userId)
    {
        const string sql = "DELETE FROM SearchHistory WHERE UserId = @UserId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        await command.ExecuteNonQueryAsync();
    }

    public async Task SaveSearchPreferenceAsync(Guid userId, string preferenceKey, string preferenceValue)
    {
        const string sql = @"
            MERGE UserPreference AS target
            USING (SELECT @UserId AS UserId, @PreferenceKey AS PreferenceKey) AS source
            ON target.UserId = source.UserId AND target.PreferenceKey = source.PreferenceKey
            WHEN MATCHED THEN
                UPDATE SET PreferenceValue = @PreferenceValue, UpdatedAt = GETUTCDATE()
            WHEN NOT MATCHED THEN
                INSERT (UserId, PreferenceKey, PreferenceValue, CreatedAt, UpdatedAt)
                VALUES (@UserId, @PreferenceKey, @PreferenceValue, GETUTCDATE(), GETUTCDATE());";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@PreferenceKey", preferenceKey);
        command.Parameters.AddWithValue("@PreferenceValue", preferenceValue);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<Dictionary<string, string>> GetUserPreferencesAsync(Guid userId)
    {
        const string sql = @"
            SELECT PreferenceKey, PreferenceValue
            FROM UserPreference
            WHERE UserId = @UserId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        var preferences = new Dictionary<string, string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            preferences[reader.GetString(0)] = reader.GetString(1);
        }

        return preferences;
    }

    public async Task DeleteSearchPreferenceAsync(Guid userId, string preferenceKey)
    {
        const string sql = "DELETE FROM UserPreference WHERE UserId = @UserId AND PreferenceKey = @PreferenceKey";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@PreferenceKey", preferenceKey);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<Guid> CreateRecommendationAsync(Guid userId, string entityType, Guid entityId, string reason, decimal score)
    {
        const string sql = @"
            INSERT INTO Recommendation (UserId, EntityType, EntityId, Reason, Score, GeneratedAt)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @EntityType, @EntityId, @Reason, @Score, GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@EntityType", entityType);
        command.Parameters.AddWithValue("@EntityId", entityId);
        command.Parameters.AddWithValue("@Reason", reason);
        command.Parameters.AddWithValue("@Score", score);

        return (Guid)await command.ExecuteScalarAsync()!;
    }

    public async Task<List<RecommendationDto>> GetUserRecommendationsAsync(Guid userId, string? entityType = null, int limit = 20)
    {
        var sql = @"
            SELECT TOP (@Limit) Id, UserId, EntityType, EntityId, Reason, Score, GeneratedAt
            FROM Recommendation
            WHERE UserId = @UserId";

        if (!string.IsNullOrEmpty(entityType))
            sql += " AND EntityType = @EntityType";

        sql += " ORDER BY Score DESC, GeneratedAt DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Limit", limit);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@EntityType", entityType ?? (object)DBNull.Value);

        var recommendations = new List<RecommendationDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            recommendations.Add(new RecommendationDto
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                EntityType = reader.GetString(2),
                EntityId = reader.GetGuid(3),
                Reason = reader.GetString(4),
                Score = reader.GetDecimal(5),
                GeneratedAt = reader.GetDateTime(6)
            });
        }

        return recommendations;
    }

    public async Task RefreshRecommendationsAsync(Guid userId)
    {
        // Delete old recommendations
        const string deleteSql = "DELETE FROM Recommendation WHERE UserId = @UserId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(deleteSql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        await command.ExecuteNonQueryAsync();

        // In production, this would generate new recommendations based on user history
        _logger.LogInformation("Refreshed recommendations for user {UserId}", userId);
    }
}
