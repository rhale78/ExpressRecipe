namespace ExpressRecipe.NotificationService.Data;

public interface INotificationRepository
{
    // Notifications
    Task<Guid> CreateNotificationAsync(Guid userId, string type, string title, string message, string? actionUrl, Dictionary<string, string>? metadata);
    Task<List<NotificationDto>> GetUserNotificationsAsync(Guid userId, bool unreadOnly = false, int limit = 50);
    Task<NotificationDto?> GetNotificationAsync(Guid notificationId);
    Task MarkAsReadAsync(Guid notificationId);
    Task MarkAllAsReadAsync(Guid userId);
    Task DeleteNotificationAsync(Guid notificationId);
    Task<int> GetUnreadCountAsync(Guid userId);

    // Preferences
    Task<Guid> SavePreferenceAsync(Guid userId, string notificationType, bool emailEnabled, bool pushEnabled, bool smsEnabled, bool inAppEnabled);
    Task<List<NotificationPreferenceDto>> GetUserPreferencesAsync(Guid userId);
    Task UpdatePreferenceAsync(Guid preferenceId, bool emailEnabled, bool pushEnabled, bool smsEnabled, bool inAppEnabled);

    // Templates
    Task<Guid> CreateTemplateAsync(string templateKey, string subject, string bodyTemplate, string? smsTemplate);
    Task<NotificationTemplateDto?> GetTemplateAsync(string templateKey);
    Task UpdateTemplateAsync(Guid templateId, string subject, string bodyTemplate, string? smsTemplate);

    // Delivery Log
    Task<Guid> LogDeliveryAsync(Guid notificationId, string channel, string status, string? recipientAddress, string? errorMessage);
    Task<List<DeliveryLogDto>> GetDeliveryHistoryAsync(Guid notificationId);
    Task<List<DeliveryLogDto>> GetUserDeliveryHistoryAsync(Guid userId, int limit = 100);
}

public class NotificationDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ActionUrl { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class NotificationPreferenceDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string NotificationType { get; set; } = string.Empty;
    public bool EmailEnabled { get; set; }
    public bool PushEnabled { get; set; }
    public bool SmsEnabled { get; set; }
    public bool InAppEnabled { get; set; }
}

public class NotificationTemplateDto
{
    public Guid Id { get; set; }
    public string TemplateKey { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string BodyTemplate { get; set; } = string.Empty;
    public string? SmsTemplate { get; set; }
}

public class DeliveryLogDto
{
    public Guid Id { get; set; }
    public Guid NotificationId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? RecipientAddress { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime SentAt { get; set; }
}
