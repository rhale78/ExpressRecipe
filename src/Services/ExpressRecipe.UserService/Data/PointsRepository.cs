using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.DTOs.User;
using Microsoft.Data.SqlClient;

namespace ExpressRecipe.UserService.Data;

public interface IPointsRepository
{
    // Contribution Types
    Task<List<ContributionTypeDto>> GetContributionTypesAsync(bool activeOnly = true);
    Task<ContributionTypeDto?> GetContributionTypeByIdAsync(Guid id);

    // User Contributions
    Task<Guid> CreateContributionAsync(Guid userId, Guid contributionTypeId, Guid? referenceId, string? referenceType, string? notes = null);
    Task<bool> ApproveContributionAsync(Guid contributionId, Guid approvedBy, bool approve, string? rejectionReason = null);
    Task<List<UserContributionDto>> GetUserContributionsAsync(Guid userId, bool? approvedOnly = null, int limit = 50);
    Task<UserContributionDto?> GetContributionByIdAsync(Guid id);

    // Point Transactions
    Task<Guid> AddPointTransactionAsync(Guid userId, int pointsAmount, string transactionType, string? description, Guid? contributionId = null, Guid? rewardItemId = null);
    Task<List<PointTransactionDto>> GetUserTransactionsAsync(Guid userId, int limit = 50);
    Task<int> GetUserPointBalanceAsync(Guid userId);
    Task<UserPointsSummaryDto> GetUserPointsSummaryAsync(Guid userId);

    // Rewards
    Task<List<RewardItemDto>> GetActiveRewardsAsync();
    Task<RewardItemDto?> GetRewardByIdAsync(Guid id);
    Task<bool> RedeemRewardAsync(Guid userId, Guid rewardItemId);
    Task<List<RewardItemDto>> GetUserRedeemedRewardsAsync(Guid userId, bool activeOnly = true);
}

public class PointsRepository : SqlHelper, IPointsRepository
{
    public PointsRepository(string connectionString) : base(connectionString)
    {
    }

    // Contribution Types

    public async Task<List<ContributionTypeDto>> GetContributionTypesAsync(bool activeOnly = true)
    {
        var sql = @"
            SELECT Id, Name, Description, PointValue, RequiresApproval, IsActive
            FROM ContributionType";

        if (activeOnly)
        {
            sql += " WHERE IsActive = 1";
        }

        sql += " ORDER BY Name";

        return await ExecuteReaderAsync(
            sql,
            reader => new ContributionTypeDto
            {
                Id = GetGuid(reader, "Id"),
                Name = GetString(reader, "Name") ?? string.Empty,
                Description = GetString(reader, "Description"),
                PointValue = GetInt(reader, "PointValue"),
                RequiresApproval = GetBoolean(reader, "RequiresApproval"),
                IsActive = GetBoolean(reader, "IsActive")
            });
    }

    public async Task<ContributionTypeDto?> GetContributionTypeByIdAsync(Guid id)
    {
        const string sql = @"
            SELECT Id, Name, Description, PointValue, RequiresApproval, IsActive
            FROM ContributionType
            WHERE Id = @Id";

        var results = await ExecuteReaderAsync(
            sql,
            reader => new ContributionTypeDto
            {
                Id = GetGuid(reader, "Id"),
                Name = GetString(reader, "Name") ?? string.Empty,
                Description = GetString(reader, "Description"),
                PointValue = GetInt(reader, "PointValue"),
                RequiresApproval = GetBoolean(reader, "RequiresApproval"),
                IsActive = GetBoolean(reader, "IsActive")
            },
            new SqlParameter("@Id", id));

        return results.FirstOrDefault();
    }

    // User Contributions

    public async Task<Guid> CreateContributionAsync(Guid userId, Guid contributionTypeId, Guid? referenceId, string? referenceType, string? notes = null)
    {
        var id = Guid.NewGuid();

        // Get contribution type to determine if approval is required
        var contributionType = await GetContributionTypeByIdAsync(contributionTypeId);
        if (contributionType == null)
        {
            throw new InvalidOperationException($"Contribution type {contributionTypeId} not found");
        }

        var isApproved = !contributionType.RequiresApproval;
        var pointsAwarded = isApproved ? contributionType.PointValue : 0;

        const string sql = @"
            INSERT INTO UserContribution (Id, UserId, ContributionTypeId, PointsAwarded, IsApproved,
                                         ReferenceId, ReferenceType, Notes, CreatedAt)
            VALUES (@Id, @UserId, @ContributionTypeId, @PointsAwarded, @IsApproved,
                    @ReferenceId, @ReferenceType, @Notes, GETUTCDATE())";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@UserId", userId),
            new SqlParameter("@ContributionTypeId", contributionTypeId),
            new SqlParameter("@PointsAwarded", pointsAwarded),
            new SqlParameter("@IsApproved", isApproved),
            new SqlParameter("@ReferenceId", (object?)referenceId ?? DBNull.Value),
            new SqlParameter("@ReferenceType", (object?)referenceType ?? DBNull.Value),
            new SqlParameter("@Notes", (object?)notes ?? DBNull.Value));

        // If auto-approved, create point transaction and update user balance
        if (isApproved && pointsAwarded > 0)
        {
            await AddPointTransactionAsync(userId, pointsAwarded, "Earned",
                $"Contribution: {contributionType.Name}", id, null);
        }

        return id;
    }

    public async Task<bool> ApproveContributionAsync(Guid contributionId, Guid approvedBy, bool approve, string? rejectionReason = null)
    {
        // Get the contribution
        var contribution = await GetContributionByIdAsync(contributionId);
        if (contribution == null || contribution.IsApproved)
        {
            return false;
        }

        // Get contribution type to know point value
        var contributionType = await GetContributionTypeByIdAsync(contribution.ContributionTypeId);
        if (contributionType == null)
        {
            return false;
        }

        const string sql = @"
            UPDATE UserContribution
            SET IsApproved = @IsApproved,
                ApprovedBy = @ApprovedBy,
                ApprovedAt = @ApprovedAt,
                RejectionReason = @RejectionReason,
                PointsAwarded = @PointsAwarded
            WHERE Id = @Id";

        var now = DateTime.UtcNow;
        var pointsAwarded = approve ? contributionType.PointValue : 0;

        var rowsAffected = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", contributionId),
            new SqlParameter("@IsApproved", approve),
            new SqlParameter("@ApprovedBy", approvedBy),
            new SqlParameter("@ApprovedAt", approve ? now : DBNull.Value),
            new SqlParameter("@RejectionReason", (object?)rejectionReason ?? DBNull.Value),
            new SqlParameter("@PointsAwarded", pointsAwarded));

        // If approved, create point transaction and update user balance
        if (approve && pointsAwarded > 0 && rowsAffected > 0)
        {
            await AddPointTransactionAsync(contribution.UserId, pointsAwarded, "Earned",
                $"Approved contribution: {contributionType.Name}", contributionId, null);
        }

        return rowsAffected > 0;
    }

    public async Task<List<UserContributionDto>> GetUserContributionsAsync(Guid userId, bool? approvedOnly = null, int limit = 50)
    {
        var sql = @"
            SELECT TOP (@Limit)
                   uc.Id, uc.UserId, uc.ContributionTypeId, ct.Name AS ContributionTypeName,
                   uc.PointsAwarded, uc.IsApproved, uc.ApprovedBy, uc.ApprovedAt,
                   uc.RejectionReason, uc.ReferenceId, uc.ReferenceType, uc.Notes, uc.CreatedAt
            FROM UserContribution uc
            INNER JOIN ContributionType ct ON uc.ContributionTypeId = ct.Id
            WHERE uc.UserId = @UserId";

        if (approvedOnly.HasValue)
        {
            sql += " AND uc.IsApproved = @IsApproved";
        }

        sql += " ORDER BY uc.CreatedAt DESC";

        var parameters = new List<SqlParameter>
        {
            new SqlParameter("@UserId", userId),
            new SqlParameter("@Limit", limit)
        };

        if (approvedOnly.HasValue)
        {
            parameters.Add(new SqlParameter("@IsApproved", approvedOnly.Value));
        }

        return await ExecuteReaderAsync(
            sql,
            reader => new UserContributionDto
            {
                Id = GetGuid(reader, "Id"),
                UserId = GetGuid(reader, "UserId"),
                ContributionTypeId = GetGuid(reader, "ContributionTypeId"),
                ContributionTypeName = GetString(reader, "ContributionTypeName"),
                PointsAwarded = GetInt(reader, "PointsAwarded"),
                IsApproved = GetBoolean(reader, "IsApproved"),
                ApprovedBy = GetGuidNullable(reader, "ApprovedBy"),
                ApprovedAt = GetDateTime(reader, "ApprovedAt"),
                RejectionReason = GetString(reader, "RejectionReason"),
                ReferenceId = GetGuidNullable(reader, "ReferenceId"),
                ReferenceType = GetString(reader, "ReferenceType"),
                Notes = GetString(reader, "Notes"),
                CreatedAt = GetDateTime(reader, "CreatedAt") ?? DateTime.UtcNow
            },
            parameters.ToArray());
    }

    public async Task<UserContributionDto?> GetContributionByIdAsync(Guid id)
    {
        const string sql = @"
            SELECT uc.Id, uc.UserId, uc.ContributionTypeId, ct.Name AS ContributionTypeName,
                   uc.PointsAwarded, uc.IsApproved, uc.ApprovedBy, uc.ApprovedAt,
                   uc.RejectionReason, uc.ReferenceId, uc.ReferenceType, uc.Notes, uc.CreatedAt
            FROM UserContribution uc
            INNER JOIN ContributionType ct ON uc.ContributionTypeId = ct.Id
            WHERE uc.Id = @Id";

        var results = await ExecuteReaderAsync(
            sql,
            reader => new UserContributionDto
            {
                Id = GetGuid(reader, "Id"),
                UserId = GetGuid(reader, "UserId"),
                ContributionTypeId = GetGuid(reader, "ContributionTypeId"),
                ContributionTypeName = GetString(reader, "ContributionTypeName"),
                PointsAwarded = GetInt(reader, "PointsAwarded"),
                IsApproved = GetBoolean(reader, "IsApproved"),
                ApprovedBy = GetGuidNullable(reader, "ApprovedBy"),
                ApprovedAt = GetDateTime(reader, "ApprovedAt"),
                RejectionReason = GetString(reader, "RejectionReason"),
                ReferenceId = GetGuidNullable(reader, "ReferenceId"),
                ReferenceType = GetString(reader, "ReferenceType"),
                Notes = GetString(reader, "Notes"),
                CreatedAt = GetDateTime(reader, "CreatedAt") ?? DateTime.UtcNow
            },
            new SqlParameter("@Id", id));

        return results.FirstOrDefault();
    }

    // Point Transactions

    public async Task<Guid> AddPointTransactionAsync(Guid userId, int pointsAmount, string transactionType,
        string? description, Guid? contributionId = null, Guid? rewardItemId = null)
    {
        var id = Guid.NewGuid();

        // Get current balance
        var currentBalance = await GetUserPointBalanceAsync(userId);
        var newBalance = currentBalance + pointsAmount;

        // Ensure balance doesn't go negative
        if (newBalance < 0)
        {
            throw new InvalidOperationException("Insufficient points balance");
        }

        const string sql = @"
            INSERT INTO PointTransaction (Id, UserId, TransactionType, PointsAmount, BalanceAfter,
                                         Description, UserContributionId, RewardItemId, TransactionDate)
            VALUES (@Id, @UserId, @TransactionType, @PointsAmount, @BalanceAfter,
                    @Description, @UserContributionId, @RewardItemId, GETUTCDATE())";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@UserId", userId),
            new SqlParameter("@TransactionType", transactionType),
            new SqlParameter("@PointsAmount", pointsAmount),
            new SqlParameter("@BalanceAfter", newBalance),
            new SqlParameter("@Description", (object?)description ?? DBNull.Value),
            new SqlParameter("@UserContributionId", (object?)contributionId ?? DBNull.Value),
            new SqlParameter("@RewardItemId", (object?)rewardItemId ?? DBNull.Value));

        // Update user profile balance
        const string updateUserSql = @"
            UPDATE UserProfile
            SET PointsBalance = @PointsBalance,
                LifetimePointsEarned = CASE WHEN @PointsAmount > 0 THEN LifetimePointsEarned + @PointsAmount ELSE LifetimePointsEarned END
            WHERE UserId = @UserId";

        await ExecuteNonQueryAsync(updateUserSql,
            new SqlParameter("@UserId", userId),
            new SqlParameter("@PointsBalance", newBalance),
            new SqlParameter("@PointsAmount", pointsAmount));

        return id;
    }

    public async Task<List<PointTransactionDto>> GetUserTransactionsAsync(Guid userId, int limit = 50)
    {
        const string sql = @"
            SELECT TOP (@Limit)
                   Id, UserId, TransactionType, PointsAmount, BalanceAfter,
                   Description, UserContributionId, RewardItemId, TransactionDate
            FROM PointTransaction
            WHERE UserId = @UserId
            ORDER BY TransactionDate DESC";

        return await ExecuteReaderAsync(
            sql,
            reader => new PointTransactionDto
            {
                Id = GetGuid(reader, "Id"),
                UserId = GetGuid(reader, "UserId"),
                TransactionType = GetString(reader, "TransactionType") ?? string.Empty,
                PointsAmount = GetInt(reader, "PointsAmount"),
                BalanceAfter = GetInt(reader, "BalanceAfter"),
                Description = GetString(reader, "Description"),
                UserContributionId = GetGuidNullable(reader, "UserContributionId"),
                RewardItemId = GetGuidNullable(reader, "RewardItemId"),
                TransactionDate = GetDateTime(reader, "TransactionDate") ?? DateTime.UtcNow
            },
            new SqlParameter("@UserId", userId),
            new SqlParameter("@Limit", limit));
    }

    public async Task<int> GetUserPointBalanceAsync(Guid userId)
    {
        const string sql = @"
            SELECT ISNULL(PointsBalance, 0)
            FROM UserProfile
            WHERE UserId = @UserId";

        return await ExecuteScalarAsync<int>(sql, new SqlParameter("@UserId", userId));
    }

    public async Task<UserPointsSummaryDto> GetUserPointsSummaryAsync(Guid userId)
    {
        const string sql = @"
            SELECT PointsBalance, LifetimePointsEarned
            FROM UserProfile
            WHERE UserId = @UserId";

        var profileResults = await ExecuteReaderAsync(
            sql,
            reader => new
            {
                CurrentBalance = GetInt(reader, "PointsBalance"),
                LifetimeEarned = GetInt(reader, "LifetimePointsEarned")
            },
            new SqlParameter("@UserId", userId));

        var profile = profileResults.FirstOrDefault();

        // Get total spent
        const string spentSql = @"
            SELECT ISNULL(SUM(ABS(PointsAmount)), 0)
            FROM PointTransaction
            WHERE UserId = @UserId AND PointsAmount < 0";

        var totalSpent = await ExecuteScalarAsync<int>(spentSql, new SqlParameter("@UserId", userId));

        // Get pending approval points
        const string pendingSql = @"
            SELECT ISNULL(SUM(ct.PointValue), 0)
            FROM UserContribution uc
            INNER JOIN ContributionType ct ON uc.ContributionTypeId = ct.Id
            WHERE uc.UserId = @UserId AND uc.IsApproved = 0";

        var pendingApproval = await ExecuteScalarAsync<int>(pendingSql, new SqlParameter("@UserId", userId));

        // Get recent transactions
        var recentTransactions = await GetUserTransactionsAsync(userId, 10);

        // Get recent contributions
        var recentContributions = await GetUserContributionsAsync(userId, null, 10);

        return new UserPointsSummaryDto
        {
            CurrentBalance = profile?.CurrentBalance ?? 0,
            LifetimeEarned = profile?.LifetimeEarned ?? 0,
            TotalSpent = totalSpent,
            PendingApproval = pendingApproval,
            RecentTransactions = recentTransactions,
            RecentContributions = recentContributions
        };
    }

    // Rewards

    public async Task<List<RewardItemDto>> GetActiveRewardsAsync()
    {
        const string sql = @"
            SELECT Id, Name, Description, PointsCost, RewardType, Value, ImageUrl,
                   IsActive, QuantityAvailable, CreatedAt
            FROM RewardItem
            WHERE IsActive = 1
              AND (QuantityAvailable IS NULL OR QuantityAvailable > 0)
            ORDER BY PointsCost, Name";

        return await ExecuteReaderAsync(
            sql,
            reader => new RewardItemDto
            {
                Id = GetGuid(reader, "Id"),
                Name = GetString(reader, "Name") ?? string.Empty,
                Description = GetString(reader, "Description"),
                PointsCost = GetInt(reader, "PointsCost"),
                RewardType = GetString(reader, "RewardType") ?? string.Empty,
                Value = GetString(reader, "Value"),
                ImageUrl = GetString(reader, "ImageUrl"),
                IsActive = GetBoolean(reader, "IsActive"),
                QuantityAvailable = GetIntNullable(reader, "QuantityAvailable"),
                CreatedAt = GetDateTime(reader, "CreatedAt") ?? DateTime.UtcNow
            });
    }

    public async Task<RewardItemDto?> GetRewardByIdAsync(Guid id)
    {
        const string sql = @"
            SELECT Id, Name, Description, PointsCost, RewardType, Value, ImageUrl,
                   IsActive, QuantityAvailable, CreatedAt
            FROM RewardItem
            WHERE Id = @Id";

        var results = await ExecuteReaderAsync(
            sql,
            reader => new RewardItemDto
            {
                Id = GetGuid(reader, "Id"),
                Name = GetString(reader, "Name") ?? string.Empty,
                Description = GetString(reader, "Description"),
                PointsCost = GetInt(reader, "PointsCost"),
                RewardType = GetString(reader, "RewardType") ?? string.Empty,
                Value = GetString(reader, "Value"),
                ImageUrl = GetString(reader, "ImageUrl"),
                IsActive = GetBoolean(reader, "IsActive"),
                QuantityAvailable = GetIntNullable(reader, "QuantityAvailable"),
                CreatedAt = GetDateTime(reader, "CreatedAt") ?? DateTime.UtcNow
            },
            new SqlParameter("@Id", id));

        return results.FirstOrDefault();
    }

    public async Task<bool> RedeemRewardAsync(Guid userId, Guid rewardItemId)
    {
        // Get reward
        var reward = await GetRewardByIdAsync(rewardItemId);
        if (reward == null || !reward.IsActive)
        {
            return false;
        }

        // Check quantity
        if (reward.QuantityAvailable.HasValue && reward.QuantityAvailable.Value <= 0)
        {
            return false;
        }

        // Check user balance
        var balance = await GetUserPointBalanceAsync(userId);
        if (balance < reward.PointsCost)
        {
            return false;
        }

        // Create redemption record
        var redemptionId = Guid.NewGuid();

        const string sql = @"
            INSERT INTO UserRewardRedemption (Id, UserId, RewardItemId, PointsSpent, RedeemedAt, IsActive)
            VALUES (@Id, @UserId, @RewardItemId, @PointsSpent, GETUTCDATE(), 1)";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", redemptionId),
            new SqlParameter("@UserId", userId),
            new SqlParameter("@RewardItemId", rewardItemId),
            new SqlParameter("@PointsSpent", reward.PointsCost));

        // Deduct points
        await AddPointTransactionAsync(userId, -reward.PointsCost, "Spent",
            $"Redeemed: {reward.Name}", null, rewardItemId);

        // Decrement quantity if limited
        if (reward.QuantityAvailable.HasValue)
        {
            const string updateQuantitySql = @"
                UPDATE RewardItem
                SET QuantityAvailable = QuantityAvailable - 1
                WHERE Id = @Id";

            await ExecuteNonQueryAsync(updateQuantitySql,
                new SqlParameter("@Id", rewardItemId));
        }

        return true;
    }

    public async Task<List<RewardItemDto>> GetUserRedeemedRewardsAsync(Guid userId, bool activeOnly = true)
    {
        var sql = @"
            SELECT r.Id, r.Name, r.Description, r.PointsCost, r.RewardType, r.Value, r.ImageUrl,
                   r.IsActive, r.QuantityAvailable, r.CreatedAt
            FROM UserRewardRedemption urr
            INNER JOIN RewardItem r ON urr.RewardItemId = r.Id
            WHERE urr.UserId = @UserId";

        if (activeOnly)
        {
            sql += " AND urr.IsActive = 1";
        }

        sql += " ORDER BY urr.RedeemedAt DESC";

        return await ExecuteReaderAsync(
            sql,
            reader => new RewardItemDto
            {
                Id = GetGuid(reader, "Id"),
                Name = GetString(reader, "Name") ?? string.Empty,
                Description = GetString(reader, "Description"),
                PointsCost = GetInt(reader, "PointsCost"),
                RewardType = GetString(reader, "RewardType") ?? string.Empty,
                Value = GetString(reader, "Value"),
                ImageUrl = GetString(reader, "ImageUrl"),
                IsActive = GetBoolean(reader, "IsActive"),
                QuantityAvailable = GetIntNullable(reader, "QuantityAvailable"),
                CreatedAt = GetDateTime(reader, "CreatedAt") ?? DateTime.UtcNow
            },
            new SqlParameter("@UserId", userId));
    }
}
