using ExpressRecipe.Shared.DTOs.User;

namespace ExpressRecipe.UserService.Data;

public interface IReportsRepository
{
    // Report Types
    Task<List<ReportTypeDto>> GetReportTypesAsync();
    Task<ReportTypeDto?> GetReportTypeByIdAsync(Guid id);

    // Saved Reports
    Task<List<SavedReportDto>> GetUserSavedReportsAsync(Guid userId);
    Task<SavedReportDto?> GetSavedReportByIdAsync(Guid id);
    Task<Guid> CreateSavedReportAsync(Guid userId, CreateSavedReportRequest request);
    Task<bool> UpdateSavedReportAsync(Guid id, Guid userId, UpdateSavedReportRequest request);
    Task<bool> DeleteSavedReportAsync(Guid id, Guid userId);

    // Report History
    Task<List<ReportHistoryDto>> GetUserReportHistoryAsync(Guid userId, int pageNumber = 1, int pageSize = 50);
    Task<Guid> CreateReportHistoryAsync(Guid userId, CreateReportHistoryRequest request);

    // User Lists
    Task<List<UserListDto>> GetUserListsAsync(Guid userId, string? listType = null);
    Task<UserListDto?> GetListByIdAsync(Guid id, bool includeItems = true);
    Task<Guid> CreateListAsync(Guid userId, CreateUserListRequest request);
    Task<bool> UpdateListAsync(Guid id, Guid userId, UpdateUserListRequest request);
    Task<bool> DeleteListAsync(Guid id, Guid userId);

    // List Items
    Task<List<UserListItemDto>> GetListItemsAsync(Guid listId);
    Task<Guid> AddListItemAsync(Guid listId, Guid userId, AddListItemRequest request);
    Task<bool> UpdateListItemAsync(Guid itemId, UpdateListItemRequest request);
    Task<bool> DeleteListItemAsync(Guid itemId, Guid userId);
    Task<bool> CheckListItemAsync(Guid itemId, bool isChecked);

    // List Sharing
    Task<List<ListSharingDto>> GetListSharingAsync(Guid listId);
    Task<List<UserListDto>> GetSharedListsAsync(Guid userId);
    Task<Guid> ShareListAsync(Guid listId, Guid ownerId, ShareListRequest request);
    Task<bool> UpdateListSharingAsync(Guid sharingId, UpdateListSharingRequest request);
    Task<bool> RemoveListSharingAsync(Guid sharingId, Guid ownerId);
}
