using ExpressRecipe.Shared.DTOs.User;

namespace ExpressRecipe.UserService.Data;

public interface IActivityRepository
{
    // User Activity
    Task<List<UserActivityDto>> GetUserActivityAsync(Guid userId, int pageNumber = 1, int pageSize = 50);
    Task<List<UserActivityDto>> GetRecentActivityAsync(Guid userId, int days = 7);
    Task<List<UserActivityDto>> GetActivityByTypeAsync(Guid userId, string activityType);
    Task<Guid> LogActivityAsync(Guid userId, CreateUserActivityRequest request);

    // Activity Summary
    Task<UserActivitySummaryDto> GetActivitySummaryAsync(Guid userId, DateTime? startDate = null, DateTime? endDate = null);
    Task<Dictionary<string, int>> GetActivityCountsByTypeAsync(Guid userId, int days = 30);

    // Streaks
    Task<int> GetCurrentStreakAsync(Guid userId);
    Task<int> GetLongestStreakAsync(Guid userId);
    Task<bool> HasActivityTodayAsync(Guid userId);
}
