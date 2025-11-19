namespace ExpressRecipe.CommunityService.Data;

public interface ICommunityRepository
{
    // User Contributions
    Task<Guid> CreateContributionAsync(Guid userId, string contributionType, Guid? recipeId, Guid? productId, string? content, int points);
    Task<List<UserContributionDto>> GetUserContributionsAsync(Guid userId, int limit = 50);
    Task<LeaderboardDto> GetLeaderboardAsync(string period, int limit = 100);
    Task<int> GetUserPointsAsync(Guid userId);

    // Product Submissions
    Task<Guid> SubmitProductAsync(Guid userId, string name, string? brand, string? barcode, string? category, byte[]? photo, string? ingredientsText);
    Task<List<ProductSubmissionDto>> GetPendingSubmissionsAsync(int limit = 50);
    Task<List<ProductSubmissionDto>> GetUserSubmissionsAsync(Guid userId);
    Task ApproveSubmissionAsync(Guid submissionId, Guid approvedBy, Guid createdProductId);
    Task RejectSubmissionAsync(Guid submissionId, Guid rejectedBy, string reason);

    // User Reports
    Task<Guid> CreateReportAsync(Guid userId, string entityType, Guid entityId, string reportType, string reason, string? details);
    Task<List<UserReportDto>> GetPendingReportsAsync(int limit = 50);
    Task<List<UserReportDto>> GetEntityReportsAsync(string entityType, Guid entityId);
    Task ResolveReportAsync(Guid reportId, Guid resolvedBy, string resolution, string? notes);

    // Community Reviews
    Task<Guid> CreateReviewAsync(Guid userId, string entityType, Guid entityId, int rating, string? comment, bool isVerifiedPurchase);
    Task<List<CommunityReviewDto>> GetEntityReviewsAsync(string entityType, Guid entityId, int limit = 50);
    Task<List<CommunityReviewDto>> GetUserReviewsAsync(Guid userId);
    Task<ReviewSummaryDto> GetReviewSummaryAsync(string entityType, Guid entityId);
    Task DeleteReviewAsync(Guid reviewId);
    Task VoteReviewAsync(Guid reviewId, Guid userId, bool isHelpful);
}

public class UserContributionDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string ContributionType { get; set; } = string.Empty;
    public Guid? RecipeId { get; set; }
    public Guid? ProductId { get; set; }
    public string? Content { get; set; }
    public int Points { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class LeaderboardDto
{
    public string Period { get; set; } = string.Empty;
    public List<LeaderboardEntry> Entries { get; set; } = new();
}

public class LeaderboardEntry
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public int TotalPoints { get; set; }
    public int Contributions { get; set; }
    public int Rank { get; set; }
}

public class ProductSubmissionDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Barcode { get; set; }
    public string? Category { get; set; }
    public byte[]? Photo { get; set; }
    public string? IngredientsText { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? ApprovedBy { get; set; }
    public Guid? CreatedProductId { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime SubmittedAt { get; set; }
}

public class UserReportDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string ReportType { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? ResolvedBy { get; set; }
    public string? Resolution { get; set; }
    public DateTime ReportedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

public class CommunityReviewDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public bool IsVerifiedPurchase { get; set; }
    public int HelpfulVotes { get; set; }
    public int UnhelpfulVotes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ReviewSummaryDto
{
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public int TotalReviews { get; set; }
    public decimal AverageRating { get; set; }
    public Dictionary<int, int> RatingDistribution { get; set; } = new();
}
