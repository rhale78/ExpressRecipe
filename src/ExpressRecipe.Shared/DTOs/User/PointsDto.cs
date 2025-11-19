using System.ComponentModel.DataAnnotations;

namespace ExpressRecipe.Shared.DTOs.User;

public class ContributionTypeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int PointValue { get; set; }
    public bool RequiresApproval { get; set; }
    public bool IsActive { get; set; }
}

public class UserContributionDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ContributionTypeId { get; set; }
    public string? ContributionTypeName { get; set; }
    public int PointsAwarded { get; set; }
    public bool IsApproved { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? ReferenceType { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PointTransactionDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public int PointsAmount { get; set; }
    public int BalanceAfter { get; set; }
    public string? Description { get; set; }
    public Guid? UserContributionId { get; set; }
    public Guid? RewardItemId { get; set; }
    public DateTime TransactionDate { get; set; }
}

public class RewardItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int PointsCost { get; set; }
    public string RewardType { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; }
    public int? QuantityAvailable { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class RedeemRewardRequest
{
    [Required]
    public Guid RewardItemId { get; set; }
}

public class SubscriptionTierDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal MonthlyPrice { get; set; }
    public decimal? YearlyPrice { get; set; }
    public string? Features { get; set; } // JSON
    public int MaxFamilyMembers { get; set; }
    public int? MaxRecipes { get; set; }
    public int? MaxMealPlans { get; set; }
    public bool AllowsRecipeImport { get; set; }
    public bool AllowsAdvancedReports { get; set; }
    public bool AllowsInventoryTracking { get; set; }
    public bool AllowsPriceTracking { get; set; }
    public string? SupportLevel { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
}

public class SubscriptionHistoryDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid SubscriptionTierId { get; set; }
    public string? SubscriptionTierName { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? BillingPeriod { get; set; }
    public decimal AmountPaid { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
    public string? PaymentProcessor { get; set; }
    public string? TransactionId { get; set; }
    public bool IsActive { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SubscribeRequest
{
    [Required]
    public Guid SubscriptionTierId { get; set; }

    [Required]
    [StringLength(20)]
    public string BillingPeriod { get; set; } = string.Empty; // Monthly, Yearly

    [Required]
    [StringLength(100)]
    public string PaymentMethodId { get; set; } = string.Empty; // External payment processor reference
}

public class CancelSubscriptionRequest
{
    [StringLength(1000)]
    public string? CancellationReason { get; set; }
}

public class UserPointsSummaryDto
{
    public int CurrentBalance { get; set; }
    public int LifetimeEarned { get; set; }
    public int TotalSpent { get; set; }
    public int PendingApproval { get; set; }
    public List<PointTransactionDto>? RecentTransactions { get; set; }
    public List<UserContributionDto>? RecentContributions { get; set; }
}
