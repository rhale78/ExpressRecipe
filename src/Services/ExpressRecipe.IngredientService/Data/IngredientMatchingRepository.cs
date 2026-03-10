using ExpressRecipe.Data.Common;
using ExpressRecipe.IngredientService.Services.Matching;
using Microsoft.Data.SqlClient;

namespace ExpressRecipe.IngredientService.Data;

public class IngredientMatchingRepository : SqlHelper, IIngredientMatchingRepository
{
    public IngredientMatchingRepository(string connectionString) : base(connectionString) { }

    public async Task<(Guid Id, string Name)?> ExactMatchAsync(string normalizedName, CancellationToken ct)
    {
        const string sql = @"
            SELECT TOP 1 Id, Name
            FROM Ingredient
            WHERE NormalizedName = @NormalizedName AND IsDeleted = 0";

        List<(Guid, string)> results = await ExecuteReaderAsync(
            sql,
            reader => (reader.GetGuid(0), reader.GetString(1)),
            new SqlParameter("@NormalizedName", normalizedName));

        if (results.Count > 0) { return results[0]; }
        return null;
    }

    public async Task<(Guid Id, string Name)?> AliasMatchAsync(string normalizedAlias, CancellationToken ct)
    {
        const string sql = @"
            SELECT TOP 1 i.Id, i.Name
            FROM IngredientAlias a
            INNER JOIN Ingredient i ON i.Id = a.IngredientId
            WHERE a.NormalizedAlias = @NormalizedAlias
              AND i.IsDeleted = 0";

        List<(Guid, string)> results = await ExecuteReaderAsync(
            sql,
            reader => (reader.GetGuid(0), reader.GetString(1)),
            new SqlParameter("@NormalizedAlias", normalizedAlias));

        if (results.Count > 0) { return results[0]; }
        return null;
    }

    public async Task<List<(Guid Id, string Name, string AlternativeNames)>> GetAllForFuzzyAsync(CancellationToken ct)
    {
        const string sql = @"
            SELECT Id, Name, ISNULL(AlternativeNames, '')
            FROM Ingredient
            WHERE IsDeleted = 0";

        return await ExecuteReaderAsync(
            sql,
            reader => (reader.GetGuid(0), reader.GetString(1), reader.GetString(2)));
    }

    public async Task QueueUnresolvedAsync(string raw, string normalized, string sourceService,
        Guid? sourceEntityId, Guid? bestMatchId, string? bestMatchName,
        decimal? bestConfidence, string? bestStrategy, CancellationToken ct)
    {
        const string sql = @"
            MERGE UnresolvedIngredientQueue WITH (HOLDLOCK) AS target
            USING (SELECT @NormalizedText AS NormalizedText) AS source
            ON target.NormalizedText = source.NormalizedText
            WHEN MATCHED AND target.ResolvedAt IS NULL THEN
                UPDATE SET
                    OccurrenceCount = target.OccurrenceCount + 1,
                    BestMatchId     = ISNULL(@BestMatchId, target.BestMatchId),
                    BestMatchName   = ISNULL(@BestMatchName, target.BestMatchName),
                    BestConfidence  = ISNULL(@BestConfidence, target.BestConfidence),
                    BestStrategy    = ISNULL(@BestStrategy, target.BestStrategy),
                    UpdatedAt       = GETUTCDATE()
            WHEN NOT MATCHED THEN
                INSERT (Id, RawText, NormalizedText, SourceService, SourceEntityId,
                        BestMatchId, BestMatchName, BestConfidence, BestStrategy, CreatedAt)
                VALUES (NEWID(), @RawText, @NormalizedText, @SourceService, @SourceEntityId,
                        @BestMatchId, @BestMatchName, @BestConfidence, @BestStrategy, GETUTCDATE());";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@RawText", raw),
            new SqlParameter("@NormalizedText", normalized),
            new SqlParameter("@SourceService", sourceService),
            new SqlParameter("@SourceEntityId", (object?)sourceEntityId ?? System.DBNull.Value),
            new SqlParameter("@BestMatchId", (object?)bestMatchId ?? System.DBNull.Value),
            new SqlParameter("@BestMatchName", (object?)bestMatchName ?? System.DBNull.Value),
            new SqlParameter("@BestConfidence", (object?)bestConfidence ?? System.DBNull.Value),
            new SqlParameter("@BestStrategy", (object?)bestStrategy ?? System.DBNull.Value));
    }

    public async Task IncrementAliasMatchCountAsync(string normalizedAlias, CancellationToken ct)
    {
        const string sql = @"
            UPDATE IngredientAlias
            SET MatchCount = MatchCount + 1
            WHERE NormalizedAlias = @NormalizedAlias";

        await ExecuteNonQueryAsync(sql, new SqlParameter("@NormalizedAlias", normalizedAlias));
    }

    public async Task<bool> CreateAliasAsync(Guid ingredientId, string aliasText, string source, CancellationToken ct)
    {
        string normalizedAlias = aliasText.Trim().ToLowerInvariant();
        const string sql = @"
            IF NOT EXISTS (SELECT 1 FROM IngredientAlias WHERE NormalizedAlias = @NormalizedAlias)
            BEGIN
                INSERT INTO IngredientAlias (Id, IngredientId, AliasText, NormalizedAlias, Source, CreatedAt)
                VALUES (NEWID(), @IngredientId, @AliasText, @NormalizedAlias, @Source, GETUTCDATE())
            END";

        int rows = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@IngredientId", ingredientId),
            new SqlParameter("@AliasText", aliasText),
            new SqlParameter("@NormalizedAlias", normalizedAlias),
            new SqlParameter("@Source", source));

        return rows > 0;
    }

    public async Task ResolveQueueItemAsync(Guid queueItemId, Guid? resolvedToId, string resolution, CancellationToken ct)
    {
        const string sql = @"
            UPDATE UnresolvedIngredientQueue
            SET ResolvedAt  = GETUTCDATE(),
                ResolvedToId = @ResolvedToId,
                Resolution   = @Resolution,
                UpdatedAt    = GETUTCDATE()
            WHERE Id = @Id";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", queueItemId),
            new SqlParameter("@ResolvedToId", (object?)resolvedToId ?? System.DBNull.Value),
            new SqlParameter("@Resolution", resolution));
    }

    public async Task<List<UnresolvedQueueItem>> GetUnresolvedQueueAsync(int page, int pageSize, int minOccurrences, CancellationToken ct)
    {
        const string sql = @"
            SELECT Id, RawText, NormalizedText, SourceService, SourceEntityId,
                   BestMatchId, BestMatchName, BestConfidence, BestStrategy,
                   OccurrenceCount, CreatedAt, UpdatedAt
            FROM UnresolvedIngredientQueue
            WHERE ResolvedAt IS NULL
              AND OccurrenceCount >= @MinOccurrences
            ORDER BY OccurrenceCount DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

        return await ExecuteReaderAsync(sql,
            reader => new UnresolvedQueueItem
            {
                Id              = reader.GetGuid(0),
                RawText         = reader.GetString(1),
                NormalizedText  = reader.GetString(2),
                SourceService   = reader.GetString(3),
                SourceEntityId  = reader.IsDBNull(4) ? null : reader.GetGuid(4),
                BestMatchId     = reader.IsDBNull(5) ? null : reader.GetGuid(5),
                BestMatchName   = reader.IsDBNull(6) ? null : reader.GetString(6),
                BestConfidence  = reader.IsDBNull(7) ? null : reader.GetDecimal(7),
                BestStrategy    = reader.IsDBNull(8) ? null : reader.GetString(8),
                OccurrenceCount = reader.GetInt32(9),
                CreatedAt       = reader.GetDateTime(10),
                UpdatedAt       = reader.IsDBNull(11) ? null : reader.GetDateTime(11)
            },
            new SqlParameter("@MinOccurrences", minOccurrences),
            new SqlParameter("@Offset", (page - 1) * pageSize),
            new SqlParameter("@PageSize", pageSize));
    }
    public async Task<UnresolvedQueueItem?> GetQueueItemAsync(Guid id, CancellationToken ct)
    {
        const string sql = @"
            SELECT Id, RawText, NormalizedText, SourceService, SourceEntityId,
                   BestMatchId, BestMatchName, BestConfidence, BestStrategy,
                   OccurrenceCount, CreatedAt, UpdatedAt
            FROM UnresolvedIngredientQueue
            WHERE Id = @Id";

        List<UnresolvedQueueItem> results = await ExecuteReaderAsync(sql,
            reader => new UnresolvedQueueItem
            {
                Id              = reader.GetGuid(0),
                RawText         = reader.GetString(1),
                NormalizedText  = reader.GetString(2),
                SourceService   = reader.GetString(3),
                SourceEntityId  = reader.IsDBNull(4) ? null : reader.GetGuid(4),
                BestMatchId     = reader.IsDBNull(5) ? null : reader.GetGuid(5),
                BestMatchName   = reader.IsDBNull(6) ? null : reader.GetString(6),
                BestConfidence  = reader.IsDBNull(7) ? null : reader.GetDecimal(7),
                BestStrategy    = reader.IsDBNull(8) ? null : reader.GetString(8),
                OccurrenceCount = reader.GetInt32(9),
                CreatedAt       = reader.GetDateTime(10),
                UpdatedAt       = reader.IsDBNull(11) ? null : reader.GetDateTime(11)
            },
            new SqlParameter("@Id", id));

        return results.Count > 0 ? results[0] : null;
    }
}

public sealed class UnresolvedQueueItem
{
    public Guid Id { get; init; }
    public string RawText { get; init; } = string.Empty;
    public string NormalizedText { get; init; } = string.Empty;
    public string SourceService { get; init; } = string.Empty;
    public Guid? SourceEntityId { get; init; }
    public Guid? BestMatchId { get; init; }
    public string? BestMatchName { get; init; }
    public decimal? BestConfidence { get; init; }
    public string? BestStrategy { get; init; }
    public int OccurrenceCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

