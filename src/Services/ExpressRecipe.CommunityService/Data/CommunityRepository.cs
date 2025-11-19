using Microsoft.Data.SqlClient;

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

    public Task<List<UserContributionDto>> GetUserContributionsAsync(Guid userId, int limit = 50) => Task.FromResult(new List<UserContributionDto>());
    public Task<LeaderboardDto> GetLeaderboardAsync(string period, int limit = 100) => Task.FromResult(new LeaderboardDto { Period = period });
    public Task<int> GetUserPointsAsync(Guid userId) => Task.FromResult(0);

    public async Task<Guid> SubmitProductAsync(Guid userId, string name, string? brand, string? barcode, string? category, byte[]? photo, string? ingredientsText)
    {
        return Guid.NewGuid(); // Stub
    }

    public Task<List<ProductSubmissionDto>> GetPendingSubmissionsAsync(int limit = 50) => Task.FromResult(new List<ProductSubmissionDto>());
    public Task<List<ProductSubmissionDto>> GetUserSubmissionsAsync(Guid userId) => Task.FromResult(new List<ProductSubmissionDto>());
    public Task ApproveSubmissionAsync(Guid submissionId, Guid approvedBy, Guid createdProductId) => Task.CompletedTask;
    public Task RejectSubmissionAsync(Guid submissionId, Guid rejectedBy, string reason) => Task.CompletedTask;

    public async Task<Guid> CreateReportAsync(Guid userId, string entityType, Guid entityId, string reportType, string reason, string? details)
    {
        return Guid.NewGuid(); // Stub
    }

    public Task<List<UserReportDto>> GetPendingReportsAsync(int limit = 50) => Task.FromResult(new List<UserReportDto>());
    public Task<List<UserReportDto>> GetEntityReportsAsync(string entityType, Guid entityId) => Task.FromResult(new List<UserReportDto>());
    public Task ResolveReportAsync(Guid reportId, Guid resolvedBy, string resolution, string? notes) => Task.CompletedTask;

    public async Task<Guid> CreateReviewAsync(Guid userId, string entityType, Guid entityId, int rating, string? comment, bool isVerifiedPurchase)
    {
        return Guid.NewGuid(); // Stub
    }

    public Task<List<CommunityReviewDto>> GetEntityReviewsAsync(string entityType, Guid entityId, int limit = 50) => Task.FromResult(new List<CommunityReviewDto>());
    public Task<List<CommunityReviewDto>> GetUserReviewsAsync(Guid userId) => Task.FromResult(new List<CommunityReviewDto>());
    public Task<ReviewSummaryDto> GetReviewSummaryAsync(string entityType, Guid entityId) => Task.FromResult(new ReviewSummaryDto { EntityType = entityType, EntityId = entityId });
    public Task DeleteReviewAsync(Guid reviewId) => Task.CompletedTask;
    public Task VoteReviewAsync(Guid reviewId, Guid userId, bool isHelpful) => Task.CompletedTask;
}
