using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.DTOs.User;
using Microsoft.Data.SqlClient;

namespace ExpressRecipe.UserService.Data;

public class ActivityRepository : SqlHelper, IActivityRepository
{
    public ActivityRepository(string connectionString) : base(connectionString) { }

    public async Task<List<UserActivityDto>> GetUserActivityAsync(Guid userId, int pageNumber = 1, int pageSize = 50)
    {
        var offset = (pageNumber - 1) * pageSize;

        const string sql = @"
            SELECT Id, UserId, ActivityType, EntityType, EntityId, Metadata,
                   ActivityDate, DeviceType, IPAddress
            FROM UserActivity
            WHERE UserId = @UserId
            ORDER BY ActivityDate DESC
            OFFSET @Offset ROWS
            FETCH NEXT @PageSize ROWS ONLY";

        return await ExecuteReaderAsync(sql, MapToDto,
            new SqlParameter("@UserId", userId),
            new SqlParameter("@Offset", offset),
            new SqlParameter("@PageSize", pageSize));
    }

    public async Task<List<UserActivityDto>> GetRecentActivityAsync(Guid userId, int days = 7)
    {
        const string sql = @"
            SELECT TOP 100 Id, UserId, ActivityType, EntityType, EntityId, Metadata,
                   ActivityDate, DeviceType, IPAddress
            FROM UserActivity
            WHERE UserId = @UserId
              AND ActivityDate >= DATEADD(DAY, -@Days, GETUTCDATE())
            ORDER BY ActivityDate DESC";

        return await ExecuteReaderAsync(sql, MapToDto,
            new SqlParameter("@UserId", userId),
            new SqlParameter("@Days", days));
    }

    public async Task<List<UserActivityDto>> GetActivityByTypeAsync(Guid userId, string activityType)
    {
        const string sql = @"
            SELECT TOP 100 Id, UserId, ActivityType, EntityType, EntityId, Metadata,
                   ActivityDate, DeviceType, IPAddress
            FROM UserActivity
            WHERE UserId = @UserId
              AND ActivityType = @ActivityType
            ORDER BY ActivityDate DESC";

        return await ExecuteReaderAsync(sql, MapToDto,
            new SqlParameter("@UserId", userId),
            new SqlParameter("@ActivityType", activityType));
    }

    public async Task<Guid> LogActivityAsync(Guid userId, LogActivityRequest request)
    {
        var id = Guid.NewGuid();

        const string sql = @"
            INSERT INTO UserActivity
                (Id, UserId, ActivityType, EntityType, EntityId, Metadata,
                 ActivityDate, DeviceType, IPAddress)
            VALUES
                (@Id, @UserId, @ActivityType, @EntityType, @EntityId, @Metadata,
                 GETUTCDATE(), @DeviceType, @IPAddress)";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@UserId", userId),
            new SqlParameter("@ActivityType", request.ActivityType),
            new SqlParameter("@EntityType", (object?)request.EntityType ?? DBNull.Value),
            new SqlParameter("@EntityId", (object?)request.EntityId ?? DBNull.Value),
            new SqlParameter("@Metadata", (object?)request.Metadata ?? DBNull.Value),
            new SqlParameter("@DeviceType", (object?)request.DeviceType ?? DBNull.Value),
            new SqlParameter("@IPAddress", (object?)request.IPAddress ?? DBNull.Value));

        return id;
    }

    public async Task<UserActivitySummaryDto> GetActivitySummaryAsync(Guid userId, DateTime? startDate = null, DateTime? endDate = null)
    {
        startDate ??= DateTime.UtcNow.AddMonths(-1);
        endDate ??= DateTime.UtcNow;

        const string sql = @"
            SELECT
                COUNT(*) AS TotalActivities,
                MAX(ActivityDate) AS LastActivityDate,
                SUM(CASE WHEN ActivityType = 'Login' THEN 1 ELSE 0 END) AS LoginCount,
                SUM(CASE WHEN ActivityType = 'RecipeViewed' THEN 1 ELSE 0 END) AS RecipesViewed,
                SUM(CASE WHEN ActivityType = 'RecipeCooked' THEN 1 ELSE 0 END) AS RecipesCooked,
                SUM(CASE WHEN ActivityType = 'ProductScanned' THEN 1 ELSE 0 END) AS ProductsScanned
            FROM UserActivity
            WHERE UserId = @UserId
              AND ActivityDate >= @StartDate
              AND ActivityDate <= @EndDate";

        var summaries = await ExecuteReaderAsync(sql, reader => new UserActivitySummaryDto
        {
            TotalActivities = reader.GetInt32(reader.GetOrdinal("TotalActivities")),
            LastActivityDate = reader.IsDBNull(reader.GetOrdinal("LastActivityDate"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("LastActivityDate")),
            LoginCount = reader.GetInt32(reader.GetOrdinal("LoginCount")),
            RecipesViewed = reader.GetInt32(reader.GetOrdinal("RecipesViewed")),
            RecipesCooked = reader.GetInt32(reader.GetOrdinal("RecipesCooked")),
            ProductsScanned = reader.GetInt32(reader.GetOrdinal("ProductsScanned"))
        },
        new SqlParameter("@UserId", userId),
        new SqlParameter("@StartDate", startDate.Value),
        new SqlParameter("@EndDate", endDate.Value));

        var summary = summaries.FirstOrDefault() ?? new UserActivitySummaryDto();

        // Get activity counts by type
        summary.ActivityCounts = await GetActivityCountsByTypeAsync(userId, 30);

        // Get recent activities
        summary.RecentActivities = await GetRecentActivityAsync(userId, 7);

        return summary;
    }

    public async Task<Dictionary<string, int>> GetActivityCountsByTypeAsync(Guid userId, int days = 30)
    {
        const string sql = @"
            SELECT ActivityType, COUNT(*) AS Count
            FROM UserActivity
            WHERE UserId = @UserId
              AND ActivityDate >= DATEADD(DAY, -@Days, GETUTCDATE())
            GROUP BY ActivityType
            ORDER BY Count DESC";

        var results = await ExecuteReaderAsync(sql, reader => new
        {
            ActivityType = reader.GetString(0),
            Count = reader.GetInt32(1)
        },
        new SqlParameter("@UserId", userId),
        new SqlParameter("@Days", days));

        return results.ToDictionary(r => r.ActivityType, r => r.Count);
    }

    public async Task<int> GetCurrentStreakAsync(Guid userId)
    {
        const string sql = @"
            WITH DailyActivity AS (
                SELECT DISTINCT CAST(ActivityDate AS DATE) AS ActivityDay
                FROM UserActivity
                WHERE UserId = @UserId
                  AND ActivityDate >= DATEADD(DAY, -365, GETUTCDATE())
            ),
            StreakCalc AS (
                SELECT
                    ActivityDay,
                    DATEDIFF(DAY, ActivityDay, CAST(GETUTCDATE() AS DATE)) AS DaysAgo,
                    ROW_NUMBER() OVER (ORDER BY ActivityDay DESC) AS RowNum,
                    DATEDIFF(DAY, ActivityDay, CAST(GETUTCDATE() AS DATE)) - ROW_NUMBER() OVER (ORDER BY ActivityDay DESC) AS GroupId
                FROM DailyActivity
            )
            SELECT COUNT(*) AS CurrentStreak
            FROM StreakCalc
            WHERE GroupId = (
                SELECT MIN(GroupId)
                FROM StreakCalc
                WHERE DaysAgo <= 1  -- Today or yesterday
            )
            GROUP BY GroupId";

        var result = await ExecuteScalarAsync<int?>(sql,
            new SqlParameter("@UserId", userId));

        return result ?? 0;
    }

    public async Task<int> GetLongestStreakAsync(Guid userId)
    {
        const string sql = @"
            WITH DailyActivity AS (
                SELECT DISTINCT CAST(ActivityDate AS DATE) AS ActivityDay
                FROM UserActivity
                WHERE UserId = @UserId
            ),
            StreakCalc AS (
                SELECT
                    ActivityDay,
                    DATEDIFF(DAY, LAG(ActivityDay) OVER (ORDER BY ActivityDay), ActivityDay) AS DaysSinceLast
                FROM DailyActivity
            ),
            StreakGroups AS (
                SELECT
                    ActivityDay,
                    SUM(CASE WHEN DaysSinceLast > 1 THEN 1 ELSE 0 END) OVER (ORDER BY ActivityDay) AS StreakGroup
                FROM StreakCalc
            )
            SELECT MAX(StreakLength) AS LongestStreak
            FROM (
                SELECT COUNT(*) AS StreakLength
                FROM StreakGroups
                GROUP BY StreakGroup
            ) AS Streaks";

        var result = await ExecuteScalarAsync<int?>(sql,
            new SqlParameter("@UserId", userId));

        return result ?? 0;
    }

    public async Task<bool> HasActivityTodayAsync(Guid userId)
    {
        const string sql = @"
            SELECT COUNT(1)
            FROM UserActivity
            WHERE UserId = @UserId
              AND CAST(ActivityDate AS DATE) = CAST(GETUTCDATE() AS DATE)";

        var count = await ExecuteScalarAsync<int>(sql,
            new SqlParameter("@UserId", userId));

        return count > 0;
    }

    private static UserActivityDto MapToDto(SqlDataReader reader)
    {
        return new UserActivityDto
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            UserId = reader.GetGuid(reader.GetOrdinal("UserId")),
            ActivityType = reader.GetString(reader.GetOrdinal("ActivityType")),
            EntityType = reader.IsDBNull(reader.GetOrdinal("EntityType"))
                ? null
                : reader.GetString(reader.GetOrdinal("EntityType")),
            EntityId = reader.IsDBNull(reader.GetOrdinal("EntityId"))
                ? null
                : reader.GetGuid(reader.GetOrdinal("EntityId")),
            Metadata = reader.IsDBNull(reader.GetOrdinal("Metadata"))
                ? null
                : reader.GetString(reader.GetOrdinal("Metadata")),
            ActivityDate = reader.GetDateTime(reader.GetOrdinal("ActivityDate")),
            DeviceType = reader.IsDBNull(reader.GetOrdinal("DeviceType"))
                ? null
                : reader.GetString(reader.GetOrdinal("DeviceType")),
            IPAddress = reader.IsDBNull(reader.GetOrdinal("IPAddress"))
                ? null
                : reader.GetString(reader.GetOrdinal("IPAddress"))
        };
    }
}
