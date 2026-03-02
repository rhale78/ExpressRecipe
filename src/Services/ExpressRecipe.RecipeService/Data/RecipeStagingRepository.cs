using ExpressRecipe.Data.Common;
using System.Data;
using Microsoft.Data.SqlClient;

namespace ExpressRecipe.RecipeService.Data;

public interface IRecipeStagingRepository
{
    Task<Guid> InsertStagingRecipeAsync(StagedRecipe recipe);
    Task<int> BulkInsertStagingRecipesAsync(IEnumerable<StagedRecipe> recipes);
    Task<List<StagedRecipe>> GetPendingRecipesAsync(int limit = 100);
    Task UpdateProcessingStatusAsync(Guid id, string status, string? error = null);
    Task BulkUpdateStatusAsync(IEnumerable<Guid> ids, string status, string? error = null);
    Task ResetProcessingStatusAsync(int? olderThanMinutes = null);
    Task ResetFailedStatusAsync(int? olderThanMinutes = null);
    Task<int> GetPendingCountAsync();
    Task<bool> ExistsByExternalIdAsync(string externalId);
    Task<bool> IsCompletedByExternalIdAsync(string externalId);
    Task<HashSet<string>> GetExistingExternalIdsAsync(IEnumerable<string> externalIds);
    Task<HashSet<string>> GetAllExternalIdsAsync();
    Task<List<StagedRecipe>> GetRecipesByStatusAsync(string status, int limit = 10000);
}

public class RecipeStagingRepository : SqlHelper, IRecipeStagingRepository
{
    public RecipeStagingRepository(string connectionString) : base(connectionString)
    {
    }

    public async Task<Guid> InsertStagingRecipeAsync(StagedRecipe recipe)
    {
        const string sql = @"
            INSERT INTO RecipeStaging (
                ExternalId, Title, Description, IngredientsRaw, DirectionsRaw,
                NerIngredientsRaw, Source, SourceUrl, CookingTimeMinutes, Servings,
                Rating, RatingCount, TagsRaw, PublishDate, ImageName, RawJson
            )
            VALUES (
                @ExternalId, @Title, @Description, @IngredientsRaw, @DirectionsRaw,
                @NerIngredientsRaw, @Source, @SourceUrl, @CookingTimeMinutes, @Servings,
                @Rating, @RatingCount, @TagsRaw, @PublishDate, @ImageName, @RawJson
            );
            SELECT CAST(SCOPE_IDENTITY() AS UNIQUEIDENTIFIER);";

        return await ExecuteScalarAsync<Guid>(sql,
            new SqlParameter("@ExternalId", (object?)recipe.ExternalId ?? DBNull.Value),
            new SqlParameter("@Title", recipe.Title),
            new SqlParameter("@Description", (object?)recipe.Description ?? DBNull.Value),
            new SqlParameter("@IngredientsRaw", (object?)recipe.IngredientsRaw ?? DBNull.Value),
            new SqlParameter("@DirectionsRaw", (object?)recipe.DirectionsRaw ?? DBNull.Value),
            new SqlParameter("@NerIngredientsRaw", (object?)recipe.NerIngredientsRaw ?? DBNull.Value),
            new SqlParameter("@Source", (object?)recipe.Source ?? DBNull.Value),
            new SqlParameter("@SourceUrl", (object?)recipe.SourceUrl ?? DBNull.Value),
            new SqlParameter("@CookingTimeMinutes", (object?)recipe.CookingTimeMinutes ?? DBNull.Value),
            new SqlParameter("@Servings", (object?)recipe.Servings ?? DBNull.Value),
            new SqlParameter("@Rating", (object?)recipe.Rating ?? DBNull.Value),
            new SqlParameter("@RatingCount", (object?)recipe.RatingCount ?? DBNull.Value),
            new SqlParameter("@TagsRaw", (object?)recipe.TagsRaw ?? DBNull.Value),
            new SqlParameter("@PublishDate", (object?)recipe.PublishDate ?? DBNull.Value),
            new SqlParameter("@ImageName", (object?)recipe.ImageName ?? DBNull.Value),
            new SqlParameter("@RawJson", (object?)recipe.RawJson ?? DBNull.Value)
        );
    }

    public async Task<int> BulkInsertStagingRecipesAsync(IEnumerable<StagedRecipe> recipes)
    {
        var recipeList = recipes.ToList();
        if (!recipeList.Any()) return 0;

        return await ExecuteWithDeadlockRetryAsync(async () =>
        {
            using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            using var transaction = connection.BeginTransaction();
            try
            {
                var dt = new DataTable();
                dt.Columns.Add("ExternalId", typeof(string));
                dt.Columns.Add("Title", typeof(string));
                dt.Columns.Add("Description", typeof(string));
                dt.Columns.Add("IngredientsRaw", typeof(string));
                dt.Columns.Add("DirectionsRaw", typeof(string));
                dt.Columns.Add("NerIngredientsRaw", typeof(string));
                dt.Columns.Add("Source", typeof(string));
                dt.Columns.Add("SourceUrl", typeof(string));
                dt.Columns.Add("CookingTimeMinutes", typeof(int));
                dt.Columns.Add("Servings", typeof(int));
                dt.Columns.Add("Rating", typeof(decimal));
                dt.Columns.Add("RatingCount", typeof(int));
                dt.Columns.Add("TagsRaw", typeof(string));
                dt.Columns.Add("PublishDate", typeof(string));
                dt.Columns.Add("ImageName", typeof(string));
                dt.Columns.Add("RawJson", typeof(string));
                dt.Columns.Add("CreatedAt", typeof(DateTime));
                dt.Columns.Add("ProcessingStatus", typeof(string));

                foreach (var recipe in recipeList)
                {
                    dt.Rows.Add(
                        (object?)recipe.ExternalId ?? DBNull.Value,
                        recipe.Title,
                        (object?)recipe.Description ?? DBNull.Value,
                        (object?)recipe.IngredientsRaw ?? DBNull.Value,
                        (object?)recipe.DirectionsRaw ?? DBNull.Value,
                        (object?)recipe.NerIngredientsRaw ?? DBNull.Value,
                        (object?)recipe.Source ?? DBNull.Value,
                        (object?)recipe.SourceUrl ?? DBNull.Value,
                        (object?)recipe.CookingTimeMinutes ?? DBNull.Value,
                        (object?)recipe.Servings ?? DBNull.Value,
                        (object?)recipe.Rating ?? DBNull.Value,
                        (object?)recipe.RatingCount ?? DBNull.Value,
                        (object?)recipe.TagsRaw ?? DBNull.Value,
                        (object?)recipe.PublishDate ?? DBNull.Value,
                        (object?)recipe.ImageName ?? DBNull.Value,
                        (object?)recipe.RawJson ?? DBNull.Value,
                        DateTime.UtcNow,
                        "Pending"
                    );
                }

                using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction);
                bulkCopy.DestinationTableName = "RecipeStaging";
                bulkCopy.BatchSize = 5000;
                bulkCopy.BulkCopyTimeout = 300;

                foreach (DataColumn col in dt.Columns)
                {
                    bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
                }

                await bulkCopy.WriteToServerAsync(dt);
                await transaction.CommitAsync();
                return recipeList.Count;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    public async Task<List<StagedRecipe>> GetPendingRecipesAsync(int limit = 100)
    {
        // Use atomic UPDATE with OUTPUT to ensure we "claim" these records
        // and prevent other workers/tasks from picking them up simultaneously.
        const string sql = @"
            WITH CTE AS (
                SELECT TOP (@Limit) *
                FROM RecipeStaging WITH (UPDLOCK, READPAST)
                WHERE ProcessingStatus = 'Pending'
                    AND IsDeleted = 0
                ORDER BY CreatedAt ASC
            )
            UPDATE CTE
            SET ProcessingStatus = 'Processing', 
                UpdatedAt = GETUTCDATE()
            OUTPUT 
                inserted.Id, inserted.ExternalId, inserted.Title, inserted.Description, inserted.IngredientsRaw, inserted.DirectionsRaw,
                inserted.NerIngredientsRaw, inserted.Source, inserted.SourceUrl, inserted.CookingTimeMinutes, inserted.Servings,
                inserted.Rating, inserted.RatingCount, inserted.TagsRaw, inserted.PublishDate, inserted.ImageName, inserted.RawJson,
                inserted.ProcessingStatus, inserted.ProcessedAt, inserted.ProcessingError, inserted.ProcessingAttempts,
                inserted.CreatedAt, inserted.UpdatedAt;";

        return await ExecuteReaderAsync(sql, MapStagedRecipe, timeoutSeconds: 120, new SqlParameter("@Limit", limit));
    }

    public async Task UpdateProcessingStatusAsync(Guid id, string status, string? error = null)
    {
        const string sql = @"
            UPDATE RecipeStaging
            SET ProcessingStatus = @Status,
                ProcessedAt = CASE WHEN @Status = 'Completed' THEN GETUTCDATE() ELSE ProcessedAt END,
                ProcessingError = @Error,
                ProcessingAttempts = ProcessingAttempts + 1,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@Status", status),
            new SqlParameter("@Error", (object?)error ?? DBNull.Value)
        );
    }

    public async Task BulkUpdateStatusAsync(IEnumerable<Guid> ids, string status, string? error = null)
    {
        var idList = ids.ToList();
        if (!idList.Any()) return;

        await ExecuteTransactionAsync(async (connection, transaction) =>
        {
            // Create temp table for IDs
            using (var cmd = new SqlCommand("CREATE TABLE #UpdateIds (Id UNIQUEIDENTIFIER PRIMARY KEY)", connection, transaction))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            // Bulk insert IDs
            using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction))
            {
                bulkCopy.DestinationTableName = "#UpdateIds";
                var dt = new DataTable();
                dt.Columns.Add("Id", typeof(Guid));
                foreach (var id in idList) dt.Rows.Add(id);
                await bulkCopy.WriteToServerAsync(dt);
            }

            // Perform join update
            const string sql = @"
                UPDATE rs
                SET ProcessingStatus = @Status,
                    ProcessingError = @Error,
                    ProcessingAttempts = ProcessingAttempts + 1,
                    UpdatedAt = GETUTCDATE()
                FROM RecipeStaging rs
                INNER JOIN #UpdateIds u ON rs.Id = u.Id";

            using (var cmd = new SqlCommand(sql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@Status", status);
                cmd.Parameters.AddWithValue("@Error", (object?)error ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
            }
        });
    }

    public async Task ResetProcessingStatusAsync(int? olderThanMinutes = null)
    {
        string sql = @"
            UPDATE RecipeStaging
            SET ProcessingStatus = 'Pending',
                UpdatedAt = GETUTCDATE()
            WHERE ProcessingStatus = 'Processing'";

        if (olderThanMinutes.HasValue)
        {
            sql += $" AND UpdatedAt < DATEADD(MINUTE, -{olderThanMinutes.Value}, GETUTCDATE())";
        }

        await ExecuteNonQueryAsync(sql, timeoutSeconds: 300);
    }

    public async Task ResetFailedStatusAsync(int? olderThanMinutes = null)
    {
        string sql = @"
            UPDATE RecipeStaging
            SET ProcessingStatus = 'Pending',
                UpdatedAt = GETUTCDATE()
            WHERE ProcessingStatus = 'Failed'";

        if (olderThanMinutes.HasValue)
        {
            sql += $" AND UpdatedAt < DATEADD(MINUTE, -{olderThanMinutes.Value}, GETUTCDATE())";
        }

        await ExecuteNonQueryAsync(sql, timeoutSeconds: 300);
    }

    public async Task<int> GetPendingCountAsync()
    {
        const string sql = @"
            SELECT COUNT(*)
            FROM RecipeStaging WITH (NOLOCK)
            WHERE ProcessingStatus = 'Pending'
                AND IsDeleted = 0";

        return await ExecuteScalarAsync<int>(sql, timeoutSeconds: 120);
    }

    public async Task<List<StagedRecipe>> GetRecipesByStatusAsync(string status, int limit = 10000)
    {
        const string sql = @"
            SELECT TOP (@Limit)
                Id, ExternalId, Title, Description, IngredientsRaw, DirectionsRaw,
                NerIngredientsRaw, Source, SourceUrl, CookingTimeMinutes, Servings,
                Rating, RatingCount, TagsRaw, PublishDate, ImageName, RawJson,
                ProcessingStatus, ProcessedAt, ProcessingError, ProcessingAttempts,
                CreatedAt, UpdatedAt
            FROM RecipeStaging WITH (NOLOCK)
            WHERE ProcessingStatus = @Status
                AND IsDeleted = 0
            ORDER BY CreatedAt ASC";

        return await ExecuteReaderAsync(sql, MapStagedRecipe, timeoutSeconds: 300, 
            new SqlParameter("@Limit", limit),
            new SqlParameter("@Status", status));
    }

    public async Task<bool> ExistsByExternalIdAsync(string externalId)
    {
        const string sql = "SELECT COUNT(1) FROM RecipeStaging WHERE ExternalId = @ExternalId AND IsDeleted = 0";
        return await ExecuteScalarAsync<int>(sql, new SqlParameter("@ExternalId", externalId)) > 0;
    }

    public async Task<bool> IsCompletedByExternalIdAsync(string externalId)
    {
        const string sql = "SELECT COUNT(1) FROM RecipeStaging WHERE ExternalId = @ExternalId AND ProcessingStatus = 'Completed' AND IsDeleted = 0";
        return await ExecuteScalarAsync<int>(sql, new SqlParameter("@ExternalId", externalId)) > 0;
    }

    public async Task<HashSet<string>> GetExistingExternalIdsAsync(IEnumerable<string> externalIds)
    {
        // ... (keep existing implementation for incremental checks if cache is not used)
        var idList = externalIds.Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
        if (!idList.Any()) return new HashSet<string>();

        var existingIds = new HashSet<string>();

        await ExecuteTransactionAsync(async (connection, transaction) =>
        {
            using (var cmd = new SqlCommand("CREATE TABLE #CheckIds (ExternalId NVARCHAR(100) PRIMARY KEY)", connection, transaction))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction))
            {
                bulkCopy.DestinationTableName = "#CheckIds";
                var dt = new DataTable();
                dt.Columns.Add("ExternalId", typeof(string));
                foreach (var id in idList) dt.Rows.Add(id);
                await bulkCopy.WriteToServerAsync(dt);
            }

            const string sql = @"
                SELECT DISTINCT rs.ExternalId 
                FROM RecipeStaging rs
                INNER JOIN #CheckIds c ON rs.ExternalId = c.ExternalId
                WHERE rs.IsDeleted = 0";

            using (var cmd = new SqlCommand(sql, connection, transaction))
            {
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        existingIds.Add(reader.GetString(0));
                    }
                }
            }
        });

        return existingIds;
    }

    public async Task<HashSet<string>> GetAllExternalIdsAsync()
    {
        const string sql = "SELECT ExternalId FROM RecipeStaging WITH (NOLOCK) WHERE ExternalId IS NOT NULL AND IsDeleted = 0";
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        await ExecuteReaderAsync<bool>(sql, reader => 
        {
            result.Add(reader.GetString(0));
            return true; // We don't care about the return list
        });
        
        return result;
    }

    private static StagedRecipe MapStagedRecipe(SqlDataReader reader)
    {
        return new StagedRecipe
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            ExternalId = reader.IsDBNull(reader.GetOrdinal("ExternalId")) ? null : reader.GetString(reader.GetOrdinal("ExternalId")),
            Title = reader.GetString(reader.GetOrdinal("Title")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
            IngredientsRaw = reader.IsDBNull(reader.GetOrdinal("IngredientsRaw")) ? null : reader.GetString(reader.GetOrdinal("IngredientsRaw")),
            DirectionsRaw = reader.IsDBNull(reader.GetOrdinal("DirectionsRaw")) ? null : reader.GetString(reader.GetOrdinal("DirectionsRaw")),
            NerIngredientsRaw = reader.IsDBNull(reader.GetOrdinal("NerIngredientsRaw")) ? null : reader.GetString(reader.GetOrdinal("NerIngredientsRaw")),
            Source = reader.IsDBNull(reader.GetOrdinal("Source")) ? null : reader.GetString(reader.GetOrdinal("Source")),
            SourceUrl = reader.IsDBNull(reader.GetOrdinal("SourceUrl")) ? null : reader.GetString(reader.GetOrdinal("SourceUrl")),
            CookingTimeMinutes = reader.IsDBNull(reader.GetOrdinal("CookingTimeMinutes")) ? null : reader.GetInt32(reader.GetOrdinal("CookingTimeMinutes")),
            Servings = reader.IsDBNull(reader.GetOrdinal("Servings")) ? null : reader.GetInt32(reader.GetOrdinal("Servings")),
            Rating = reader.IsDBNull(reader.GetOrdinal("Rating")) ? null : reader.GetDecimal(reader.GetOrdinal("Rating")),
            RatingCount = reader.IsDBNull(reader.GetOrdinal("RatingCount")) ? null : reader.GetInt32(reader.GetOrdinal("RatingCount")),
            TagsRaw = reader.IsDBNull(reader.GetOrdinal("TagsRaw")) ? null : reader.GetString(reader.GetOrdinal("TagsRaw")),
            PublishDate = reader.IsDBNull(reader.GetOrdinal("PublishDate")) ? null : reader.GetString(reader.GetOrdinal("PublishDate")),
            ImageName = reader.IsDBNull(reader.GetOrdinal("ImageName")) ? null : reader.GetString(reader.GetOrdinal("ImageName")),
            RawJson = reader.IsDBNull(reader.GetOrdinal("RawJson")) ? null : reader.GetString(reader.GetOrdinal("RawJson")),
            ProcessingStatus = reader.GetString(reader.GetOrdinal("ProcessingStatus")),
            ProcessedAt = reader.IsDBNull(reader.GetOrdinal("ProcessedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("ProcessedAt")),
            ProcessingError = reader.IsDBNull(reader.GetOrdinal("ProcessingError")) ? null : reader.GetString(reader.GetOrdinal("ProcessingError")),
            ProcessingAttempts = reader.GetInt32(reader.GetOrdinal("ProcessingAttempts")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            UpdatedAt = reader.IsDBNull(reader.GetOrdinal("UpdatedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("UpdatedAt"))
        };
    }
}

public class StagedRecipe
{
    public Guid Id { get; set; }
    public string? ExternalId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IngredientsRaw { get; set; }
    public string? DirectionsRaw { get; set; }
    public string? NerIngredientsRaw { get; set; }
    public string? Source { get; set; }
    public string? SourceUrl { get; set; }
    public int? CookingTimeMinutes { get; set; }
    public int? Servings { get; set; }
    public decimal? Rating { get; set; }
    public int? RatingCount { get; set; }
    public string? TagsRaw { get; set; }
    public string? PublishDate { get; set; }
    public string? ImageName { get; set; }
    public string? RawJson { get; set; }
    public string ProcessingStatus { get; set; } = "Pending";
    public DateTime? ProcessedAt { get; set; }
    public string? ProcessingError { get; set; }
    public int ProcessingAttempts { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
