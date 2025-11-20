namespace ExpressRecipe.NotificationService.Data.Models;

public class Notification
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Type { get; set; } = string.Empty; // Expiring, Recall, Price, Community
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Priority { get; set; } = "Normal"; // Low, Normal, High, Urgent
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Data { get; set; } // JSON data for notification-specific info
}

public class NotificationPreferences
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public bool EmailEnabled { get; set; } = true;
    public bool PushEnabled { get; set; } = true;
    public bool ExpiringItemsEnabled { get; set; } = true;
    public int ExpiringItemsDaysAhead { get; set; } = 3;
    public bool RecallAlertsEnabled { get; set; } = true;
    public bool PriceDropAlertsEnabled { get; set; } = true;
    public bool CommunityUpdatesEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
