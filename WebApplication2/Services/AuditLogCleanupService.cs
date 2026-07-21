using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;

namespace WebApplication2.Services
{
    public class AuditLogCleanupService : BackgroundService
    {
        private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(24);
        private readonly IServiceScopeFactory _scopeFactory;

        public AuditLogCleanupService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await CleanupAsync(stoppingToken);

            using var timer = new PeriodicTimer(CleanupInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await CleanupAsync(stoppingToken);
            }
        }

        private async Task CleanupAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var cutoffDate = DateTime.UtcNow.AddDays(-3);

            var oldEntries = await context.AuditLogs
                .Where(entry => entry.TimestampUtc < cutoffDate)
                .ToListAsync(cancellationToken);

            if (oldEntries.Count == 0)
            {
                return;
            }

            context.AuditLogs.RemoveRange(oldEntries);
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
