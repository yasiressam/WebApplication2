using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;

namespace WebApplication2.Services
{
    public class NotificationCleanupService : BackgroundService
    {
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
                var delay = CleanupSchedule.GetDelayUntilNextRun(DateTimeOffset.Now);

                try
                {
                    _logger.LogInformation("تمت جدولة تنظيف الإشعارات بعد {Delay}.", delay);
                    await Task.Delay(delay, stoppingToken);
                    await CleanupExpiredNotificationsAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "حدث خطأ أثناء حذف الإشعارات القديمة.");
                }
            }
        }

        private async Task CleanupExpiredNotificationsAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var cutoffDate = DateTime.Now.AddDays(-7);
            var deletedCount = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                var deleted = await context.Notifications
                    .Where(n => n.SentAt < cutoffDate)
                    .OrderBy(n => n.Id)
                    .Take(CleanupBatching.BatchSize)
                    .ExecuteDeleteAsync(cancellationToken);

                if (deleted == 0)
                {
                    break;
                }

                deletedCount += deleted;
            }

            if (deletedCount > 0)
            {
                _logger.LogInformation("تم حذف {Count} إشعار قديم تجاوز 7 أيام.", deletedCount);
            }
        }
    }
}
