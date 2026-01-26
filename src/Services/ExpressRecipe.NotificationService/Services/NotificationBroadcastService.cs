using ExpressRecipe.NotificationService.Data;
using ExpressRecipe.NotificationService.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace ExpressRecipe.NotificationService.Services
{
    /// <summary>
    /// Service for broadcasting notifications via SignalR
    /// </summary>
    public class NotificationBroadcastService
    {
        private readonly IHubContext<NotificationHub, INotificationClient> _hubContext;
        private readonly ILogger<NotificationBroadcastService> _logger;

        public NotificationBroadcastService(
            IHubContext<NotificationHub, INotificationClient> hubContext,
            ILogger<NotificationBroadcastService> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        /// <summary>
        /// Convert NotificationDto to NotificationMessage for SignalR
        /// </summary>
        private static NotificationMessage ToNotificationMessage(NotificationDto dto)
        {
            return new NotificationMessage
            {
                Id = dto.Id,
                Type = dto.Type,
                Title = dto.Title,
                Message = dto.Message,
                Severity = "Info", // Default, could be derived from Type
                Data = dto.Metadata,
                CreatedAt = dto.CreatedAt,
                ActionUrl = dto.ActionUrl,
                ActionText = null,
                ImageUrl = null
            };
        }

        /// <summary>
        /// Broadcast a notification to a specific user
        /// </summary>
        public async Task BroadcastToUserAsync(Guid userId, NotificationDto notification)
        {
            try
            {
                NotificationMessage message = ToNotificationMessage(notification);
                await _hubContext.Clients
                    .Group($"user:{userId}")
                    .ReceiveNotification(message);

                _logger.LogInformation(
                    "Broadcasted notification {NotificationId} to user {UserId}",
                    notification.Id, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to broadcast notification {NotificationId} to user {UserId}",
                    notification.Id, userId);
            }
        }

        /// <summary>
        /// Broadcast unread count update to a specific user
        /// </summary>
        public async Task BroadcastUnreadCountAsync(Guid userId, int count)
        {
            try
            {
                await _hubContext.Clients
                    .Group($"user:{userId}")
                    .UnreadCountUpdated(count);

                _logger.LogDebug("Broadcasted unread count {Count} to user {UserId}", count, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to broadcast unread count to user {UserId}", userId);
            }
        }

        /// <summary>
        /// Broadcast multiple notifications to a user
        /// </summary>
        public async Task BroadcastMultipleToUserAsync(Guid userId, List<NotificationDto> notifications)
        {
            foreach (NotificationDto notification in notifications)
            {
                await BroadcastToUserAsync(userId, notification);
            }
        }

        /// <summary>
        /// Broadcast to all connected users (admin notifications)
        /// </summary>
        public async Task BroadcastToAllAsync(NotificationDto notification)
        {
            try
            {
                NotificationMessage message = ToNotificationMessage(notification);
                await _hubContext.Clients.All.ReceiveNotification(message);

                _logger.LogInformation(
                    "Broadcasted notification {NotificationId} to all users",
                    notification.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to broadcast notification {NotificationId} to all users",
                    notification.Id);
            }
        }
    }
}
