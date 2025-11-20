namespace ExpressRecipe.Client.Shared.Models.Notification;

public class NotificationDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Type { get; set; } = string.Empty; // ExpiringItem, ProductRecall, LowStock, ShoppingReminder, MealPlan
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Priority { get; set; } = "Normal"; // High, Normal, Low
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }

    // Context data (JSON)
    public string? ContextData { get; set; } // Related entity IDs, etc.
    public string? ActionUrl { get; set; } // Where to navigate when clicked

    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public class CreateNotificationRequest
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Priority { get; set; } = "Normal";
    public string? ContextData { get; set; }
    public string? ActionUrl { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public class MarkNotificationReadRequest
{
    public Guid NotificationId { get; set; }
    public bool IsRead { get; set; }
}

public class NotificationSearchRequest
{
    public string? Type { get; set; }
    public string? Priority { get; set; }
    public bool? IsRead { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public string? SortBy { get; set; } = "CreatedAt";
    public bool SortDescending { get; set; } = true;
}

public class NotificationSearchResult
{
    public List<NotificationDto> Notifications { get; set; } = new();
    public int TotalCount { get; set; }
    public int UnreadCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public class NotificationSummaryDto
{
    public int TotalNotifications { get; set; }
    public int UnreadNotifications { get; set; }
    public int HighPriorityUnread { get; set; }
    public Dictionary<string, int> NotificationsByType { get; set; } = new();
}

public class NotificationPreferencesDto
{
    public Guid UserId { get; set; }

    // Email notifications
    public bool EmailEnabled { get; set; } = true;
    public bool EmailOnExpiringItems { get; set; } = true;
    public bool EmailOnProductRecalls { get; set; } = true;
    public bool EmailOnLowStock { get; set; } = true;

    // Push notifications
    public bool PushEnabled { get; set; } = true;
    public bool PushOnExpiringItems { get; set; } = true;
    public bool PushOnProductRecalls { get; set; } = true;
    public bool PushOnLowStock { get; set; } = true;

    // In-app notifications
    public bool InAppEnabled { get; set; } = true;

    // Timing preferences
    public int ExpiringItemsDaysAhead { get; set; } = 3; // Alert 3 days before expiration
    public int LowStockThreshold { get; set; } = 2; // Alert when items < 2

    public DateTime? UpdatedAt { get; set; }
}

public class UpdateNotificationPreferencesRequest
{
    public bool EmailEnabled { get; set; } = true;
    public bool EmailOnExpiringItems { get; set; } = true;
    public bool EmailOnProductRecalls { get; set; } = true;
    public bool EmailOnLowStock { get; set; } = true;

    public bool PushEnabled { get; set; } = true;
    public bool PushOnExpiringItems { get; set; } = true;
    public bool PushOnProductRecalls { get; set; } = true;
    public bool PushOnLowStock { get; set; } = true;

    public bool InAppEnabled { get; set; } = true;

    public int ExpiringItemsDaysAhead { get; set; } = 3;
    public int LowStockThreshold { get; set; } = 2;
}

// Specific notification types for type safety
public static class NotificationTypes
{
    public const string ExpiringItem = "ExpiringItem";
    public const string ProductRecall = "ProductRecall";
    public const string LowStock = "LowStock";
    public const string ShoppingReminder = "ShoppingReminder";
    public const string MealPlanReminder = "MealPlanReminder";
    public const string RecipeRecommendation = "RecipeRecommendation";
}

public static class NotificationPriorities
{
    public const string High = "High";
    public const string Normal = "Normal";
    public const string Low = "Low";
}
