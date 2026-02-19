using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using PreConHub.Models.Entities;

namespace PreConHub.Hubs
{
    /// <summary>
    /// SignalR Hub for real-time notifications
    /// Allows pushing notifications to connected users instantly
    /// </summary>
    [Authorize]
    public class NotificationHub : Hub
    {
        private readonly ILogger<NotificationHub> _logger;

        public NotificationHub(ILogger<NotificationHub> logger)
        {
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.UserIdentifier;
            if (!string.IsNullOrEmpty(userId))
            {
                // Add user to their personal group for targeted notifications
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
                _logger.LogDebug("User {UserId} connected to notification hub", userId);
            }
            
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.UserIdentifier;
            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user-{userId}");
                _logger.LogDebug("User {UserId} disconnected from notification hub", userId);
            }
            
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Called by client to acknowledge receipt of notification
        /// </summary>
        public async Task AcknowledgeNotification(int notificationId)
        {
            var userId = Context.UserIdentifier;
            _logger.LogDebug("User {UserId} acknowledged notification {NotificationId}", userId, notificationId);
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Service for sending real-time notifications via SignalR
    /// Inject this into NotificationService for real-time delivery
    /// </summary>
    public interface INotificationHubService
    {
        Task SendNotificationAsync(string userId, Notification notification);
        Task SendNotificationCountUpdateAsync(string userId, int count);
        Task BroadcastToRoleAsync(string role, string title, string message);
    }

    public class NotificationHubService : INotificationHubService
    {
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<NotificationHubService> _logger;

        public NotificationHubService(
            IHubContext<NotificationHub> hubContext,
            ILogger<NotificationHubService> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task SendNotificationAsync(string userId, Notification notification)
        {
            try
            {
                await _hubContext.Clients.Group($"user-{userId}").SendAsync("ReceiveNotification", new
                {
                    notification.Id,
                    notification.Title,
                    notification.Message,
                    Type = notification.Type.ToString(),
                    TypeIcon = GetTypeIcon(notification.Type),
                    TypeColor = GetTypeColor(notification.Type),
                    Priority = notification.Priority.ToString(),
                    notification.ActionUrl,
                    notification.ActionText,
                    CreatedAt = notification.CreatedAt,
                    TimeAgo = "Just now"
                });
                
                _logger.LogDebug("Sent real-time notification to user {UserId}: {Title}", userId, notification.Title);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send real-time notification to user {UserId}", userId);
            }
        }

        public async Task SendNotificationCountUpdateAsync(string userId, int count)
        {
            try
            {
                await _hubContext.Clients.Group($"user-{userId}").SendAsync("UpdateNotificationCount", count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notification count update to user {UserId}", userId);
            }
        }

        public async Task BroadcastToRoleAsync(string role, string title, string message)
        {
            try
            {
                await _hubContext.Clients.Group($"role-{role}").SendAsync("ReceiveBroadcast", new
                {
                    Title = title,
                    Message = message,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to broadcast to role {Role}", role);
            }
        }

        private static string GetTypeIcon(NotificationType type)
        {
            return type switch
            {
                NotificationType.Info => "bi-info-circle-fill",
                NotificationType.Success => "bi-check-circle-fill",
                NotificationType.Warning => "bi-exclamation-triangle-fill",
                NotificationType.Alert => "bi-exclamation-circle-fill",
                NotificationType.Mortgage => "bi-bank",
                NotificationType.Document => "bi-file-earmark-text-fill",
                NotificationType.Closing => "bi-calendar-check-fill",
                NotificationType.Deposit => "bi-cash-stack",
                NotificationType.Lawyer => "bi-briefcase-fill",
                NotificationType.Purchaser => "bi-person-fill",
                NotificationType.System => "bi-gear-fill",
                _ => "bi-bell-fill"
            };
        }

        private static string GetTypeColor(NotificationType type)
        {
            return type switch
            {
                NotificationType.Info => "primary",
                NotificationType.Success => "success",
                NotificationType.Warning => "warning",
                NotificationType.Alert => "danger",
                NotificationType.Mortgage => "purple",
                NotificationType.Document => "secondary",
                NotificationType.Closing => "orange",
                NotificationType.Deposit => "success",
                NotificationType.Lawyer => "info",
                NotificationType.Purchaser => "teal",
                NotificationType.System => "secondary",
                _ => "primary"
            };
        }
    }
}
