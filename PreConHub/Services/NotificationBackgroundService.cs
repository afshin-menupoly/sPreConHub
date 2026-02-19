using PreConHub.Services;

namespace PreConHub.Services
{
    /// <summary>
    /// Background service that runs periodic notification checks
    /// Checks for closing date reminders and deposit due dates
    /// </summary>
    public class NotificationBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<NotificationBackgroundService> _logger;
        
        // Run checks once per day at 8 AM
        private readonly TimeSpan _checkTime = new TimeSpan(8, 0, 0);

        public NotificationBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<NotificationBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Notification Background Service starting...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Calculate time until next 8 AM
                    var now = DateTime.Now;
                    var nextRun = now.Date.Add(_checkTime);
                    if (now > nextRun)
                        nextRun = nextRun.AddDays(1);

                    var delay = nextRun - now;
                    _logger.LogInformation("Next notification check scheduled for: {NextRun}", nextRun);

                    // Wait until next scheduled run
                    await Task.Delay(delay, stoppingToken);

                    // Run the checks
                    await RunNotificationChecks(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Service is stopping
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in notification background service");
                    // Wait 5 minutes before retrying on error
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
            }

            _logger.LogInformation("Notification Background Service stopped.");
        }

        private async Task RunNotificationChecks(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Running scheduled notification checks...");

            using var scope = _serviceProvider.CreateScope();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

            try
            {
                // Check closing date reminders
                await notificationService.CheckClosingDateRemindersAsync();
                _logger.LogInformation("Closing date reminder check completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking closing date reminders");
            }

            try
            {
                // Check deposit due reminders
                await notificationService.CheckDepositDueRemindersAsync();
                _logger.LogInformation("Deposit due reminder check completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking deposit due reminders");
            }

            try
            {
                // Clean up old notifications (older than 90 days and read)
                await notificationService.DeleteOldNotificationsAsync(90);
                _logger.LogInformation("Old notification cleanup completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up old notifications");
            }
        }
    }

    /// <summary>
    /// Hosted service that runs notification checks immediately on startup
    /// then defers to the background service
    /// </summary>
    public class NotificationStartupService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<NotificationStartupService> _logger;

        public NotificationStartupService(
            IServiceProvider serviceProvider,
            ILogger<NotificationStartupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // Run initial check on startup (delayed by 30 seconds to let app fully start)
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                
                if (cancellationToken.IsCancellationRequested) return;

                _logger.LogInformation("Running startup notification checks...");
                
                using var scope = _serviceProvider.CreateScope();
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                try
                {
                    await notificationService.CheckClosingDateRemindersAsync();
                    await notificationService.CheckDepositDueRemindersAsync();
                    _logger.LogInformation("Startup notification checks completed");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during startup notification checks");
                }
            }, cancellationToken);

            await Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
