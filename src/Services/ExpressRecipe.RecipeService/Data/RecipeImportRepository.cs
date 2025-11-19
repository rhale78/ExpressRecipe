using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.DTOs.Recipe;
using Microsoft.Data.SqlClient;

namespace ExpressRecipe.RecipeService.Data;

public interface IRecipeImportRepository
{
    // Import Sources
    Task<List<RecipeImportSourceDto>> GetImportSourcesAsync(bool activeOnly = true);
    Task<RecipeImportSourceDto?> GetImportSourceByIdAsync(Guid id);

    // Import Jobs
    Task<Guid> CreateImportJobAsync(Guid userId, StartImportJobRequest request);
    Task<RecipeImportJobDto?> GetImportJobByIdAsync(Guid id);
    Task<List<RecipeImportJobDto>> GetUserImportJobsAsync(Guid userId, int limit = 50);
    Task<bool> UpdateImportJobStatusAsync(Guid jobId, string status, int successCount, int failureCount, string? errorLog = null);
    Task<bool> CompleteImportJobAsync(Guid jobId);

    // Import Results
    Task<Guid> CreateImportResultAsync(Guid jobId, string sourceRecipeId, string sourceRecipeName, Guid? importedRecipeId, string status, string? errorMessage = null, string? rawData = null);
    Task<List<RecipeImportResultDto>> GetImportResultsAsync(Guid jobId);

    // Recipe Versions
    Task<Guid> CreateRecipeVersionAsync(Guid recipeId, string changeDescription, Guid createdBy, string snapshotData);
    Task<List<RecipeVersionDto>> GetRecipeVersionsAsync(Guid recipeId);
    Task<RecipeVersionDto?> GetRecipeVersionByIdAsync(Guid id);

    // Recipe Forks
    Task<Guid> ForkRecipeAsync(Guid originalRecipeId, Guid forkedRecipeId, Guid forkedBy, string? forkReason = null);
    Task<List<RecipeForkDto>> GetRecipeForksAsync(Guid originalRecipeId);

    // Recipe Collections
    Task<Guid> CreateCollectionAsync(Guid userId, CreateRecipeCollectionRequest request);
    Task<RecipeCollectionDto?> GetCollectionByIdAsync(Guid id, bool includeItems = true);
    Task<List<RecipeCollectionDto>> GetUserCollectionsAsync(Guid userId, bool includeItemCounts = true);
    Task<bool> UpdateCollectionAsync(Guid id, Guid userId, UpdateRecipeCollectionRequest request);
    Task<bool> DeleteCollectionAsync(Guid id, Guid userId);
    Task<Guid> AddRecipeToCollectionAsync(Guid collectionId, Guid userId, AddRecipeToCollectionRequest request);
    Task<bool> RemoveRecipeFromCollectionAsync(Guid collectionId, Guid recipeId, Guid userId);
    Task<bool> UpdateCollectionItemAsync(Guid itemId, UpdateCollectionItemRequest request);

    // Export
    Task<Guid> CreateExportHistoryAsync(Guid userId, string exportFormat, List<Guid> recipeIds, string fileName, long? fileSize = null, string? fileUrl = null);
    Task<List<RecipeExportHistoryDto>> GetUserExportHistoryAsync(Guid userId, int limit = 50);

    // Summary
    Task<ImportSummaryDto> GetImportSummaryAsync(Guid userId);
    Task<CollectionSummaryDto> GetCollectionSummaryAsync(Guid userId);
}

public class RecipeImportRepository : SqlHelper, IRecipeImportRepository
{
    public RecipeImportRepository(string connectionString) : base(connectionString)
    {
    }

    // Import Sources

    public async Task<List<RecipeImportSourceDto>> GetImportSourcesAsync(bool activeOnly = true)
    {
        var sql = @"
            SELECT Id, Name, SourceType, Description, ParserClassName, SupportedFileExtensions,
                   RequiresApiKey, Website, IsActive
            FROM RecipeImportSource";

        if (activeOnly)
        {
            sql += " WHERE IsActive = 1";
        }

        sql += " ORDER BY Name";

        return await ExecuteReaderAsync(
            sql,
            reader => new RecipeImportSourceDto
            {
                Id = GetGuid(reader, "Id"),
                Name = GetString(reader, "Name") ?? string.Empty,
                SourceType = GetString(reader, "SourceType") ?? string.Empty,
                Description = GetString(reader, "Description"),
                ParserClassName = GetString(reader, "ParserClassName"),
                IsActive = GetBoolean(reader, "IsActive"),
                SupportedFileExtensions = GetString(reader, "SupportedFileExtensions"),
                RequiresApiKey = GetBoolean(reader, "RequiresApiKey"),
                Website = GetString(reader, "Website")
            });
    }

    public async Task<RecipeImportSourceDto?> GetImportSourceByIdAsync(Guid id)
    {
        const string sql = @"
            SELECT Id, Name, SourceType, Description, ParserClassName, SupportedFileExtensions,
                   RequiresApiKey, Website, IsActive
            FROM RecipeImportSource
            WHERE Id = @Id";

        var results = await ExecuteReaderAsync(
            sql,
            reader => new RecipeImportSourceDto
            {
                Id = GetGuid(reader, "Id"),
                Name = GetString(reader, "Name") ?? string.Empty,
                SourceType = GetString(reader, "SourceType") ?? string.Empty,
                Description = GetString(reader, "Description"),
                ParserClassName = GetString(reader, "ParserClassName"),
                IsActive = GetBoolean(reader, "IsActive"),
                SupportedFileExtensions = GetString(reader, "SupportedFileExtensions"),
                RequiresApiKey = GetBoolean(reader, "RequiresApiKey"),
                Website = GetString(reader, "Website")
            },
            new SqlParameter("@Id", id));

        return results.FirstOrDefault();
    }

    // Import Jobs

    public async Task<Guid> CreateImportJobAsync(Guid userId, StartImportJobRequest request)
    {
        var id = Guid.NewGuid();

        const string sql = @"
            INSERT INTO RecipeImportJob (Id, UserId, ImportSourceId, FileName, FileUrl,
                                        Status, TotalRecipes, SuccessCount, FailureCount, CreatedAt)
            VALUES (@Id, @UserId, @ImportSourceId, @FileName, @FileUrl,
                    'Pending', 0, 0, 0, GETUTCDATE())";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@UserId", userId),
            new SqlParameter("@ImportSourceId", request.ImportSourceId),
            new SqlParameter("@FileName", (object?)request.FileName ?? DBNull.Value),
            new SqlParameter("@FileUrl", (object?)request.FileUrl ?? DBNull.Value));

        return id;
    }

    public async Task<RecipeImportJobDto?> GetImportJobByIdAsync(Guid id)
    {
        const string sql = @"
            SELECT rij.Id, rij.UserId, rij.ImportSourceId, ris.Name AS ImportSourceName,
                   rij.FileName, rij.FileUrl, rij.Status, rij.TotalRecipes,
                   rij.SuccessCount, rij.FailureCount, rij.ErrorLog,
                   rij.StartedAt, rij.CompletedAt, rij.CreatedAt
            FROM RecipeImportJob rij
            INNER JOIN RecipeImportSource ris ON rij.ImportSourceId = ris.Id
            WHERE rij.Id = @Id";

        var results = await ExecuteReaderAsync(
            sql,
            reader => new RecipeImportJobDto
            {
                Id = GetGuid(reader, "Id"),
                UserId = GetGuid(reader, "UserId"),
                ImportSourceId = GetGuid(reader, "ImportSourceId"),
                ImportSourceName = GetString(reader, "ImportSourceName"),
                FileName = GetString(reader, "FileName"),
                FileUrl = GetString(reader, "FileUrl"),
                Status = GetString(reader, "Status") ?? string.Empty,
                TotalRecipes = GetInt(reader, "TotalRecipes"),
                SuccessCount = GetInt(reader, "SuccessCount"),
                FailureCount = GetInt(reader, "FailureCount"),
                ErrorLog = GetString(reader, "ErrorLog"),
                StartedAt = GetDateTime(reader, "StartedAt"),
                CompletedAt = GetDateTime(reader, "CompletedAt"),
                CreatedAt = GetDateTime(reader, "CreatedAt") ?? DateTime.UtcNow
            },
            new SqlParameter("@Id", id));

        var job = results.FirstOrDefault();

        if (job != null)
        {
            job.Results = await GetImportResultsAsync(job.Id);
        }

        return job;
    }

    public async Task<List<RecipeImportJobDto>> GetUserImportJobsAsync(Guid userId, int limit = 50)
    {
        const string sql = @"
            SELECT TOP (@Limit)
                   rij.Id, rij.UserId, rij.ImportSourceId, ris.Name AS ImportSourceName,
                   rij.FileName, rij.FileUrl, rij.Status, rij.TotalRecipes,
                   rij.SuccessCount, rij.FailureCount, rij.ErrorLog,
                   rij.StartedAt, rij.CompletedAt, rij.CreatedAt
            FROM RecipeImportJob rij
            INNER JOIN RecipeImportSource ris ON rij.ImportSourceId = ris.Id
            WHERE rij.UserId = @UserId
            ORDER BY rij.CreatedAt DESC";

        return await ExecuteReaderAsync(
            sql,
            reader => new RecipeImportJobDto
            {
                Id = GetGuid(reader, "Id"),
                UserId = GetGuid(reader, "UserId"),
                ImportSourceId = GetGuid(reader, "ImportSourceId"),
                ImportSourceName = GetString(reader, "ImportSourceName"),
                FileName = GetString(reader, "FileName"),
                FileUrl = GetString(reader, "FileUrl"),
                Status = GetString(reader, "Status") ?? string.Empty,
                TotalRecipes = GetInt(reader, "TotalRecipes"),
                SuccessCount = GetInt(reader, "SuccessCount"),
                FailureCount = GetInt(reader, "FailureCount"),
                ErrorLog = GetString(reader, "ErrorLog"),
                StartedAt = GetDateTime(reader, "StartedAt"),
                CompletedAt = GetDateTime(reader, "CompletedAt"),
                CreatedAt = GetDateTime(reader, "CreatedAt") ?? DateTime.UtcNow
            },
            new SqlParameter("@UserId", userId),
            new SqlParameter("@Limit", limit));
    }

    public async Task<bool> UpdateImportJobStatusAsync(Guid jobId, string status, int successCount, int failureCount, string? errorLog = null)
    {
        var sql = @"
            UPDATE RecipeImportJob
            SET Status = @Status,
                SuccessCount = @SuccessCount,
                FailureCount = @FailureCount,
                ErrorLog = @ErrorLog";

        if (status == "Processing" || status == "Completed" || status == "Failed")
        {
            sql += ", StartedAt = COALESCE(StartedAt, GETUTCDATE())";
        }

        sql += " WHERE Id = @Id";

        var rowsAffected = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", jobId),
            new SqlParameter("@Status", status),
            new SqlParameter("@SuccessCount", successCount),
            new SqlParameter("@FailureCount", failureCount),
            new SqlParameter("@ErrorLog", (object?)errorLog ?? DBNull.Value));

        return rowsAffected > 0;
    }

    public async Task<bool> CompleteImportJobAsync(Guid jobId)
    {
        const string sql = @"
            UPDATE RecipeImportJob
            SET Status = CASE
                    WHEN FailureCount = 0 THEN 'Completed'
                    WHEN SuccessCount = 0 THEN 'Failed'
                    ELSE 'PartialSuccess'
                END,
                CompletedAt = GETUTCDATE()
            WHERE Id = @Id";

        var rowsAffected = await ExecuteNonQueryAsync(sql, new SqlParameter("@Id", jobId));

        return rowsAffected > 0;
    }

    // Import Results

    public async Task<Guid> CreateImportResultAsync(Guid jobId, string sourceRecipeId, string sourceRecipeName,
        Guid? importedRecipeId, string status, string? errorMessage = null, string? rawData = null)
    {
        var id = Guid.NewGuid();

        const string sql = @"
            INSERT INTO RecipeImportResult (Id, ImportJobId, SourceRecipeId, SourceRecipeName,
                                          ImportedRecipeId, Status, ErrorMessage, RawData, CreatedAt)
            VALUES (@Id, @JobId, @SourceRecipeId, @SourceRecipeName,
                    @ImportedRecipeId, @Status, @ErrorMessage, @RawData, GETUTCDATE())";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@JobId", jobId),
            new SqlParameter("@SourceRecipeId", sourceRecipeId),
            new SqlParameter("@SourceRecipeName", sourceRecipeName),
            new SqlParameter("@ImportedRecipeId", (object?)importedRecipeId ?? DBNull.Value),
            new SqlParameter("@Status", status),
            new SqlParameter("@ErrorMessage", (object?)errorMessage ?? DBNull.Value),
            new SqlParameter("@RawData", (object?)rawData ?? DBNull.Value));

        return id;
    }

    public async Task<List<RecipeImportResultDto>> GetImportResultsAsync(Guid jobId)
    {
        const string sql = @"
            SELECT Id, ImportJobId, SourceRecipeId, SourceRecipeName,
                   ImportedRecipeId, Status, ErrorMessage, RawData, CreatedAt
            FROM RecipeImportResult
            WHERE ImportJobId = @JobId
            ORDER BY CreatedAt";

        return await ExecuteReaderAsync(
            sql,
            reader => new RecipeImportResultDto
            {
                Id = GetGuid(reader, "Id"),
                ImportJobId = GetGuid(reader, "ImportJobId"),
                SourceRecipeId = GetString(reader, "SourceRecipeId"),
                SourceRecipeName = GetString(reader, "SourceRecipeName"),
                ImportedRecipeId = GetGuidNullable(reader, "ImportedRecipeId"),
                Status = GetString(reader, "Status") ?? string.Empty,
                ErrorMessage = GetString(reader, "ErrorMessage"),
                RawData = GetString(reader, "RawData"),
                CreatedAt = GetDateTime(reader, "CreatedAt") ?? DateTime.UtcNow
            },
            new SqlParameter("@JobId", jobId));
    }

    // Recipe Versions

    public async Task<Guid> CreateRecipeVersionAsync(Guid recipeId, string changeDescription, Guid createdBy, string snapshotData)
    {
        var id = Guid.NewGuid();

        // Get next version number
        const string getVersionSql = @"
            SELECT ISNULL(MAX(VersionNumber), 0) + 1
            FROM RecipeVersion
            WHERE RecipeId = @RecipeId";

        var versionNumber = await ExecuteScalarAsync<int>(getVersionSql, new SqlParameter("@RecipeId", recipeId));

        const string sql = @"
            INSERT INTO RecipeVersion (Id, RecipeId, VersionNumber, ChangeDescription,
                                      CreatedBy, CreatedAt, SnapshotData)
            VALUES (@Id, @RecipeId, @VersionNumber, @ChangeDescription,
                    @CreatedBy, GETUTCDATE(), @SnapshotData)";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@RecipeId", recipeId),
            new SqlParameter("@VersionNumber", versionNumber),
            new SqlParameter("@ChangeDescription", changeDescription),
            new SqlParameter("@CreatedBy", createdBy),
            new SqlParameter("@SnapshotData", snapshotData));

        // Update recipe's current version
        await ExecuteNonQueryAsync(
            "UPDATE Recipe SET CurrentVersion = @Version WHERE Id = @Id",
            new SqlParameter("@Version", versionNumber),
            new SqlParameter("@Id", recipeId));

        return id;
    }

    public async Task<List<RecipeVersionDto>> GetRecipeVersionsAsync(Guid recipeId)
    {
        const string sql = @"
            SELECT rv.Id, rv.RecipeId, rv.VersionNumber, rv.ChangeDescription,
                   rv.CreatedBy, u.Email AS CreatedByName, rv.CreatedAt, rv.SnapshotData
            FROM RecipeVersion rv
            LEFT JOIN [User] u ON rv.CreatedBy = u.Id
            WHERE rv.RecipeId = @RecipeId
            ORDER BY rv.VersionNumber DESC";

        return await ExecuteReaderAsync(
            sql,
            reader => new RecipeVersionDto
            {
                Id = GetGuid(reader, "Id"),
                RecipeId = GetGuid(reader, "RecipeId"),
                VersionNumber = GetInt(reader, "VersionNumber"),
                ChangeDescription = GetString(reader, "ChangeDescription"),
                CreatedBy = GetGuid(reader, "CreatedBy"),
                CreatedByName = GetString(reader, "CreatedByName"),
                CreatedAt = GetDateTime(reader, "CreatedAt") ?? DateTime.UtcNow,
                SnapshotData = GetString(reader, "SnapshotData") ?? string.Empty
            },
            new SqlParameter("@RecipeId", recipeId));
    }

    public async Task<RecipeVersionDto?> GetRecipeVersionByIdAsync(Guid id)
    {
        const string sql = @"
            SELECT rv.Id, rv.RecipeId, rv.VersionNumber, rv.ChangeDescription,
                   rv.CreatedBy, u.Email AS CreatedByName, rv.CreatedAt, rv.SnapshotData
            FROM RecipeVersion rv
            LEFT JOIN [User] u ON rv.CreatedBy = u.Id
            WHERE rv.Id = @Id";

        var results = await ExecuteReaderAsync(
            sql,
            reader => new RecipeVersionDto
            {
                Id = GetGuid(reader, "Id"),
                RecipeId = GetGuid(reader, "RecipeId"),
                VersionNumber = GetInt(reader, "VersionNumber"),
                ChangeDescription = GetString(reader, "ChangeDescription"),
                CreatedBy = GetGuid(reader, "CreatedBy"),
                CreatedByName = GetString(reader, "CreatedByName"),
                CreatedAt = GetDateTime(reader, "CreatedAt") ?? DateTime.UtcNow,
                SnapshotData = GetString(reader, "SnapshotData") ?? string.Empty
            },
            new SqlParameter("@Id", id));

        return results.FirstOrDefault();
    }

    // Recipe Forks

    public async Task<Guid> ForkRecipeAsync(Guid originalRecipeId, Guid forkedRecipeId, Guid forkedBy, string? forkReason = null)
    {
        var id = Guid.NewGuid();

        const string sql = @"
            INSERT INTO RecipeFork (Id, OriginalRecipeId, ForkedRecipeId, ForkedBy, ForkReason, ForkedAt)
            VALUES (@Id, @OriginalRecipeId, @ForkedRecipeId, @ForkedBy, @ForkReason, GETUTCDATE())";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@OriginalRecipeId", originalRecipeId),
            new SqlParameter("@ForkedRecipeId", forkedRecipeId),
            new SqlParameter("@ForkedBy", forkedBy),
            new SqlParameter("@ForkReason", (object?)forkReason ?? DBNull.Value));

        // Update recipe metadata
        await ExecuteNonQueryAsync(
            @"UPDATE Recipe
              SET ParentRecipeId = @ParentId, IsForked = 1
              WHERE Id = @Id",
            new SqlParameter("@ParentId", originalRecipeId),
            new SqlParameter("@Id", forkedRecipeId));

        return id;
    }

    public async Task<List<RecipeForkDto>> GetRecipeForksAsync(Guid originalRecipeId)
    {
        const string sql = @"
            SELECT rf.Id, rf.OriginalRecipeId, ro.Name AS OriginalRecipeName,
                   rf.ForkedRecipeId, rf.Name AS ForkedRecipeName,
                   rf.ForkedBy, u.Email AS ForkedByName, rf.ForkReason, rf.ForkedAt
            FROM RecipeFork rf
            LEFT JOIN Recipe ro ON rf.OriginalRecipeId = ro.Id
            LEFT JOIN Recipe rf2 ON rf.ForkedRecipeId = rf2.Id
            LEFT JOIN [User] u ON rf.ForkedBy = u.Id
            WHERE rf.OriginalRecipeId = @OriginalRecipeId
            ORDER BY rf.ForkedAt DESC";

        return await ExecuteReaderAsync(
            sql,
            reader => new RecipeForkDto
            {
                Id = GetGuid(reader, "Id"),
                OriginalRecipeId = GetGuid(reader, "OriginalRecipeId"),
                OriginalRecipeName = GetString(reader, "OriginalRecipeName"),
                ForkedRecipeId = GetGuid(reader, "ForkedRecipeId"),
                ForkedRecipeName = GetString(reader, "ForkedRecipeName"),
                ForkedBy = GetGuid(reader, "ForkedBy"),
                ForkedByName = GetString(reader, "ForkedByName"),
                ForkReason = GetString(reader, "ForkReason"),
                ForkedAt = GetDateTime(reader, "ForkedAt") ?? DateTime.UtcNow
            },
            new SqlParameter("@OriginalRecipeId", originalRecipeId));
    }

    // Recipe Collections

    public async Task<Guid> CreateCollectionAsync(Guid userId, CreateRecipeCollectionRequest request)
    {
        var id = Guid.NewGuid();

        const string sql = @"
            INSERT INTO RecipeCollection (Id, UserId, Name, Description, ImageUrl,
                                         IsPublic, SortOrder, CreatedAt)
            VALUES (@Id, @UserId, @Name, @Description, @ImageUrl,
                    @IsPublic, @SortOrder, GETUTCDATE())";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@UserId", userId),
            new SqlParameter("@Name", request.Name),
            new SqlParameter("@Description", (object?)request.Description ?? DBNull.Value),
            new SqlParameter("@ImageUrl", (object?)request.ImageUrl ?? DBNull.Value),
            new SqlParameter("@IsPublic", request.IsPublic),
            new SqlParameter("@SortOrder", request.SortOrder));

        return id;
    }

    public async Task<RecipeCollectionDto?> GetCollectionByIdAsync(Guid id, bool includeItems = true)
    {
        const string sql = @"
            SELECT Id, UserId, Name, Description, ImageUrl, IsPublic, SortOrder,
                   CreatedAt, UpdatedAt, IsDeleted, DeletedAt
            FROM RecipeCollection
            WHERE Id = @Id AND IsDeleted = 0";

        var results = await ExecuteReaderAsync(
            sql,
            reader => new RecipeCollectionDto
            {
                Id = GetGuid(reader, "Id"),
                UserId = GetGuid(reader, "UserId"),
                Name = GetString(reader, "Name") ?? string.Empty,
                Description = GetString(reader, "Description"),
                ImageUrl = GetString(reader, "ImageUrl"),
                IsPublic = GetBoolean(reader, "IsPublic"),
                SortOrder = GetInt(reader, "SortOrder"),
                CreatedAt = GetDateTime(reader, "CreatedAt") ?? DateTime.UtcNow,
                UpdatedAt = GetDateTime(reader, "UpdatedAt"),
                IsDeleted = GetBoolean(reader, "IsDeleted"),
                DeletedAt = GetDateTime(reader, "DeletedAt")
            },
            new SqlParameter("@Id", id));

        var collection = results.FirstOrDefault();

        if (collection != null && includeItems)
        {
            collection.Items = await GetCollectionItemsAsync(collection.Id);
            collection.RecipeCount = collection.Items.Count;
        }

        return collection;
    }

    public async Task<List<RecipeCollectionDto>> GetUserCollectionsAsync(Guid userId, bool includeItemCounts = true)
    {
        const string sql = @"
            SELECT Id, UserId, Name, Description, ImageUrl, IsPublic, SortOrder,
                   CreatedAt, UpdatedAt, IsDeleted, DeletedAt
            FROM RecipeCollection
            WHERE UserId = @UserId AND IsDeleted = 0
            ORDER BY SortOrder, Name";

        var collections = await ExecuteReaderAsync(
            sql,
            reader => new RecipeCollectionDto
            {
                Id = GetGuid(reader, "Id"),
                UserId = GetGuid(reader, "UserId"),
                Name = GetString(reader, "Name") ?? string.Empty,
                Description = GetString(reader, "Description"),
                ImageUrl = GetString(reader, "ImageUrl"),
                IsPublic = GetBoolean(reader, "IsPublic"),
                SortOrder = GetInt(reader, "SortOrder"),
                CreatedAt = GetDateTime(reader, "CreatedAt") ?? DateTime.UtcNow,
                UpdatedAt = GetDateTime(reader, "UpdatedAt"),
                IsDeleted = GetBoolean(reader, "IsDeleted"),
                DeletedAt = GetDateTime(reader, "DeletedAt")
            },
            new SqlParameter("@UserId", userId));

        if (includeItemCounts)
        {
            foreach (var collection in collections)
            {
                collection.RecipeCount = await GetCollectionItemCountAsync(collection.Id);
            }
        }

        return collections;
    }

    public async Task<bool> UpdateCollectionAsync(Guid id, Guid userId, UpdateRecipeCollectionRequest request)
    {
        const string sql = @"
            UPDATE RecipeCollection
            SET Name = @Name,
                Description = @Description,
                ImageUrl = @ImageUrl,
                IsPublic = @IsPublic,
                SortOrder = @SortOrder,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND UserId = @UserId AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@UserId", userId),
            new SqlParameter("@Name", request.Name),
            new SqlParameter("@Description", (object?)request.Description ?? DBNull.Value),
            new SqlParameter("@ImageUrl", (object?)request.ImageUrl ?? DBNull.Value),
            new SqlParameter("@IsPublic", request.IsPublic),
            new SqlParameter("@SortOrder", request.SortOrder));

        return rowsAffected > 0;
    }

    public async Task<bool> DeleteCollectionAsync(Guid id, Guid userId)
    {
        const string sql = @"
            UPDATE RecipeCollection
            SET IsDeleted = 1,
                DeletedAt = GETUTCDATE()
            WHERE Id = @Id AND UserId = @UserId";

        var rowsAffected = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@UserId", userId));

        return rowsAffected > 0;
    }

    public async Task<Guid> AddRecipeToCollectionAsync(Guid collectionId, Guid userId, AddRecipeToCollectionRequest request)
    {
        // Verify ownership
        var collection = await GetCollectionByIdAsync(collectionId, false);
        if (collection == null || collection.UserId != userId)
        {
            throw new UnauthorizedAccessException("Collection not found or access denied");
        }

        var id = Guid.NewGuid();

        const string sql = @"
            INSERT INTO RecipeCollectionItem (Id, CollectionId, RecipeId, OrderIndex, Notes, AddedAt)
            VALUES (@Id, @CollectionId, @RecipeId, @OrderIndex, @Notes, GETUTCDATE())";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@CollectionId", collectionId),
            new SqlParameter("@RecipeId", request.RecipeId),
            new SqlParameter("@OrderIndex", request.OrderIndex),
            new SqlParameter("@Notes", (object?)request.Notes ?? DBNull.Value));

        return id;
    }

    public async Task<bool> RemoveRecipeFromCollectionAsync(Guid collectionId, Guid recipeId, Guid userId)
    {
        const string sql = @"
            DELETE FROM RecipeCollectionItem
            WHERE CollectionId = @CollectionId
              AND RecipeId = @RecipeId
              AND EXISTS (SELECT 1 FROM RecipeCollection WHERE Id = @CollectionId AND UserId = @UserId)";

        var rowsAffected = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@CollectionId", collectionId),
            new SqlParameter("@RecipeId", recipeId),
            new SqlParameter("@UserId", userId));

        return rowsAffected > 0;
    }

    public async Task<bool> UpdateCollectionItemAsync(Guid itemId, UpdateCollectionItemRequest request)
    {
        const string sql = @"
            UPDATE RecipeCollectionItem
            SET OrderIndex = @OrderIndex,
                Notes = @Notes
            WHERE Id = @Id";

        var rowsAffected = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", itemId),
            new SqlParameter("@OrderIndex", request.OrderIndex),
            new SqlParameter("@Notes", (object?)request.Notes ?? DBNull.Value));

        return rowsAffected > 0;
    }

    // Export

    public async Task<Guid> CreateExportHistoryAsync(Guid userId, string exportFormat, List<Guid> recipeIds,
        string fileName, long? fileSize = null, string? fileUrl = null)
    {
        var id = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddDays(7); // Exports expire after 7 days

        const string sql = @"
            INSERT INTO RecipeExportHistory (Id, UserId, ExportFormat, RecipeCount, FileName,
                                           FileSize, FileUrl, ExpiresAt, CreatedAt)
            VALUES (@Id, @UserId, @ExportFormat, @RecipeCount, @FileName,
                    @FileSize, @FileUrl, @ExpiresAt, GETUTCDATE())";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@UserId", userId),
            new SqlParameter("@ExportFormat", exportFormat),
            new SqlParameter("@RecipeCount", recipeIds.Count),
            new SqlParameter("@FileName", fileName),
            new SqlParameter("@FileSize", (object?)fileSize ?? DBNull.Value),
            new SqlParameter("@FileUrl", (object?)fileUrl ?? DBNull.Value),
            new SqlParameter("@ExpiresAt", expiresAt));

        return id;
    }

    public async Task<List<RecipeExportHistoryDto>> GetUserExportHistoryAsync(Guid userId, int limit = 50)
    {
        const string sql = @"
            SELECT TOP (@Limit)
                   Id, UserId, ExportFormat, RecipeCount, FileName,
                   FileSize, FileUrl, ExpiresAt, CreatedAt
            FROM RecipeExportHistory
            WHERE UserId = @UserId
            ORDER BY CreatedAt DESC";

        return await ExecuteReaderAsync(
            sql,
            reader => new RecipeExportHistoryDto
            {
                Id = GetGuid(reader, "Id"),
                UserId = GetGuid(reader, "UserId"),
                ExportFormat = GetString(reader, "ExportFormat") ?? string.Empty,
                RecipeCount = GetInt(reader, "RecipeCount"),
                FileName = GetString(reader, "FileName"),
                FileSize = GetLongNullable(reader, "FileSize"),
                FileUrl = GetString(reader, "FileUrl"),
                ExpiresAt = GetDateTime(reader, "ExpiresAt"),
                CreatedAt = GetDateTime(reader, "CreatedAt") ?? DateTime.UtcNow
            },
            new SqlParameter("@UserId", userId),
            new SqlParameter("@Limit", limit));
    }

    // Summaries

    public async Task<ImportSummaryDto> GetImportSummaryAsync(Guid userId)
    {
        const string jobsSql = @"
            SELECT
                COUNT(*) AS TotalJobs,
                COUNT(CASE WHEN Status = 'Pending' OR Status = 'Processing' THEN 1 END) AS PendingJobs,
                COUNT(CASE WHEN Status = 'Completed' THEN 1 END) AS CompletedJobs,
                COUNT(CASE WHEN Status = 'Failed' THEN 1 END) AS FailedJobs,
                ISNULL(SUM(SuccessCount), 0) AS TotalRecipesImported,
                MAX(CompletedAt) AS LastImportDate
            FROM RecipeImportJob
            WHERE UserId = @UserId";

        var jobData = await ExecuteReaderAsync(
            jobsSql,
            reader => new
            {
                TotalJobs = GetInt(reader, "TotalJobs"),
                PendingJobs = GetInt(reader, "PendingJobs"),
                CompletedJobs = GetInt(reader, "CompletedJobs"),
                FailedJobs = GetInt(reader, "FailedJobs"),
                TotalRecipesImported = GetInt(reader, "TotalRecipesImported"),
                LastImportDate = GetDateTime(reader, "LastImportDate")
            },
            new SqlParameter("@UserId", userId));

        var data = jobData.FirstOrDefault();

        return new ImportSummaryDto
        {
            TotalJobs = data?.TotalJobs ?? 0,
            PendingJobs = data?.PendingJobs ?? 0,
            CompletedJobs = data?.CompletedJobs ?? 0,
            FailedJobs = data?.FailedJobs ?? 0,
            TotalRecipesImported = data?.TotalRecipesImported ?? 0,
            LastImportDate = data?.LastImportDate,
            RecentJobs = await GetUserImportJobsAsync(userId, 10)
        };
    }

    public async Task<CollectionSummaryDto> GetCollectionSummaryAsync(Guid userId)
    {
        const string sql = @"
            SELECT
                COUNT(*) AS TotalCollections,
                COUNT(CASE WHEN IsPublic = 1 THEN 1 END) AS PublicCollections,
                COUNT(CASE WHEN IsPublic = 0 THEN 1 END) AS PrivateCollections
            FROM RecipeCollection
            WHERE UserId = @UserId AND IsDeleted = 0";

        var collectionData = await ExecuteReaderAsync(
            sql,
            reader => new
            {
                TotalCollections = GetInt(reader, "TotalCollections"),
                PublicCollections = GetInt(reader, "PublicCollections"),
                PrivateCollections = GetInt(reader, "PrivateCollections")
            },
            new SqlParameter("@UserId", userId));

        var data = collectionData.FirstOrDefault();

        const string recipeCountSql = @"
            SELECT COUNT(DISTINCT rci.RecipeId)
            FROM RecipeCollectionItem rci
            INNER JOIN RecipeCollection rc ON rci.CollectionId = rc.Id
            WHERE rc.UserId = @UserId AND rc.IsDeleted = 0";

        var totalRecipes = await ExecuteScalarAsync<int>(recipeCountSql, new SqlParameter("@UserId", userId));

        return new CollectionSummaryDto
        {
            TotalCollections = data?.TotalCollections ?? 0,
            PublicCollections = data?.PublicCollections ?? 0,
            PrivateCollections = data?.PrivateCollections ?? 0,
            TotalRecipes = totalRecipes,
            RecentCollections = await GetUserCollectionsAsync(userId, true)
        };
    }

    // Helper methods

    private async Task<List<RecipeCollectionItemDto>> GetCollectionItemsAsync(Guid collectionId)
    {
        const string sql = @"
            SELECT rci.Id, rci.CollectionId, rci.RecipeId, r.Name AS RecipeName,
                   r.ImageUrl AS RecipeImageUrl, rci.OrderIndex, rci.Notes, rci.AddedAt
            FROM RecipeCollectionItem rci
            INNER JOIN Recipe r ON rci.RecipeId = r.Id
            WHERE rci.CollectionId = @CollectionId
            ORDER BY rci.OrderIndex, rci.AddedAt";

        return await ExecuteReaderAsync(
            sql,
            reader => new RecipeCollectionItemDto
            {
                Id = GetGuid(reader, "Id"),
                CollectionId = GetGuid(reader, "CollectionId"),
                RecipeId = GetGuid(reader, "RecipeId"),
                RecipeName = GetString(reader, "RecipeName"),
                RecipeImageUrl = GetString(reader, "RecipeImageUrl"),
                OrderIndex = GetInt(reader, "OrderIndex"),
                Notes = GetString(reader, "Notes"),
                AddedAt = GetDateTime(reader, "AddedAt") ?? DateTime.UtcNow
            },
            new SqlParameter("@CollectionId", collectionId));
    }

    private async Task<int> GetCollectionItemCountAsync(Guid collectionId)
    {
        const string sql = "SELECT COUNT(*) FROM RecipeCollectionItem WHERE CollectionId = @CollectionId";
        return await ExecuteScalarAsync<int>(sql, new SqlParameter("@CollectionId", collectionId));
    }

    private long? GetLongNullable(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);
    }
}
