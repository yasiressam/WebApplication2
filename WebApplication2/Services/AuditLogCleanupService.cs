using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;

namespace WebApplication2.Services
{
    public class AuditLogCleanupService : BackgroundService
    {
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
                var delay = CleanupSchedule.GetDelayUntilNextRun(DateTimeOffset.Now);

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
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
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
            }

            if (deletedCount > 0)
            {
                _logger.LogInformation("تم حذف {Count} سجل تدقيق قديم.", deletedCount);
            }
        }
    }
}
