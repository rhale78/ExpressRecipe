using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace ExpressRecipe.NotificationService.Data;

public class NotificationRepository : INotificationRepository
{
    private readonly string _connectionString;
    private readonly ILogger<NotificationRepository> _logger;

    public NotificationRepository(string connectionString, ILogger<NotificationRepository> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<Guid> CreateNotificationAsync(Guid userId, string type, string title, string message, string? actionUrl, Dictionary<string, string>? metadata)
    {
        const string sql = @"
            INSERT INTO Notification (UserId, Type, Title, Message, ActionUrl, Metadata, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @Type, @Title, @Message, @ActionUrl, @Metadata, GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@Type", type);
        command.Parameters.AddWithValue("@Title", title);
        command.Parameters.AddWithValue("@Message", message);
        command.Parameters.AddWithValue("@ActionUrl", actionUrl ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Metadata", metadata != null ? JsonSerializer.Serialize(metadata) : DBNull.Value);

        return (Guid)await command.ExecuteScalarAsync()!;
    }

    public async Task<List<NotificationDto>> GetUserNotificationsAsync(Guid userId, bool unreadOnly = false, int limit = 50)
    {
        var sql = $@"
            SELECT TOP (@Limit) Id, UserId, Type, Title, Message, ActionUrl, Metadata, IsRead, ReadAt, CreatedAt
            FROM Notification
            WHERE UserId = @UserId {(unreadOnly ? "AND IsRead = 0" : "")}
            ORDER BY CreatedAt DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@Limit", limit);

        var notifications = new List<NotificationDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            notifications.Add(new NotificationDto
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                Type = reader.GetString(2),
                Title = reader.GetString(3),
                Message = reader.GetString(4),
                ActionUrl = reader.IsDBNull(5) ? null : reader.GetString(5),
                Metadata = reader.IsDBNull(6) ? new() : JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(6)) ?? new(),
                IsRead = reader.GetBoolean(7),
                ReadAt = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                CreatedAt = reader.GetDateTime(9)
            });
        }

        return notifications;
    }

    public async Task<NotificationDto?> GetNotificationAsync(Guid notificationId)
    {
        const string sql = @"
            SELECT Id, UserId, Type, Title, Message, ActionUrl, Metadata, IsRead, ReadAt, CreatedAt
            FROM Notification
            WHERE Id = @NotificationId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@NotificationId", notificationId);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new NotificationDto
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                Type = reader.GetString(2),
                Title = reader.GetString(3),
                Message = reader.GetString(4),
                ActionUrl = reader.IsDBNull(5) ? null : reader.GetString(5),
                Metadata = reader.IsDBNull(6) ? new() : JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(6)) ?? new(),
                IsRead = reader.GetBoolean(7),
                ReadAt = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                CreatedAt = reader.GetDateTime(9)
            };
        }

        return null;
    }

    public async Task MarkAsReadAsync(Guid notificationId)
    {
        const string sql = "UPDATE Notification SET IsRead = 1, ReadAt = GETUTCDATE() WHERE Id = @NotificationId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@NotificationId", notificationId);

        await command.ExecuteNonQueryAsync();
    }

    public async Task MarkAllAsReadAsync(Guid userId)
    {
        const string sql = "UPDATE Notification SET IsRead = 1, ReadAt = GETUTCDATE() WHERE UserId = @UserId AND IsRead = 0";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteNotificationAsync(Guid notificationId)
    {
        const string sql = "DELETE FROM Notification WHERE Id = @NotificationId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@NotificationId", notificationId);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<int> GetUnreadCountAsync(Guid userId)
    {
        const string sql = "SELECT COUNT(*) FROM Notification WHERE UserId = @UserId AND IsRead = 0";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        return (int)await command.ExecuteScalarAsync()!;
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

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@NotificationType", notificationType);
        command.Parameters.AddWithValue("@EmailEnabled", emailEnabled);
        command.Parameters.AddWithValue("@PushEnabled", pushEnabled);
        command.Parameters.AddWithValue("@SmsEnabled", smsEnabled);
        command.Parameters.AddWithValue("@InAppEnabled", inAppEnabled);

        return (Guid)await command.ExecuteScalarAsync()!;
    }

    public async Task<List<NotificationPreferenceDto>> GetUserPreferencesAsync(Guid userId)
    {
        const string sql = @"
            SELECT Id, UserId, NotificationType, EmailEnabled, PushEnabled, SmsEnabled, InAppEnabled
            FROM NotificationPreference
            WHERE UserId = @UserId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        var prefs = new List<NotificationPreferenceDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            prefs.Add(new NotificationPreferenceDto
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                NotificationType = reader.GetString(2),
                EmailEnabled = reader.GetBoolean(3),
                PushEnabled = reader.GetBoolean(4),
                SmsEnabled = reader.GetBoolean(5),
                InAppEnabled = reader.GetBoolean(6)
            });
        }

        return prefs;
    }

    public async Task UpdatePreferenceAsync(Guid preferenceId, bool emailEnabled, bool pushEnabled, bool smsEnabled, bool inAppEnabled)
    {
        const string sql = @"
            UPDATE NotificationPreference
            SET EmailEnabled = @EmailEnabled, PushEnabled = @PushEnabled, SmsEnabled = @SmsEnabled, InAppEnabled = @InAppEnabled
            WHERE Id = @PreferenceId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@PreferenceId", preferenceId);
        command.Parameters.AddWithValue("@EmailEnabled", emailEnabled);
        command.Parameters.AddWithValue("@PushEnabled", pushEnabled);
        command.Parameters.AddWithValue("@SmsEnabled", smsEnabled);
        command.Parameters.AddWithValue("@InAppEnabled", inAppEnabled);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<Guid> CreateTemplateAsync(string templateKey, string subject, string bodyTemplate, string? smsTemplate)
    {
        const string sql = @"
            INSERT INTO NotificationTemplate (TemplateKey, Subject, BodyTemplate, SmsTemplate, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@TemplateKey, @Subject, @BodyTemplate, @SmsTemplate, GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@TemplateKey", templateKey);
        command.Parameters.AddWithValue("@Subject", subject);
        command.Parameters.AddWithValue("@BodyTemplate", bodyTemplate);
        command.Parameters.AddWithValue("@SmsTemplate", smsTemplate ?? (object)DBNull.Value);

        return (Guid)await command.ExecuteScalarAsync()!;
    }

    public async Task<NotificationTemplateDto?> GetTemplateAsync(string templateKey)
    {
        const string sql = @"
            SELECT Id, TemplateKey, Subject, BodyTemplate, SmsTemplate
            FROM NotificationTemplate
            WHERE TemplateKey = @TemplateKey";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@TemplateKey", templateKey);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new NotificationTemplateDto
            {
                Id = reader.GetGuid(0),
                TemplateKey = reader.GetString(1),
                Subject = reader.GetString(2),
                BodyTemplate = reader.GetString(3),
                SmsTemplate = reader.IsDBNull(4) ? null : reader.GetString(4)
            };
        }

        return null;
    }

    public async Task UpdateTemplateAsync(Guid templateId, string subject, string bodyTemplate, string? smsTemplate)
    {
        const string sql = @"
            UPDATE NotificationTemplate
            SET Subject = @Subject, BodyTemplate = @BodyTemplate, SmsTemplate = @SmsTemplate, UpdatedAt = GETUTCDATE()
            WHERE Id = @TemplateId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@TemplateId", templateId);
        command.Parameters.AddWithValue("@Subject", subject);
        command.Parameters.AddWithValue("@BodyTemplate", bodyTemplate);
        command.Parameters.AddWithValue("@SmsTemplate", smsTemplate ?? (object)DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<Guid> LogDeliveryAsync(Guid notificationId, string channel, string status, string? recipientAddress, string? errorMessage)
    {
        const string sql = @"
            INSERT INTO DeliveryLog (NotificationId, Channel, Status, RecipientAddress, ErrorMessage, SentAt)
            OUTPUT INSERTED.Id
            VALUES (@NotificationId, @Channel, @Status, @RecipientAddress, @ErrorMessage, GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@NotificationId", notificationId);
        command.Parameters.AddWithValue("@Channel", channel);
        command.Parameters.AddWithValue("@Status", status);
        command.Parameters.AddWithValue("@RecipientAddress", recipientAddress ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@ErrorMessage", errorMessage ?? (object)DBNull.Value);

        return (Guid)await command.ExecuteScalarAsync()!;
    }

    public async Task<List<DeliveryLogDto>> GetDeliveryHistoryAsync(Guid notificationId)
    {
        const string sql = @"
            SELECT Id, NotificationId, Channel, Status, RecipientAddress, ErrorMessage, SentAt
            FROM DeliveryLog
            WHERE NotificationId = @NotificationId
            ORDER BY SentAt DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@NotificationId", notificationId);

        var logs = new List<DeliveryLogDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            logs.Add(new DeliveryLogDto
            {
                Id = reader.GetGuid(0),
                NotificationId = reader.GetGuid(1),
                Channel = reader.GetString(2),
                Status = reader.GetString(3),
                RecipientAddress = reader.IsDBNull(4) ? null : reader.GetString(4),
                ErrorMessage = reader.IsDBNull(5) ? null : reader.GetString(5),
                SentAt = reader.GetDateTime(6)
            });
        }

        return logs;
    }

    public async Task<List<DeliveryLogDto>> GetUserDeliveryHistoryAsync(Guid userId, int limit = 100)
    {
        const string sql = @"
            SELECT TOP (@Limit) dl.Id, dl.NotificationId, dl.Channel, dl.Status, dl.RecipientAddress, dl.ErrorMessage, dl.SentAt
            FROM DeliveryLog dl
            INNER JOIN Notification n ON dl.NotificationId = n.Id
            WHERE n.UserId = @UserId
            ORDER BY dl.SentAt DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@Limit", limit);

        var logs = new List<DeliveryLogDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            logs.Add(new DeliveryLogDto
            {
                Id = reader.GetGuid(0),
                NotificationId = reader.GetGuid(1),
                Channel = reader.GetString(2),
                Status = reader.GetString(3),
                RecipientAddress = reader.IsDBNull(4) ? null : reader.GetString(4),
                ErrorMessage = reader.IsDBNull(5) ? null : reader.GetString(5),
                SentAt = reader.GetDateTime(6)
            });
        }

        return logs;
    }
}
