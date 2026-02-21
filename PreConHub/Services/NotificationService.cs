using Microsoft.EntityFrameworkCore;
using PreConHub.Data;
using PreConHub.Models.Entities;

namespace PreConHub.Services
{
    public interface INotificationService
    {
        // Create notifications
        Task<Notification> CreateAsync(string userId, string title, string message, NotificationType type, 
            NotificationPriority priority = NotificationPriority.Normal, string? actionUrl = null, 
            string? actionText = null, int? projectId = null, int? unitId = null, string? groupKey = null);
        
        // Bulk create for multiple users
        Task CreateForMultipleUsersAsync(IEnumerable<string> userIds, string title, string message, 
            NotificationType type, NotificationPriority priority = NotificationPriority.Normal, 
            string? actionUrl = null, int? projectId = null);
        
        // Specific notification types
        Task NotifyMortgageInfoSubmittedAsync(int unitId, string purchaserName);
        Task NotifyLawyerApprovedAsync(int unitId, string lawyerName, string builderId);
        Task NotifyLawyerRequestedRevisionAsync(int unitId, string lawyerName, string builderId, string notes);
        Task NotifyClosingDateApproachingAsync(int unitId, int daysRemaining);
        Task NotifyDepositDueAsync(int depositId, int daysRemaining);
        Task NotifyDepositReceivedAsync(int depositId, string purchaserName, decimal amount);
        Task NotifyPurchaserAddedAsync(int unitId, string purchaserName, string builderId);
        Task NotifyDocumentUploadedAsync(int unitId, string documentName, string uploadedBy);
        Task NotifyExtensionRequestSubmittedAsync(int unitId, string purchaserName);
        Task NotifyExtensionApprovedAsync(int unitId, string purchaserName, string purchaserId);
        Task NotifyExtensionRejectedAsync(int unitId, string purchaserName, string purchaserId);
        Task NotifySOAVersionCreatedAsync(int unitId, string createdByName, string source);
        Task NotifyMarketingAgencySuggestionAsync(int projectId, string agencyUserName, string builderId);

        // Get notifications
        Task<List<Notification>> GetUserNotificationsAsync(string userId, int count = 20, bool unreadOnly = false);
        Task<int> GetUnreadCountAsync(string userId);
        
        // Mark as read
        Task MarkAsReadAsync(int notificationId, string userId);
        Task MarkAllAsReadAsync(string userId);
        
        // Delete/cleanup
        Task DeleteAsync(int notificationId, string userId);
        Task DeleteOldNotificationsAsync(int daysOld = 90);
        
        // Background job triggers
        Task CheckClosingDateRemindersAsync();
        Task CheckDepositDueRemindersAsync();
    }

    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(ApplicationDbContext context, ILogger<NotificationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        #region Create Notifications

        public async Task<Notification> CreateAsync(string userId, string title, string message, 
            NotificationType type, NotificationPriority priority = NotificationPriority.Normal, 
            string? actionUrl = null, string? actionText = null, int? projectId = null, 
            int? unitId = null, string? groupKey = null)
        {
            var notification = new Notification
            {
                UserId = userId,
                Title = title,
                Message = message,
                Type = type,
                Priority = priority,
                ActionUrl = actionUrl,
                ActionText = actionText ?? "View",
                ProjectId = projectId,
                UnitId = unitId,
                GroupKey = groupKey,
                CreatedAt = DateTime.UtcNow
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Notification created for user {UserId}: {Title}", userId, title);
            return notification;
        }

        public async Task CreateForMultipleUsersAsync(IEnumerable<string> userIds, string title, 
            string message, NotificationType type, NotificationPriority priority = NotificationPriority.Normal, 
            string? actionUrl = null, int? projectId = null)
        {
            var notifications = userIds.Select(userId => new Notification
            {
                UserId = userId,
                Title = title,
                Message = message,
                Type = type,
                Priority = priority,
                ActionUrl = actionUrl,
                ProjectId = projectId,
                CreatedAt = DateTime.UtcNow
            }).ToList();

            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Created {Count} notifications for: {Title}", notifications.Count, title);
        }

        #endregion

        #region Specific Notification Types

        public async Task NotifyMortgageInfoSubmittedAsync(int unitId, string purchaserName)
        {
            var unit = await _context.Units
                .Include(u => u.Project)
                .FirstOrDefaultAsync(u => u.Id == unitId);

            if (unit?.Project == null) return;

            await CreateAsync(
                userId: unit.Project.BuilderId,
                title: "Mortgage Info Submitted",
                message: $"{purchaserName} has submitted mortgage information for Unit {unit.UnitNumber}",
                type: NotificationType.Mortgage,
                priority: NotificationPriority.High,
                actionUrl: $"/Units/Details/{unitId}",
                actionText: "View Unit",
                projectId: unit.ProjectId,
                unitId: unitId,
                groupKey: $"mortgage-{unitId}"
            );
        }

        public async Task NotifyLawyerApprovedAsync(int unitId, string lawyerName, string builderId)
        {
            var unit = await _context.Units.Include(u => u.Project).FirstOrDefaultAsync(u => u.Id == unitId);
            if (unit == null) return;

            await CreateAsync(
                userId: builderId,
                title: "SOA Approved by Lawyer",
                message: $"{lawyerName} has approved the Statement of Adjustments for Unit {unit.UnitNumber}",
                type: NotificationType.Lawyer,
                priority: NotificationPriority.Normal,
                actionUrl: $"/Units/Details/{unitId}",
                actionText: "View SOA",
                projectId: unit.ProjectId,
                unitId: unitId,
                groupKey: $"lawyer-approval-{unitId}"
            );
        }

        public async Task NotifyLawyerRequestedRevisionAsync(int unitId, string lawyerName, string builderId, string notes)
        {
            var unit = await _context.Units.Include(u => u.Project).FirstOrDefaultAsync(u => u.Id == unitId);
            if (unit == null) return;

            var truncatedNotes = notes.Length > 100 ? notes.Substring(0, 100) + "..." : notes;

            await CreateAsync(
                userId: builderId,
                title: "SOA Revision Requested",
                message: $"{lawyerName} has requested revisions for Unit {unit.UnitNumber}: {truncatedNotes}",
                type: NotificationType.Warning,
                priority: NotificationPriority.High,
                actionUrl: $"/Units/Details/{unitId}",
                actionText: "Review",
                projectId: unit.ProjectId,
                unitId: unitId,
                groupKey: $"lawyer-revision-{unitId}"
            );
        }

        public async Task NotifyClosingDateApproachingAsync(int unitId, int daysRemaining)
        {
            var unit = await _context.Units
                .Include(u => u.Project)
                .Include(u => u.Purchasers).ThenInclude(p => p.Purchaser)
                .FirstOrDefaultAsync(u => u.Id == unitId);

            if (unit?.Project == null) return;

            var urgencyLevel = daysRemaining switch
            {
                <= 7 => NotificationPriority.Urgent,
                <= 14 => NotificationPriority.High,
                _ => NotificationPriority.Normal
            };

            var type = daysRemaining <= 7 ? NotificationType.Alert : NotificationType.Closing;

            // Notify Builder
            await CreateAsync(
                userId: unit.Project.BuilderId,
                title: daysRemaining <= 7 ? "⚠️ Closing in " + daysRemaining + " Days" : "Closing Date Approaching",
                message: $"Unit {unit.UnitNumber} is scheduled to close in {daysRemaining} days",
                type: type,
                priority: urgencyLevel,
                actionUrl: $"/Units/Details/{unitId}",
                actionText: "View Unit",
                projectId: unit.ProjectId,
                unitId: unitId,
                groupKey: $"closing-reminder-{unitId}-{daysRemaining}"
            );

            // Notify Primary Purchaser
            var primaryPurchaser = unit.Purchasers.FirstOrDefault(p => p.IsPrimaryPurchaser);
            if (primaryPurchaser != null)
            {
                await CreateAsync(
                    userId: primaryPurchaser.PurchaserId,
                    title: daysRemaining <= 7 ? "⚠️ Your Closing is in " + daysRemaining + " Days" : "Closing Date Reminder",
                    message: $"Your unit {unit.UnitNumber} at {unit.Project.Name} is scheduled to close in {daysRemaining} days",
                    type: type,
                    priority: urgencyLevel,
                    actionUrl: "/Purchaser/Dashboard",
                    actionText: "View Details",
                    projectId: unit.ProjectId,
                    unitId: unitId,
                    groupKey: $"closing-reminder-purchaser-{unitId}-{daysRemaining}"
                );
            }
        }

        public async Task NotifyDepositDueAsync(int depositId, int daysRemaining)
        {
            var deposit = await _context.Deposits
                .Include(d => d.Unit).ThenInclude(u => u.Project)
                .Include(d => d.Unit).ThenInclude(u => u.Purchasers).ThenInclude(p => p.Purchaser)
                .FirstOrDefaultAsync(d => d.Id == depositId);

            if (deposit?.Unit?.Project == null) return;

            var urgencyLevel = daysRemaining switch
            {
                <= 0 => NotificationPriority.Urgent,
                <= 3 => NotificationPriority.High,
                _ => NotificationPriority.Normal
            };

            var type = daysRemaining <= 0 ? NotificationType.Alert : NotificationType.Deposit;
            var title = daysRemaining <= 0 
                ? "⚠️ Deposit Overdue" 
                : $"Deposit Due in {daysRemaining} Days";

            // Notify Builder
            await CreateAsync(
                userId: deposit.Unit.Project.BuilderId,
                title: title,
                message: $"Deposit #{deposit.DepositName} ({deposit.Amount:C0}) for Unit {deposit.Unit.UnitNumber} " +
                         (daysRemaining <= 0 ? "is overdue" : $"is due in {daysRemaining} days"),
                type: type,
                priority: urgencyLevel,
                actionUrl: $"/Units/Details/{deposit.UnitId}",
                actionText: "View Deposits",
                projectId: deposit.Unit.ProjectId,
                unitId: deposit.UnitId,
                groupKey: $"deposit-due-{depositId}"
            );

            // Notify Purchaser
            var primaryPurchaser = deposit.Unit.Purchasers.FirstOrDefault(p => p.IsPrimaryPurchaser);
            if (primaryPurchaser != null)
            {
                await CreateAsync(
                    userId: primaryPurchaser.PurchaserId,
                    title: daysRemaining <= 0 ? "⚠️ Your Deposit is Overdue" : $"Deposit Payment Reminder",
                    message: $"Deposit #{deposit.DepositName} ({deposit.Amount:C0}) for your unit " +
                             (daysRemaining <= 0 ? "is overdue. Please make payment immediately." : $"is due in {daysRemaining} days."),
                    type: type,
                    priority: urgencyLevel,
                    actionUrl: "/Purchaser/Deposits",
                    actionText: "View Details",
                    projectId: deposit.Unit.ProjectId,
                    unitId: deposit.UnitId,
                    groupKey: $"deposit-due-purchaser-{depositId}"
                );
            }
        }

        public async Task NotifyDepositReceivedAsync(int depositId, string purchaserName, decimal amount)
        {
            var deposit = await _context.Deposits
                .Include(d => d.Unit).ThenInclude(u => u.Project)
                .FirstOrDefaultAsync(d => d.Id == depositId);

            if (deposit?.Unit?.Project == null) return;

            await CreateAsync(
                userId: deposit.Unit.Project.BuilderId,
                title: "Deposit Received",
                message: $"{purchaserName} has paid deposit #{deposit.DepositName} ({amount:C0}) for Unit {deposit.Unit.UnitNumber}",
                type: NotificationType.Success,
                priority: NotificationPriority.Normal,
                actionUrl: $"/Units/Details/{deposit.UnitId}",
                actionText: "View",
                projectId: deposit.Unit.ProjectId,
                unitId: deposit.UnitId,
                groupKey: $"deposit-received-{depositId}"
            );
        }

        public async Task NotifyPurchaserAddedAsync(int unitId, string purchaserName, string builderId)
        {
            var unit = await _context.Units.Include(u => u.Project).FirstOrDefaultAsync(u => u.Id == unitId);
            if (unit == null) return;

            await CreateAsync(
                userId: builderId,
                title: "New Purchaser Added",
                message: $"{purchaserName} has been added as a purchaser for Unit {unit.UnitNumber}",
                type: NotificationType.Purchaser,
                priority: NotificationPriority.Normal,
                actionUrl: $"/Units/Details/{unitId}",
                actionText: "View",
                projectId: unit.ProjectId,
                unitId: unitId,
                groupKey: $"purchaser-added-{unitId}"
            );
        }

        public async Task NotifyDocumentUploadedAsync(int unitId, string documentName, string uploadedBy)
        {
            var unit = await _context.Units.Include(u => u.Project).FirstOrDefaultAsync(u => u.Id == unitId);
            if (unit == null) return;

            await CreateAsync(
                userId: unit.Project.BuilderId,
                title: "New Document Uploaded",
                message: $"{uploadedBy} uploaded '{documentName}' for Unit {unit.UnitNumber}",
                type: NotificationType.Document,
                priority: NotificationPriority.Normal,
                actionUrl: $"/Units/Details/{unitId}#documents",
                actionText: "View",
                projectId: unit.ProjectId,
                unitId: unitId,
                groupKey: $"document-{unitId}"
            );
        }

        public async Task NotifyExtensionRequestSubmittedAsync(int unitId, string purchaserName)
        {
            var unit = await _context.Units.Include(u => u.Project).FirstOrDefaultAsync(u => u.Id == unitId);
            if (unit?.Project == null) return;

            await CreateAsync(
                userId: unit.Project.BuilderId,
                title: "Extension Request Submitted",
                message: $"{purchaserName} has requested a closing date extension for Unit {unit.UnitNumber} in {unit.Project.Name}.",
                type: NotificationType.Info,
                priority: NotificationPriority.High,
                actionUrl: $"/ExtensionRequest",
                actionText: "Review",
                projectId: unit.ProjectId,
                unitId: unitId,
                groupKey: $"extension-request-{unitId}"
            );
        }

        public async Task NotifyExtensionApprovedAsync(int unitId, string purchaserName, string purchaserId)
        {
            var unit = await _context.Units.Include(u => u.Project).FirstOrDefaultAsync(u => u.Id == unitId);
            if (unit == null) return;

            await CreateAsync(
                userId: purchaserId,
                title: "Extension Approved",
                message: $"Your closing date extension for Unit {unit.UnitNumber} in {unit.Project.Name} has been approved.",
                type: NotificationType.Success,
                priority: NotificationPriority.High,
                actionUrl: $"/Purchaser/Dashboard",
                actionText: "View",
                projectId: unit.ProjectId,
                unitId: unitId,
                groupKey: $"extension-approved-{unitId}"
            );
        }

        public async Task NotifyExtensionRejectedAsync(int unitId, string purchaserName, string purchaserId)
        {
            var unit = await _context.Units.Include(u => u.Project).FirstOrDefaultAsync(u => u.Id == unitId);
            if (unit == null) return;

            await CreateAsync(
                userId: purchaserId,
                title: "Extension Rejected",
                message: $"Your closing date extension for Unit {unit.UnitNumber} in {unit.Project.Name} has been rejected.",
                type: NotificationType.Alert,
                priority: NotificationPriority.High,
                actionUrl: $"/Purchaser/Dashboard",
                actionText: "View",
                projectId: unit.ProjectId,
                unitId: unitId,
                groupKey: $"extension-rejected-{unitId}"
            );
        }

        public async Task NotifySOAVersionCreatedAsync(int unitId, string createdByName, string source)
        {
            var unit = await _context.Units.Include(u => u.Project).FirstOrDefaultAsync(u => u.Id == unitId);
            if (unit?.Project == null) return;

            await CreateAsync(
                userId: unit.Project.BuilderId,
                title: "SOA Version Created",
                message: $"A new SOA version ({source}) was created for Unit {unit.UnitNumber} by {createdByName}.",
                type: NotificationType.Info,
                priority: NotificationPriority.Normal,
                actionUrl: $"/Units/SOAVersionHistory/{unitId}",
                actionText: "View History",
                projectId: unit.ProjectId,
                unitId: unitId,
                groupKey: $"soa-version-{unitId}"
            );
        }

        public async Task NotifyMarketingAgencySuggestionAsync(int projectId, string agencyUserName, string builderId)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null) return;

            await CreateAsync(
                userId: builderId,
                title: "Marketing Agency Suggestion",
                message: $"{agencyUserName} submitted a discount/credit suggestion for {project.Name}.",
                type: NotificationType.Info,
                priority: NotificationPriority.Normal,
                actionUrl: $"/Projects/Dashboard/{projectId}",
                actionText: "View Project",
                projectId: projectId,
                groupKey: $"ma-suggestion-{projectId}"
            );
        }

        #endregion

        #region Get Notifications

        public async Task<List<Notification>> GetUserNotificationsAsync(string userId, int count = 20, bool unreadOnly = false)
        {
            var query = _context.Notifications
                .Where(n => n.UserId == userId)
                .Where(n => n.ExpiresAt == null || n.ExpiresAt > DateTime.UtcNow);

            if (unreadOnly)
                query = query.Where(n => !n.IsRead);

            return await query
                .OrderByDescending(n => n.CreatedAt)
                .ThenByDescending(n => n.Priority)
                .Take(count)
                .ToListAsync();
        }

        public async Task<int> GetUnreadCountAsync(string userId)
        {
            return await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .Where(n => n.ExpiresAt == null || n.ExpiresAt > DateTime.UtcNow)
                .CountAsync();
        }

        #endregion

        #region Mark as Read

        public async Task MarkAsReadAsync(int notificationId, string userId)
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

            if (notification != null && !notification.IsRead)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        public async Task MarkAllAsReadAsync(string userId)
        {
            var unreadNotifications = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            foreach (var notification in unreadNotifications)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Marked {Count} notifications as read for user {UserId}", 
                unreadNotifications.Count, userId);
        }

        #endregion

        #region Delete/Cleanup

        public async Task DeleteAsync(int notificationId, string userId)
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

            if (notification != null)
            {
                _context.Notifications.Remove(notification);
                await _context.SaveChangesAsync();
            }
        }

        public async Task DeleteOldNotificationsAsync(int daysOld = 90)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-daysOld);
            var oldNotifications = await _context.Notifications
                .Where(n => n.CreatedAt < cutoffDate && n.IsRead)
                .ToListAsync();

            _context.Notifications.RemoveRange(oldNotifications);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Deleted {Count} old notifications", oldNotifications.Count);
        }

        #endregion

        #region Background Job Methods

        public async Task CheckClosingDateRemindersAsync()
        {
            var today = DateTime.Today;
            var reminderDays = new[] { 30, 14, 7, 3, 1 }; // Days before closing to send reminders

            foreach (var days in reminderDays)
            {
                var targetDate = today.AddDays(days);
                
                var unitsClosing = await _context.Units
                    .Include(u => u.Project)
                    .Where(u => u.ClosingDate.HasValue && u.ClosingDate.Value.Date == targetDate)
                    .Where(u => u.Status != UnitStatus.Closed && u.Status != UnitStatus.Cancelled)
                    .ToListAsync();

                foreach (var unit in unitsClosing)
                {
                    // Check if we already sent this reminder
                    var groupKey = $"closing-reminder-{unit.Id}-{days}";
                    var exists = await _context.Notifications
                        .AnyAsync(n => n.GroupKey == groupKey && n.CreatedAt.Date == today);

                    if (!exists)
                    {
                        await NotifyClosingDateApproachingAsync(unit.Id, days);
                    }
                }
            }

            _logger.LogInformation("Closing date reminder check completed");
        }

        public async Task CheckDepositDueRemindersAsync()
        {
            var today = DateTime.Today;
            var reminderDays = new[] { 7, 3, 1, 0, -1, -3, -7 }; // Days before/after due date

            foreach (var days in reminderDays)
            {
                var targetDate = today.AddDays(days);
                
                var depositsDue = await _context.Deposits
                    .Include(d => d.Unit).ThenInclude(u => u.Project)
                    .Where(d => !d.IsPaid && d.DueDate.Date == targetDate)

                    .ToListAsync();

                foreach (var deposit in depositsDue)
                {
                    // Check if we already sent this reminder
                    var groupKey = $"deposit-due-{deposit.Id}";
                    var existingToday = await _context.Notifications
                        .AnyAsync(n => n.GroupKey == groupKey && n.CreatedAt.Date == today);

                    if (!existingToday)
                    {
                        var daysRemaining = days >= 0 ? days : days; // Negative means overdue
                        await NotifyDepositDueAsync(deposit.Id, daysRemaining);
                    }
                }
            }

            _logger.LogInformation("Deposit due reminder check completed");
        }

        #endregion
    }
}
