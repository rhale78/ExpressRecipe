using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.DTOs.User;
using Microsoft.Data.SqlClient;

namespace ExpressRecipe.UserService.Data;

public class ReportsRepository : SqlHelper, IReportsRepository
{
    public ReportsRepository(string connectionString) : base(connectionString) { }

    #region Report Types

    public async Task<List<ReportTypeDto>> GetReportTypesAsync()
    {
        const string sql = @"
            SELECT Id, Name, Description, Category, ParameterSchema, IsActive
            FROM ReportType
            WHERE IsActive = 1
            ORDER BY Category, Name";

        return await ExecuteReaderAsync(sql, reader => new ReportTypeDto
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description"))
                ? null
                : reader.GetString(reader.GetOrdinal("Description")),
            Category = reader.GetString(reader.GetOrdinal("Category")),
            ParameterSchema = reader.IsDBNull(reader.GetOrdinal("ParameterSchema"))
                ? null
                : reader.GetString(reader.GetOrdinal("ParameterSchema")),
            IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
        });
    }

    public async Task<ReportTypeDto?> GetReportTypeByIdAsync(Guid id)
    {
        const string sql = @"
            SELECT Id, Name, Description, Category, ParameterSchema, IsActive
            FROM ReportType
            WHERE Id = @Id";

        var reportTypes = await ExecuteReaderAsync(sql, reader => new ReportTypeDto
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description"))
                ? null
                : reader.GetString(reader.GetOrdinal("Description")),
            Category = reader.GetString(reader.GetOrdinal("Category")),
            ParameterSchema = reader.IsDBNull(reader.GetOrdinal("ParameterSchema"))
                ? null
                : reader.GetString(reader.GetOrdinal("ParameterSchema")),
            IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
        }, new SqlParameter("@Id", id));

        return reportTypes.FirstOrDefault();
    }

    #endregion

    #region Saved Reports

    public async Task<List<SavedReportDto>> GetUserSavedReportsAsync(Guid userId)
    {
        const string sql = @"
            SELECT sr.Id, sr.UserId, sr.ReportTypeId, sr.ReportName, sr.Parameters,
                   sr.ScheduleFrequency, sr.IsScheduled, sr.LastRunAt, sr.CreatedAt,
                   rt.Name AS ReportTypeName, rt.Category
            FROM SavedReport sr
            INNER JOIN ReportType rt ON sr.ReportTypeId = rt.Id
            WHERE sr.UserId = @UserId AND sr.IsDeleted = 0
            ORDER BY sr.CreatedAt DESC";

        return await ExecuteReaderAsync(sql, reader => new SavedReportDto
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            UserId = reader.GetGuid(reader.GetOrdinal("UserId")),
            ReportTypeId = reader.GetGuid(reader.GetOrdinal("ReportTypeId")),
            ReportName = reader.GetString(reader.GetOrdinal("ReportName")),
            Parameters = reader.IsDBNull(reader.GetOrdinal("Parameters"))
                ? null
                : reader.GetString(reader.GetOrdinal("Parameters")),
            ScheduleFrequency = reader.IsDBNull(reader.GetOrdinal("ScheduleFrequency"))
                ? null
                : reader.GetString(reader.GetOrdinal("ScheduleFrequency")),
            IsScheduled = reader.GetBoolean(reader.GetOrdinal("IsScheduled")),
            LastRunAt = reader.IsDBNull(reader.GetOrdinal("LastRunAt"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("LastRunAt")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            ReportTypeName = reader.GetString(reader.GetOrdinal("ReportTypeName")),
            Category = reader.GetString(reader.GetOrdinal("Category"))
        }, new SqlParameter("@UserId", userId));
    }

    public async Task<SavedReportDto?> GetSavedReportByIdAsync(Guid id)
    {
        const string sql = @"
            SELECT sr.Id, sr.UserId, sr.ReportTypeId, sr.ReportName, sr.Parameters,
                   sr.ScheduleFrequency, sr.IsScheduled, sr.LastRunAt, sr.CreatedAt,
                   rt.Name AS ReportTypeName, rt.Category
            FROM SavedReport sr
            INNER JOIN ReportType rt ON sr.ReportTypeId = rt.Id
            WHERE sr.Id = @Id AND sr.IsDeleted = 0";

        var savedReports = await ExecuteReaderAsync(sql, reader => new SavedReportDto
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            UserId = reader.GetGuid(reader.GetOrdinal("UserId")),
            ReportTypeId = reader.GetGuid(reader.GetOrdinal("ReportTypeId")),
            ReportName = reader.GetString(reader.GetOrdinal("ReportName")),
            Parameters = reader.IsDBNull(reader.GetOrdinal("Parameters"))
                ? null
                : reader.GetString(reader.GetOrdinal("Parameters")),
            ScheduleFrequency = reader.IsDBNull(reader.GetOrdinal("ScheduleFrequency"))
                ? null
                : reader.GetString(reader.GetOrdinal("ScheduleFrequency")),
            IsScheduled = reader.GetBoolean(reader.GetOrdinal("IsScheduled")),
            LastRunAt = reader.IsDBNull(reader.GetOrdinal("LastRunAt"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("LastRunAt")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            ReportTypeName = reader.GetString(reader.GetOrdinal("ReportTypeName")),
            Category = reader.GetString(reader.GetOrdinal("Category"))
        }, new SqlParameter("@Id", id));

        return savedReports.FirstOrDefault();
    }

    public async Task<Guid> CreateSavedReportAsync(Guid userId, CreateSavedReportRequest request)
    {
        var id = Guid.NewGuid();

        const string sql = @"
            INSERT INTO SavedReport
                (Id, UserId, ReportTypeId, ReportName, Parameters, ScheduleFrequency,
                 IsScheduled, CreatedAt, IsDeleted)
            VALUES
                (@Id, @UserId, @ReportTypeId, @ReportName, @Parameters, @ScheduleFrequency,
                 @IsScheduled, GETUTCDATE(), 0)";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@UserId", userId),
            new SqlParameter("@ReportTypeId", request.ReportTypeId),
            new SqlParameter("@ReportName", request.ReportName),
            new SqlParameter("@Parameters", (object?)request.Parameters ?? DBNull.Value),
            new SqlParameter("@ScheduleFrequency", (object?)request.ScheduleFrequency ?? DBNull.Value),
            new SqlParameter("@IsScheduled", request.IsScheduled));

        return id;
    }

    public async Task<bool> UpdateSavedReportAsync(Guid id, Guid userId, UpdateSavedReportRequest request)
    {
        const string sql = @"
            UPDATE SavedReport
            SET ReportName = @ReportName,
                Parameters = @Parameters,
                ScheduleFrequency = @ScheduleFrequency,
                IsScheduled = @IsScheduled,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND UserId = @UserId AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@UserId", userId),
            new SqlParameter("@ReportName", request.ReportName),
            new SqlParameter("@Parameters", (object?)request.Parameters ?? DBNull.Value),
            new SqlParameter("@ScheduleFrequency", (object?)request.ScheduleFrequency ?? DBNull.Value),
            new SqlParameter("@IsScheduled", request.IsScheduled));

        return rowsAffected > 0;
    }

    public async Task<bool> DeleteSavedReportAsync(Guid id, Guid userId)
    {
        const string sql = @"
            UPDATE SavedReport
            SET IsDeleted = 1,
                DeletedAt = GETUTCDATE(),
                DeletedBy = @UserId
            WHERE Id = @Id AND UserId = @UserId AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@UserId", userId));

        return rowsAffected > 0;
    }

    #endregion

    #region Report History

    public async Task<List<ReportHistoryDto>> GetUserReportHistoryAsync(Guid userId, int pageNumber = 1, int pageSize = 50)
    {
        var offset = (pageNumber - 1) * pageSize;

        const string sql = @"
            SELECT rh.Id, rh.UserId, rh.ReportTypeId, rh.Parameters, rh.GeneratedAt,
                   rh.FileSize, rh.ExportFormat, rh.Status, rh.ErrorMessage,
                   rt.Name AS ReportTypeName
            FROM ReportHistory rh
            INNER JOIN ReportType rt ON rh.ReportTypeId = rt.Id
            WHERE rh.UserId = @UserId
            ORDER BY rh.GeneratedAt DESC
            OFFSET @Offset ROWS
            FETCH NEXT @PageSize ROWS ONLY";

        return await ExecuteReaderAsync(sql, reader => new ReportHistoryDto
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            UserId = reader.GetGuid(reader.GetOrdinal("UserId")),
            ReportTypeId = reader.GetGuid(reader.GetOrdinal("ReportTypeId")),
            Parameters = reader.IsDBNull(reader.GetOrdinal("Parameters"))
                ? null
                : reader.GetString(reader.GetOrdinal("Parameters")),
            GeneratedAt = reader.GetDateTime(reader.GetOrdinal("GeneratedAt")),
            FileSize = reader.IsDBNull(reader.GetOrdinal("FileSize"))
                ? null
                : reader.GetInt64(reader.GetOrdinal("FileSize")),
            ExportFormat = reader.IsDBNull(reader.GetOrdinal("ExportFormat"))
                ? null
                : reader.GetString(reader.GetOrdinal("ExportFormat")),
            Status = reader.GetString(reader.GetOrdinal("Status")),
            ErrorMessage = reader.IsDBNull(reader.GetOrdinal("ErrorMessage"))
                ? null
                : reader.GetString(reader.GetOrdinal("ErrorMessage")),
            ReportTypeName = reader.GetString(reader.GetOrdinal("ReportTypeName"))
        },
        new SqlParameter("@UserId", userId),
        new SqlParameter("@Offset", offset),
        new SqlParameter("@PageSize", pageSize));
    }

    public async Task<Guid> CreateReportHistoryAsync(Guid userId, CreateReportHistoryRequest request)
    {
        var id = Guid.NewGuid();

        const string sql = @"
            INSERT INTO ReportHistory
                (Id, UserId, ReportTypeId, Parameters, GeneratedAt, FileSize,
                 ExportFormat, Status, ErrorMessage)
            VALUES
                (@Id, @UserId, @ReportTypeId, @Parameters, GETUTCDATE(), @FileSize,
                 @ExportFormat, @Status, @ErrorMessage)";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@UserId", userId),
            new SqlParameter("@ReportTypeId", request.ReportTypeId),
            new SqlParameter("@Parameters", (object?)request.Parameters ?? DBNull.Value),
            new SqlParameter("@FileSize", (object?)request.FileSize ?? DBNull.Value),
            new SqlParameter("@ExportFormat", (object?)request.ExportFormat ?? DBNull.Value),
            new SqlParameter("@Status", request.Status),
            new SqlParameter("@ErrorMessage", (object?)request.ErrorMessage ?? DBNull.Value));

        // Update LastRunAt on SavedReport if this was from a saved report
        if (request.SavedReportId.HasValue)
        {
            const string updateSql = @"
                UPDATE SavedReport
                SET LastRunAt = GETUTCDATE()
                WHERE Id = @SavedReportId";

            await ExecuteNonQueryAsync(updateSql,
                new SqlParameter("@SavedReportId", request.SavedReportId.Value));
        }

        return id;
    }

    #endregion

    #region User Lists

    public async Task<List<UserListDto>> GetUserListsAsync(Guid userId, string? listType = null)
    {
        var sql = @"
            SELECT Id, UserId, ListName, ListType, Description, Icon,
                   IsShared, ItemCount, CreatedAt, UpdatedAt
            FROM UserList
            WHERE UserId = @UserId AND IsDeleted = 0";

        var parameters = new List<SqlParameter> { new SqlParameter("@UserId", userId) };

        if (!string.IsNullOrEmpty(listType))
        {
            sql += " AND ListType = @ListType";
            parameters.Add(new SqlParameter("@ListType", listType));
        }

        sql += " ORDER BY CreatedAt DESC";

        return await ExecuteReaderAsync(sql, reader => new UserListDto
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            UserId = reader.GetGuid(reader.GetOrdinal("UserId")),
            ListName = reader.GetString(reader.GetOrdinal("ListName")),
            ListType = reader.GetString(reader.GetOrdinal("ListType")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description"))
                ? null
                : reader.GetString(reader.GetOrdinal("Description")),
            Icon = reader.IsDBNull(reader.GetOrdinal("Icon"))
                ? null
                : reader.GetString(reader.GetOrdinal("Icon")),
            IsShared = reader.GetBoolean(reader.GetOrdinal("IsShared")),
            ItemCount = reader.GetInt32(reader.GetOrdinal("ItemCount")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            UpdatedAt = reader.IsDBNull(reader.GetOrdinal("UpdatedAt"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("UpdatedAt")),
            Items = null
        }, parameters.ToArray());
    }

    public async Task<UserListDto?> GetListByIdAsync(Guid id, bool includeItems = true)
    {
        const string sql = @"
            SELECT Id, UserId, ListName, ListType, Description, Icon,
                   IsShared, ItemCount, CreatedAt, UpdatedAt
            FROM UserList
            WHERE Id = @Id AND IsDeleted = 0";

        var lists = await ExecuteReaderAsync(sql, reader => new UserListDto
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            UserId = reader.GetGuid(reader.GetOrdinal("UserId")),
            ListName = reader.GetString(reader.GetOrdinal("ListName")),
            ListType = reader.GetString(reader.GetOrdinal("ListType")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description"))
                ? null
                : reader.GetString(reader.GetOrdinal("Description")),
            Icon = reader.IsDBNull(reader.GetOrdinal("Icon"))
                ? null
                : reader.GetString(reader.GetOrdinal("Icon")),
            IsShared = reader.GetBoolean(reader.GetOrdinal("IsShared")),
            ItemCount = reader.GetInt32(reader.GetOrdinal("ItemCount")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            UpdatedAt = reader.IsDBNull(reader.GetOrdinal("UpdatedAt"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("UpdatedAt")),
            Items = null
        }, new SqlParameter("@Id", id));

        var list = lists.FirstOrDefault();
        if (list != null && includeItems)
        {
            list.Items = await GetListItemsAsync(id);
        }

        return list;
    }

    public async Task<Guid> CreateListAsync(Guid userId, CreateUserListRequest request)
    {
        var id = Guid.NewGuid();

        const string sql = @"
            INSERT INTO UserList
                (Id, UserId, ListName, ListType, Description, Icon, IsShared,
                 ItemCount, CreatedAt, IsDeleted)
            VALUES
                (@Id, @UserId, @ListName, @ListType, @Description, @Icon, 0,
                 0, GETUTCDATE(), 0)";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@UserId", userId),
            new SqlParameter("@ListName", request.ListName),
            new SqlParameter("@ListType", request.ListType),
            new SqlParameter("@Description", (object?)request.Description ?? DBNull.Value),
            new SqlParameter("@Icon", (object?)request.Icon ?? DBNull.Value));

        return id;
    }

    public async Task<bool> UpdateListAsync(Guid id, Guid userId, UpdateUserListRequest request)
    {
        const string sql = @"
            UPDATE UserList
            SET ListName = @ListName,
                Description = @Description,
                Icon = @Icon,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND UserId = @UserId AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@UserId", userId),
            new SqlParameter("@ListName", request.ListName),
            new SqlParameter("@Description", (object?)request.Description ?? DBNull.Value),
            new SqlParameter("@Icon", (object?)request.Icon ?? DBNull.Value));

        return rowsAffected > 0;
    }

    public async Task<bool> DeleteListAsync(Guid id, Guid userId)
    {
        const string sql = @"
            UPDATE UserList
            SET IsDeleted = 1,
                DeletedAt = GETUTCDATE(),
                DeletedBy = @UserId
            WHERE Id = @Id AND UserId = @UserId AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@UserId", userId));

        return rowsAffected > 0;
    }

    #endregion

    #region List Items

    public async Task<List<UserListItemDto>> GetListItemsAsync(Guid listId)
    {
        const string sql = @"
            SELECT Id, ListId, EntityType, EntityId, ItemText, Quantity,
                   Unit, Notes, IsChecked, SortOrder, CreatedAt, UpdatedAt
            FROM UserListItem
            WHERE ListId = @ListId AND IsDeleted = 0
            ORDER BY SortOrder, CreatedAt";

        return await ExecuteReaderAsync(sql, reader => new UserListItemDto
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            ListId = reader.GetGuid(reader.GetOrdinal("ListId")),
            EntityType = reader.IsDBNull(reader.GetOrdinal("EntityType"))
                ? null
                : reader.GetString(reader.GetOrdinal("EntityType")),
            EntityId = reader.IsDBNull(reader.GetOrdinal("EntityId"))
                ? null
                : reader.GetGuid(reader.GetOrdinal("EntityId")),
            ItemText = reader.GetString(reader.GetOrdinal("ItemText")),
            Quantity = reader.IsDBNull(reader.GetOrdinal("Quantity"))
                ? null
                : reader.GetDecimal(reader.GetOrdinal("Quantity")),
            Unit = reader.IsDBNull(reader.GetOrdinal("Unit"))
                ? null
                : reader.GetString(reader.GetOrdinal("Unit")),
            Notes = reader.IsDBNull(reader.GetOrdinal("Notes"))
                ? null
                : reader.GetString(reader.GetOrdinal("Notes")),
            IsChecked = reader.GetBoolean(reader.GetOrdinal("IsChecked")),
            SortOrder = reader.GetInt32(reader.GetOrdinal("SortOrder")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            UpdatedAt = reader.IsDBNull(reader.GetOrdinal("UpdatedAt"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("UpdatedAt"))
        }, new SqlParameter("@ListId", listId));
    }

    public async Task<Guid> AddListItemAsync(Guid listId, Guid userId, AddListItemRequest request)
    {
        var id = Guid.NewGuid();

        // Get next sort order
        const string getOrderSql = @"
            SELECT ISNULL(MAX(SortOrder), 0) + 1
            FROM UserListItem
            WHERE ListId = @ListId";

        var sortOrder = await ExecuteScalarAsync<int>(getOrderSql,
            new SqlParameter("@ListId", listId));

        const string sql = @"
            INSERT INTO UserListItem
                (Id, ListId, EntityType, EntityId, ItemText, Quantity, Unit,
                 Notes, IsChecked, SortOrder, CreatedAt, IsDeleted)
            VALUES
                (@Id, @ListId, @EntityType, @EntityId, @ItemText, @Quantity, @Unit,
                 @Notes, 0, @SortOrder, GETUTCDATE(), 0)";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@ListId", listId),
            new SqlParameter("@EntityType", (object?)request.EntityType ?? DBNull.Value),
            new SqlParameter("@EntityId", (object?)request.EntityId ?? DBNull.Value),
            new SqlParameter("@ItemText", request.ItemText),
            new SqlParameter("@Quantity", (object?)request.Quantity ?? DBNull.Value),
            new SqlParameter("@Unit", (object?)request.Unit ?? DBNull.Value),
            new SqlParameter("@Notes", (object?)request.Notes ?? DBNull.Value),
            new SqlParameter("@SortOrder", sortOrder));

        // Update list item count
        await UpdateListItemCountAsync(listId);

        return id;
    }

    public async Task<bool> UpdateListItemAsync(Guid itemId, UpdateListItemRequest request)
    {
        const string sql = @"
            UPDATE UserListItem
            SET ItemText = @ItemText,
                Quantity = @Quantity,
                Unit = @Unit,
                Notes = @Notes,
                SortOrder = @SortOrder,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", itemId),
            new SqlParameter("@ItemText", request.ItemText),
            new SqlParameter("@Quantity", (object?)request.Quantity ?? DBNull.Value),
            new SqlParameter("@Unit", (object?)request.Unit ?? DBNull.Value),
            new SqlParameter("@Notes", (object?)request.Notes ?? DBNull.Value),
            new SqlParameter("@SortOrder", request.SortOrder));

        return rowsAffected > 0;
    }

    public async Task<bool> DeleteListItemAsync(Guid itemId, Guid userId)
    {
        // Get list ID first to update count later
        const string getListSql = "SELECT ListId FROM UserListItem WHERE Id = @Id";
        var listId = await ExecuteScalarAsync<Guid>(getListSql,
            new SqlParameter("@Id", itemId));

        const string sql = @"
            UPDATE UserListItem
            SET IsDeleted = 1,
                DeletedAt = GETUTCDATE(),
                DeletedBy = @UserId
            WHERE Id = @Id AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", itemId),
            new SqlParameter("@UserId", userId));

        if (rowsAffected > 0 && listId != Guid.Empty)
        {
            await UpdateListItemCountAsync(listId);
            return true;
        }

        return false;
    }

    public async Task<bool> CheckListItemAsync(Guid itemId, bool isChecked)
    {
        const string sql = @"
            UPDATE UserListItem
            SET IsChecked = @IsChecked,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", itemId),
            new SqlParameter("@IsChecked", isChecked));

        return rowsAffected > 0;
    }

    private async Task UpdateListItemCountAsync(Guid listId)
    {
        const string sql = @"
            UPDATE UserList
            SET ItemCount = (SELECT COUNT(1) FROM UserListItem WHERE ListId = @ListId AND IsDeleted = 0),
                UpdatedAt = GETUTCDATE()
            WHERE Id = @ListId";

        await ExecuteNonQueryAsync(sql, new SqlParameter("@ListId", listId));
    }

    #endregion

    #region List Sharing

    public async Task<List<ListSharingDto>> GetListSharingAsync(Guid listId)
    {
        const string sql = @"
            SELECT Id, ListId, SharedWithUserId, Permission, SharedAt, ExpiresAt
            FROM ListSharing
            WHERE ListId = @ListId
            ORDER BY SharedAt DESC";

        return await ExecuteReaderAsync(sql, reader => new ListSharingDto
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            ListId = reader.GetGuid(reader.GetOrdinal("ListId")),
            SharedWithUserId = reader.GetGuid(reader.GetOrdinal("SharedWithUserId")),
            Permission = reader.GetString(reader.GetOrdinal("Permission")),
            SharedAt = reader.GetDateTime(reader.GetOrdinal("SharedAt")),
            ExpiresAt = reader.IsDBNull(reader.GetOrdinal("ExpiresAt"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("ExpiresAt"))
        }, new SqlParameter("@ListId", listId));
    }

    public async Task<List<UserListDto>> GetSharedListsAsync(Guid userId)
    {
        const string sql = @"
            SELECT ul.Id, ul.UserId, ul.ListName, ul.ListType, ul.Description,
                   ul.Icon, ul.IsShared, ul.ItemCount, ul.CreatedAt, ul.UpdatedAt
            FROM UserList ul
            INNER JOIN ListSharing ls ON ul.Id = ls.ListId
            WHERE ls.SharedWithUserId = @UserId
              AND ul.IsDeleted = 0
              AND (ls.ExpiresAt IS NULL OR ls.ExpiresAt > GETUTCDATE())
            ORDER BY ul.UpdatedAt DESC";

        return await ExecuteReaderAsync(sql, reader => new UserListDto
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            UserId = reader.GetGuid(reader.GetOrdinal("UserId")),
            ListName = reader.GetString(reader.GetOrdinal("ListName")),
            ListType = reader.GetString(reader.GetOrdinal("ListType")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description"))
                ? null
                : reader.GetString(reader.GetOrdinal("Description")),
            Icon = reader.IsDBNull(reader.GetOrdinal("Icon"))
                ? null
                : reader.GetString(reader.GetOrdinal("Icon")),
            IsShared = reader.GetBoolean(reader.GetOrdinal("IsShared")),
            ItemCount = reader.GetInt32(reader.GetOrdinal("ItemCount")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            UpdatedAt = reader.IsDBNull(reader.GetOrdinal("UpdatedAt"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("UpdatedAt")),
            Items = null
        }, new SqlParameter("@UserId", userId));
    }

    public async Task<Guid> ShareListAsync(Guid listId, Guid ownerId, ShareListRequest request)
    {
        var id = Guid.NewGuid();

        const string sql = @"
            INSERT INTO ListSharing
                (Id, ListId, SharedWithUserId, Permission, SharedAt, ExpiresAt)
            VALUES
                (@Id, @ListId, @SharedWithUserId, @Permission, GETUTCDATE(), @ExpiresAt)";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@ListId", listId),
            new SqlParameter("@SharedWithUserId", request.SharedWithUserId),
            new SqlParameter("@Permission", request.Permission),
            new SqlParameter("@ExpiresAt", (object?)request.ExpiresAt ?? DBNull.Value));

        // Update IsShared flag on list
        const string updateListSql = @"
            UPDATE UserList
            SET IsShared = 1
            WHERE Id = @ListId";

        await ExecuteNonQueryAsync(updateListSql, new SqlParameter("@ListId", listId));

        return id;
    }

    public async Task<bool> UpdateListSharingAsync(Guid sharingId, UpdateListSharingRequest request)
    {
        const string sql = @"
            UPDATE ListSharing
            SET Permission = @Permission,
                ExpiresAt = @ExpiresAt
            WHERE Id = @Id";

        var rowsAffected = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", sharingId),
            new SqlParameter("@Permission", request.Permission),
            new SqlParameter("@ExpiresAt", (object?)request.ExpiresAt ?? DBNull.Value));

        return rowsAffected > 0;
    }

    public async Task<bool> RemoveListSharingAsync(Guid sharingId, Guid ownerId)
    {
        // Get list ID and check if this is the last sharing
        const string getListSql = "SELECT ListId FROM ListSharing WHERE Id = @Id";
        var listId = await ExecuteScalarAsync<Guid>(getListSql,
            new SqlParameter("@Id", sharingId));

        const string sql = "DELETE FROM ListSharing WHERE Id = @Id";
        var rowsAffected = await ExecuteNonQueryAsync(sql, new SqlParameter("@Id", sharingId));

        if (rowsAffected > 0 && listId != Guid.Empty)
        {
            // Check if there are any other shares
            const string countSql = "SELECT COUNT(1) FROM ListSharing WHERE ListId = @ListId";
            var shareCount = await ExecuteScalarAsync<int>(countSql,
                new SqlParameter("@ListId", listId));

            // If no more shares, update IsShared flag
            if (shareCount == 0)
            {
                const string updateListSql = @"
                    UPDATE UserList
                    SET IsShared = 0
                    WHERE Id = @ListId";

                await ExecuteNonQueryAsync(updateListSql, new SqlParameter("@ListId", listId));
            }

            return true;
        }

        return false;
    }

    #endregion
}
