using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PreConHub.Models.Entities;
using PreConHub.Services;

namespace PreConHub.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class NotificationsApiController : ControllerBase
    {
        private readonly INotificationService _notificationService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<NotificationsApiController> _logger;

        public NotificationsApiController(
            INotificationService notificationService,
            UserManager<ApplicationUser> userManager,
            ILogger<NotificationsApiController> logger)
        {
            _notificationService = notificationService;
            _userManager = userManager;
            _logger = logger;
        }

        /// <summary>
        /// Get current user's notifications
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetNotifications([FromQuery] int count = 20, [FromQuery] bool unreadOnly = false)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var notifications = await _notificationService.GetUserNotificationsAsync(userId, count, unreadOnly);
            var unreadCount = await _notificationService.GetUnreadCountAsync(userId);

            return Ok(new
            {
                notifications = notifications.Select(n => new
                {
                    n.Id,
                    n.Title,
                    n.Message,
                    type = n.Type.ToString(),
                    typeIcon = GetTypeIcon(n.Type),
                    typeColor = GetTypeColor(n.Type),
                    priority = n.Priority.ToString(),
                    n.IsRead,
                    n.ActionUrl,
                    n.ActionText,
                    createdAt = n.CreatedAt,
                    timeAgo = GetTimeAgo(n.CreatedAt),
                    n.ProjectId,
                    n.UnitId
                }),
                unreadCount
            });
        }

        /// <summary>
        /// Get unread count only (for badge updates)
        /// </summary>
        [HttpGet("count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var count = await _notificationService.GetUnreadCountAsync(userId);
            return Ok(new { count });
        }

        /// <summary>
        /// Mark single notification as read
        /// </summary>
        [HttpPost("{id}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            await _notificationService.MarkAsReadAsync(id, userId);
            var unreadCount = await _notificationService.GetUnreadCountAsync(userId);
            
            return Ok(new { success = true, unreadCount });
        }

        /// <summary>
        /// Mark all notifications as read
        /// </summary>
        [HttpPost("read-all")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            await _notificationService.MarkAllAsReadAsync(userId);
            return Ok(new { success = true, unreadCount = 0 });
        }

        /// <summary>
        /// Delete a notification
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            await _notificationService.DeleteAsync(id, userId);
            var unreadCount = await _notificationService.GetUnreadCountAsync(userId);
            
            return Ok(new { success = true, unreadCount });
        }

        #region Helper Methods

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

        private static string GetTimeAgo(DateTime dateTime)
        {
            var timeSpan = DateTime.UtcNow - dateTime;

            if (timeSpan.TotalMinutes < 1)
                return "Just now";
            if (timeSpan.TotalMinutes < 60)
                return $"{(int)timeSpan.TotalMinutes}m ago";
            if (timeSpan.TotalHours < 24)
                return $"{(int)timeSpan.TotalHours}h ago";
            if (timeSpan.TotalDays < 7)
                return $"{(int)timeSpan.TotalDays}d ago";
            if (timeSpan.TotalDays < 30)
                return $"{(int)(timeSpan.TotalDays / 7)}w ago";
            return dateTime.ToString("MMM dd");
        }

        #endregion
    }

    /// <summary>
    /// MVC Controller for notification pages
    /// </summary>
    [Authorize]
    public class NotificationsController : Controller
    {
        private readonly INotificationService _notificationService;
        private readonly UserManager<ApplicationUser> _userManager;

        public NotificationsController(
            INotificationService notificationService,
            UserManager<ApplicationUser> userManager)
        {
            _notificationService = notificationService;
            _userManager = userManager;
        }

        /// <summary>
        /// Full notifications page
        /// </summary>
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Account");

            var notifications = await _notificationService.GetUserNotificationsAsync(userId, 100, false);
            return View(notifications);
        }

        /// <summary>
        /// Mark and redirect to action URL
        /// </summary>
        [HttpGet("Notifications/Open/{id}")]
        public async Task<IActionResult> Open(int id, string? returnUrl = null)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Account");

            // Mark as read
            await _notificationService.MarkAsReadAsync(id, userId);

            // Redirect to action URL or return URL
            if (!string.IsNullOrEmpty(returnUrl))
                return LocalRedirect(returnUrl);

            return RedirectToAction("Index");
        }
    }
}
