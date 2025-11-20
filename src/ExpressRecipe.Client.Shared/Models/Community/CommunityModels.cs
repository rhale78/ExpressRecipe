namespace ExpressRecipe.Client.Shared.Models.Community;

// Recipe Ratings
public class RecipeRatingDto
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int Rating { get; set; } // 1-5 stars
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateRecipeRatingRequest
{
    public Guid RecipeId { get; set; }
    public int Rating { get; set; } // 1-5 stars
}

public class UpdateRecipeRatingRequest
{
    public Guid RatingId { get; set; }
    public int Rating { get; set; }
}

// Recipe Reviews
public class RecipeReviewDto
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? UserAvatar { get; set; }
    public int Rating { get; set; } // 1-5 stars
    public string Title { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public List<string> Photos { get; set; } = new();
    public int HelpfulCount { get; set; }
    public bool IsVerifiedPurchase { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // User interaction
    public bool CurrentUserMarkedHelpful { get; set; }
}

public class CreateRecipeReviewRequest
{
    public Guid RecipeId { get; set; }
    public int Rating { get; set; } // 1-5 stars
    public string Title { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public List<string> PhotoUrls { get; set; } = new();
}

public class UpdateRecipeReviewRequest
{
    public Guid ReviewId { get; set; }
    public int Rating { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public List<string> PhotoUrls { get; set; } = new();
}

public class MarkReviewHelpfulRequest
{
    public Guid ReviewId { get; set; }
    public bool IsHelpful { get; set; }
}

// Recipe Rating Summary
public class RecipeRatingSummaryDto
{
    public Guid RecipeId { get; set; }
    public double AverageRating { get; set; }
    public int TotalRatings { get; set; }
    public int TotalReviews { get; set; }
    public Dictionary<int, int> RatingDistribution { get; set; } = new(); // Star -> Count
    public int FiveStarCount { get; set; }
    public int FourStarCount { get; set; }
    public int ThreeStarCount { get; set; }
    public int TwoStarCount { get; set; }
    public int OneStarCount { get; set; }
}

// Review Search/Filter
public class RecipeReviewSearchRequest
{
    public Guid RecipeId { get; set; }
    public int? MinRating { get; set; }
    public int? MaxRating { get; set; }
    public bool? VerifiedOnly { get; set; }
    public string SortBy { get; set; } = "MostRecent"; // MostRecent, MostHelpful, HighestRating, LowestRating
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class RecipeReviewSearchResult
{
    public List<RecipeReviewDto> Reviews { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

// Recipe Favorites/Bookmarks
public class RecipeFavoriteDto
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public Guid UserId { get; set; }
    public string? Notes { get; set; }
    public List<string> Tags { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

public class CreateRecipeFavoriteRequest
{
    public Guid RecipeId { get; set; }
    public string? Notes { get; set; }
    public List<string> Tags { get; set; } = new();
}

// Recipe Sharing
public class SharedRecipeDto
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public Guid SharedByUserId { get; set; }
    public string SharedByUserName { get; set; } = string.Empty;
    public string ShareToken { get; set; } = string.Empty;
    public bool IsPublic { get; set; }
    public int ViewCount { get; set; }
    public int CopyCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public class ShareRecipeRequest
{
    public Guid RecipeId { get; set; }
    public bool IsPublic { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public class CopySharedRecipeRequest
{
    public string ShareToken { get; set; } = string.Empty;
}

// User Recipe Stats
public class UserRecipeStatsDto
{
    public Guid UserId { get; set; }
    public int TotalRecipesCreated { get; set; }
    public int TotalRecipesShared { get; set; }
    public int TotalReviewsWritten { get; set; }
    public double AverageRatingGiven { get; set; }
    public int TotalHelpfulMarks { get; set; }
    public List<string> TopCategories { get; set; } = new();
}

// Popular/Trending Recipes
public class PopularRecipesRequest
{
    public string TimePeriod { get; set; } = "Week"; // Day, Week, Month, AllTime
    public string? Category { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class TrendingRecipeDto
{
    public Guid RecipeId { get; set; }
    public string RecipeName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public double AverageRating { get; set; }
    public int RatingCount { get; set; }
    public int ViewCount { get; set; }
    public int FavoriteCount { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
}

// Recipe Comments (Different from Reviews)
public class RecipeCommentDto
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public Guid? ParentCommentId { get; set; } // For threading/replies
    public int LikeCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool CurrentUserLiked { get; set; }
}

public class CreateRecipeCommentRequest
{
    public Guid RecipeId { get; set; }
    public string Comment { get; set; } = string.Empty;
    public Guid? ParentCommentId { get; set; }
}

public class LikeCommentRequest
{
    public Guid CommentId { get; set; }
    public bool IsLiked { get; set; }
}

// Recipe Reports/Flags
public class RecipeReportDto
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public Guid ReportedByUserId { get; set; }
    public string ReportReason { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Reviewed, Resolved
    public DateTime CreatedAt { get; set; }
}

public class ReportRecipeRequest
{
    public Guid RecipeId { get; set; }
    public string ReportReason { get; set; } = string.Empty; // Inappropriate, Copyright, Inaccurate, etc.
    public string? Details { get; set; }
}

// Achievement/Badges
public class UserBadgeDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string BadgeType { get; set; } = string.Empty;
    public string BadgeName { get; set; } = string.Empty;
    public string BadgeDescription { get; set; } = string.Empty;
    public string BadgeIcon { get; set; } = string.Empty;
    public DateTime EarnedAt { get; set; }
}

public static class BadgeTypes
{
    public const string FirstRecipe = "FirstRecipe";
    public const string TenRecipes = "TenRecipes";
    public const string FiftyRecipes = "FiftyRecipes";
    public const string HundredRecipes = "HundredRecipes";
    public const string FirstReview = "FirstReview";
    public const string TenReviews = "TenReviews";
    public const string HelpfulReviewer = "HelpfulReviewer"; // 50+ helpful marks
    public const string RecipeExplorer = "RecipeExplorer"; // Tried 100+ recipes
    public const string HealthyChef = "HealthyChef"; // 20+ healthy recipes
    public const string GlobalCook = "GlobalCook"; // Recipes from 10+ cuisines
}
