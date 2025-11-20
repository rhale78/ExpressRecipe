using System.ComponentModel.DataAnnotations;

namespace ExpressRecipe.Shared.DTOs.User;

// Friend Management

public class UserFriendDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid FriendUserId { get; set; }
    public string? FriendUserName { get; set; }
    public string? FriendDisplayName { get; set; }
    public string Status { get; set; } = string.Empty; // Pending, Accepted, Blocked
    public Guid RequestedBy { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? BlockedAt { get; set; }
    public Guid? BlockedBy { get; set; }
    public string? Notes { get; set; }
}

public class FriendInvitationDto
{
    public Guid Id { get; set; }
    public Guid InviterId { get; set; }
    public string? InviterName { get; set; }
    public string InviteeEmail { get; set; } = string.Empty;
    public string? InviteePhone { get; set; }
    public string InvitationCode { get; set; } = string.Empty;
    public string? InvitationMessage { get; set; }
    public string Status { get; set; } = string.Empty; // Sent, Accepted, Expired
    public DateTime SentAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public Guid? AcceptedByUserId { get; set; }
    public DateTime ExpiresAt { get; set; }
}

public class SendFriendRequestRequest
{
    [Required]
    public Guid FriendUserId { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}

public class InviteFriendRequest
{
    [Required]
    [EmailAddress]
    [StringLength(200)]
    public string InviteeEmail { get; set; } = string.Empty;

    [Phone]
    [StringLength(20)]
    public string? InviteePhone { get; set; }

    [StringLength(1000)]
    public string? InvitationMessage { get; set; }
}

public class AcceptFriendRequestRequest
{
    [Required]
    public Guid FriendRequestId { get; set; }
}

public class BlockUserRequest
{
    [Required]
    public Guid UserIdToBlock { get; set; }

    [StringLength(500)]
    public string? Reason { get; set; }
}

// Comments

public class RecipeCommentDto
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public Guid UserId { get; set; }
    public string? UserName { get; set; }
    public Guid? ParentCommentId { get; set; }
    public string CommentText { get; set; } = string.Empty;
    public bool IsEdited { get; set; }
    public DateTime? EditedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public int LikeCount { get; set; }
    public bool UserHasLiked { get; set; }

    // Navigation properties
    public List<RecipeCommentDto>? Replies { get; set; }
}

public class ProductCommentDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid UserId { get; set; }
    public string? UserName { get; set; }
    public Guid? ParentCommentId { get; set; }
    public string CommentText { get; set; } = string.Empty;
    public bool IsEdited { get; set; }
    public DateTime? EditedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public int LikeCount { get; set; }
    public bool UserHasLiked { get; set; }

    // Navigation properties
    public List<ProductCommentDto>? Replies { get; set; }
}

public class RestaurantCommentDto
{
    public Guid Id { get; set; }
    public Guid RestaurantId { get; set; }
    public Guid UserId { get; set; }
    public string? UserName { get; set; }
    public Guid? ParentCommentId { get; set; }
    public string CommentText { get; set; } = string.Empty;
    public bool IsEdited { get; set; }
    public DateTime? EditedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public int LikeCount { get; set; }
    public bool UserHasLiked { get; set; }

    // Navigation properties
    public List<RestaurantCommentDto>? Replies { get; set; }
}

public class IngredientCommentDto
{
    public Guid Id { get; set; }
    public Guid? IngredientId { get; set; }
    public Guid? BaseIngredientId { get; set; }
    public Guid UserId { get; set; }
    public string? UserName { get; set; }
    public Guid? ParentCommentId { get; set; }
    public string CommentText { get; set; } = string.Empty;
    public bool IsEdited { get; set; }
    public DateTime? EditedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public int LikeCount { get; set; }
    public bool UserHasLiked { get; set; }

    // Navigation properties
    public List<IngredientCommentDto>? Replies { get; set; }
}

public class CreateCommentRequest
{
    [Required]
    public Guid EntityId { get; set; }

    public Guid? ParentCommentId { get; set; }

    [Required]
    [StringLength(5000)]
    public string CommentText { get; set; } = string.Empty;
}

public class UpdateCommentRequest
{
    [Required]
    [StringLength(5000)]
    public string CommentText { get; set; } = string.Empty;
}

public class CommentLikeDto
{
    public Guid Id { get; set; }
    public string CommentType { get; set; } = string.Empty; // Recipe, Product, Restaurant, Ingredient
    public Guid CommentId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class LikeCommentRequest
{
    [Required]
    public string CommentType { get; set; } = string.Empty;

    [Required]
    public Guid CommentId { get; set; }
}

// Family Scores

public class FamilyScoreDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string EntityType { get; set; } = string.Empty; // Recipe, Product, Restaurant, MenuItem, Ingredient
    public Guid EntityId { get; set; }
    public string? EntityName { get; set; }
    public decimal? FamilyAverageScore { get; set; }
    public string? Notes { get; set; }
    public bool IsFavorite { get; set; }
    public bool IsBlacklisted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public List<FamilyMemberScoreDto>? MemberScores { get; set; }
}

public class FamilyMemberScoreDto
{
    public Guid Id { get; set; }
    public Guid FamilyScoreId { get; set; }
    public Guid FamilyMemberId { get; set; }
    public string? FamilyMemberName { get; set; }
    public int IndividualScore { get; set; } // 1-5
    public string? Notes { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class CreateFamilyScoreRequest
{
    [Required]
    [StringLength(50)]
    public string EntityType { get; set; } = string.Empty;

    [Required]
    public Guid EntityId { get; set; }

    public string? Notes { get; set; }

    public bool IsFavorite { get; set; }

    public bool IsBlacklisted { get; set; }

    public List<CreateFamilyMemberScoreRequest>? MemberScores { get; set; }
}

public class CreateFamilyMemberScoreRequest
{
    [Required]
    public Guid FamilyMemberId { get; set; }

    [Required]
    [Range(1, 5)]
    public int IndividualScore { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}

public class UpdateFamilyScoreRequest
{
    public string? Notes { get; set; }

    public bool IsFavorite { get; set; }

    public bool IsBlacklisted { get; set; }
}

public class UpdateFamilyMemberScoreRequest
{
    [Required]
    [Range(1, 5)]
    public int IndividualScore { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}

// User Activity

public class UserActivityDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public string? Metadata { get; set; } // JSON
    public DateTime ActivityDate { get; set; }
    public string? DeviceType { get; set; }
    public string? IPAddress { get; set; }
}

public class LogActivityRequest
{
    [Required]
    [StringLength(100)]
    public string ActivityType { get; set; } = string.Empty;

    [StringLength(50)]
    public string? EntityType { get; set; }

    public Guid? EntityId { get; set; }

    public string? Metadata { get; set; }

    [StringLength(50)]
    public string? DeviceType { get; set; }
}

public class UserActivitySummaryDto
{
    public int TotalActivities { get; set; }
    public DateTime? LastActivityDate { get; set; }
    public int LoginCount { get; set; }
    public int RecipesViewed { get; set; }
    public int RecipesCooked { get; set; }
    public int ProductsScanned { get; set; }
    public Dictionary<string, int>? ActivityCounts { get; set; }
    public List<UserActivityDto>? RecentActivities { get; set; }
}

public class FriendsSummaryDto
{
    public int TotalFriends { get; set; }
    public int PendingRequests { get; set; }
    public int SentRequests { get; set; }
    public int BlockedUsers { get; set; }
    public List<UserFriendDto>? RecentFriends { get; set; }
    public List<UserFriendDto>? PendingFriendRequests { get; set; }
}
