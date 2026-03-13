using ExpressRecipe.Data.Common;
using ExpressRecipe.NotificationService.Hubs;
using ExpressRecipe.NotificationService.Services;
using ExpressRecipe.Shared.Services;
using System.Text.Json;

namespace ExpressRecipe.NotificationService.Data;

public class NotificationRepository : SqlHelper, INotificationRepository
{
    private readonly ILogger<NotificationRepository> _logger;
    private readonly NotificationBroadcastService? _broadcastService;
    private readonly HybridCacheService? _cache;

    // Read notifications are immutable — cache them for 30 minutes.
    private static readonly TimeSpan ReadNotificationCacheTtl = TimeSpan.FromMinutes(30);

    public NotificationRepository(
        string connectionString,
        ILogger<NotificationRepository> logger,
        NotificationBroadcastService? broadcastService = null,
        HybridCacheService? cache = null) : base(connectionString)
    {
        _logger = logger;
        _broadcastService = broadcastService;
        _cache = cache;
    }

    public async Task<Guid> CreateNotificationAsync(Guid userId, string type, string title, string message, string? actionUrl, Dictionary<string, string>? metadata)
    {
        const string sql = @"
            INSERT INTO Notification (UserId, Type, Title, Message, ActionUrl, Metadata, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @Type, @Title, @Message, @ActionUrl, @Metadata, GETUTCDATE())";

        var notificationId = (await ExecuteScalarAsync<Guid>(sql,
            CreateParameter("@UserId", userId),
            CreateParameter("@Type", type),
            CreateParameter("@Title", title),
            CreateParameter("@Message", message),
            CreateParameter("@ActionUrl", (object?)actionUrl ?? DBNull.Value),
            CreateParameter("@Metadata", metadata != null ? (object)JsonSerializer.Serialize(metadata) : DBNull.Value)))!;

        // Broadcast notification in real-time via SignalR
        if (_broadcastService != null)
        {
            var notificationDto = new NotificationDto
            {
                Id = notificationId,
                UserId = userId,
                Type = type,
                Title = title,
                Message = message,
                ActionUrl = actionUrl,
                Metadata = metadata ?? new(),
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            await _broadcastService.BroadcastToUserAsync(userId, notificationDto);
        }

        return notificationId;
    }

    public async Task<List<NotificationDto>> GetUserNotificationsAsync(Guid userId, bool unreadOnly = false, int limit = 50)
    {
        var sql = $@"
            SELECT TOP (@Limit) Id, UserId, Type, Title, Message, ActionUrl, Metadata, IsRead, ReadAt, CreatedAt
            FROM Notification
            WHERE UserId = @UserId AND IsDeleted = 0 {(unreadOnly ? "AND IsRead = 0" : "")}
            ORDER BY CreatedAt DESC";

        return await ExecuteReaderAsync<NotificationDto>(sql, reader => new NotificationDto
        {
            Id = GetGuid(reader, "Id"),
            UserId = GetGuid(reader, "UserId"),
            Type = GetString(reader, "Type")!,
            Title = GetString(reader, "Title")!,
            Message = GetString(reader, "Message")!,
            ActionUrl = GetString(reader, "ActionUrl"),
            Metadata = GetString(reader, "Metadata") is string m ? JsonSerializer.Deserialize<Dictionary<string, string>>(m) ?? new() : new(),
            IsRead = GetBoolean(reader, "IsRead"),
            ReadAt = GetNullableDateTime(reader, "ReadAt"),
            CreatedAt = GetDateTime(reader, "CreatedAt")
        },
        CreateParameter("@UserId", userId),
        CreateParameter("@Limit", limit));
    }

    public async Task<NotificationDto?> GetNotificationAsync(Guid notificationId)
    {
        // Serve already-read notifications from cache — they no longer change.
        var cacheKey = $"notification:{notificationId}";
        if (_cache is not null)
        {
            var cached = await _cache.GetAsync<NotificationDto>(cacheKey);
            if (cached is not null)
                return cached;
        }

        const string sql = @"
            SELECT Id, UserId, Type, Title, Message, ActionUrl, Metadata, IsRead, ReadAt, CreatedAt
            FROM Notification
            WHERE Id = @NotificationId AND IsDeleted = 0";

        var results = await ExecuteReaderAsync<NotificationDto>(sql, reader => new NotificationDto
        {
            Id = GetGuid(reader, "Id"),
            UserId = GetGuid(reader, "UserId"),
            Type = GetString(reader, "Type")!,
            Title = GetString(reader, "Title")!,
            Message = GetString(reader, "Message")!,
            ActionUrl = GetString(reader, "ActionUrl"),
            Metadata = GetString(reader, "Metadata") is string m ? JsonSerializer.Deserialize<Dictionary<string, string>>(m) ?? new() : new(),
            IsRead = GetBoolean(reader, "IsRead"),
            ReadAt = GetNullableDateTime(reader, "ReadAt"),
            CreatedAt = GetDateTime(reader, "CreatedAt")
        },
        CreateParameter("@NotificationId", notificationId));

        var dto = results.FirstOrDefault();

        // Cache read notifications — they are effectively immutable once read.
        if (dto is not null && dto.IsRead && _cache is not null)
            await _cache.SetAsync(cacheKey, dto, ReadNotificationCacheTtl);

        return dto;
    }

    public async Task MarkAsReadAsync(Guid notificationId)
    {
        const string getUserSql = "SELECT UserId FROM Notification WHERE Id = @NotificationId";
        var userId = await ExecuteScalarAsync<Guid?>(getUserSql, CreateParameter("@NotificationId", notificationId));

        const string sql = "UPDATE Notification SET IsRead = 1, ReadAt = GETUTCDATE() WHERE Id = @NotificationId";
        await ExecuteNonQueryAsync(sql, CreateParameter("@NotificationId", notificationId));

        // Evict stale cache so the next GetNotificationAsync call re-caches with IsRead=true.
        if (_cache is not null)
            _ = _cache.RemoveAsync($"notification:{notificationId}");

        // Broadcast updated unread count
        if (userId.HasValue && _broadcastService != null)
        {
            var unreadCount = await GetUnreadCountAsync(userId.Value);
            await _broadcastService.BroadcastUnreadCountAsync(userId.Value, unreadCount);
        }
    }

    public async Task MarkAsUnreadAsync(Guid notificationId)
    {
        const string getUserSql = "SELECT UserId FROM Notification WHERE Id = @NotificationId";
        var userId = await ExecuteScalarAsync<Guid?>(getUserSql, CreateParameter("@NotificationId", notificationId));

        const string sql = "UPDATE Notification SET IsRead = 0, ReadAt = NULL WHERE Id = @NotificationId";
        await ExecuteNonQueryAsync(sql, CreateParameter("@NotificationId", notificationId));

        // Evict cache since the notification is no longer in a read (cacheable) state.
        if (_cache is not null)
            _ = _cache.RemoveAsync($"notification:{notificationId}");

        // Broadcast updated unread count
        if (userId.HasValue && _broadcastService != null)
        {
            var unreadCount = await GetUnreadCountAsync(userId.Value);
            await _broadcastService.BroadcastUnreadCountAsync(userId.Value, unreadCount);
        }
    }

    public async Task MarkAllAsReadAsync(Guid userId)
    {
        const string sql = "UPDATE Notification SET IsRead = 1, ReadAt = GETUTCDATE() WHERE UserId = @UserId AND IsRead = 0 AND IsDeleted = 0";
        await ExecuteNonQueryAsync(sql, CreateParameter("@UserId", userId));

        // Broadcast updated unread count (should be 0)
        if (_broadcastService != null)
        {
            await _broadcastService.BroadcastUnreadCountAsync(userId, 0);
        }
    }

    public async Task DeleteNotificationAsync(Guid notificationId)
    {
        // Soft delete — use HardDeleteNotificationAsync for GDPR erasure
        const string sql = "UPDATE Notification SET IsDeleted = 1, DeletedAt = GETUTCDATE() WHERE Id = @NotificationId";
        await ExecuteNonQueryAsync(sql, CreateParameter("@NotificationId", notificationId));

        if (_cache is not null)
            _ = _cache.RemoveAsync($"notification:{notificationId}");
    }

    public async Task HardDeleteNotificationAsync(Guid notificationId)
    {
        // Permanent deletion — for GDPR right-to-erasure requests only
        const string sql = "DELETE FROM Notification WHERE Id = @NotificationId";
        await ExecuteNonQueryAsync(sql, CreateParameter("@NotificationId", notificationId));

        if (_cache is not null)
            _ = _cache.RemoveAsync($"notification:{notificationId}");
    }

    public async Task DeleteAllReadAsync(Guid userId)
    {
        // Soft delete all read notifications
        const string sql = "UPDATE Notification SET IsDeleted = 1, DeletedAt = GETUTCDATE() WHERE UserId = @UserId AND IsRead = 1 AND IsDeleted = 0";
        await ExecuteNonQueryAsync(sql, CreateParameter("@UserId", userId));
    }

    public async Task HardDeleteAllUserNotificationsAsync(Guid userId)
    {
        // Permanent deletion of ALL user notification data — for GDPR account deletion
        const string sql = "DELETE FROM Notification WHERE UserId = @UserId";
        await ExecuteNonQueryAsync(sql, CreateParameter("@UserId", userId));
    }

    public async Task<int> GetUnreadCountAsync(Guid userId)
    {
        const string sql = "SELECT COUNT(*) FROM Notification WHERE UserId = @UserId AND IsRead = 0 AND IsDeleted = 0";
        return (await ExecuteScalarAsync<int>(sql, CreateParameter("@UserId", userId)))!;
    }

    public async Task<Guid> SavePreferenceAsync(Guid userId, string notificationType, bool emailEnabled, bool pushEnabled, bool smsEnabled, bool inAppEnabled)
    {
        const string sql = @"
            MERGE NotificationPreference AS target
            USING (SELECT @UserId AS UserId, @NotificationType AS NotificationType) AS source
            ON target.UserId = source.UserId AND target.NotificationType = source.NotificationType
            WHEN MATCHED THEN
                UPDATE SET EmailEnabled = @EmailEnabled, PushEnabled = @PushEnabled, SmsEnabled = @SmsEnabled, InAppEnabled = @InAppEnabled
            WHEN NOT MATCHED THEN
                INSERT (UserId, NotificationType, EmailEnabled, PushEnabled, SmsEnabled, InAppEnabled)
                VALUES (@UserId, @NotificationType, @EmailEnabled, @PushEnabled, @SmsEnabled, @InAppEnabled)
            OUTPUT INSERTED.Id;";

        return (await ExecuteScalarAsync<Guid>(sql,
            CreateParameter("@UserId", userId),
            CreateParameter("@NotificationType", notificationType),
            CreateParameter("@EmailEnabled", emailEnabled),
            CreateParameter("@PushEnabled", pushEnabled),
            CreateParameter("@SmsEnabled", smsEnabled),
            CreateParameter("@InAppEnabled", inAppEnabled)))!;
    }

    public async Task<List<NotificationPreferenceDto>> GetUserPreferencesAsync(Guid userId)
    {
        const string sql = @"
            SELECT Id, UserId, NotificationType, EmailEnabled, PushEnabled, SmsEnabled, InAppEnabled
            FROM NotificationPreference
            WHERE UserId = @UserId";

        return await ExecuteReaderAsync<NotificationPreferenceDto>(sql, reader => new NotificationPreferenceDto
        {
            Id = GetGuid(reader, "Id"),
            UserId = GetGuid(reader, "UserId"),
            NotificationType = GetString(reader, "NotificationType")!,
            EmailEnabled = GetBoolean(reader, "EmailEnabled"),
            PushEnabled = GetBoolean(reader, "PushEnabled"),
            SmsEnabled = GetBoolean(reader, "SmsEnabled"),
            InAppEnabled = GetBoolean(reader, "InAppEnabled")
        },
        CreateParameter("@UserId", userId));
    }

    public async Task UpdatePreferenceAsync(Guid preferenceId, bool emailEnabled, bool pushEnabled, bool smsEnabled, bool inAppEnabled)
    {
        const string sql = @"
            UPDATE NotificationPreference
            SET EmailEnabled = @EmailEnabled, PushEnabled = @PushEnabled, SmsEnabled = @SmsEnabled, InAppEnabled = @InAppEnabled
            WHERE Id = @PreferenceId";

        await ExecuteNonQueryAsync(sql,
            CreateParameter("@PreferenceId", preferenceId),
            CreateParameter("@EmailEnabled", emailEnabled),
            CreateParameter("@PushEnabled", pushEnabled),
            CreateParameter("@SmsEnabled", smsEnabled),
            CreateParameter("@InAppEnabled", inAppEnabled));
    }

    public async Task<Guid> CreateTemplateAsync(string templateKey, string subject, string bodyTemplate, string? smsTemplate)
    {
        const string sql = @"
            INSERT INTO NotificationTemplate (TemplateKey, Subject, BodyTemplate, SmsTemplate, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@TemplateKey, @Subject, @BodyTemplate, @SmsTemplate, GETUTCDATE())";

        return (await ExecuteScalarAsync<Guid>(sql,
            CreateParameter("@TemplateKey", templateKey),
            CreateParameter("@Subject", subject),
            CreateParameter("@BodyTemplate", bodyTemplate),
            CreateParameter("@SmsTemplate", (object?)smsTemplate ?? DBNull.Value)))!;
    }

    public async Task<NotificationTemplateDto?> GetTemplateAsync(string templateKey)
    {
        const string sql = @"
            SELECT Id, TemplateKey, Subject, BodyTemplate, SmsTemplate
            FROM NotificationTemplate
            WHERE TemplateKey = @TemplateKey";

        var results = await ExecuteReaderAsync<NotificationTemplateDto>(sql, reader => new NotificationTemplateDto
        {
            Id = GetGuid(reader, "Id"),
            TemplateKey = GetString(reader, "TemplateKey")!,
            Subject = GetString(reader, "Subject")!,
            BodyTemplate = GetString(reader, "BodyTemplate")!,
            SmsTemplate = GetString(reader, "SmsTemplate")
        },
        CreateParameter("@TemplateKey", templateKey));

        return results.FirstOrDefault();
    }

    public async Task UpdateTemplateAsync(Guid templateId, string subject, string bodyTemplate, string? smsTemplate)
    {
        const string sql = @"
            UPDATE NotificationTemplate
            SET Subject = @Subject, BodyTemplate = @BodyTemplate, SmsTemplate = @SmsTemplate, UpdatedAt = GETUTCDATE()
            WHERE Id = @TemplateId";

        await ExecuteNonQueryAsync(sql,
            CreateParameter("@TemplateId", templateId),
            CreateParameter("@Subject", subject),
            CreateParameter("@BodyTemplate", bodyTemplate),
            CreateParameter("@SmsTemplate", (object?)smsTemplate ?? DBNull.Value));
    }

    public async Task<Guid> LogDeliveryAsync(Guid notificationId, string channel, string status, string? recipientAddress, string? errorMessage)
    {
        const string sql = @"
            INSERT INTO DeliveryLog (NotificationId, Channel, Status, RecipientAddress, ErrorMessage, SentAt)
            OUTPUT INSERTED.Id
            VALUES (@NotificationId, @Channel, @Status, @RecipientAddress, @ErrorMessage, GETUTCDATE())";

        return (await ExecuteScalarAsync<Guid>(sql,
            CreateParameter("@NotificationId", notificationId),
            CreateParameter("@Channel", channel),
            CreateParameter("@Status", status),
            CreateParameter("@RecipientAddress", (object?)recipientAddress ?? DBNull.Value),
            CreateParameter("@ErrorMessage", (object?)errorMessage ?? DBNull.Value)))!;
    }

    public async Task<List<DeliveryLogDto>> GetDeliveryHistoryAsync(Guid notificationId)
    {
        const string sql = @"
            SELECT Id, NotificationId, Channel, Status, RecipientAddress, ErrorMessage, SentAt
            FROM DeliveryLog
            WHERE NotificationId = @NotificationId
            ORDER BY SentAt DESC";

        return await ExecuteReaderAsync<DeliveryLogDto>(sql, reader => new DeliveryLogDto
        {
            Id = GetGuid(reader, "Id"),
            NotificationId = GetGuid(reader, "NotificationId"),
            Channel = GetString(reader, "Channel")!,
            Status = GetString(reader, "Status")!,
            RecipientAddress = GetString(reader, "RecipientAddress"),
            ErrorMessage = GetString(reader, "ErrorMessage"),
            SentAt = GetDateTime(reader, "SentAt")
        },
        CreateParameter("@NotificationId", notificationId));
    }

    public async Task<List<DeliveryLogDto>> GetUserDeliveryHistoryAsync(Guid userId, int limit = 100)
    {
        const string sql = @"
            SELECT TOP (@Limit) dl.Id, dl.NotificationId, dl.Channel, dl.Status, dl.RecipientAddress, dl.ErrorMessage, dl.SentAt
            FROM DeliveryLog dl
            INNER JOIN Notification n ON dl.NotificationId = n.Id
            WHERE n.UserId = @UserId
            ORDER BY dl.SentAt DESC";

        return await ExecuteReaderAsync<DeliveryLogDto>(sql, reader => new DeliveryLogDto
        {
            Id = GetGuid(reader, "Id"),
            NotificationId = GetGuid(reader, "NotificationId"),
            Channel = GetString(reader, "Channel")!,
            Status = GetString(reader, "Status")!,
            RecipientAddress = GetString(reader, "RecipientAddress"),
            ErrorMessage = GetString(reader, "ErrorMessage"),
            SentAt = GetDateTime(reader, "SentAt")
        },
        CreateParameter("@UserId", userId),
        CreateParameter("@Limit", limit));
    }
}
