namespace ExpressRecipe.InventoryService.Logging;

using Microsoft.Extensions.Logging;

/// <summary>
/// High-performance source-generated logging for InventoryService.
/// Zero allocation when log level is disabled.
/// </summary>
public static partial class InventoryServiceLogs
{
    // ── Controller – request lifecycle ───────────────────────────────────────

    [LoggerMessage(
        EventId = 7001,
        Level = LogLevel.Debug,
        Message = "[Inventory] Getting inventory for user {UserId}")]
    public static partial void LogGettingInventory(this ILogger logger, Guid userId);

    [LoggerMessage(
        EventId = 7002,
        Level = LogLevel.Debug,
        Message = "[Inventory] Item {ItemId} added to inventory for user {UserId}")]
    public static partial void LogInventoryItemAdded(this ILogger logger, Guid userId, Guid itemId);

    [LoggerMessage(
        EventId = 7003,
        Level = LogLevel.Debug,
        Message = "[Inventory] Item {ItemId} deleted from inventory for user {UserId}")]
    public static partial void LogInventoryItemDeleted(this ILogger logger, Guid userId, Guid itemId);

    [LoggerMessage(
        EventId = 7004,
        Level = LogLevel.Debug,
        Message = "[Inventory] Storage location {LocationId} created for user {UserId}")]
    public static partial void LogStorageLocationCreated(this ILogger logger, Guid userId, Guid locationId);

    [LoggerMessage(
        EventId = 7005,
        Level = LogLevel.Debug,
        Message = "[Inventory] Storage location {LocationId} deleted for user {UserId}")]
    public static partial void LogStorageLocationDeleted(this ILogger logger, Guid userId, Guid locationId);

    [LoggerMessage(
        EventId = 7006,
        Level = LogLevel.Debug,
        Message = "[Inventory] Getting items expiring within {Days} days for user {UserId}")]
    public static partial void LogGettingExpiringItems(this ILogger logger, Guid userId, int days);

    [LoggerMessage(
        EventId = 7007,
        Level = LogLevel.Debug,
        Message = "[Inventory] Purchase event recorded for user {UserId}, product {ProductId}, household {HouseholdId}")]
    public static partial void LogPurchaseEventRecorded(this ILogger logger, Guid userId, Guid? productId, Guid? householdId);

    [LoggerMessage(
        EventId = 7008,
        Level = LogLevel.Debug,
        Message = "[Inventory] Getting consumption patterns for user {UserId} household {HouseholdId}")]
    public static partial void LogGettingPatterns(this ILogger logger, Guid userId, Guid? householdId);

    [LoggerMessage(
        EventId = 7009,
        Level = LogLevel.Debug,
        Message = "[Inventory] Getting low stock predictions for user {UserId} household {HouseholdId}")]
    public static partial void LogGettingLowStockPredictions(this ILogger logger, Guid userId, Guid? householdId);

    [LoggerMessage(
        EventId = 7010,
        Level = LogLevel.Debug,
        Message = "[Inventory] Price watch alert set for user {UserId}, item {ItemId}, target ${TargetPrice}")]
    public static partial void LogPriceWatchAlertSet(this ILogger logger, Guid userId, Guid itemId, decimal targetPrice);

    [LoggerMessage(
        EventId = 7011,
        Level = LogLevel.Debug,
        Message = "[Inventory] Getting price watch alerts for user {UserId} household {HouseholdId}")]
    public static partial void LogGettingPriceWatchAlerts(this ILogger logger, Guid userId, Guid? householdId);

    [LoggerMessage(
        EventId = 7012,
        Level = LogLevel.Debug,
        Message = "[Inventory] Getting waste report for user {UserId} household {HouseholdId}")]
    public static partial void LogGettingWasteReport(this ILogger logger, Guid userId, Guid? householdId);

    [LoggerMessage(
        EventId = 7013,
        Level = LogLevel.Debug,
        Message = "[Inventory] Getting abandoned products for user {UserId} household {HouseholdId}")]
    public static partial void LogGettingAbandonedProducts(this ILogger logger, Guid userId, Guid? householdId);

    [LoggerMessage(
        EventId = 7014,
        Level = LogLevel.Debug,
        Message = "[Inventory] Inquiry response recorded for user {UserId}, inquiry {InquiryId}")]
    public static partial void LogInquiryResponseRecorded(this ILogger logger, Guid userId, Guid inquiryId);

    [LoggerMessage(
        EventId = 7015,
        Level = LogLevel.Debug,
        Message = "[Inventory] Getting pending inquiries for user {UserId}")]
    public static partial void LogGettingPendingInquiries(this ILogger logger, Guid userId);

    // ── Background workers ────────────────────────────────────────────────────

    [LoggerMessage(
        EventId = 7016,
        Level = LogLevel.Debug,
        Message = "[Inventory] Low stock alert created for user {UserId}: product {ProductId} has {Quantity} remaining")]
    public static partial void LogLowStockAlertCreated(this ILogger logger, Guid userId, Guid? productId, decimal quantity);

    [LoggerMessage(
        EventId = 7017,
        Level = LogLevel.Debug,
        Message = "[Inventory] Price drop detected for user {UserId}: product {ProductId} dropped from {OldPrice:C} to {NewPrice:C}")]
    public static partial void LogPriceDropAlertCreated(this ILogger logger, Guid userId, Guid? productId, decimal oldPrice, decimal newPrice);

    [LoggerMessage(
        EventId = 7018,
        Level = LogLevel.Debug,
        Message = "[Inventory] Purchase pattern updated for user {UserId}: product {ProductId} avg interval {AvgDays:F1} days")]
    public static partial void LogPurchasePatternUpdated(this ILogger logger, Guid userId, Guid? productId, double avgDays);

    [LoggerMessage(
        EventId = 7019,
        Level = LogLevel.Debug,
        Message = "[Inventory] Abandoned product found for user {UserId}: product {ProductId} not purchased in {DaysSince:F0} days")]
    public static partial void LogAbandonedProductFound(this ILogger logger, Guid userId, Guid? productId, double daysSince);

    [LoggerMessage(
        EventId = 7020,
        Level = LogLevel.Debug,
        Message = "[Inventory] Deducted recipe ingredients for user {UserId}: item {InventoryItemId} \u2192 {NewQty} remaining")]
    public static partial void LogRecipeCookedDeduction(this ILogger logger, Guid userId, Guid inventoryItemId, decimal newQty);

    [LoggerMessage(
        EventId = 7021,
        Level = LogLevel.Debug,
        Message = "[Inventory] Skipped inventory deduction for user {UserId}, recipe {RecipeId}: {Reason}")]
    public static partial void LogRecipeCookedSkipped(this ILogger logger, Guid userId, Guid recipeId, string reason);

    [LoggerMessage(
        EventId = 7022,
        Level = LogLevel.Debug,
        Message = "[Inventory] Expiration alert ({AlertType}) sent for user {UserId}, item {ItemId}")]
    public static partial void LogExpirationAlertSent(this ILogger logger, Guid userId, Guid itemId, string alertType);
}
