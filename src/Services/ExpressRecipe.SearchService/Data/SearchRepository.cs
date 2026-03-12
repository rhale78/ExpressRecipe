using ExpressRecipe.Data.Common;
using System.Text.Json;

namespace ExpressRecipe.SearchService.Data;

public class SearchRepository : SqlHelper, ISearchRepository
{
    private readonly ILogger<SearchRepository> _logger;

    public SearchRepository(string connectionString, ILogger<SearchRepository> logger) : base(connectionString)
    {
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

        return (await ExecuteScalarAsync<Guid>(sql,
            CreateParameter("@EntityType", entityType),
            CreateParameter("@EntityId", entityId),
            CreateParameter("@Title", title),
            CreateParameter("@Description", description),
            CreateParameter("@Category", (object?)category ?? DBNull.Value),
            CreateParameter("@Tags", JsonSerializer.Serialize(tags)),
            CreateParameter("@Metadata", JsonSerializer.Serialize(metadata))))!;
    }

    public async Task UpdateIndexAsync(Guid indexId, string title, string description, string? category, List<string> tags)
    {
        const string sql = @"
            UPDATE SearchIndex
            SET Title = @Title, Description = @Description, Category = @Category, Tags = @Tags, UpdatedAt = GETUTCDATE()
            WHERE Id = @IndexId";

        await ExecuteNonQueryAsync(sql,
            CreateParameter("@IndexId", indexId),
            CreateParameter("@Title", title),
            CreateParameter("@Description", description),
            CreateParameter("@Category", (object?)category ?? DBNull.Value),
            CreateParameter("@Tags", JsonSerializer.Serialize(tags)));
    }

    public async Task RemoveFromIndexAsync(string entityType, Guid entityId)
    {
        const string sql = "DELETE FROM SearchIndex WHERE EntityType = @EntityType AND EntityId = @EntityId";
        await ExecuteNonQueryAsync(sql,
            CreateParameter("@EntityType", entityType),
            CreateParameter("@EntityId", entityId));
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

        var items = new List<SearchItemDto>();
        int totalCount = 0;

        var rows = await ExecuteReaderAsync<(SearchItemDto Item, int Total)>(sql, reader =>
        {
            var total = GetInt32(reader, "TotalCount");
            return (new SearchItemDto
            {
                EntityType = GetString(reader, "EntityType")!,
                EntityId = GetGuid(reader, "EntityId"),
                Title = GetString(reader, "Title")!,
                Description = GetString(reader, "Description")!,
                Category = GetString(reader, "Category"),
                Tags = JsonSerializer.Deserialize<List<string>>(GetString(reader, "Tags")!) ?? new(),
                Metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(GetString(reader, "Metadata")!) ?? new(),
                Relevance = GetInt32(reader, "Relevance")
            }, total);
        },
        CreateParameter("@LikeQuery", $"%{query}%"),
        CreateParameter("@EntityType", (object?)entityType ?? DBNull.Value),
        CreateParameter("@Category", (object?)category ?? DBNull.Value),
        CreateParameter("@Offset", offset),
        CreateParameter("@Limit", limit));

        foreach (var (item, total) in rows)
        {
            if (totalCount == 0) totalCount = total;
            items.Add(item);
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

        return await ExecuteReaderAsync<SearchSuggestionDto>(sql, reader => new SearchSuggestionDto
        {
            Suggestion = GetString(reader, "Suggestion")!,
            EntityType = GetString(reader, "EntityType")!,
            Frequency = GetInt32(reader, "Frequency")
        },
        CreateParameter("@Query", $"{partialQuery}%"),
        CreateParameter("@Limit", limit));
    }

    public async Task<SearchResultDto> SearchByTagsAsync(List<string> tags, string? entityType = null, int limit = 50)
    {
        var sql = @"
            SELECT EntityType, EntityId, Title, Description, Category, Tags, Metadata
            FROM SearchIndex
            WHERE ";

        var tagConditions = new List<string>();
        for (int i = 0; i < tags.Count; i++)
        {
            tagConditions.Add($"Tags LIKE @Tag{i}");
        }
        sql += string.Join(" OR ", tagConditions);

        if (!string.IsNullOrEmpty(entityType))
            sql += " AND EntityType = @EntityType";

        sql += " ORDER BY Title OFFSET 0 ROWS FETCH NEXT @Limit ROWS ONLY";

        var paramList = new List<System.Data.Common.DbParameter>();
        for (int i = 0; i < tags.Count; i++)
        {
            paramList.Add(CreateParameter($"@Tag{i}", $"%{tags[i]}%"));
        }
        paramList.Add(CreateParameter("@EntityType", (object?)entityType ?? DBNull.Value));
        paramList.Add(CreateParameter("@Limit", limit));

        var items = await ExecuteReaderAsync<SearchItemDto>(sql, reader => new SearchItemDto
        {
            EntityType = GetString(reader, "EntityType")!,
            EntityId = GetGuid(reader, "EntityId"),
            Title = GetString(reader, "Title")!,
            Description = GetString(reader, "Description")!,
            Category = GetString(reader, "Category"),
            Tags = JsonSerializer.Deserialize<List<string>>(GetString(reader, "Tags")!) ?? new(),
            Metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(GetString(reader, "Metadata")!) ?? new()
        },
        paramList.ToArray());

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

        return (await ExecuteScalarAsync<Guid>(sql,
            CreateParameter("@UserId", userId),
            CreateParameter("@Query", query),
            CreateParameter("@EntityType", (object?)entityType ?? DBNull.Value),
            CreateParameter("@ResultCount", resultCount),
            CreateParameter("@HadResults", hadResults)))!;
    }

    public async Task<List<SearchHistoryDto>> GetUserSearchHistoryAsync(Guid userId, int limit = 20)
    {
        const string sql = @"
            SELECT TOP (@Limit) Id, UserId, Query, EntityType, ResultCount, HadResults, SearchedAt
            FROM SearchHistory
            WHERE UserId = @UserId
            ORDER BY SearchedAt DESC";

        return await ExecuteReaderAsync<SearchHistoryDto>(sql, reader => new SearchHistoryDto
        {
            Id = GetGuid(reader, "Id"),
            UserId = GetGuid(reader, "UserId"),
            Query = GetString(reader, "Query")!,
            EntityType = GetString(reader, "EntityType"),
            ResultCount = GetInt32(reader, "ResultCount"),
            HadResults = GetBoolean(reader, "HadResults"),
            SearchedAt = GetDateTime(reader, "SearchedAt")
        },
        CreateParameter("@UserId", userId),
        CreateParameter("@Limit", limit));
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

        return await ExecuteReaderAsync<PopularSearchDto>(sql, reader => new PopularSearchDto
        {
            Query = GetString(reader, "Query")!,
            EntityType = GetString(reader, "EntityType"),
            SearchCount = GetInt32(reader, "SearchCount"),
            UniqueUsers = GetInt32(reader, "UniqueUsers")
        },
        CreateParameter("@Limit", limit),
        CreateParameter("@DaysBack", daysBack),
        CreateParameter("@EntityType", (object?)entityType ?? DBNull.Value));
    }

    public async Task ClearUserSearchHistoryAsync(Guid userId)
    {
        const string sql = "DELETE FROM SearchHistory WHERE UserId = @UserId";
        await ExecuteNonQueryAsync(sql, CreateParameter("@UserId", userId));
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

        await ExecuteNonQueryAsync(sql,
            CreateParameter("@UserId", userId),
            CreateParameter("@PreferenceKey", preferenceKey),
            CreateParameter("@PreferenceValue", preferenceValue));
    }

    public async Task<Dictionary<string, string>> GetUserPreferencesAsync(Guid userId)
    {
        const string sql = @"
            SELECT PreferenceKey, PreferenceValue
            FROM UserPreference
            WHERE UserId = @UserId";

        var rows = await ExecuteReaderAsync<(string Key, string Value)>(sql,
            reader => (GetString(reader, "PreferenceKey")!, GetString(reader, "PreferenceValue")!),
            CreateParameter("@UserId", userId));

        return rows.ToDictionary(r => r.Key, r => r.Value);
    }

    public async Task DeleteSearchPreferenceAsync(Guid userId, string preferenceKey)
    {
        const string sql = "DELETE FROM UserPreference WHERE UserId = @UserId AND PreferenceKey = @PreferenceKey";
        await ExecuteNonQueryAsync(sql,
            CreateParameter("@UserId", userId),
            CreateParameter("@PreferenceKey", preferenceKey));
    }

    public async Task<Guid> CreateRecommendationAsync(Guid userId, string entityType, Guid entityId, string reason, decimal score)
    {
        const string sql = @"
            INSERT INTO Recommendation (UserId, EntityType, EntityId, Reason, Score, GeneratedAt)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @EntityType, @EntityId, @Reason, @Score, GETUTCDATE())";

        return (await ExecuteScalarAsync<Guid>(sql,
            CreateParameter("@UserId", userId),
            CreateParameter("@EntityType", entityType),
            CreateParameter("@EntityId", entityId),
            CreateParameter("@Reason", reason),
            CreateParameter("@Score", score)))!;
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

        return await ExecuteReaderAsync<RecommendationDto>(sql, reader => new RecommendationDto
        {
            Id = GetGuid(reader, "Id"),
            UserId = GetGuid(reader, "UserId"),
            EntityType = GetString(reader, "EntityType")!,
            EntityId = GetGuid(reader, "EntityId"),
            Reason = GetString(reader, "Reason")!,
            Score = GetDecimal(reader, "Score"),
            GeneratedAt = GetDateTime(reader, "GeneratedAt")
        },
        CreateParameter("@Limit", limit),
        CreateParameter("@UserId", userId),
        CreateParameter("@EntityType", (object?)entityType ?? DBNull.Value));
    }

    public async Task RefreshRecommendationsAsync(Guid userId)
    {
        const string deleteSql = "DELETE FROM Recommendation WHERE UserId = @UserId";
        await ExecuteNonQueryAsync(deleteSql, CreateParameter("@UserId", userId));

        // In production, this would generate new recommendations based on user history
        _logger.LogInformation("Refreshed recommendations for user {UserId}", userId);
    }

    public async Task DeleteUserDataAsync(Guid userId, CancellationToken ct = default)
    {
        const string sql = @"
DELETE FROM Recommendation  WHERE UserId = @UserId;
DELETE FROM SearchHistory   WHERE UserId = @UserId;
DELETE FROM UserPreference  WHERE UserId = @UserId;";

        await ExecuteNonQueryAsync(sql, CreateParameter("@UserId", userId));
    }
}
