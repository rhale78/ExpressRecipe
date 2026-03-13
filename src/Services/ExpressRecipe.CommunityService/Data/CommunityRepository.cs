using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.Services;
using System.Text.Json;

namespace ExpressRecipe.CommunityService.Data;

public class CommunityRepository : SqlHelper, ICommunityRepository
{
    private readonly ILogger<CommunityRepository> _logger;
    private readonly HybridCacheService? _cache;
    private const string CachePrefix = "community:";

    public CommunityRepository(string connectionString, ILogger<CommunityRepository> logger, HybridCacheService? cache = null)
        : base(connectionString)
    {
        _logger = logger;
        _cache = cache;
    }

    public async Task<Guid> CreateContributionAsync(Guid userId, string contributionType, Guid? recipeId, Guid? productId, string? content, int points)
    {
        const string sql = @"
            INSERT INTO UserContribution (UserId, ContributionType, RecipeId, ProductId, Content, Points, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @ContributionType, @RecipeId, @ProductId, @Content, @Points, GETUTCDATE())";

        Guid result = (await ExecuteScalarAsync<Guid>(sql,
            CreateParameter("@UserId", userId),
            CreateParameter("@ContributionType", contributionType),
            CreateParameter("@RecipeId", recipeId ?? (object)DBNull.Value),
            CreateParameter("@ProductId", productId ?? (object)DBNull.Value),
            CreateParameter("@Content", content ?? (object)DBNull.Value),
            CreateParameter("@Points", points)))!;

        // Evict all leaderboard period variants since a new contribution changes rankings
        if (_cache != null)
        {
            foreach (var period in new[] { "day", "week", "month", "year" })
                await _cache.RemoveAsync($"{CachePrefix}leaderboard:{period}:100");
        }

        return result;
    }

    public async Task<List<UserContributionDto>> GetUserContributionsAsync(Guid userId, int limit = 50)
    {
        const string sql = @"
            SELECT TOP (@Limit) Id, UserId, ContributionType, RecipeId, ProductId, Content, Points, CreatedAt
            FROM UserContribution
            WHERE UserId = @UserId
            ORDER BY CreatedAt DESC";

        return await ExecuteReaderAsync<UserContributionDto>(sql, reader => new UserContributionDto
        {
            Id = GetGuid(reader, "Id"),
            UserId = GetGuid(reader, "UserId"),
            ContributionType = GetString(reader, "ContributionType")!,
            RecipeId = GetGuidNullable(reader, "RecipeId"),
            ProductId = GetGuidNullable(reader, "ProductId"),
            Content = GetString(reader, "Content"),
            Points = GetInt32(reader, "Points"),
            CreatedAt = GetDateTime(reader, "CreatedAt")
        },
        CreateParameter("@UserId", userId),
        CreateParameter("@Limit", limit));
    }

    public async Task<LeaderboardDto> GetLeaderboardAsync(string period, int limit = 100)
    {
        if (_cache != null)
        {
            return await _cache.GetOrSetAsync(
                $"{CachePrefix}leaderboard:{period}:{limit}",
                async (ct) => await GetLeaderboardFromDbAsync(period, limit),
                expiration: TimeSpan.FromMinutes(5))
                ?? new LeaderboardDto { Period = period, Entries = new() };
        }

        return await GetLeaderboardFromDbAsync(period, limit);
    }

    private async Task<LeaderboardDto> GetLeaderboardFromDbAsync(string period, int limit)
    {
        var startDate = period.ToLower() switch
        {
            "day" => DateTime.UtcNow.AddDays(-1),
            "week" => DateTime.UtcNow.AddDays(-7),
            "month" => DateTime.UtcNow.AddMonths(-1),
            "year" => DateTime.UtcNow.AddYears(-1),
            _ => DateTime.UtcNow.AddMonths(-1)
        };

        const string sql = @"
            SELECT TOP (@Limit)
                UserId,
                '' AS Username,
                SUM(Points) AS TotalPoints,
                COUNT(*) AS Contributions,
                ROW_NUMBER() OVER (ORDER BY SUM(Points) DESC) AS Rank
            FROM UserContribution
            WHERE CreatedAt >= @StartDate
            GROUP BY UserId
            ORDER BY TotalPoints DESC";

        var entries = await ExecuteReaderAsync<LeaderboardEntry>(sql, reader => new LeaderboardEntry
        {
            UserId = GetGuid(reader, "UserId"),
            Username = GetString(reader, "Username")!,
            TotalPoints = GetInt32(reader, "TotalPoints"),
            Contributions = GetInt32(reader, "Contributions"),
            Rank = (int)reader.GetInt64(reader.GetOrdinal("Rank"))
        },
        CreateParameter("@Limit", limit),
        CreateParameter("@StartDate", startDate));

        return new LeaderboardDto { Period = period, Entries = entries };
    }

    public async Task<int> GetUserPointsAsync(Guid userId)
    {
        const string sql = "SELECT ISNULL(SUM(Points), 0) FROM UserContribution WHERE UserId = @UserId";
        return (await ExecuteScalarAsync<int>(sql, CreateParameter("@UserId", userId)))!;
    }

    public async Task<Guid> SubmitProductAsync(Guid userId, string name, string? brand, string? barcode, string? category, byte[]? photo, string? ingredientsText)
    {
        const string sql = @"
            INSERT INTO ProductSubmission (UserId, Name, Brand, Barcode, Category, Photo, IngredientsText, Status, SubmittedAt)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @Name, @Brand, @Barcode, @Category, @Photo, @IngredientsText, 'Pending', GETUTCDATE())";

        return (await ExecuteScalarAsync<Guid>(sql,
            CreateParameter("@UserId", userId),
            CreateParameter("@Name", name),
            CreateParameter("@Brand", brand ?? (object)DBNull.Value),
            CreateParameter("@Barcode", barcode ?? (object)DBNull.Value),
            CreateParameter("@Category", category ?? (object)DBNull.Value),
            CreateParameter("@Photo", photo ?? (object)DBNull.Value),
            CreateParameter("@IngredientsText", ingredientsText ?? (object)DBNull.Value)))!;
    }

    public async Task<List<ProductSubmissionDto>> GetPendingSubmissionsAsync(int limit = 50)
    {
        const string sql = @"
            SELECT TOP (@Limit) Id, UserId, Name, Brand, Barcode, Category, Photo, IngredientsText, Status, ApprovedBy, CreatedProductId, RejectionReason, SubmittedAt
            FROM ProductSubmission
            WHERE Status = 'Pending'
            ORDER BY SubmittedAt ASC";

        return await ReadProductSubmissionsAsync(sql, CreateParameter("@Limit", limit));
    }

    public async Task<List<ProductSubmissionDto>> GetUserSubmissionsAsync(Guid userId)
    {
        const string sql = @"
            SELECT Id, UserId, Name, Brand, Barcode, Category, Photo, IngredientsText, Status, ApprovedBy, CreatedProductId, RejectionReason, SubmittedAt
            FROM ProductSubmission
            WHERE UserId = @UserId
            ORDER BY SubmittedAt DESC";

        return await ReadProductSubmissionsAsync(sql, CreateParameter("@UserId", userId));
    }

    public async Task ApproveSubmissionAsync(Guid submissionId, Guid approvedBy, Guid createdProductId)
    {
        const string sql = @"
            UPDATE ProductSubmission
            SET Status = 'Approved', ApprovedBy = @ApprovedBy, CreatedProductId = @CreatedProductId, ProcessedAt = GETUTCDATE()
            WHERE Id = @SubmissionId";

        await ExecuteNonQueryAsync(sql,
            CreateParameter("@SubmissionId", submissionId),
            CreateParameter("@ApprovedBy", approvedBy),
            CreateParameter("@CreatedProductId", createdProductId));
    }

    public async Task RejectSubmissionAsync(Guid submissionId, Guid rejectedBy, string reason)
    {
        const string sql = @"
            UPDATE ProductSubmission
            SET Status = 'Rejected', RejectedBy = @RejectedBy, RejectionReason = @Reason, ProcessedAt = GETUTCDATE()
            WHERE Id = @SubmissionId";

        await ExecuteNonQueryAsync(sql,
            CreateParameter("@SubmissionId", submissionId),
            CreateParameter("@RejectedBy", rejectedBy),
            CreateParameter("@Reason", reason));
    }

    public async Task<Guid> CreateReportAsync(Guid userId, string entityType, Guid entityId, string reportType, string reason, string? details)
    {
        const string sql = @"
            INSERT INTO UserReport (UserId, EntityType, EntityId, ReportType, Reason, Details, Status, ReportedAt)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @EntityType, @EntityId, @ReportType, @Reason, @Details, 'Pending', GETUTCDATE())";

        return (await ExecuteScalarAsync<Guid>(sql,
            CreateParameter("@UserId", userId),
            CreateParameter("@EntityType", entityType),
            CreateParameter("@EntityId", entityId),
            CreateParameter("@ReportType", reportType),
            CreateParameter("@Reason", reason),
            CreateParameter("@Details", details ?? (object)DBNull.Value)))!;
    }

    public async Task<List<UserReportDto>> GetPendingReportsAsync(int limit = 50)
    {
        const string sql = @"
            SELECT TOP (@Limit) Id, UserId, EntityType, EntityId, ReportType, Reason, Details, Status, ResolvedBy, Resolution, ReportedAt, ResolvedAt
            FROM UserReport
            WHERE Status = 'Pending'
            ORDER BY ReportedAt ASC";

        return await ReadUserReportsAsync(sql, CreateParameter("@Limit", limit));
    }

    public async Task<List<UserReportDto>> GetEntityReportsAsync(string entityType, Guid entityId)
    {
        const string sql = @"
            SELECT Id, UserId, EntityType, EntityId, ReportType, Reason, Details, Status, ResolvedBy, Resolution, ReportedAt, ResolvedAt
            FROM UserReport
            WHERE EntityType = @EntityType AND EntityId = @EntityId
            ORDER BY ReportedAt DESC";

        return await ReadUserReportsAsync(sql,
            CreateParameter("@EntityType", entityType),
            CreateParameter("@EntityId", entityId));
    }

    public async Task ResolveReportAsync(Guid reportId, Guid resolvedBy, string resolution, string? notes)
    {
        const string sql = @"
            UPDATE UserReport
            SET Status = 'Resolved', ResolvedBy = @ResolvedBy, Resolution = @Resolution, Notes = @Notes, ResolvedAt = GETUTCDATE()
            WHERE Id = @ReportId";

        await ExecuteNonQueryAsync(sql,
            CreateParameter("@ReportId", reportId),
            CreateParameter("@ResolvedBy", resolvedBy),
            CreateParameter("@Resolution", resolution),
            CreateParameter("@Notes", notes ?? (object)DBNull.Value));
    }

    public async Task<Guid> CreateReviewAsync(Guid userId, string entityType, Guid entityId, int rating, string? comment, bool isVerifiedPurchase)
    {
        const string sql = @"
            INSERT INTO CommunityReview (UserId, EntityType, EntityId, Rating, Comment, IsVerifiedPurchase, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @EntityType, @EntityId, @Rating, @Comment, @IsVerifiedPurchase, GETUTCDATE())";

        return (await ExecuteScalarAsync<Guid>(sql,
            CreateParameter("@UserId", userId),
            CreateParameter("@EntityType", entityType),
            CreateParameter("@EntityId", entityId),
            CreateParameter("@Rating", rating),
            CreateParameter("@Comment", comment ?? (object)DBNull.Value),
            CreateParameter("@IsVerifiedPurchase", isVerifiedPurchase)))!;
    }

    public async Task<List<CommunityReviewDto>> GetEntityReviewsAsync(string entityType, Guid entityId, int limit = 50)
    {
        const string sql = @"
            SELECT TOP (@Limit) Id, UserId, EntityType, EntityId, Rating, Comment, IsVerifiedPurchase, HelpfulVotes, UnhelpfulVotes, CreatedAt
            FROM CommunityReview
            WHERE EntityType = @EntityType AND EntityId = @EntityId AND IsDeleted = 0
            ORDER BY HelpfulVotes DESC, CreatedAt DESC";

        return await ReadCommunityReviewsAsync(sql,
            CreateParameter("@Limit", limit),
            CreateParameter("@EntityType", entityType),
            CreateParameter("@EntityId", entityId));
    }

    public async Task<List<CommunityReviewDto>> GetUserReviewsAsync(Guid userId)
    {
        const string sql = @"
            SELECT Id, UserId, EntityType, EntityId, Rating, Comment, IsVerifiedPurchase, HelpfulVotes, UnhelpfulVotes, CreatedAt
            FROM CommunityReview
            WHERE UserId = @UserId AND IsDeleted = 0
            ORDER BY CreatedAt DESC";

        return await ReadCommunityReviewsAsync(sql, CreateParameter("@UserId", userId));
    }

    public async Task<ReviewSummaryDto> GetReviewSummaryAsync(string entityType, Guid entityId)
    {
        const string sql = @"
            SELECT
                COUNT(*) AS TotalReviews,
                AVG(CAST(Rating AS DECIMAL(3,2))) AS AverageRating,
                SUM(CASE WHEN Rating = 1 THEN 1 ELSE 0 END) AS Rating1,
                SUM(CASE WHEN Rating = 2 THEN 1 ELSE 0 END) AS Rating2,
                SUM(CASE WHEN Rating = 3 THEN 1 ELSE 0 END) AS Rating3,
                SUM(CASE WHEN Rating = 4 THEN 1 ELSE 0 END) AS Rating4,
                SUM(CASE WHEN Rating = 5 THEN 1 ELSE 0 END) AS Rating5
            FROM CommunityReview
            WHERE EntityType = @EntityType AND EntityId = @EntityId AND IsDeleted = 0";

        var results = await ExecuteReaderAsync<ReviewSummaryDto>(sql, reader => new ReviewSummaryDto
        {
            EntityType = entityType,
            EntityId = entityId,
            TotalReviews = GetInt32(reader, "TotalReviews"),
            AverageRating = GetNullableDecimal(reader, "AverageRating") ?? 0,
            RatingDistribution = new Dictionary<int, int>
            {
                [1] = GetInt32(reader, "Rating1"),
                [2] = GetInt32(reader, "Rating2"),
                [3] = GetInt32(reader, "Rating3"),
                [4] = GetInt32(reader, "Rating4"),
                [5] = GetInt32(reader, "Rating5")
            }
        },
        CreateParameter("@EntityType", entityType),
        CreateParameter("@EntityId", entityId));

        return results.FirstOrDefault() ?? new ReviewSummaryDto { EntityType = entityType, EntityId = entityId };
    }

    public async Task DeleteReviewAsync(Guid reviewId)
    {
        const string sql = "UPDATE CommunityReview SET IsDeleted = 1 WHERE Id = @ReviewId";
        await ExecuteNonQueryAsync(sql, CreateParameter("@ReviewId", reviewId));
    }

    public async Task<bool> VoteReviewAsync(Guid reviewId, Guid userId, bool isHelpful)
    {
        const string insertVoteSql = @"
            INSERT INTO ReviewVote (Id, ReviewId, UserId, IsHelpful, VotedAt)
            SELECT NEWID(), @ReviewId, @UserId, @IsHelpful, GETUTCDATE()
            WHERE NOT EXISTS (
                SELECT 1 FROM ReviewVote WHERE ReviewId = @ReviewId AND UserId = @UserId
            )";

        var rowsInserted = await ExecuteNonQueryAsync(insertVoteSql,
            CreateParameter("@ReviewId", reviewId),
            CreateParameter("@UserId", userId),
            CreateParameter("@IsHelpful", isHelpful));

        if (rowsInserted == 0)
            return false;

        var incrementSql = isHelpful
            ? "UPDATE CommunityReview SET HelpfulVotes = HelpfulVotes + 1 WHERE Id = @ReviewId"
            : "UPDATE CommunityReview SET UnhelpfulVotes = UnhelpfulVotes + 1 WHERE Id = @ReviewId";

        await ExecuteNonQueryAsync(incrementSql, CreateParameter("@ReviewId", reviewId));
        return true;
    }

    public async Task<Guid?> GetSubmissionUserIdAsync(Guid submissionId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT UserId
            FROM ProductSubmission
            WHERE Id = @SubmissionId";

        var result = await ExecuteScalarAsync<Guid?>(sql, ct, CreateParameter("@SubmissionId", submissionId));
        return result;
    }

    public async Task<(Guid ReviewOwnerId, int HelpfulCount)?> GetReviewHelpfulInfoAsync(Guid reviewId)
    {
        const string sql = @"
            SELECT UserId, HelpfulVotes
            FROM CommunityReview
            WHERE Id = @ReviewId";

        var results = await ExecuteReaderAsync<(Guid, int)>(sql,
            reader => (GetGuid(reader, "UserId"), GetInt32(reader, "HelpfulVotes")),
            CreateParameter("@ReviewId", reviewId));

        if (results.Count == 0) return null;
        return results[0];
    }

    private Task<List<ProductSubmissionDto>> ReadProductSubmissionsAsync(string sql, params System.Data.Common.DbParameter[] parameters)
    {
        return ExecuteReaderAsync<ProductSubmissionDto>(sql, reader => new ProductSubmissionDto
        {
            Id = GetGuid(reader, "Id"),
            UserId = GetGuid(reader, "UserId"),
            Name = GetString(reader, "Name")!,
            Brand = GetString(reader, "Brand"),
            Barcode = GetString(reader, "Barcode"),
            Category = GetString(reader, "Category"),
            Photo = reader.IsDBNull(reader.GetOrdinal("Photo")) ? null : (byte[])reader["Photo"],
            IngredientsText = GetString(reader, "IngredientsText"),
            Status = GetString(reader, "Status")!,
            ApprovedBy = GetGuidNullable(reader, "ApprovedBy"),
            CreatedProductId = GetGuidNullable(reader, "CreatedProductId"),
            RejectionReason = GetString(reader, "RejectionReason"),
            SubmittedAt = GetDateTime(reader, "SubmittedAt")
        }, parameters);
    }

    private Task<List<UserReportDto>> ReadUserReportsAsync(string sql, params System.Data.Common.DbParameter[] parameters)
    {
        return ExecuteReaderAsync<UserReportDto>(sql, reader => new UserReportDto
        {
            Id = GetGuid(reader, "Id"),
            UserId = GetGuid(reader, "UserId"),
            EntityType = GetString(reader, "EntityType")!,
            EntityId = GetGuid(reader, "EntityId"),
            ReportType = GetString(reader, "ReportType")!,
            Reason = GetString(reader, "Reason")!,
            Details = GetString(reader, "Details"),
            Status = GetString(reader, "Status")!,
            ResolvedBy = GetGuidNullable(reader, "ResolvedBy"),
            Resolution = GetString(reader, "Resolution"),
            ReportedAt = GetDateTime(reader, "ReportedAt"),
            ResolvedAt = GetNullableDateTime(reader, "ResolvedAt")
        }, parameters);
    }

    private Task<List<CommunityReviewDto>> ReadCommunityReviewsAsync(string sql, params System.Data.Common.DbParameter[] parameters)
    {
        return ExecuteReaderAsync<CommunityReviewDto>(sql, reader => new CommunityReviewDto
        {
            Id = GetGuid(reader, "Id"),
            UserId = GetGuid(reader, "UserId"),
            EntityType = GetString(reader, "EntityType")!,
            EntityId = GetGuid(reader, "EntityId"),
            Rating = GetInt32(reader, "Rating"),
            Comment = GetString(reader, "Comment"),
            IsVerifiedPurchase = GetBoolean(reader, "IsVerifiedPurchase"),
            HelpfulVotes = GetInt32(reader, "HelpfulVotes"),
            UnhelpfulVotes = GetInt32(reader, "UnhelpfulVotes"),
            CreatedAt = GetDateTime(reader, "CreatedAt")
        }, parameters);
    }

    /// <summary>
    /// GDPR Right to be Forgotten: anonymises the user's identity in community records while
    /// preserving contribution data for database integrity. UserId is zeroed to Guid.Empty,
    /// product-submission photos are cleared, and review comment text is replaced.
    /// </summary>
    public async Task AnonymizeUserDataAsync(Guid userId, CancellationToken ct = default)
    {
        const string sql = @"
UPDATE UserContribution  SET UserId = '00000000-0000-0000-0000-000000000000' WHERE UserId = @UserId;
UPDATE ProductSubmission SET UserId = '00000000-0000-0000-0000-000000000000', Photo = NULL WHERE UserId = @UserId;
UPDATE CommunityReview   SET UserId = '00000000-0000-0000-0000-000000000000', Comment = '[deleted]' WHERE UserId = @UserId;
DELETE FROM UserReport   WHERE UserId = @UserId;";

        await ExecuteNonQueryAsync(sql, CreateParameter("@UserId", userId));
    }
}
