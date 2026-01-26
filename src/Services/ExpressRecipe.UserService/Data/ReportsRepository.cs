using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.DTOs.User;
using Microsoft.Data.SqlClient;

namespace ExpressRecipe.UserService.Data
{
    public class ReportsRepository : SqlHelper, IReportsRepository
    {
        public ReportsRepository(string connectionString) : base(connectionString) { }

        #region Report Types

        public async Task<List<ReportTypeDto>> GetReportTypesAsync()
        {
            const string sql = @"
            SELECT Id, Name, Description, Category, IsActive
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
                IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
            });
        }

        public async Task<ReportTypeDto?> GetReportTypeByIdAsync(Guid id)
        {
            const string sql = @"
            SELECT Id, Name, Description, Category, IsActive
            FROM ReportType
            WHERE Id = @Id";

            List<ReportTypeDto> reportTypes = await ExecuteReaderAsync(sql, reader => new ReportTypeDto
            {
                Id = reader.GetGuid(reader.GetOrdinal("Id")),
                Name = reader.GetString(reader.GetOrdinal("Name")),
                Description = reader.IsDBNull(reader.GetOrdinal("Description"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("Description")),
                Category = reader.GetString(reader.GetOrdinal("Category")),
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
                   sr.Schedule, sr.LastRunAt, sr.CreatedAt,
                   rt.Name AS ReportTypeName
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
                Schedule = reader.IsDBNull(reader.GetOrdinal("Schedule"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("Schedule")),
                LastRunAt = reader.IsDBNull(reader.GetOrdinal("LastRunAt"))
                    ? null
                    : reader.GetDateTime(reader.GetOrdinal("LastRunAt")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                ReportTypeName = reader.GetString(reader.GetOrdinal("ReportTypeName"))
            }, new SqlParameter("@UserId", userId));
        }

        public async Task<SavedReportDto?> GetSavedReportByIdAsync(Guid id)
        {
            const string sql = @"
            SELECT sr.Id, sr.UserId, sr.ReportTypeId, sr.ReportName, sr.Parameters,
                   sr.Schedule, sr.LastRunAt, sr.CreatedAt,
                   rt.Name AS ReportTypeName
            FROM SavedReport sr
            INNER JOIN ReportType rt ON sr.ReportTypeId = rt.Id
            WHERE sr.Id = @Id AND sr.IsDeleted = 0";

            List<SavedReportDto> savedReports = await ExecuteReaderAsync(sql, reader => new SavedReportDto
            {
                Id = reader.GetGuid(reader.GetOrdinal("Id")),
                UserId = reader.GetGuid(reader.GetOrdinal("UserId")),
                ReportTypeId = reader.GetGuid(reader.GetOrdinal("ReportTypeId")),
                ReportName = reader.GetString(reader.GetOrdinal("ReportName")),
                Parameters = reader.IsDBNull(reader.GetOrdinal("Parameters"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("Parameters")),
                Schedule = reader.IsDBNull(reader.GetOrdinal("Schedule"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("Schedule")),
                LastRunAt = reader.IsDBNull(reader.GetOrdinal("LastRunAt"))
                    ? null
                    : reader.GetDateTime(reader.GetOrdinal("LastRunAt")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                ReportTypeName = reader.GetString(reader.GetOrdinal("ReportTypeName"))
            }, new SqlParameter("@Id", id));

            return savedReports.FirstOrDefault();
        }

        public async Task<Guid> CreateSavedReportAsync(Guid userId, CreateSavedReportRequest request)
        {
            Guid id = Guid.NewGuid();

            const string sql = @"
            INSERT INTO SavedReport
                (Id, UserId, ReportTypeId, ReportName, Parameters, Schedule,
                 CreatedAt, IsDeleted)
            VALUES
                (@Id, @UserId, @ReportTypeId, @ReportName, @Parameters, @Schedule,
                 GETUTCDATE(), 0)";

            await ExecuteNonQueryAsync(sql,
                new SqlParameter("@Id", id),
                new SqlParameter("@UserId", userId),
                new SqlParameter("@ReportTypeId", request.ReportTypeId),
                new SqlParameter("@ReportName", request.ReportName),
                new SqlParameter("@Parameters", (object?)request.Parameters ?? DBNull.Value),
                new SqlParameter("@Schedule", (object?)request.Schedule ?? DBNull.Value));

            return id;
        }

        public async Task<bool> UpdateSavedReportAsync(Guid id, Guid userId, UpdateSavedReportRequest request)
        {
            const string sql = @"
            UPDATE SavedReport
            SET ReportName = @ReportName,
                Parameters = @Parameters,
                Schedule = @Schedule,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND UserId = @UserId AND IsDeleted = 0";

            var rowsAffected = await ExecuteNonQueryAsync(sql,
                new SqlParameter("@Id", id),
                new SqlParameter("@UserId", userId),
                new SqlParameter("@ReportName", request.ReportName),
                new SqlParameter("@Parameters", (object?)request.Parameters ?? DBNull.Value),
                new SqlParameter("@Schedule", (object?)request.Schedule ?? DBNull.Value));

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
                   rh.FileSize, rh.Format, rh.Status, rh.ErrorMessage,
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
                Format = reader.IsDBNull(reader.GetOrdinal("Format"))
                    ? string.Empty
                    : reader.GetString(reader.GetOrdinal("Format")),
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
            Guid id = Guid.NewGuid();

            const string sql = @"
            INSERT INTO ReportHistory
                (Id, UserId, ReportTypeId, Parameters, GeneratedAt, FileSize,
                 Format, Status, ErrorMessage)
            VALUES
                (@Id, @UserId, @ReportTypeId, @Parameters, GETUTCDATE(), @FileSize,
                 @Format, @Status, @ErrorMessage)";

            await ExecuteNonQueryAsync(sql,
                new SqlParameter("@Id", id),
                new SqlParameter("@UserId", userId),
                new SqlParameter("@ReportTypeId", request.ReportTypeId),
                new SqlParameter("@Parameters", (object?)request.Parameters ?? DBNull.Value),
                new SqlParameter("@FileSize", (object?)request.FileSize ?? DBNull.Value),
                new SqlParameter("@Format", (object?)request.ExportFormat ?? DBNull.Value),
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
            SELECT Id, UserId, Name, ListType, Description,
                   IsShared, ItemCount, CreatedAt, UpdatedAt
            FROM UserList
            WHERE UserId = @UserId AND IsDeleted = 0";

            List<SqlParameter> parameters = [new SqlParameter("@UserId", userId)];

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
                Name = reader.GetString(reader.GetOrdinal("Name")),
                ListType = reader.GetString(reader.GetOrdinal("ListType")),
                Description = reader.IsDBNull(reader.GetOrdinal("Description"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("Description")),
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
            SELECT Id, UserId, Name, ListType, Description,
                   IsShared, ItemCount, CreatedAt, UpdatedAt
            FROM UserList
            WHERE Id = @Id AND IsDeleted = 0";

            List<UserListDto> lists = await ExecuteReaderAsync(sql, reader => new UserListDto
            {
                Id = reader.GetGuid(reader.GetOrdinal("Id")),
                UserId = reader.GetGuid(reader.GetOrdinal("UserId")),
                Name = reader.GetString(reader.GetOrdinal("Name")),
                ListType = reader.GetString(reader.GetOrdinal("ListType")),
                Description = reader.IsDBNull(reader.GetOrdinal("Description"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("Description")),
                IsShared = reader.GetBoolean(reader.GetOrdinal("IsShared")),
                ItemCount = reader.GetInt32(reader.GetOrdinal("ItemCount")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                UpdatedAt = reader.IsDBNull(reader.GetOrdinal("UpdatedAt"))
                    ? null
                    : reader.GetDateTime(reader.GetOrdinal("UpdatedAt")),
                Items = null
            }, new SqlParameter("@Id", id));

            UserListDto? list = lists.FirstOrDefault();
            if (list != null && includeItems)
            {
                list.Items = await GetListItemsAsync(id);
            }

            return list;
        }

        public async Task<Guid> CreateListAsync(Guid userId, CreateUserListRequest request)
        {
            Guid id = Guid.NewGuid();

            const string sql = @"
            INSERT INTO UserList
                (Id, UserId, Name, ListType, Description, IsShared,
                 ItemCount, CreatedAt, IsDeleted)
            VALUES
                (@Id, @UserId, @Name, @ListType, @Description, @IsShared,
                 0, GETUTCDATE(), 0)";

            await ExecuteNonQueryAsync(sql,
                new SqlParameter("@Id", id),
                new SqlParameter("@UserId", userId),
                new SqlParameter("@Name", request.Name),
                new SqlParameter("@ListType", request.ListType),
                new SqlParameter("@Description", (object?)request.Description ?? DBNull.Value),
                new SqlParameter("@IsShared", request.IsShared));

            return id;
        }

        public async Task<bool> UpdateListAsync(Guid id, Guid userId, UpdateUserListRequest request)
        {
            const string sql = @"
            UPDATE UserList
            SET Name = @Name,
                Description = @Description,
                IsShared = @IsShared,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND UserId = @UserId AND IsDeleted = 0";

            var rowsAffected = await ExecuteNonQueryAsync(sql,
                new SqlParameter("@Id", id),
                new SqlParameter("@UserId", userId),
                new SqlParameter("@Name", request.Name),
                new SqlParameter("@Description", (object?)request.Description ?? DBNull.Value),
                new SqlParameter("@IsShared", request.IsShared));

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
            SELECT Id, ListId, ItemType, ItemId, ItemName, Quantity,
                   Unit, Notes, IsChecked, OrderIndex, AddedAt
            FROM UserListItem
            WHERE ListId = @ListId AND IsDeleted = 0
            ORDER BY OrderIndex, AddedAt";

            return await ExecuteReaderAsync(sql, reader => new UserListItemDto
            {
                Id = reader.GetGuid(reader.GetOrdinal("Id")),
                ListId = reader.GetGuid(reader.GetOrdinal("ListId")),
                ItemType = reader.GetString(reader.GetOrdinal("ItemType")),
                ItemId = reader.IsDBNull(reader.GetOrdinal("ItemId"))
                    ? null
                    : reader.GetGuid(reader.GetOrdinal("ItemId")),
                ItemName = reader.IsDBNull(reader.GetOrdinal("ItemName"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("ItemName")),
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
                OrderIndex = reader.GetInt32(reader.GetOrdinal("OrderIndex")),
                AddedAt = reader.GetDateTime(reader.GetOrdinal("AddedAt"))
            }, new SqlParameter("@ListId", listId));
        }

        public async Task<Guid> AddListItemAsync(Guid listId, Guid userId, AddListItemRequest request)
        {
            Guid id = Guid.NewGuid();

            // Get next sort order
            const string getOrderSql = @"
            SELECT ISNULL(MAX(OrderIndex), 0) + 1
            FROM UserListItem
            WHERE ListId = @ListId";

            var sortOrder = await ExecuteScalarAsync<int>(getOrderSql,
                new SqlParameter("@ListId", listId));

            const string sql = @"
            INSERT INTO UserListItem
                (Id, ListId, ItemType, ItemId, ItemName, Quantity, Unit,
                 Notes, IsChecked, OrderIndex, AddedAt, IsDeleted)
            VALUES
                (@Id, @ListId, @ItemType, @ItemId, @ItemName, @Quantity, @Unit,
                 @Notes, 0, @OrderIndex, GETUTCDATE(), 0)";

            await ExecuteNonQueryAsync(sql,
                new SqlParameter("@Id", id),
                new SqlParameter("@ListId", listId),
                new SqlParameter("@ItemType", request.ItemType),
                new SqlParameter("@ItemId", (object?)request.ItemId ?? DBNull.Value),
                new SqlParameter("@ItemName", (object?)request.ItemName ?? DBNull.Value),
                new SqlParameter("@Quantity", (object?)request.Quantity ?? DBNull.Value),
                new SqlParameter("@Unit", (object?)request.Unit ?? DBNull.Value),
                new SqlParameter("@Notes", (object?)request.Notes ?? DBNull.Value),
                new SqlParameter("@OrderIndex", sortOrder));

            // Update list item count
            await UpdateListItemCountAsync(listId);

            return id;
        }

        public async Task<bool> UpdateListItemAsync(Guid itemId, UpdateListItemRequest request)
        {
            const string sql = @"
            UPDATE UserListItem
            SET Quantity = @Quantity,
                Unit = @Unit,
                Notes = @Notes,
                IsChecked = @IsChecked,
                OrderIndex = @OrderIndex
            WHERE Id = @Id AND IsDeleted = 0";

            var rowsAffected = await ExecuteNonQueryAsync(sql,
                new SqlParameter("@Id", itemId),
                new SqlParameter("@Quantity", (object?)request.Quantity ?? DBNull.Value),
                new SqlParameter("@Unit", (object?)request.Unit ?? DBNull.Value),
                new SqlParameter("@Notes", (object?)request.Notes ?? DBNull.Value),
                new SqlParameter("@IsChecked", request.IsChecked),
                new SqlParameter("@OrderIndex", request.OrderIndex));

            return rowsAffected > 0;
        }

        public async Task<bool> DeleteListItemAsync(Guid itemId, Guid userId)
        {
            // Get list ID first to update count later
            const string getListSql = "SELECT ListId FROM UserListItem WHERE Id = @Id";
            Guid listId = await ExecuteScalarAsync<Guid>(getListSql,
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
            SELECT Id, ListId, SharedWithUserId, CanEdit, SharedAt, ExpiresAt
            FROM ListSharing
            WHERE ListId = @ListId
            ORDER BY SharedAt DESC";

            return await ExecuteReaderAsync(sql, reader => new ListSharingDto
            {
                Id = reader.GetGuid(reader.GetOrdinal("Id")),
                ListId = reader.GetGuid(reader.GetOrdinal("ListId")),
                SharedWithUserId = reader.IsDBNull(reader.GetOrdinal("SharedWithUserId"))
                    ? null
                    : reader.GetGuid(reader.GetOrdinal("SharedWithUserId")),
                CanEdit = reader.GetBoolean(reader.GetOrdinal("CanEdit")),
                SharedAt = reader.GetDateTime(reader.GetOrdinal("SharedAt")),
                ExpiresAt = reader.IsDBNull(reader.GetOrdinal("ExpiresAt"))
                    ? null
                    : reader.GetDateTime(reader.GetOrdinal("ExpiresAt"))
            }, new SqlParameter("@ListId", listId));
        }

        public async Task<List<UserListDto>> GetSharedListsAsync(Guid userId)
        {
            const string sql = @"
            SELECT ul.Id, ul.UserId, ul.Name, ul.ListType, ul.Description,
                   ul.IsShared, ul.ItemCount, ul.CreatedAt, ul.UpdatedAt
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
                Name = reader.GetString(reader.GetOrdinal("Name")),
                ListType = reader.GetString(reader.GetOrdinal("ListType")),
                Description = reader.IsDBNull(reader.GetOrdinal("Description"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("Description")),
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
            Guid id = Guid.NewGuid();

            const string sql = @"
            INSERT INTO ListSharing
                (Id, ListId, SharedWithUserId, CanEdit, SharedAt, ExpiresAt)
            VALUES
                (@Id, @ListId, @SharedWithUserId, @CanEdit, GETUTCDATE(), @ExpiresAt)";

            await ExecuteNonQueryAsync(sql,
                new SqlParameter("@Id", id),
                new SqlParameter("@ListId", listId),
                new SqlParameter("@SharedWithUserId", (object?)request.SharedWithUserId ?? DBNull.Value),
                new SqlParameter("@CanEdit", request.CanEdit),
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
            SET CanEdit = @CanEdit,
                ExpiresAt = @ExpiresAt
            WHERE Id = @Id";

            var rowsAffected = await ExecuteNonQueryAsync(sql,
                new SqlParameter("@Id", sharingId),
                new SqlParameter("@CanEdit", request.CanEdit),
                new SqlParameter("@ExpiresAt", (object?)request.ExpiresAt ?? DBNull.Value));

            return rowsAffected > 0;
        }

        public async Task<bool> RemoveListSharingAsync(Guid sharingId, Guid ownerId)
        {
            // Get list ID and check if this is the last sharing
            const string getListSql = "SELECT ListId FROM ListSharing WHERE Id = @Id";
            Guid listId = await ExecuteScalarAsync<Guid>(getListSql,
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
}
