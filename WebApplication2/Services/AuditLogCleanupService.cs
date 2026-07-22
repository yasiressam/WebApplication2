using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models.Helpers;

namespace WebApplication2.Services
{
    public class AuditLogCleanupService : BackgroundService
    {
        private static readonly TimeSpan ScheduledTime = new(3, 30, 0);
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AuditLogCleanupService> _logger;

        public AuditLogCleanupService(IServiceScopeFactory scopeFactory, ILogger<AuditLogCleanupService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var delay = CleanupSchedule.GetDelayUntilNextRun(IraqTime.Now(), ScheduledTime);

                try
                {
                    _logger.LogInformation("تمت جدولة تنظيف سجل التدقيق بعد {Delay}.", delay);
                    await Task.Delay(delay, stoppingToken);
                    await CleanupAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "حدث خطأ أثناء حذف سجل التدقيق القديم.");
                }
            }
        }

        private async Task CleanupAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

            context.Database.SetCommandTimeout(30);
            var cutoffDate = DateTime.UtcNow.AddDays(-3);
            var deletedCount = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                var deleted = await context.AuditLogs
                    .Where(entry => entry.TimestampUtc < cutoffDate)
                    .OrderBy(entry => entry.Id)
                    .Take(CleanupBatching.BatchSize)
                    .ExecuteDeleteAsync(cancellationToken);

                if (deleted == 0)
                {
                    break;
                }

                deletedCount += deleted;

                // استراحة قصيرة بين الدفعات لتقليل الضغط على SQL Server
                await Task.Delay(
                    TimeSpan.FromMilliseconds(500),
                    cancellationToken);
            }

            if (deletedCount > 0)
            {
                _logger.LogInformation("تم حذف {Count} سجل تدقيق قديم.", deletedCount);
            }
        }
    }
}
