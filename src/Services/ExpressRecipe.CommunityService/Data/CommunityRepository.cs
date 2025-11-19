using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace ExpressRecipe.CommunityService.Data;

public class CommunityRepository : ICommunityRepository
{
    private readonly string _connectionString;
    private readonly ILogger<CommunityRepository> _logger;

    public CommunityRepository(string connectionString, ILogger<CommunityRepository> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<Guid> CreateContributionAsync(Guid userId, string contributionType, Guid? recipeId, Guid? productId, string? content, int points)
    {
        const string sql = @"
            INSERT INTO UserContribution (UserId, ContributionType, RecipeId, ProductId, Content, Points, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @ContributionType, @RecipeId, @ProductId, @Content, @Points, GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@ContributionType", contributionType);
        command.Parameters.AddWithValue("@RecipeId", recipeId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@ProductId", productId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Content", content ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Points", points);

        return (Guid)await command.ExecuteScalarAsync()!;
    }

    public async Task<List<UserContributionDto>> GetUserContributionsAsync(Guid userId, int limit = 50)
    {
        const string sql = @"
            SELECT TOP (@Limit) Id, UserId, ContributionType, RecipeId, ProductId, Content, Points, CreatedAt
            FROM UserContribution
            WHERE UserId = @UserId
            ORDER BY CreatedAt DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@Limit", limit);

        var contributions = new List<UserContributionDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            contributions.Add(new UserContributionDto
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                ContributionType = reader.GetString(2),
                RecipeId = reader.IsDBNull(3) ? null : reader.GetGuid(3),
                ProductId = reader.IsDBNull(4) ? null : reader.GetGuid(4),
                Content = reader.IsDBNull(5) ? null : reader.GetString(5),
                Points = reader.GetInt32(6),
                CreatedAt = reader.GetDateTime(7)
            });
        }

        return contributions;
    }

    public async Task<LeaderboardDto> GetLeaderboardAsync(string period, int limit = 100)
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

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Limit", limit);
        command.Parameters.AddWithValue("@StartDate", startDate);

        var entries = new List<LeaderboardEntry>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            entries.Add(new LeaderboardEntry
            {
                UserId = reader.GetGuid(0),
                Username = reader.GetString(1),
                TotalPoints = reader.GetInt32(2),
                Contributions = reader.GetInt32(3),
                Rank = (int)reader.GetInt64(4)
            });
        }

        return new LeaderboardDto { Period = period, Entries = entries };
    }

    public async Task<int> GetUserPointsAsync(Guid userId)
    {
        const string sql = "SELECT ISNULL(SUM(Points), 0) FROM UserContribution WHERE UserId = @UserId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        return (int)await command.ExecuteScalarAsync()!;
    }

    public async Task<Guid> SubmitProductAsync(Guid userId, string name, string? brand, string? barcode, string? category, byte[]? photo, string? ingredientsText)
    {
        const string sql = @"
            INSERT INTO ProductSubmission (UserId, Name, Brand, Barcode, Category, Photo, IngredientsText, Status, SubmittedAt)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @Name, @Brand, @Barcode, @Category, @Photo, @IngredientsText, 'Pending', GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@Name", name);
        command.Parameters.AddWithValue("@Brand", brand ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Barcode", barcode ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Category", category ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Photo", photo ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@IngredientsText", ingredientsText ?? (object)DBNull.Value);

        return (Guid)await command.ExecuteScalarAsync()!;
    }

    public async Task<List<ProductSubmissionDto>> GetPendingSubmissionsAsync(int limit = 50)
    {
        const string sql = @"
            SELECT TOP (@Limit) Id, UserId, Name, Brand, Barcode, Category, Photo, IngredientsText, Status, ApprovedBy, CreatedProductId, RejectionReason, SubmittedAt
            FROM ProductSubmission
            WHERE Status = 'Pending'
            ORDER BY SubmittedAt ASC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Limit", limit);

        return await ReadProductSubmissions(command);
    }

    public async Task<List<ProductSubmissionDto>> GetUserSubmissionsAsync(Guid userId)
    {
        const string sql = @"
            SELECT Id, UserId, Name, Brand, Barcode, Category, Photo, IngredientsText, Status, ApprovedBy, CreatedProductId, RejectionReason, SubmittedAt
            FROM ProductSubmission
            WHERE UserId = @UserId
            ORDER BY SubmittedAt DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        return await ReadProductSubmissions(command);
    }

    public async Task ApproveSubmissionAsync(Guid submissionId, Guid approvedBy, Guid createdProductId)
    {
        const string sql = @"
            UPDATE ProductSubmission
            SET Status = 'Approved', ApprovedBy = @ApprovedBy, CreatedProductId = @CreatedProductId, ProcessedAt = GETUTCDATE()
            WHERE Id = @SubmissionId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@SubmissionId", submissionId);
        command.Parameters.AddWithValue("@ApprovedBy", approvedBy);
        command.Parameters.AddWithValue("@CreatedProductId", createdProductId);

        await command.ExecuteNonQueryAsync();
    }

    public async Task RejectSubmissionAsync(Guid submissionId, Guid rejectedBy, string reason)
    {
        const string sql = @"
            UPDATE ProductSubmission
            SET Status = 'Rejected', RejectedBy = @RejectedBy, RejectionReason = @Reason, ProcessedAt = GETUTCDATE()
            WHERE Id = @SubmissionId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@SubmissionId", submissionId);
        command.Parameters.AddWithValue("@RejectedBy", rejectedBy);
        command.Parameters.AddWithValue("@Reason", reason);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<Guid> CreateReportAsync(Guid userId, string entityType, Guid entityId, string reportType, string reason, string? details)
    {
        const string sql = @"
            INSERT INTO UserReport (UserId, EntityType, EntityId, ReportType, Reason, Details, Status, ReportedAt)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @EntityType, @EntityId, @ReportType, @Reason, @Details, 'Pending', GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@EntityType", entityType);
        command.Parameters.AddWithValue("@EntityId", entityId);
        command.Parameters.AddWithValue("@ReportType", reportType);
        command.Parameters.AddWithValue("@Reason", reason);
        command.Parameters.AddWithValue("@Details", details ?? (object)DBNull.Value);

        return (Guid)await command.ExecuteScalarAsync()!;
    }

    public async Task<List<UserReportDto>> GetPendingReportsAsync(int limit = 50)
    {
        const string sql = @"
            SELECT TOP (@Limit) Id, UserId, EntityType, EntityId, ReportType, Reason, Details, Status, ResolvedBy, Resolution, ReportedAt, ResolvedAt
            FROM UserReport
            WHERE Status = 'Pending'
            ORDER BY ReportedAt ASC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Limit", limit);

        return await ReadUserReports(command);
    }

    public async Task<List<UserReportDto>> GetEntityReportsAsync(string entityType, Guid entityId)
    {
        const string sql = @"
            SELECT Id, UserId, EntityType, EntityId, ReportType, Reason, Details, Status, ResolvedBy, Resolution, ReportedAt, ResolvedAt
            FROM UserReport
            WHERE EntityType = @EntityType AND EntityId = @EntityId
            ORDER BY ReportedAt DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@EntityType", entityType);
        command.Parameters.AddWithValue("@EntityId", entityId);

        return await ReadUserReports(command);
    }

    public async Task ResolveReportAsync(Guid reportId, Guid resolvedBy, string resolution, string? notes)
    {
        const string sql = @"
            UPDATE UserReport
            SET Status = 'Resolved', ResolvedBy = @ResolvedBy, Resolution = @Resolution, Notes = @Notes, ResolvedAt = GETUTCDATE()
            WHERE Id = @ReportId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ReportId", reportId);
        command.Parameters.AddWithValue("@ResolvedBy", resolvedBy);
        command.Parameters.AddWithValue("@Resolution", resolution);
        command.Parameters.AddWithValue("@Notes", notes ?? (object)DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<Guid> CreateReviewAsync(Guid userId, string entityType, Guid entityId, int rating, string? comment, bool isVerifiedPurchase)
    {
        const string sql = @"
            INSERT INTO CommunityReview (UserId, EntityType, EntityId, Rating, Comment, IsVerifiedPurchase, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @EntityType, @EntityId, @Rating, @Comment, @IsVerifiedPurchase, GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@EntityType", entityType);
        command.Parameters.AddWithValue("@EntityId", entityId);
        command.Parameters.AddWithValue("@Rating", rating);
        command.Parameters.AddWithValue("@Comment", comment ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@IsVerifiedPurchase", isVerifiedPurchase);

        return (Guid)await command.ExecuteScalarAsync()!;
    }

    public async Task<List<CommunityReviewDto>> GetEntityReviewsAsync(string entityType, Guid entityId, int limit = 50)
    {
        const string sql = @"
            SELECT TOP (@Limit) Id, UserId, EntityType, EntityId, Rating, Comment, IsVerifiedPurchase, HelpfulVotes, UnhelpfulVotes, CreatedAt
            FROM CommunityReview
            WHERE EntityType = @EntityType AND EntityId = @EntityId AND IsDeleted = 0
            ORDER BY HelpfulVotes DESC, CreatedAt DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Limit", limit);
        command.Parameters.AddWithValue("@EntityType", entityType);
        command.Parameters.AddWithValue("@EntityId", entityId);

        return await ReadCommunityReviews(command);
    }

    public async Task<List<CommunityReviewDto>> GetUserReviewsAsync(Guid userId)
    {
        const string sql = @"
            SELECT Id, UserId, EntityType, EntityId, Rating, Comment, IsVerifiedPurchase, HelpfulVotes, UnhelpfulVotes, CreatedAt
            FROM CommunityReview
            WHERE UserId = @UserId AND IsDeleted = 0
            ORDER BY CreatedAt DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        return await ReadCommunityReviews(command);
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

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@EntityType", entityType);
        command.Parameters.AddWithValue("@EntityId", entityId);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new ReviewSummaryDto
            {
                EntityType = entityType,
                EntityId = entityId,
                TotalReviews = reader.GetInt32(0),
                AverageRating = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1),
                RatingDistribution = new Dictionary<int, int>
                {
                    [1] = reader.GetInt32(2),
                    [2] = reader.GetInt32(3),
                    [3] = reader.GetInt32(4),
                    [4] = reader.GetInt32(5),
                    [5] = reader.GetInt32(6)
                }
            };
        }

        return new ReviewSummaryDto { EntityType = entityType, EntityId = entityId };
    }

    public async Task DeleteReviewAsync(Guid reviewId)
    {
        const string sql = "UPDATE CommunityReview SET IsDeleted = 1 WHERE Id = @ReviewId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ReviewId", reviewId);

        await command.ExecuteNonQueryAsync();
    }

    public async Task VoteReviewAsync(Guid reviewId, Guid userId, bool isHelpful)
    {
        const string sql = isHelpful
            ? "UPDATE CommunityReview SET HelpfulVotes = HelpfulVotes + 1 WHERE Id = @ReviewId"
            : "UPDATE CommunityReview SET UnhelpfulVotes = UnhelpfulVotes + 1 WHERE Id = @ReviewId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ReviewId", reviewId);

        await command.ExecuteNonQueryAsync();
    }

    private async Task<List<ProductSubmissionDto>> ReadProductSubmissions(SqlCommand command)
    {
        var submissions = new List<ProductSubmissionDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            submissions.Add(new ProductSubmissionDto
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                Name = reader.GetString(2),
                Brand = reader.IsDBNull(3) ? null : reader.GetString(3),
                Barcode = reader.IsDBNull(4) ? null : reader.GetString(4),
                Category = reader.IsDBNull(5) ? null : reader.GetString(5),
                Photo = reader.IsDBNull(6) ? null : (byte[])reader[6],
                IngredientsText = reader.IsDBNull(7) ? null : reader.GetString(7),
                Status = reader.GetString(8),
                ApprovedBy = reader.IsDBNull(9) ? null : reader.GetGuid(9),
                CreatedProductId = reader.IsDBNull(10) ? null : reader.GetGuid(10),
                RejectionReason = reader.IsDBNull(11) ? null : reader.GetString(11),
                SubmittedAt = reader.GetDateTime(12)
            });
        }
        return submissions;
    }

    private async Task<List<UserReportDto>> ReadUserReports(SqlCommand command)
    {
        var reports = new List<UserReportDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            reports.Add(new UserReportDto
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                EntityType = reader.GetString(2),
                EntityId = reader.GetGuid(3),
                ReportType = reader.GetString(4),
                Reason = reader.GetString(5),
                Details = reader.IsDBNull(6) ? null : reader.GetString(6),
                Status = reader.GetString(7),
                ResolvedBy = reader.IsDBNull(8) ? null : reader.GetGuid(8),
                Resolution = reader.IsDBNull(9) ? null : reader.GetString(9),
                ReportedAt = reader.GetDateTime(10),
                ResolvedAt = reader.IsDBNull(11) ? null : reader.GetDateTime(11)
            });
        }
        return reports;
    }

    private async Task<List<CommunityReviewDto>> ReadCommunityReviews(SqlCommand command)
    {
        var reviews = new List<CommunityReviewDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            reviews.Add(new CommunityReviewDto
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                EntityType = reader.GetString(2),
                EntityId = reader.GetGuid(3),
                Rating = reader.GetInt32(4),
                Comment = reader.IsDBNull(5) ? null : reader.GetString(5),
                IsVerifiedPurchase = reader.GetBoolean(6),
                HelpfulVotes = reader.GetInt32(7),
                UnhelpfulVotes = reader.GetInt32(8),
                CreatedAt = reader.GetDateTime(9)
            });
        }
        return reviews;
    }
}
