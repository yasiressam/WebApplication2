using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;

namespace WebApplication2.Services
{
    public class NotificationCleanupService : BackgroundService
    {
        private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(24);
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<NotificationCleanupService> _logger;

        public NotificationCleanupService(IServiceScopeFactory scopeFactory, ILogger<NotificationCleanupService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CleanupExpiredNotificationsAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "خطأ أثناء حذف الإشعارات القديمة");
                }

                try
                {
                    await Task.Delay(CleanupInterval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        private async Task CleanupExpiredNotificationsAsync(CancellationToken cancellationToken)
        {
            var cutoffDate = DateTime.Now.AddDays(-7);

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var expiredNotifications = await context.Notifications
                .Where(n => n.SentAt < cutoffDate)
                .ToListAsync(cancellationToken);

            if (expiredNotifications.Count == 0)
                return;

            context.Notifications.RemoveRange(expiredNotifications);
            await context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("تم حذف {Count} إشعار قديم تجاوز 7 أيام", expiredNotifications.Count);
        }
    }
}
